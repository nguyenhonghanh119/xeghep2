using Microsoft.EntityFrameworkCore;
using VnPayXeGhep.Data;
using VnPayXeGhep.Models;

namespace VnPayXeGhep.Services;

public interface IPassengerService
{
    Task<List<TripSearchResult>> SearchTripsAsync(string? from, string? to, DateTime? date, int seats);
    Task<TripDetail?> GetTripDetailAsync(string tripId);
    Task<BookingCreateResult> CreateBookingAsync(int passengerId, BookingCreateRequest req, string ipAddress);
    Task<List<MyBookingItem>> GetMyBookingsAsync(int passengerId);
    Task<(bool ok, string? error)> CancelBookingAsync(int passengerId, string bookingId);
    Task<(bool ok, string? error)> AddReviewAsync(int passengerId, string tripId, ReviewCreateRequest req);
    Task<ProfileViewModel?> GetProfileAsync(int passengerId);
    Task<(bool ok, string? error)> UpdateProfileAsync(int passengerId, ProfileUpdateRequest req);
    Task<List<PaymentMethodItem>> GetPaymentMethodsAsync(int passengerId);
    Task<PaymentMethodItem> AddPaymentMethodAsync(int passengerId, AddPaymentMethodRequest req);
    Task<(bool ok, string? error)> RemovePaymentMethodAsync(int passengerId, int methodId);
    Task<List<TransactionHistoryItem>> GetTransactionHistoryAsync(int passengerId);

    /// <summary>Khách tự rút tiền từ ví nội bộ về ngân hàng — xử lý tự động thành công ngay, không cần admin duyệt.</summary>
    Task<(bool ok, string? error, decimal? newBalance)> WithdrawFromWalletAsync(int passengerId, WithdrawRequest req);
    Task<List<WithdrawalHistoryItem>> GetWithdrawalHistoryAsync(int passengerId);

    /// <summary>Xử lý kết quả trả về / IPN từ VNPay: cập nhật booking, tạo transaction, cộng ví tài xế.</summary>
    Task<(bool ok, string message)> ConfirmVnPayPaymentAsync(string vnpTxnRef, string vnpResponseCode, long vnpAmount);
}

public class PassengerService : IPassengerService
{
    private readonly AppDbContext _db;
    private readonly IVnPayService _vnPay;

    public PassengerService(AppDbContext db, IVnPayService vnPay)
    {
        _db = db;
        _vnPay = vnPay;
    }

    public async Task<List<TripSearchResult>> SearchTripsAsync(string? from, string? to, DateTime? date, int seats)
    {
        var query = _db.Trips
            .Include(t => t.Driver)
            .Where(t => t.Status == "upcoming" && t.AvailableSeats >= Math.Max(1, seats));

        if (!string.IsNullOrWhiteSpace(from))
            query = query.Where(t => t.RouteFrom.Contains(from));
        if (!string.IsNullOrWhiteSpace(to))
            query = query.Where(t => t.RouteTo.Contains(to));
        if (date.HasValue)
            query = query.Where(t => t.DepartureTime.Date == date.Value.Date);

        var trips = await query.OrderBy(t => t.DepartureTime).ToListAsync();

        var driverIds = trips.Select(t => t.DriverId).Distinct().ToList();
        var drivers = await _db.Users.Where(u => driverIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId);

        return trips.Select(t => new TripSearchResult
        {
            TripId = t.TripId,
            DepartureTime = t.DepartureTime,
            PricePerSeat = t.PricePerSeat,
            RouteFrom = t.RouteFrom,
            RouteTo = t.RouteTo,
            PickupLocation = t.PickupLocation,
            DropoffLocation = t.DropoffLocation,
            AvailableSeats = t.AvailableSeats,
            VehicleType = t.Driver?.VehicleType ?? "",
            DriverName = drivers.TryGetValue(t.DriverId, out var u) ? u.FullName : "",
            DriverRating = t.Driver?.Rating ?? 5.0m,
        }).ToList();
    }

    public async Task<TripDetail?> GetTripDetailAsync(string tripId)
    {
        var t = await _db.Trips.Include(x => x.Driver).FirstOrDefaultAsync(x => x.TripId == tripId);
        if (t == null) return null;
        var driverUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == t.DriverId);

        return new TripDetail
        {
            TripId = t.TripId,
            DepartureTime = t.DepartureTime,
            PricePerSeat = t.PricePerSeat,
            RouteFrom = t.RouteFrom,
            RouteTo = t.RouteTo,
            PickupLocation = t.PickupLocation,
            DropoffLocation = t.DropoffLocation,
            AvailableSeats = t.AvailableSeats,
            VehicleType = t.Driver?.VehicleType ?? "",
            LicensePlate = t.Driver?.LicensePlate ?? "",
            DriverName = driverUser?.FullName ?? "",
            DriverRating = t.Driver?.Rating ?? 5.0m,
        };
    }

    public async Task<BookingCreateResult> CreateBookingAsync(int passengerId, BookingCreateRequest req, string ipAddress)
    {
        // Không tin số ghế/giá tiền do client gửi lên — luôn lấy giá thật + số ghế trống từ DB,
        // giống hệt phần TODO quan trọng trong khach-thanh-toan.php gốc.
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.TripId == req.TripId);
        if (trip == null)
            return new BookingCreateResult { Success = false, Error = "Không tìm thấy chuyến đi." };

        // Chỉ cho phép đặt chuyến mới khi KHÔNG còn chuyến nào đang "dang dở" (chưa hoàn thành).
        // "Dang dở" = booking chưa bị huỷ/từ chối VÀ chuyến tương ứng chưa "done" (đang chờ duyệt,
        // đã duyệt, hoặc đang chạy). Chỉ khi chuyến đã "done" hoặc booking đã "cancelled"/"rejected"
        // thì mới coi là xong, cho phép đặt tiếp.
        var hasOngoingBooking = await _db.Bookings
            .Include(b => b.Trip)
            .AnyAsync(b => b.PassengerId == passengerId
                && b.Status != "cancelled"
                && b.Status != "rejected"
                && b.Trip != null
                && b.Trip.Status != "done"
                && b.Trip.Status != "cancelled");
        if (hasOngoingBooking)
        {
            return new BookingCreateResult
            {
                Success = false,
                Error = "Bạn đang có một chuyến chưa hoàn thành. Vui lòng chờ chuyến đó hoàn thành (hoặc huỷ) trước khi đặt chuyến mới."
            };
        }

        var seats = Math.Max(1, req.Seats);
        if (trip.AvailableSeats < seats)
            return new BookingCreateResult { Success = false, Error = "Chuyến không còn đủ ghế trống." };

        var totalAmount = trip.PricePerSeat * seats;
        var paymentMethod = req.PaymentMethod == "cash" ? "cash" : "online";

        var bookingId = await GenerateNextBookingIdAsync();

        var booking = new Booking
        {
            BookingId = bookingId,
            TripId = trip.TripId,
            PassengerId = passengerId,
            Seats = seats,
            TotalAmount = totalAmount,
            PaymentMethod = paymentMethod,
            PaymentStatus = paymentMethod == "cash" ? "pending_cash" : "pending",
            Status = "pending_approval",
            CreatedAt = DateTime.Now,
        };

        trip.AvailableSeats -= seats;

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        if (paymentMethod == "cash")
        {
            // Tiền mặt thu khi lên xe — coi như đặt chỗ được duyệt ngay ở bản demo này.
            booking.Status = "approved";
            await _db.SaveChangesAsync();
            return new BookingCreateResult { Success = true, BookingId = bookingId, TotalAmount = totalAmount };
        }

        // Thanh toán online: vnp_TxnRef gắn kèm booking_id để vnpay_return/ipn biết cần cập nhật booking nào
        var txnRef = $"{bookingId}-{DateTime.Now:yyyyMMddHHmmss}";
        var orderInfo = $"Thanh toan dat cho XeGhep booking {bookingId}";
        var checkoutUrl = _vnPay.BuildPaymentUrl(txnRef, totalAmount, orderInfo, ipAddress);

        return new BookingCreateResult
        {
            Success = true,
            BookingId = bookingId,
            CheckoutUrl = checkoutUrl,
            TotalAmount = totalAmount,
        };
    }

    private async Task<string> GenerateNextBookingIdAsync()
    {
        var ids = await _db.Bookings.Select(b => b.BookingId).ToListAsync();
        var maxNum = 0;
        foreach (var id in ids)
        {
            var parts = id.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var n) && n > maxNum) maxNum = n;
        }
        return $"BK-{(maxNum + 1):D3}";
    }

    public async Task<List<MyBookingItem>> GetMyBookingsAsync(int passengerId)
    {
        var bookings = await _db.Bookings
            .Include(b => b.Trip).ThenInclude(t => t!.Driver)
            .Where(b => b.PassengerId == passengerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var driverIds = bookings.Where(b => b.Trip != null).Select(b => b.Trip!.DriverId).Distinct().ToList();
        var drivers = await _db.Users.Where(u => driverIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId);

        var reviewedTripIds = (await _db.Reviews
            .Where(r => r.PassengerId == passengerId)
            .Select(r => r.TripId)
            .ToListAsync()).ToHashSet();

        return bookings.Where(b => b.Trip != null).Select(b => new MyBookingItem
        {
            BookingId = b.BookingId,
            TripId = b.TripId,
            RouteFrom = b.Trip!.RouteFrom,
            RouteTo = b.Trip.RouteTo,
            PickupLocation = b.Trip.PickupLocation,
            DropoffLocation = b.Trip.DropoffLocation,
            DepartureTime = b.Trip.DepartureTime,
            Seats = b.Seats,
            TotalAmount = b.TotalAmount,
            PaymentMethod = b.PaymentMethod,
            PaymentStatus = b.PaymentStatus,
            TripStatus = b.Trip.Status,
            BookingStatus = b.Status,
            DriverName = drivers.TryGetValue(b.Trip.DriverId, out var u) ? u.FullName : "",
            DriverRating = b.Trip.Driver?.Rating ?? 5.0m,
            AlreadyReviewed = reviewedTripIds.Contains(b.TripId),
        }).ToList();
    }

    public async Task<(bool ok, string? error)> CancelBookingAsync(int passengerId, string bookingId)
    {
        var booking = await _db.Bookings.Include(b => b.Trip)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.PassengerId == passengerId);
        if (booking == null) return (false, "Không tìm thấy đặt chỗ.");
        if (booking.Status == "cancelled") return (false, "Chuyến đã được huỷ trước đó.");
        if (booking.Trip != null && booking.Trip.Status is "done" or "running")
            return (false, "Không thể huỷ chuyến đã khởi hành hoặc đã hoàn thành.");

        // Chỉ hoàn tiền nếu đã thanh toán online và được VNPay xác nhận thành công (payment_status = paid).
        var shouldRefundToWallet = booking.PaymentStatus == "paid";

        booking.Status = "cancelled";
        if (shouldRefundToWallet)
            booking.PaymentStatus = "refunded";

        if (booking.Trip != null)
            booking.Trip.AvailableSeats += booking.Seats;

        // Hoàn tiền tự động 100% vào ví nội bộ của hành khách — không cần admin duyệt.
        if (shouldRefundToWallet)
        {
            var profile = await _db.PassengerProfiles.FirstOrDefaultAsync(p => p.PassengerId == passengerId);
            if (profile != null)
                profile.WalletBalance += booking.TotalAmount;
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool ok, string? error)> AddReviewAsync(int passengerId, string tripId, ReviewCreateRequest req)
    {
        if (req.Rating < 1 || req.Rating > 5) return (false, "Số sao đánh giá phải từ 1 đến 5.");

        var hasCompletedBooking = await _db.Bookings.Include(b => b.Trip)
            .AnyAsync(b => b.PassengerId == passengerId && b.TripId == tripId && b.Trip!.Status == "done");
        if (!hasCompletedBooking) return (false, "Bạn chỉ có thể đánh giá chuyến đã hoàn thành.");

        var already = await _db.Reviews.AnyAsync(r => r.PassengerId == passengerId && r.TripId == tripId);
        if (already) return (false, "Bạn đã đánh giá chuyến này rồi.");

        var trip = await _db.Trips.FirstAsync(t => t.TripId == tripId);

        _db.Reviews.Add(new Review
        {
            TripId = tripId,
            PassengerId = passengerId,
            DriverId = trip.DriverId,
            Rating = req.Rating,
            Comment = req.Comment,
            CreatedAt = DateTime.Now,
        });

        // Cập nhật lại rating trung bình của tài xế
        var driverProfile = await _db.DriverProfiles.FirstAsync(d => d.DriverId == trip.DriverId);
        var allRatings = await _db.Reviews.Where(r => r.DriverId == trip.DriverId).Select(r => r.Rating).ToListAsync();
        allRatings.Add(req.Rating);
        driverProfile.Rating = Math.Round((decimal)allRatings.Average(), 2);

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<ProfileViewModel?> GetProfileAsync(int passengerId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == passengerId && u.Role == "passenger");
        if (user == null) return null;
        var profile = await _db.PassengerProfiles.FirstOrDefaultAsync(p => p.PassengerId == passengerId);

        return new ProfileViewModel
        {
            FullName = user.FullName,
            Phone = user.Phone,
            Email = user.Email,
            JoinedYear = user.CreatedAt.Year > 1 ? user.CreatedAt.Year : DateTime.Now.Year,
            PhoneVerified = user.Status == "active",
            WalletBalance = profile?.WalletBalance ?? 0m,
        };
    }

    public async Task<(bool ok, string? error)> UpdateProfileAsync(int passengerId, ProfileUpdateRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == passengerId);
        if (user == null) return (false, "Không tìm thấy hồ sơ.");
        if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Phone))
            return (false, "Họ tên và số điện thoại không được để trống.");

        user.FullName = req.FullName.Trim();
        user.Phone = req.Phone.Trim();
        user.Email = req.Email?.Trim();
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<PaymentMethodItem>> GetPaymentMethodsAsync(int passengerId)
    {
        return await _db.PassengerPaymentMethods
            .Where(m => m.PassengerId == passengerId)
            .OrderBy(m => m.MethodId)
            .Select(m => new PaymentMethodItem { MethodId = m.MethodId, Provider = m.Provider, AccountNumber = m.AccountNumber })
            .ToListAsync();
    }

    public async Task<PaymentMethodItem> AddPaymentMethodAsync(int passengerId, AddPaymentMethodRequest req)
    {
        var entity = new PassengerPaymentMethod
        {
            PassengerId = passengerId,
            Provider = req.Provider,
            AccountNumber = req.AccountNumber,
            CreatedAt = DateTime.Now,
        };
        _db.PassengerPaymentMethods.Add(entity);
        await _db.SaveChangesAsync();
        return new PaymentMethodItem { MethodId = entity.MethodId, Provider = entity.Provider, AccountNumber = entity.AccountNumber };
    }

    public async Task<(bool ok, string? error)> RemovePaymentMethodAsync(int passengerId, int methodId)
    {
        var entity = await _db.PassengerPaymentMethods.FirstOrDefaultAsync(m => m.MethodId == methodId && m.PassengerId == passengerId);
        if (entity == null) return (false, "Không tìm thấy phương thức thanh toán.");
        _db.PassengerPaymentMethods.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<TransactionHistoryItem>> GetTransactionHistoryAsync(int passengerId)
    {
        var bookings = await _db.Bookings.Include(b => b.Trip)
            .Where(b => b.PassengerId == passengerId && b.Status != "pending_approval")
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return bookings.Where(b => b.Trip != null).Select(b => new TransactionHistoryItem
        {
            Date = b.CreatedAt,
            RouteFrom = b.Trip!.RouteFrom,
            RouteTo = b.Trip.RouteTo,
            PaymentMethod = b.PaymentMethod,
            Amount = b.TotalAmount,
            Status = b.Status == "cancelled" ? "refunded"
                : b.PaymentStatus == "paid" ? "approved"
                : b.PaymentStatus == "pending_cash" ? "pending_cash_audit"
                : "approved",
        }).ToList();
    }

    public async Task<(bool ok, string message)> ConfirmVnPayPaymentAsync(string vnpTxnRef, string vnpResponseCode, long vnpAmount)
    {
        // vnp_TxnRef có dạng "{bookingId}-{yyyyMMddHHmmss}"
        var bookingId = vnpTxnRef.Contains('-') ? vnpTxnRef[..vnpTxnRef.LastIndexOf('-')] : vnpTxnRef;

        var booking = await _db.Bookings.Include(b => b.Trip).FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking == null) return (false, "Không tìm thấy đơn đặt chỗ tương ứng.");

        var expectedAmount = (long)(booking.TotalAmount * 100);
        if (expectedAmount != vnpAmount) return (false, "Số tiền không khớp.");

        // Tránh xử lý trùng lặp nếu đã ghi nhận thanh toán trước đó
        if (booking.PaymentStatus == "paid") return (true, "Giao dịch đã được xác nhận trước đó.");

        if (vnpResponseCode != "00")
        {
            booking.PaymentStatus = "pending";
            await _db.SaveChangesAsync();
            return (false, "Thanh toán không thành công.");
        }

        booking.PaymentStatus = "paid";
        booking.Status = "approved";

        if (booking.Trip != null)
        {
            var commissionRate = await GetCommissionRateAsync();
            var commission = Math.Round(booking.TotalAmount * commissionRate / 100m, 0);
            var driverReceive = booking.TotalAmount - commission;

            _db.Transactions.Add(new Transaction
            {
                BookingId = booking.BookingId,
                TripId = booking.TripId,
                PassengerId = booking.PassengerId,
                DriverId = booking.Trip.DriverId,
                TotalAmount = booking.TotalAmount,
                CommissionAmount = commission,
                DriverReceive = driverReceive,
                PaymentMethod = "online",
                Status = "approved",
                Note = $"Thanh toan online qua VNPay cho booking {booking.BookingId}",
                CreatedAt = DateTime.Now,
            });

            var driverProfile = await _db.DriverProfiles.FirstOrDefaultAsync(d => d.DriverId == booking.Trip.DriverId);
            if (driverProfile != null)
            {
                var autoPayout = await GetAutoPayoutAsync();
                if (autoPayout) driverProfile.WalletBalance += driverReceive;
            }
        }

        var passengerProfile = await _db.PassengerProfiles.FirstOrDefaultAsync(p => p.PassengerId == booking.PassengerId);
        if (passengerProfile != null) passengerProfile.TotalBookings += 1;

        await _db.SaveChangesAsync();
        return (true, "Xác nhận thanh toán thành công.");
    }

    private async Task<decimal> GetCommissionRateAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "commission_rate");
        return setting != null && decimal.TryParse(setting.SettingValue, out var rate) ? rate : 10m;
    }

    private async Task<bool> GetAutoPayoutAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "auto_payout");
        return setting?.SettingValue.Trim().ToLower() != "false";
    }

    public async Task<(bool ok, string? error, decimal? newBalance)> WithdrawFromWalletAsync(int passengerId, WithdrawRequest req)
    {
        if (req.Amount <= 0)
            return (false, "Số tiền rút phải lớn hơn 0.", null);
        if (string.IsNullOrWhiteSpace(req.BankName) || string.IsNullOrWhiteSpace(req.AccountNumber))
            return (false, "Vui lòng nhập đầy đủ tên ngân hàng và số tài khoản nhận tiền.", null);

        var profile = await _db.PassengerProfiles.FirstOrDefaultAsync(p => p.PassengerId == passengerId);
        if (profile == null)
            return (false, "Không tìm thấy hồ sơ hành khách.", null);
        if (profile.WalletBalance < req.Amount)
            return (false, "Số dư ví không đủ để rút số tiền này.", null);

        // Trừ ví ngay lập tức để tránh khách bấm rút trùng nhiều lần trước khi trang kịp cập nhật.
        profile.WalletBalance -= req.Amount;

        var withdrawal = new PassengerWithdrawal
        {
            WithdrawalId = await GenerateNextWithdrawalIdAsync(),
            PassengerId = passengerId,
            Amount = req.Amount,
            BankInfo = $"{req.BankName.Trim()} {req.AccountNumber.Trim()}",
            Status = "success", // Tự động thành công ngay, không cần admin duyệt.
            CreatedAt = DateTime.Now,
        };
        _db.PassengerWithdrawals.Add(withdrawal);

        await _db.SaveChangesAsync();
        return (true, null, profile.WalletBalance);
    }

    private async Task<string> GenerateNextWithdrawalIdAsync()
    {
        var ids = await _db.PassengerWithdrawals.Select(w => w.WithdrawalId).ToListAsync();
        var maxNum = 0;
        foreach (var id in ids)
        {
            var parts = id.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var n) && n > maxNum) maxNum = n;
        }
        return $"PWD-{(maxNum + 1):D3}";
    }

    public async Task<List<WithdrawalHistoryItem>> GetWithdrawalHistoryAsync(int passengerId)
    {
        return await _db.PassengerWithdrawals
            .Where(w => w.PassengerId == passengerId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WithdrawalHistoryItem
            {
                WithdrawalId = w.WithdrawalId,
                Amount = w.Amount,
                BankInfo = w.BankInfo,
                Status = w.Status,
                CreatedAt = w.CreatedAt,
            })
            .ToListAsync();
    }
}