using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

public class DangKyModel : PageModel
{
    private const int OtpTtlSeconds = 90;

    public string ErrorMessage { get; set; } = "";
    public string SuccessMessage { get; set; } = "";

    public IActionResult OnGet() => Page();

   public async Task<IActionResult> OnPostAsync([FromServices] IWebHostEnvironment env)
    {
        if (!Request.HasFormContentType)
        {
            return Page();
        }

        var form = await Request.ReadFormAsync();

        // ============ XỬ LÝ AJAX: GỬI OTP / GỬI LẠI OTP / XÁC THỰC OTP ============
        if (form.ContainsKey("ajax_action"))
        {
            var ajaxAction = form["ajax_action"].ToString();

            if (ajaxAction == "send_otp" || ajaxAction == "resend_otp")
            {
                var target = (form["otp_target"].ToString() ?? "").Trim();
                var role = form["reg_role"].ToString();
                if (string.IsNullOrEmpty(role)) role = "passenger";

                if (target == "")
                {
                    return new JsonResult(new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["message"] = "Thiếu số điện thoại/email nhận mã OTP."
                    });
                }

                var otpCode = Random.Shared.Next(0, 1000000).ToString("D6");

                HttpContext.Session.SetString("otp_code", otpCode);
                HttpContext.Session.SetString("otp_target", target);
                HttpContext.Session.SetString("otp_role", role);
                HttpContext.Session.SetInt32("otp_expires", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + OtpTtlSeconds);
                HttpContext.Session.Remove("otp_verified");

                // TODO: Tích hợp gửi SMS/Email thật tại đây.
                // Hiện chưa nối API gửi thật nên trả kèm mã trong response để có thể test giao diện.
                return new JsonResult(new Dictionary<string, object?>
                {
                    ["success"] = true,
                    ["expires_in"] = OtpTtlSeconds,
                    ["debug_code"] = otpCode
                });
            }

            if (ajaxAction == "verify_otp")
            {
                var inputCode = (form["otp_code"].ToString() ?? "").Trim();
                var sessionCode = HttpContext.Session.GetString("otp_code");
                var sessionExpires = HttpContext.Session.GetInt32("otp_expires");

                if (string.IsNullOrEmpty(sessionCode) || sessionExpires is null)
                {
                    return new JsonResult(new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["message"] = "Bạn chưa yêu cầu gửi mã OTP."
                    });
                }

                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > sessionExpires.Value)
                {
                    return new JsonResult(new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["expired"] = true,
                        ["message"] = "Mã OTP đã hết hạn. Vui lòng bấm \"Gửi lại mã OTP\"."
                    });
                }

                if (sessionCode != inputCode)
                {
                    return new JsonResult(new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["message"] = "Mã xác nhận không chính xác."
                    });
                }

                HttpContext.Session.SetString("otp_verified", "1");
                return new JsonResult(new Dictionary<string, object?> { ["success"] = true });
            }

            return new JsonResult(new Dictionary<string, object?>
            {
                ["success"] = false,
                ["message"] = "Yêu cầu không hợp lệ."
            });
        }

        // ============ SUBMIT ĐĂNG KÝ CHÍNH THỨC (multipart, không có ajax_action) ============
        var reqRole = form["reg_role"].ToString();
        if (string.IsNullOrEmpty(reqRole)) reqRole = "passenger";
        var password = form["password"].ToString() ?? "";

        string? phone = null;
        string? email = null;
        string fullName = "";

        if (reqRole == "passenger")
        {
            var contact = (form["contact"].ToString() ?? "").Trim();
            fullName = "Khách hàng mới";
            if (contact.Contains('@'))
            {
                email = contact;
                phone = "EXT_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                phone = contact;
            }
        }
        else
        {
            fullName = (form["full_name"].ToString() ?? "").Trim();
            phone = (form["driver_phone"].ToString() ?? "").Trim();
            email = (form["driver_email"].ToString() ?? "").Trim();
        }

        var otpSubmittedTarget = reqRole == "passenger"
            ? (form["contact"].ToString() ?? "").Trim()
            : (!string.IsNullOrEmpty((form["driver_email"].ToString() ?? "").Trim())
                ? (form["driver_email"].ToString() ?? "").Trim()
                : (form["driver_phone"].ToString() ?? "").Trim());

        var otpVerified = HttpContext.Session.GetString("otp_verified") == "1";
        var otpTarget = HttpContext.Session.GetString("otp_target");

        if (!otpVerified || otpTarget != otpSubmittedTarget)
        {
            ErrorMessage = "Vui lòng xác thực mã OTP hợp lệ (còn hiệu lực) trước khi hoàn tất đăng ký.";
            return Page();
        }

        if (string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Vui lòng nhập mật khẩu.";
            return Page();
        }

        await using var conn = await Db.OpenAsync();

        await using (var checkCmd = new MySqlCommand(
            "SELECT user_id FROM users WHERE phone = @phone OR (email = @email AND email IS NOT NULL)", conn))
        {
            checkCmd.Parameters.AddWithValue("@phone", (object?)phone ?? DBNull.Value);
            checkCmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
            await using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                ErrorMessage = "Số điện thoại hoặc Email này đã được đăng ký.";
                return Page();
            }
        }

        var passwordHash = PasswordHelper.Hash(password);
        var status = reqRole == "driver" ? "pending" : "active";

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            long userId;
            await using (var insertUserCmd = new MySqlCommand(
                @"INSERT INTO users (full_name, phone, email, password_hash, role, status)
                  VALUES (@full_name, @phone, @email, @password_hash, @role, @status);
                  SELECT LAST_INSERT_ID();", conn, tx))
            {
                insertUserCmd.Parameters.AddWithValue("@full_name", fullName);
                insertUserCmd.Parameters.AddWithValue("@phone", (object?)phone ?? DBNull.Value);
                insertUserCmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
                insertUserCmd.Parameters.AddWithValue("@password_hash", passwordHash);
                insertUserCmd.Parameters.AddWithValue("@role", reqRole);
                insertUserCmd.Parameters.AddWithValue("@status", status);
                userId = Convert.ToInt64(await insertUserCmd.ExecuteScalarAsync());
            }

            if (reqRole == "passenger")
            {
                await using var insertProfileCmd = new MySqlCommand(
                    "INSERT INTO passenger_profiles (passenger_id) VALUES (@id)", conn, tx);
                insertProfileCmd.Parameters.AddWithValue("@id", userId);
                await insertProfileCmd.ExecuteNonQueryAsync();

                SuccessMessage = "Tạo tài khoản thành công! Bạn có thể đăng nhập ngay bây giờ.";
            }
            else
            {
                var vehicleType = (form["vehicle_type"].ToString() ?? "").Trim();
                var licensePlate = (form["license_plate"].ToString() ?? "").Trim();

                await using (var insertDriverCmd = new MySqlCommand(
                    "INSERT INTO driver_profiles (driver_id, vehicle_type, license_plate) VALUES (@id, @vehicle, @plate)", conn, tx))
                {
                    insertDriverCmd.Parameters.AddWithValue("@id", userId);
                    insertDriverCmd.Parameters.AddWithValue("@vehicle", vehicleType);
                    insertDriverCmd.Parameters.AddWithValue("@plate", licensePlate);
                    await insertDriverCmd.ExecuteNonQueryAsync();
                }

                var uploadDir = Path.Combine(env.WebRootPath, "uploads", "docs");
                Directory.CreateDirectory(uploadDir);

                var docs = new Dictionary<string, string>
                {
                    ["portrait"] = "Ảnh chân dung",
                    ["cccd_front"] = "CCCD Mặt trước",
                    ["cccd_back"] = "CCCD Mặt sau",
                    ["gplx"] = "Giấy phép lái xe",
                    ["lltp"] = "Lý lịch tư pháp",
                    ["gksk"] = "Giấy khám sức khỏe",
                    ["cavet"] = "Cà vẹt xe",
                    ["baohiem"] = "Bảo hiểm xe",
                    ["hinhxe"] = "Hình ảnh thực tế xe",
                };

                foreach (var (inputName, docName) in docs)
                {
                    var file = form.Files[inputName];
                    if (file is null || file.Length == 0) continue;

                    var ext = Path.GetExtension(file.FileName).TrimStart('.');
                    var filename = $"{inputName}_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{ext}";
                    var destination = Path.Combine(uploadDir, filename);

                    await using (var stream = new FileStream(destination, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    await using var insertDocCmd = new MySqlCommand(
                        "INSERT INTO driver_documents (driver_id, doc_type, doc_name, file_path, status) VALUES (@driver_id, @doc_type, @doc_name, @file_path, 'pending')", conn, tx);
                    insertDocCmd.Parameters.AddWithValue("@driver_id", userId);
                    insertDocCmd.Parameters.AddWithValue("@doc_type", inputName);
                    insertDocCmd.Parameters.AddWithValue("@doc_name", docName);
                    insertDocCmd.Parameters.AddWithValue("@file_path", $"/uploads/docs/{filename}");
                    await insertDocCmd.ExecuteNonQueryAsync();
                }

                SuccessMessage = "Đăng ký làm tài xế thành công! Hồ sơ đang chờ Admin phê duyệt.";
            }

            await tx.CommitAsync();

            // Đăng ký thành công -> dọn dẹp dữ liệu OTP trong session
            HttpContext.Session.Remove("otp_code");
            HttpContext.Session.Remove("otp_target");
            HttpContext.Session.Remove("otp_expires");
            HttpContext.Session.Remove("otp_verified");
            HttpContext.Session.Remove("otp_role");
        }
        catch (Exception e)
        {
            await tx.RollbackAsync();
            ErrorMessage = "Đã xảy ra lỗi hệ thống: " + e.Message;
        }

        return Page();
    }
}