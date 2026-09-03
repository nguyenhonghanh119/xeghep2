using Microsoft.EntityFrameworkCore;
using VnPayXeGhep.Models;

namespace VnPayXeGhep.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<PassengerProfile> PassengerProfiles => Set<PassengerProfile>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<PassengerPaymentMethod> PassengerPaymentMethods => Set<PassengerPaymentMethod>();
    public DbSet<PassengerWithdrawal> PassengerWithdrawals => Set<PassengerWithdrawal>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();


    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.FullName).HasColumnName("full_name");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Avatar).HasColumnName("avatar");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<PassengerProfile>(e =>
        {
            e.ToTable("passenger_profiles");
            e.HasKey(x => x.PassengerId);
            e.Property(x => x.PassengerId).HasColumnName("passenger_id");
            e.Property(x => x.IdentityCard).HasColumnName("identity_card");
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.TotalBookings).HasColumnName("total_bookings");
            e.Property(x => x.WalletBalance).HasColumnName("wallet_balance");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.PassengerId);
        });

        b.Entity<DriverProfile>(e =>
        {
            e.ToTable("driver_profiles");
            e.HasKey(x => x.DriverId);
            e.Property(x => x.DriverId).HasColumnName("driver_id");
            e.Property(x => x.VehicleType).HasColumnName("vehicle_type");
            e.Property(x => x.LicensePlate).HasColumnName("license_plate");
            e.Property(x => x.Rating).HasColumnName("rating");
            e.Property(x => x.TotalTrips).HasColumnName("total_trips");
            e.Property(x => x.WalletBalance).HasColumnName("wallet_balance");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.DriverId);
        });

        b.Entity<Trip>(e =>
        {
            e.ToTable("trips");
            e.HasKey(x => x.TripId);
            e.Property(x => x.TripId).HasColumnName("trip_id").HasMaxLength(20);
            e.Property(x => x.DriverId).HasColumnName("driver_id");
            e.Property(x => x.RouteFrom).HasColumnName("route_from");
            e.Property(x => x.RouteTo).HasColumnName("route_to");
            e.Property(x => x.PickupLocation).HasColumnName("pickup_location");
            e.Property(x => x.DropoffLocation).HasColumnName("dropoff_location");
            e.Property(x => x.DepartureTime).HasColumnName("departure_time");
            e.Property(x => x.PricePerSeat).HasColumnName("price_per_seat");
            e.Property(x => x.TotalSeats).HasColumnName("total_seats");
            e.Property(x => x.AvailableSeats).HasColumnName("available_seats");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId);
        });

        b.Entity<Booking>(e =>
        {
            e.ToTable("bookings");
            e.HasKey(x => x.BookingId);
            e.Property(x => x.BookingId).HasColumnName("booking_id").HasMaxLength(20);
            e.Property(x => x.TripId).HasColumnName("trip_id");
            e.Property(x => x.PassengerId).HasColumnName("passenger_id");
            e.Property(x => x.Seats).HasColumnName("seats");
            e.Property(x => x.TotalAmount).HasColumnName("total_amount");
            e.Property(x => x.PaymentMethod).HasColumnName("payment_method");
            e.Property(x => x.PaymentStatus).HasColumnName("payment_status");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId);
        });

        b.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.TransactionId);
            e.Property(x => x.TransactionId).HasColumnName("transaction_id");
            e.Property(x => x.BookingId).HasColumnName("booking_id");
            e.Property(x => x.TripId).HasColumnName("trip_id");
            e.Property(x => x.PassengerId).HasColumnName("passenger_id");
            e.Property(x => x.DriverId).HasColumnName("driver_id");
            e.Property(x => x.TotalAmount).HasColumnName("total_amount");
            e.Property(x => x.CommissionAmount).HasColumnName("commission_amount");
            e.Property(x => x.DriverReceive).HasColumnName("driver_receive");
            e.Property(x => x.PaymentMethod).HasColumnName("payment_method");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Note).HasColumnName("note");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<Review>(e =>
        {
            e.ToTable("reviews");
            e.HasKey(x => x.ReviewId);
            e.Property(x => x.ReviewId).HasColumnName("review_id");
            e.Property(x => x.TripId).HasColumnName("trip_id");
            e.Property(x => x.PassengerId).HasColumnName("passenger_id");
            e.Property(x => x.DriverId).HasColumnName("driver_id");
            e.Property(x => x.Rating).HasColumnName("rating");
            e.Property(x => x.Comment).HasColumnName("comment");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<PassengerPaymentMethod>(e =>
        {
            e.ToTable("passenger_payment_methods");
            e.HasKey(x => x.MethodId);
            e.Property(x => x.MethodId).HasColumnName("method_id");
            e.Property(x => x.PassengerId).HasColumnName("passenger_id");
            e.Property(x => x.Provider).HasColumnName("provider");
            e.Property(x => x.AccountNumber).HasColumnName("account_number");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<SystemSetting>(e =>
        {
            e.ToTable("system_settings");
            e.HasKey(x => x.SettingKey);
            e.Property(x => x.SettingKey).HasColumnName("setting_key");
            e.Property(x => x.SettingValue).HasColumnName("setting_value");
            e.Property(x => x.Description).HasColumnName("description");
        });

        b.Entity<PassengerWithdrawal>(e =>
        {
            e.ToTable("passenger_withdrawals");
            e.HasKey(x => x.WithdrawalId);
            e.Property(x => x.WithdrawalId).HasColumnName("withdrawal_id").HasMaxLength(20);
            e.Property(x => x.PassengerId).HasColumnName("passenger_id");
            e.Property(x => x.Amount).HasColumnName("amount");
            e.Property(x => x.BankInfo).HasColumnName("bank_info");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
