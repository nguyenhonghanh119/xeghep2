namespace VnPayXeGhep.Models;

// Bảng `trips`
public class Trip
{
    public string TripId { get; set; } = string.Empty; // VD: TRIP-001
    public int DriverId { get; set; }
    public string RouteFrom { get; set; } = string.Empty;
    public string RouteTo { get; set; } = string.Empty;
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public decimal PricePerSeat { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public string Status { get; set; } = "upcoming"; // upcoming | running | done | cancelled
    public DateTime CreatedAt { get; set; }

    public DriverProfile? Driver { get; set; }
}

// Bảng `bookings`
public class Booking
{
    public string BookingId { get; set; } = string.Empty; // VD: BK-001
    public string TripId { get; set; } = string.Empty;
    public int PassengerId { get; set; }
    public int Seats { get; set; } = 1;
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "online"; // online | cash
    public string PaymentStatus { get; set; } = "pending"; // pending | paid | pending_cash | refunded
    public string Status { get; set; } = "pending_approval"; // pending_approval | approved | rejected | running | done | cancelled
    public DateTime CreatedAt { get; set; }

    public Trip? Trip { get; set; }
}

// Bảng `transactions`
public class Transaction
{
    public int TransactionId { get; set; }
    public string? BookingId { get; set; }
    public string? TripId { get; set; }
    public int? PassengerId { get; set; }
    public int? DriverId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal DriverReceive { get; set; }
    public string PaymentMethod { get; set; } = "online";
    public string Status { get; set; } = "approved"; // approved | pending_cash_audit | cancelled | refunded
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Bảng `reviews`
public class Review
{
    public int ReviewId { get; set; }
    public string TripId { get; set; } = string.Empty;
    public int PassengerId { get; set; }
    public int DriverId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Bảng `passenger_payment_methods`
public class PassengerPaymentMethod
{
    public int MethodId { get; set; }
    public int PassengerId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Bảng `passenger_withdrawals` — lệnh rút tiền từ ví nội bộ về ngân hàng của hành khách.
// Tự động xử lý thành công ngay khi tạo (không cần admin duyệt), giống cách thanh toán tiền mặt
// được duyệt ngay trong bản demo này.
public class PassengerWithdrawal
{
    public string WithdrawalId { get; set; } = string.Empty; // VD: PWD-001
    public int PassengerId { get; set; }
    public decimal Amount { get; set; }
    public string BankInfo { get; set; } = string.Empty;
    public string Status { get; set; } = "success";
    public DateTime CreatedAt { get; set; }
}

// Bảng `system_settings`
public class SystemSetting
{
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string? Description { get; set; }
}
