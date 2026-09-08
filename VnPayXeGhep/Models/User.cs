namespace VnPayXeGhep.Models;

// Tương ứng bảng `users` trong datxeghep.sql
public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "passenger"; // admin | driver | passenger
    public string Status { get; set; } = "active";  // pending | active | locked
    public string? Avatar { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Tương ứng bảng `passenger_profiles`
public class PassengerProfile
{
    public int PassengerId { get; set; }
    public string? IdentityCard { get; set; }
    public string? Address { get; set; }
    public int TotalBookings { get; set; }
    public decimal WalletBalance { get; set; }
    public User? User { get; set; }
}

// Tương ứng bảng `driver_profiles`
public class DriverProfile
{
    public int DriverId { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public decimal Rating { get; set; } = 5.00m;
    public int TotalTrips { get; set; }
    public decimal WalletBalance { get; set; }
    public User? User { get; set; }
}
