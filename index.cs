using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class ActivityVm
{
    public string Ic { get; set; } = "";
    public string Title { get; set; } = "";
    public string Time { get; set; } = "";
    public long Ts { get; set; }
}

public class PaxVm
{
    public string FullName { get; set; } = "";
    public string? Avatar { get; set; }
    public int Seats { get; set; }
    public string FirstName => LastToken(FullName);
    private static string LastToken(string s)
    {
        var parts = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : s;
    }
}

public class TripVm
{
    public string TripId { get; set; } = "";
    public string RouteFrom { get; set; } = "";
    public string RouteTo { get; set; } = "";
    public string PickupLocation { get; set; } = "";
    public string DropoffLocation { get; set; } = "";
    public DateTime DepartureTime { get; set; }
    public decimal? PickupLat { get; set; }
    public decimal? PickupLng { get; set; }
    public decimal? DropoffLat { get; set; }
    public decimal? DropoffLng { get; set; }
}

public class PeriodStatVm
{
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public string PeriodValue { get; set; } = "";
    public decimal AcceptanceRate { get; set; }
    public decimal PrevPeriodRate { get; set; }
}

public class IndexModel : PageModel
{
    public string FullName { get; private set; } = "";
    public string? Avatar { get; private set; }
    public decimal Rating { get; private set; }
    public int TotalTrips { get; private set; }
    public string FirstName { get; private set; } = "";

    public int PendingCount { get; private set; }
    public int TripsToday { get; private set; }
    public decimal WeeklyIncome { get; private set; }

    public PeriodStatVm DayStat { get; private set; } = new() { PeriodValue = DateTime.Today.ToString("yyyy-MM-dd") };
    public PeriodStatVm MonthStat { get; private set; } = new() { PeriodValue = DateTime.Today.ToString("yyyy-MM") };

    public TripVm? NextTrip { get; private set; }
    public List<PaxVm> PaxList { get; private set; } = new();
    public bool HasMapCoords { get; private set; }

    public List<ActivityVm> Activities { get; private set; } = new();

    public string CurrentDayVN { get; private set; } = "";

    public string RateDiffText { get; private set; } = "";
    public string RateDiffClass { get; private set; } = "";

    public async Task OnGetAsync()
    {
        var driverId = Constants.DriverId;
        await using var conn = await Db.OpenAsync();

        // 1. Thông tin cá nhân và hồ sơ tài xế
        await using (var cmd = new MySqlCommand(@"SELECT u.full_name, u.avatar, dp.rating, dp.total_trips
                       FROM users u JOIN driver_profiles dp ON u.user_id = dp.driver_id
                       WHERE u.user_id = @driver_id", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                FullName = reader.GetString("full_name");
                Avatar = reader.IsDBNull(reader.GetOrdinal("avatar")) ? null : reader.GetString("avatar");
                Rating = reader.GetDecimal("rating");
                TotalTrips = reader.GetInt32("total_trips");
            }
        }
        var nameParts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        FirstName = nameParts.Length > 0 ? nameParts[^1] : FullName;

        // 2. Số yêu cầu đặt chỗ đang chờ duyệt
        await using (var cmd = new MySqlCommand(@"SELECT COUNT(*) FROM bookings b
                       JOIN trips t ON b.trip_id = t.trip_id
                       WHERE t.driver_id = @driver_id AND b.status = 'pending_approval'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            PendingCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // 3. Chuyến đi hôm nay
        await using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM trips WHERE driver_id = @driver_id AND DATE(departure_time) = @today", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            cmd.Parameters.AddWithValue("@today", DateTime.Today.ToString("yyyy-MM-dd"));
            TripsToday = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // 4. Thu nhập tuần này
        await using (var cmd = new MySqlCommand(@"SELECT SUM(driver_receive) FROM transactions
                       WHERE driver_id = @driver_id AND status = 'approved'
                       AND YEARWEEK(created_at, 1) = YEARWEEK(CURDATE(), 1)", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            var val = await cmd.ExecuteScalarAsync();
            WeeklyIncome = val is null or DBNull ? 0 : Convert.ToDecimal(val);
        }

        // 5. Thống kê nhận/từ chối chuyến
        await using (var cmd = new MySqlCommand("SELECT * FROM driver_acceptance_stats WHERE driver_id = @driver_id", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var periodType = reader.GetString("period_type");
                var stat = new PeriodStatVm
                {
                    AcceptedCount = reader.GetInt32("accepted_count"),
                    RejectedCount = reader.GetInt32("rejected_count"),
                    PeriodValue = reader.GetValue(reader.GetOrdinal("period_value")).ToString() ?? "",
                    AcceptanceRate = HasColumn(reader, "acceptance_rate") && !reader.IsDBNull(reader.GetOrdinal("acceptance_rate")) ? reader.GetDecimal("acceptance_rate") : 0,
                    PrevPeriodRate = HasColumn(reader, "prev_period_rate") && !reader.IsDBNull(reader.GetOrdinal("prev_period_rate")) ? reader.GetDecimal("prev_period_rate") : 0,
                };
                if (periodType == "day") DayStat = stat;
                if (periodType == "month") MonthStat = stat;
            }
        }

        var rateDiff = MonthStat.AcceptanceRate - MonthStat.PrevPeriodRate;
        RateDiffText = (rateDiff > 0 ? "+" : "") + rateDiff + "% so với tháng trước";
        RateDiffClass = rateDiff < 0 ? "down" : "";

        // 6. Chuyến sắp khởi hành + danh sách khách
        await using (var cmd = new MySqlCommand(
            "SELECT * FROM trips WHERE driver_id = @driver_id AND status = 'upcoming' ORDER BY departure_time ASC LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                NextTrip = new TripVm
                {
                    TripId = reader.GetString("trip_id"),
                    RouteFrom = reader.GetString("route_from"),
                    RouteTo = reader.GetString("route_to"),
                    PickupLocation = reader.GetString("pickup_location"),
                    DropoffLocation = reader.GetString("dropoff_location"),
                    DepartureTime = reader.GetDateTime("departure_time"),
                    PickupLat = HasColumn(reader, "pickup_lat") && !reader.IsDBNull(reader.GetOrdinal("pickup_lat")) ? reader.GetDecimal("pickup_lat") : null,
                    PickupLng = HasColumn(reader, "pickup_lng") && !reader.IsDBNull(reader.GetOrdinal("pickup_lng")) ? reader.GetDecimal("pickup_lng") : null,
                    DropoffLat = HasColumn(reader, "dropoff_lat") && !reader.IsDBNull(reader.GetOrdinal("dropoff_lat")) ? reader.GetDecimal("dropoff_lat") : null,
                    DropoffLng = HasColumn(reader, "dropoff_lng") && !reader.IsDBNull(reader.GetOrdinal("dropoff_lng")) ? reader.GetDecimal("dropoff_lng") : null,
                };
            }
        }

        if (NextTrip is not null)
        {
            await using var cmd = new MySqlCommand(@"SELECT u.full_name, u.avatar, b.seats
                       FROM bookings b JOIN users u ON b.passenger_id = u.user_id
                       WHERE b.trip_id = @trip_id AND b.status = 'approved'", conn);
            cmd.Parameters.AddWithValue("@trip_id", NextTrip.TripId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                PaxList.Add(new PaxVm
                {
                    FullName = reader.GetString("full_name"),
                    Avatar = reader.IsDBNull(reader.GetOrdinal("avatar")) ? null : reader.GetString("avatar"),
                    Seats = reader.GetInt32("seats")
                });
            }

            HasMapCoords = NextTrip.PickupLat is not null && NextTrip.PickupLng is not null
                           && NextTrip.DropoffLat is not null && NextTrip.DropoffLng is not null;
        }

        // 7. Hoạt động gần đây: đánh giá
        await using (var cmd = new MySqlCommand(@"SELECT u.full_name, r.rating, r.created_at
                       FROM reviews r JOIN users u ON r.passenger_id = u.user_id
                       WHERE r.driver_id = @driver_id ORDER BY r.created_at DESC LIMIT 2", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString("full_name");
                var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var first = parts.Length > 0 ? parts[^1] : name;
                var rating = reader.GetInt32("rating");
                var createdAt = reader.GetDateTime("created_at");
                Activities.Add(new ActivityVm
                {
                    Ic = "⭐",
                    Title = $"Nhận đánh giá {rating} sao từ {first}",
                    Time = createdAt.ToString("dd/MM HH:mm"),
                    Ts = ((DateTimeOffset)DateTime.SpecifyKind(createdAt, DateTimeKind.Local)).ToUnixTimeSeconds()
                });
            }
        }

        // Hoạt động gần đây: giao dịch
        await using (var cmd = new MySqlCommand(@"SELECT driver_receive, status, created_at FROM transactions
                       WHERE driver_id = @driver_id ORDER BY created_at DESC LIMIT 2", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var isApproved = reader.GetString("status") == "approved";
                var amount = reader.GetDecimal("driver_receive");
                var createdAt = reader.GetDateTime("created_at");
                Activities.Add(new ActivityVm
                {
                    Ic = isApproved ? "💵" : "⏳",
                    Title = (isApproved ? "Đã nhận thanh toán " : "Chờ đối soát tiền mặt ") + amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + "đ",
                    Time = createdAt.ToString("dd/MM HH:mm"),
                    Ts = ((DateTimeOffset)DateTime.SpecifyKind(createdAt, DateTimeKind.Local)).ToUnixTimeSeconds()
                });
            }
        }

        Activities = Activities.OrderByDescending(a => a.Ts).ToList();

        string[] days = { "Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" };
        CurrentDayVN = $"{days[(int)DateTime.Now.DayOfWeek]}, {DateTime.Now:dd/MM/yyyy}";
    }

    private static bool HasColumn(MySqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
