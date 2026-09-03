using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class MyTripVm
{
    public string TripId { get; set; } = "";
    public string RouteFrom { get; set; } = "";
    public string RouteTo { get; set; } = "";
    public string PickupLocation { get; set; } = "";
    public string DropoffLocation { get; set; } = "";
    public DateTime DepartureTime { get; set; }
    public int TotalSeats { get; set; }
    public decimal PricePerSeat { get; set; }
    public List<PaxVm> Pax { get; set; } = new();
    public int SeatsBooked { get; set; }
}

public class ChuyenCuaToiModel : PageModel
{
    public string FullName { get; private set; } = "";
    public string? Avatar { get; private set; }
    public decimal Rating { get; private set; }
    public int PendingCount { get; private set; }

    public List<MyTripVm> UpcomingTrips { get; private set; } = new();
    public List<MyTripVm> RunningTrips { get; private set; } = new();
    public List<MyTripVm> DoneTrips { get; private set; } = new();
    public Dictionary<string, decimal> DoneIncome { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var driverId = Constants.DriverId;
        await using var conn = await Db.OpenAsync();

        // 1. Thông tin tài xế cho sidebar
        await using (var cmd = new MySqlCommand(@"SELECT u.full_name, u.avatar, dp.rating
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
            }
        }

        // 2. Badge số yêu cầu đặt chỗ đang chờ duyệt
        await using (var cmd = new MySqlCommand(@"SELECT COUNT(*) FROM bookings b
                       JOIN trips t ON b.trip_id = t.trip_id
                       WHERE t.driver_id = @driver_id AND b.status = 'pending_approval'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            PendingCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // Tỉ lệ hoa hồng hệ thống
        decimal commissionRate = 10m;
        await using (var cmd = new MySqlCommand("SELECT setting_value FROM system_settings WHERE setting_key = 'commission_rate'", conn))
        {
            var val = await cmd.ExecuteScalarAsync();
            if (val is not null && decimal.TryParse(val.ToString(), out var parsed)) commissionRate = parsed;
        }

        // 4. Sắp tới
        UpcomingTrips = await LoadTrips(conn, driverId, "upcoming", null);
        foreach (var t in UpcomingTrips)
        {
            t.Pax = await GetPaxList(conn, t.TripId, new[] { "approved" });
            t.SeatsBooked = await BookedSeats(conn, t.TripId);
        }

        // 5. Đang chạy
        RunningTrips = await LoadTrips(conn, driverId, "running", null);
        foreach (var t in RunningTrips)
        {
            t.Pax = await GetPaxList(conn, t.TripId, new[] { "running" });
        }

        // 6. Hoàn thành (20 gần nhất)
        DoneTrips = await LoadTrips(conn, driverId, "done", 20);
        foreach (var t in DoneTrips)
        {
            var seatsDone = await BookedSeats(conn, t.TripId);
            var revenue = seatsDone * t.PricePerSeat;
            DoneIncome[t.TripId] = Math.Round(revenue * (1 - commissionRate / 100m), 2, MidpointRounding.AwayFromZero);
        }
    }

    private static async Task<List<MyTripVm>> LoadTrips(MySqlConnection conn, int driverId, string status, int? limit)
    {
        var order = status == "done" ? "DESC" : "ASC";
        var sql = $"SELECT * FROM trips WHERE driver_id = @driver_id AND status = @status ORDER BY departure_time {order}";
        if (limit is not null) sql += $" LIMIT {limit.Value}";

        var list = new List<MyTripVm>();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        cmd.Parameters.AddWithValue("@status", status);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new MyTripVm
            {
                TripId = reader.GetString("trip_id"),
                RouteFrom = reader.GetString("route_from"),
                RouteTo = reader.GetString("route_to"),
                PickupLocation = reader.GetString("pickup_location"),
                DropoffLocation = reader.GetString("dropoff_location"),
                DepartureTime = reader.GetDateTime("departure_time"),
                TotalSeats = reader.GetInt32("total_seats"),
                PricePerSeat = reader.GetDecimal("price_per_seat"),
            });
        }
        return list;
    }

    private static async Task<List<PaxVm>> GetPaxList(MySqlConnection conn, string tripId, string[] statuses)
    {
        var placeholders = string.Join(",", statuses.Select((_, i) => $"@s{i}"));
        var sql = $@"SELECT u.full_name, u.avatar, b.seats
                     FROM bookings b JOIN users u ON b.passenger_id = u.user_id
                     WHERE b.trip_id = @trip_id AND b.status IN ({placeholders})";

        var list = new List<PaxVm>();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@trip_id", tripId);
        for (int i = 0; i < statuses.Length; i++) cmd.Parameters.AddWithValue($"@s{i}", statuses[i]);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PaxVm
            {
                FullName = reader.GetString("full_name"),
                Avatar = reader.IsDBNull(reader.GetOrdinal("avatar")) ? null : reader.GetString("avatar"),
                Seats = reader.GetInt32("seats")
            });
        }
        return list;
    }

    private static async Task<int> BookedSeats(MySqlConnection conn, string tripId)
    {
        await using var cmd = new MySqlCommand(@"SELECT COALESCE(SUM(seats),0) FROM bookings
                       WHERE trip_id = @trip_id AND status IN ('approved','running','done')", conn);
        cmd.Parameters.AddWithValue("@trip_id", tripId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}