using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class BookingRequestVm
{
    public long BookingId { get; set; }
    public int Seats { get; set; }
    public string FullName { get; set; } = "";
    public string? Avatar { get; set; }
}

public class TripWithRequestsVm
{
    public string TripId { get; set; } = "";
    public string RouteFrom { get; set; } = "";
    public string RouteTo { get; set; } = "";
    public DateTime DepartureTime { get; set; }
    public int AvailableSeats { get; set; }
    public string PickupLocation { get; set; } = "";
    public int PendingReqCount { get; set; }
    public List<BookingRequestVm> Requests { get; set; } = new();
}

public class YeuCauDatChoModel : PageModel
{
    public string FullName { get; private set; } = "";
    public string? Avatar { get; private set; }
    public decimal Rating { get; private set; }
    public int TotalPendingCount { get; private set; }

    public List<TripWithRequestsVm> TripsWithRequests { get; private set; } = new();

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

        // 2. Danh sách chuyến đang có yêu cầu chờ duyệt
        await using (var cmd = new MySqlCommand(@"
            SELECT t.trip_id, t.route_from, t.route_to, t.departure_time, t.available_seats, t.pickup_location,
                   COUNT(b.booking_id) as pending_req_count
            FROM trips t
            JOIN bookings b ON t.trip_id = b.trip_id
            WHERE t.driver_id = @driver_id AND b.status = 'pending_approval'
            GROUP BY t.trip_id
            ORDER BY t.departure_time ASC", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                TripsWithRequests.Add(new TripWithRequestsVm
                {
                    TripId = reader.GetString("trip_id"),
                    RouteFrom = reader.GetString("route_from"),
                    RouteTo = reader.GetString("route_to"),
                    DepartureTime = reader.GetDateTime("departure_time"),
                    AvailableSeats = reader.GetInt32("available_seats"),
                    PickupLocation = reader.GetString("pickup_location"),
                    PendingReqCount = reader.GetInt32("pending_req_count"),
                });
            }
        }

        TotalPendingCount = TripsWithRequests.Sum(t => t.PendingReqCount);

        // 4. Chi tiết các booking cho các chuyến ở trên
        if (TotalPendingCount > 0)
        {
            var tripIds = TripsWithRequests.Select(t => t.TripId).ToList();
            var placeholders = string.Join(",", tripIds.Select((_, i) => $"@t{i}"));

            await using var cmd = new MySqlCommand($@"
                SELECT b.booking_id, b.trip_id, b.seats, u.full_name, u.avatar
                FROM bookings b
                JOIN users u ON b.passenger_id = u.user_id
                WHERE b.trip_id IN ({placeholders}) AND b.status = 'pending_approval'
                ORDER BY b.created_at ASC", conn);
            for (int i = 0; i < tripIds.Count; i++) cmd.Parameters.AddWithValue($"@t{i}", tripIds[i]);

            var byTrip = TripsWithRequests.ToDictionary(t => t.TripId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var tripId = reader.GetString("trip_id");
                if (byTrip.TryGetValue(tripId, out var trip))
                {
                    trip.Requests.Add(new BookingRequestVm
                    {
                        BookingId = reader.GetInt64("booking_id"),
                        Seats = reader.GetInt32("seats"),
                        FullName = reader.GetString("full_name"),
                        Avatar = reader.IsDBNull(reader.GetOrdinal("avatar")) ? null : reader.GetString("avatar"),
                    });
                }
            }
        }
    }

    public static string FormatVietnameseDate(DateTime dt)
    {
        string[] days = { "Chủ Nhật", "Th.Hai", "Th.Ba", "Th.Tư", "Th.Năm", "Th.Sáu", "Th.Bảy" };
        return $"{days[(int)dt.DayOfWeek]} {dt:dd/MM}";
    }

    public static string FirstName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : fullName;
    }
}