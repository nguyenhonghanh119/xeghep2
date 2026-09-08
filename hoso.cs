using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class DriverDocumentVm
{
    public long DocId { get; set; }
    public string DocType { get; set; } = "";
    public string DocName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class HoSoModel : PageModel
{
    public string FullName { get; private set; } = "";
    public string? Avatar { get; private set; }
    public string Phone { get; private set; } = "";
    public string AccountStatus { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public string VehicleType { get; private set; } = "";
    public string LicensePlate { get; private set; } = "";
    public decimal Rating { get; private set; }

    public int PendingCount { get; private set; }
    public List<DriverDocumentVm> Documents { get; private set; } = new();

    public static readonly Dictionary<string, string> DocIcons = new()
    {
        ["cccd"] = "🪪",
        ["license"] = "📇",
        ["registration"] = "🚗",
        ["insurance"] = "🛡️",
    };

    public bool AllApproved => Documents.Count > 0 && Documents.All(d => d.Status == "approved");
    public int PendingDocsCount => Documents.Count(d => d.Status != "approved");
    public string AvatarLetter => Avatar ?? XeGhepApp.Pages.YeuCauDatChoModel.FirstName(FullName).Substring(0, 1);
    public string DriverSinceYear => CreatedAt.ToString("yyyy");

    public static string DocIcon(string type) => DocIcons.TryGetValue(type, out var ic) ? ic : "📄";

    public static (string Label, string Css) StatusLabel(string status) => status switch
    {
        "approved" => ("Đã duyệt", "approved"),
        "rejected" => ("Bị từ chối", "rejected"),
        _ => ("Chờ duyệt", "pending"),
    };

    public async Task OnGetAsync()
    {
        var driverId = Constants.DriverId;
        await using var conn = await Db.OpenAsync();

        // 1. Thông tin cá nhân & xe
        await using (var cmd = new MySqlCommand(@"SELECT u.full_name, u.avatar, u.phone, u.status AS account_status, u.created_at,
                              dp.vehicle_type, dp.license_plate, dp.rating
                       FROM users u JOIN driver_profiles dp ON u.user_id = dp.driver_id
                       WHERE u.user_id = @driver_id", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                FullName = reader.GetString("full_name");
                Avatar = reader.IsDBNull(reader.GetOrdinal("avatar")) ? null : reader.GetString("avatar");
                Phone = reader.GetString("phone");
                AccountStatus = reader.GetString("account_status");
                CreatedAt = reader.GetDateTime("created_at");
                VehicleType = reader.GetString("vehicle_type");
                LicensePlate = reader.GetString("license_plate");
                Rating = reader.GetDecimal("rating");
            }
        }

        // 2. Badge số yêu cầu đặt chỗ chờ duyệt
        await using (var cmd = new MySqlCommand(@"SELECT COUNT(*) FROM bookings b
                       JOIN trips t ON b.trip_id = t.trip_id
                       WHERE t.driver_id = @driver_id AND b.status = 'pending_approval'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            PendingCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // 3. Danh sách giấy tờ đã nộp
        await using (var cmd = new MySqlCommand(
            "SELECT doc_id, doc_type, doc_name, file_path, status, created_at FROM driver_documents WHERE driver_id = @driver_id ORDER BY created_at ASC", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Documents.Add(new DriverDocumentVm
                {
                    DocId = reader.GetInt64("doc_id"),
                    DocType = reader.GetString("doc_type"),
                    DocName = reader.GetString("doc_name"),
                    FilePath = reader.IsDBNull(reader.GetOrdinal("file_path")) ? "" : reader.GetString("file_path"),
                    Status = reader.GetString("status"),
                    CreatedAt = reader.GetDateTime("created_at"),
                });
            }
        }
    }
}