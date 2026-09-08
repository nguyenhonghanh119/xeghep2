using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

[EnableRateLimiting("auth")]
public class LoginModel : PageModel
{
    private readonly DriverTokenService _tokenService;

    public LoginModel(DriverTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [BindProperty]
    public string Phone { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string ErrorMessage { get; set; } = "";

    public IActionResult OnGet()
    {
        // Nếu đã đăng nhập từ trước, tự động điều hướng theo phân quyền
        var userId = HttpContext.Session.GetInt32("user_id");
        if (userId is not null)
        {
            var redirect = ResolveRedirect(HttpContext.Session.GetString("role"), HttpContext.Session.GetString("status"));
            if (redirect is not null) return Redirect(redirect);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var phone = (Phone ?? "").Trim();
        var password = Password ?? "";

        if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ số điện thoại và mật khẩu.";
            return Page();
        }

        await using var conn = await Db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "SELECT user_id, full_name, password_hash, role, status FROM users WHERE phone = @phone", conn);
        cmd.Parameters.AddWithValue("@phone", phone);

        long? userId = null;
        string? fullName = null, passwordHash = null, role = null, status = null;

        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                userId = reader.GetInt64("user_id");
                fullName = reader.GetString("full_name");
                passwordHash = reader.GetString("password_hash");
                role = reader.GetString("role");
                status = reader.GetString("status");
            }
        }

        if (userId is not null && PasswordHelper.Verify(password, passwordHash!))
        {
            if (status == "locked")
            {
                ErrorMessage = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin.";
                return Page();
            }

            if (role != "driver")
            {
                ErrorMessage = "Tài khoản này không thuộc phân hệ tài xế.";
                return Page();
            }

            if (status == "pending")
            {
                HttpContext.Session.SetString("status", status);
                ErrorMessage = "Hồ sơ tài xế đang chờ Admin phê duyệt.";
                return Page();
            }

            var auth = await _tokenService.IssueAsync(
                checked((int)userId.Value), fullName ?? "", role,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent);
            _tokenService.WriteCookies(Response, Request.IsHttps, auth.accessToken, auth.refreshToken, auth.accessExpiresAt);

            HttpContext.Session.SetInt32("user_id", (int)userId.Value);
            HttpContext.Session.SetString("role", role ?? "");
            HttpContext.Session.SetString("status", status ?? "");
            HttpContext.Session.SetString("full_name", fullName ?? "");

            var redirect = ResolveRedirect(role, status);
            if (redirect is not null) return Redirect(redirect);

            return Page();
        }

        ErrorMessage = "Số điện thoại hoặc mật khẩu không đúng.";
        return Page();
    }

    private static string? ResolveRedirect(string? role, string? status)
    {
        if (status == "pending" && role == "driver") return "/Login";
        if (role == "driver") return "/";
        return null;
    }
}
