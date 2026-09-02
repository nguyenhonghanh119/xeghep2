namespace VnPayXeGhep.Models;

public class TripSearchResult
{
    public string TripId { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public decimal PricePerSeat { get; set; }
    public string RouteFrom { get; set; } = string.Empty;
    public string RouteTo { get; set; } = string.Empty;
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public int AvailableSeats { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public decimal DriverRating { get; set; }
}

public class TripDetail : TripSearchResult
{
    public string LicensePlate { get; set; } = string.Empty;
}

public class BookingCreateRequest
{
    public string TripId { get; set; } = string.Empty;
    public int Seats { get; set; } = 1;
    public string PaymentMethod { get; set; } = "online"; // online | cash
    public string? Gateway { get; set; } = "vnpay";
}

public class BookingCreateResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? BookingId { get; set; }
    public string? CheckoutUrl { get; set; } // chỉ có khi thanh toán online
    public decimal TotalAmount { get; set; }
}

public class MyBookingItem
{
    public string BookingId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string RouteFrom { get; set; } = string.Empty;
    public string RouteTo { get; set; } = string.Empty;
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public int Seats { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string TripStatus { get; set; } = string.Empty; // upcoming|running|done|cancelled
    public string BookingStatus { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public decimal DriverRating { get; set; }
    public bool AlreadyReviewed { get; set; }

    // Tab hiển thị trên giao diện: k-upcoming | k-running | k-done | k-cancelled
    public string Tab
    {
        get
        {
            if (BookingStatus == "cancelled" || TripStatus == "cancelled") return "k-cancelled";
            if (TripStatus == "done") return "k-done";
            if (TripStatus == "running") return "k-running";
            return "k-upcoming";
        }
    }
}

public class ProfileViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string InitialLetter => string.IsNullOrEmpty(FullName) ? "?" : FullName.Trim()[0].ToString().ToUpper();
    public int JoinedYear { get; set; }
    public bool PhoneVerified { get; set; }
    public decimal WalletBalance { get; set; }
}

public class ProfileUpdateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class PaymentMethodItem
{
    public int MethodId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Icon => Provider.Contains("Momo", StringComparison.OrdinalIgnoreCase)
        || Provider.Contains("Ví", StringComparison.OrdinalIgnoreCase) ? "📱" : "💳";
}

public class AddPaymentMethodRequest
{
    public string Provider { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

public class TransactionHistoryItem
{
    public DateTime Date { get; set; }
    public string RouteFrom { get; set; } = string.Empty;
    public string RouteTo { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty; // approved | pending_cash_audit | cancelled | refunded
}

public class ReviewCreateRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

// Yêu cầu rút tiền từ ví nội bộ về ngân hàng — khách tự nhập số tiền và thông tin nhận tiền.
// Xử lý tự động thành công ngay, không cần admin duyệt.
public class WithdrawRequest
{
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

public class WithdrawalHistoryItem
{
    public string WithdrawalId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string BankInfo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // success
    public DateTime CreatedAt { get; set; }
}
