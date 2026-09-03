using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class RutTienModel : PageModel
{
    private const decimal MinWithdrawAmount = 50000;
    private const int MaxProcessingHours = 12;
    private static readonly CultureInfo Vn = new("vi-VN");

    public string FullName { get; private set; } = "";
    public string? Avatar { get; private set; }
    public decimal Rating { get; private set; }
    public int PendingCount { get; private set; }

    public string ErrorMessage { get; private set; } = "";
    public string SuccessMessage { get; private set; } = "";
    public bool PasswordError { get; private set; }

    public decimal PostedAmount { get; private set; }
    public string PostedBankChoice { get; private set; } = "";
    public string PostedNewBankName { get; private set; } = "";
    public string PostedNewBankNumber { get; private set; } = "";
    public string PostedNewBankHolder { get; private set; } = "";

    public decimal PendingWithdrawSum { get; private set; }
    public decimal AvailableToWithdrawDisplay { get; private set; }

    public List<WithdrawalRowVm> RecentWithdrawals { get; private set; } = new();

    public int MinWithdrawAmountInt => (int)MinWithdrawAmount;
    public int MaxProcessingHoursInt => MaxProcessingHours;

    public async Task OnGetAsync()
    {
        await LoadDisplayDataAsync();
    }

    public async Task OnPostAsync()
    {
        var driverId = Constants.DriverId;
        var form = await Request.ReadFormAsync();

        PostedAmount = int.TryParse(form["amount"], out var amt) ? amt : 0;
        PostedBankChoice = form["bank_choice"].ToString() ?? "";
        PostedNewBankName = (form["new_bank_name"].ToString() ?? "").Trim();
        PostedNewBankNumber = (form["new_bank_number"].ToString() ?? "").Trim();
        PostedNewBankHolder = (form["new_bank_holder"].ToString() ?? "").Trim();
        var loginPassword = form["login_password"].ToString() ?? "";

        string bankInfo;
        if (PostedBankChoice == "new")
        {
            bankInfo = (PostedNewBankName != "" && PostedNewBankNumber != "" && PostedNewBankHolder != "")
                ? $"{PostedNewBankName} - {PostedNewBankNumber} - {PostedNewBankHolder}"
                : "";
        }
        else
        {
            bankInfo = PostedBankChoice.Trim();
        }

        await using var conn = await Db.OpenAsync();

        string? passwordHash = null;
        decimal walletBalance = 0;
        await using (var cmd = new MySqlCommand(@"SELECT u.password_hash, dp.wallet_balance
                       FROM users u JOIN driver_profiles dp ON u.user_id = dp.driver_id
                       WHERE u.user_id = @driver_id", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                passwordHash = reader.GetString("password_hash");
                walletBalance = reader.GetDecimal("wallet_balance");
            }
        }

        decimal pendingSum;
        await using (var cmd = new MySqlCommand(
            "SELECT IFNULL(SUM(amount), 0) FROM withdrawals WHERE driver_id = @driver_id AND status = 'pending'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            pendingSum = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
        }

        var availableToWithdraw = Math.Max(0, walletBalance - pendingSum);

        if (string.IsNullOrEmpty(loginPassword) || passwordHash is null || !PasswordHelper.Verify(loginPassword, passwordHash))
        {
            ErrorMessage = "Mật khẩu đăng nhập không chính xác. Vui lòng thử lại.";
            PasswordError = true;
        }
        else if (PostedAmount < MinWithdrawAmount)
        {
            ErrorMessage = $"Số tiền rút tối thiểu là {MinWithdrawAmount.ToString("N0", Vn)}đ.";
        }
        else if (bankInfo == "")
        {
            ErrorMessage = "Vui lòng chọn hoặc nhập đầy đủ thông tin tài khoản nhận tiền.";
        }
        else if (PostedAmount > availableToWithdraw)
        {
            ErrorMessage = "Số tiền vượt quá số dư khả dụng (đã trừ các yêu cầu đang chờ duyệt).";
        }
        else
        {
            try
            {
                await using var cmd = new MySqlCommand(
                    "INSERT INTO withdrawals (driver_id, amount, bank_info, status, created_at) VALUES (@driver_id, @amount, @bank_info, 'pending', NOW())", conn);
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                cmd.Parameters.AddWithValue("@amount", PostedAmount);
                cmd.Parameters.AddWithValue("@bank_info", bankInfo);
                await cmd.ExecuteNonQueryAsync();

                SuccessMessage = $"Yêu cầu rút {PostedAmount.ToString("N0", Vn)}đ đã được gửi đến Admin để duyệt. "
                    + $"Yêu cầu sẽ được xử lý trong tối đa {MaxProcessingHours} giờ. "
                    + "Số dư của bạn chỉ bị trừ sau khi tiền đã chuyển thành công vào tài khoản ngân hàng.";

                PostedAmount = 0;
                PostedBankChoice = "";
                PostedNewBankName = PostedNewBankNumber = PostedNewBankHolder = "";
            }
            catch (Exception e)
            {
                ErrorMessage = "Đã xảy ra lỗi hệ thống: " + e.Message;
            }
        }

        await LoadDisplayDataAsync();
    }

    private async Task LoadDisplayDataAsync()
    {
        var driverId = Constants.DriverId;
        await using var conn = await Db.OpenAsync();

        await using (var cmd = new MySqlCommand(@"SELECT u.full_name, u.avatar, dp.rating, dp.wallet_balance
                       FROM users u JOIN driver_profiles dp ON u.user_id = dp.driver_id
                       WHERE u.user_id = @driver_id", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            decimal walletBalance = 0;
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                FullName = reader.GetString("full_name");
                Avatar = reader.IsDBNull(reader.GetOrdinal("avatar")) ? null : reader.GetString("avatar");
                Rating = reader.GetDecimal("rating");
                walletBalance = reader.GetDecimal("wallet_balance");
            }
            AvailableToWithdrawDisplay = walletBalance; // sẽ trừ pendingWithdrawSum bên dưới
        }

        await using (var cmd = new MySqlCommand(@"SELECT COUNT(*) FROM bookings b
                       JOIN trips t ON b.trip_id = t.trip_id
                       WHERE t.driver_id = @driver_id AND b.status = 'pending_approval'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            PendingCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        await using (var cmd = new MySqlCommand(
            "SELECT IFNULL(SUM(amount), 0) FROM withdrawals WHERE driver_id = @driver_id AND status = 'pending'", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            PendingWithdrawSum = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
        }

        AvailableToWithdrawDisplay = Math.Max(0, AvailableToWithdrawDisplay - PendingWithdrawSum);

        await using (var cmd = new MySqlCommand(
            "SELECT created_at, amount, bank_info, status FROM withdrawals WHERE driver_id = @driver_id ORDER BY created_at DESC LIMIT 5", conn))
        {
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                RecentWithdrawals.Add(new WithdrawalRowVm
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
        "pending" => "<span class=\"status-pill pending\" style=\"background:#FFF3DF;color:#92650E\">Chờ Admin duyệt</span>",
        "rejected" => "<span class=\"status-pill rejected\" style=\"background:var(--coral-dim);color:var(--coral)\">Từ chối</span>",
        _ => "<span class=\"status-pill\">Không rõ</span>",
    };
}