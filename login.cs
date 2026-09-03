using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class LoginModel : PageModel
{
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
            ErrorMessage = "Vui lòng nhập đầy đủ thông tin đăng nhập.";
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

            HttpContext.Session.SetInt32("user_id", (int)userId.Value);
            HttpContext.Session.SetString("role", role ?? "");
            HttpContext.Session.SetString("status", status ?? "");
            HttpContext.Session.SetString("full_name", fullName ?? "");

            var redirect = ResolveRedirect(role, status);
            if (redirect is not null) return Redirect(redirect);

            return Page();
        }

        // YÊU CẦU: Báo lỗi chung chung khi sai SĐT hoặc Mật khẩu để tăng tính bảo mật
        ErrorMessage = "Thông tin đăng nhập sai, hãy kiểm tra lại.";
        return Page();
    }

    private static string? ResolveRedirect(string? role, string? status)
    {
        // YÊU CẦU: Phân quyền và điều hướng chuẩn xác (Đã loại bỏ đuôi .php)
        if (status == "pending" && role == "driver") return "/TaiXe/ChoDuyet"; // Chuyển tài xế chưa duyệt vào trang chờ
        if (role == "admin") return "/QuanTri/Index";         // Chuyển Admin vào trang quản trị
        if (role == "driver") return "/Index";                // Chuyển Tài xế (đã duyệt) vào trang tổng quan tài xế
        if (role == "passenger") return "/NguoiDung/Index";   // Chuyển Khách hàng vào trang người dùng
        return null;
    }
}