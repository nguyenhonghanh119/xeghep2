using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class TransactionRowVm
{
    public DateTime CreatedAt { get; set; }
    public string RouteFrom { get; set; } = "";
    public string RouteTo { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal DriverReceive { get; set; }
    public int TotalPax { get; set; }
}

public class WithdrawalRowVm
{
    public DateTime CreatedAt { get; set; }
    public decimal Amount { get; set; }
    public string BankInfo { get; set; } = "";
    public string Status { get; set; } = "";
}

public class ThuNhapModel : PageModel
{
    public string FullName { get; private set; } = "";
    public string? Avatar { get; private set; }
    public decimal Rating { get; private set; }
    public decimal WalletBalance { get; private set; }
    public int TotalTrips { get; private set; }

    public int PendingCount { get; private set; }
    public decimal MonthIncome { get; private set; }
    public decimal CommissionRate { get; private set; } = 10;

    public List<TransactionRowVm> Transactions { get; private set; } = new();
    public List<WithdrawalRowVm> Withdrawals { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var driverId = Constants.DriverId;
        await using var conn = await Db.OpenAsync();

        // 1. Thông tin tài xế và số dư ví
        await using (var cmd = new MySqlCommand(@"SELECT u.full_name, u.avatar, dp.rating, dp.wallet_balance, dp.total_trips
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
                WalletBalance = reader.GetDecimal("wallet_balance");
                TotalTrips = reader.GetInt32("total_trips");
            }
        }

        // 2. Số yêu cầu đặt chỗ chờ duyệt
        await using (var cmd = new MySqlCommand(@"SELECT COUNT(*) FROM bookings b
                       JOIN trips t ON b.trip_id = t.trip_id
                       WHERE t.driver_id = @driver_id AND b.status = 'pending_approval'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            PendingCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // 3. Thu nhập trong tháng hiện tại
        await using (var cmd = new MySqlCommand(@"SELECT SUM(driver_receive) FROM transactions
                       WHERE driver_id = @driver_id
                       AND MONTH(created_at) = MONTH(CURRENT_DATE())
                       AND YEAR(created_at) = YEAR(CURRENT_DATE())
                       AND status = 'approved'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            var val = await cmd.ExecuteScalarAsync();
            MonthIncome = val is null or DBNull ? 0 : Convert.ToDecimal(val);
        }

        // Tỉ lệ hoa hồng
        await using (var cmd = new MySqlCommand("SELECT setting_value FROM system_settings WHERE setting_key = 'commission_rate'", conn))
        {
            var val = await cmd.ExecuteScalarAsync();
            if (val is not null && decimal.TryParse(val.ToString(), out var parsed)) CommissionRate = parsed;
        }

        // 4. Lịch sử giao dịch
        await using (var cmd = new MySqlCommand(@"
            SELECT t.created_at, tr.route_from, tr.route_to, t.total_amount, t.commission_amount, t.driver_receive,
                   (SELECT IFNULL(SUM(seats), 0) FROM bookings b WHERE b.trip_id = tr.trip_id AND b.status IN ('done', 'approved')) as total_pax
            FROM transactions t
            JOIN trips tr ON t.trip_id = tr.trip_id
            WHERE t.driver_id = @driver_id
            ORDER BY t.created_at DESC", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Transactions.Add(new TransactionRowVm
                {
                    CreatedAt = reader.GetDateTime("created_at"),
                    RouteFrom = reader.GetString("route_from"),
                    RouteTo = reader.GetString("route_to"),
                    TotalAmount = reader.GetDecimal("total_amount"),
                    CommissionAmount = reader.GetDecimal("commission_amount"),
                    DriverReceive = reader.GetDecimal("driver_receive"),
                    TotalPax = Convert.ToInt32(reader["total_pax"]),
                });
            }
        }

        // 5. Lịch sử rút tiền
        await using (var cmd = new MySqlCommand(
            "SELECT created_at, amount, bank_info, status FROM withdrawals WHERE driver_id = @driver_id ORDER BY created_at DESC", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Withdrawals.Add(new WithdrawalRowVm
                {
                    CreatedAt = reader.GetDateTime("created_at"),
                    Amount = reader.GetDecimal("amount"),
                    BankInfo = reader.GetString("bank_info"),
                    Status = reader.GetString("status"),
                });
            }
        }
    }

    public static string GetWithdrawStatusHtml(string status) => status switch
    {
        "approved" => "<span class=\"status-pill approved\" style=\"background:var(--green-dim);color:var(--green)\">Thành công</span>",
        "pending" => "<span class=\"status-pill pending\" style=\"background:#FFF3DF;color:#92650E\">Đang xử lý</span>",
        "rejected" => "<span class=\"status-pill rejected\" style=\"background:var(--coral-dim);color:var(--coral)\">Từ chối</span>",
        _ => "<span class=\"status-pill\">Không rõ</span>",
    };
}