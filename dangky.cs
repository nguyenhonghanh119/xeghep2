using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Pages;

[EnableRateLimiting("auth")]
public class DangKyModel : PageModel
{
    private const int OtpTtlSeconds = 90;
    private const int OtpMaxAttempts = 5;
    private readonly IWebHostEnvironment _environment;
    private readonly bool _exposeDemoCode;
    private readonly string _driverDocumentRoot;

    public DangKyModel(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _exposeDemoCode = environment.IsDevelopment() || configuration.GetValue<bool>("Otp:ExposeDemoCode");
        var configuredRoot = configuration["Storage:DriverDocumentsRoot"] ?? "Data";
        _driverDocumentRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredRoot));
    }

    public string ErrorMessage { get; set; } = "";
    public string SuccessMessage { get; set; } = "";

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
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

                await using var otpConn = await Db.OpenAsync();
                await using (var throttle = new MySqlCommand(@"SELECT COUNT(*) FROM otp_verifications
                    WHERE target = @target AND purpose = 'register' AND created_at > UTC_TIMESTAMP() - INTERVAL 60 SECOND", otpConn))
                {
                    throttle.Parameters.AddWithValue("@target", target);
                    if (Convert.ToInt64(await throttle.ExecuteScalarAsync()) > 0)
                        return new JsonResult(new Dictionary<string, object?> { ["success"] = false, ["message"] = "Vui lòng chờ 60 giây trước khi gửi lại OTP." });
                }

                var otpCode = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
                long otpId;
                await using (var insertOtp = new MySqlCommand(@"INSERT INTO otp_verifications
                    (target, purpose, role, code_hash, expires_at, attempts, send_count, created_at)
                    VALUES (@target, 'register', @role, @hash, UTC_TIMESTAMP() + INTERVAL 90 SECOND, 0, 1, UTC_TIMESTAMP())", otpConn))
                {
                    insertOtp.Parameters.AddWithValue("@target", target);
                    insertOtp.Parameters.AddWithValue("@role", role == "driver" ? "driver" : "passenger");
                    insertOtp.Parameters.AddWithValue("@hash", PasswordHelper.Hash(otpCode));
                    await insertOtp.ExecuteNonQueryAsync();
                    otpId = insertOtp.LastInsertedId;
                }

                HttpContext.Session.SetString("otp_id", otpId.ToString());
                HttpContext.Session.SetString("otp_target", target);
                HttpContext.Session.SetString("otp_role", role);
                HttpContext.Session.Remove("otp_verified");

                // Phạm vi hiện tại dùng OTP demo/sandbox: mã chỉ trả về khi cấu hình cho phép.
                return new JsonResult(new Dictionary<string, object?>
                {
                    ["success"] = true,
                    ["expires_in"] = OtpTtlSeconds,
                    ["debug_code"] = _exposeDemoCode ? otpCode : null
                });
            }

            if (ajaxAction == "verify_otp")
            {
                var inputCode = (form["otp_code"].ToString() ?? "").Trim();
                var otpIdText = HttpContext.Session.GetString("otp_id");

                if (!long.TryParse(otpIdText, out var otpId))
                {
                    return new JsonResult(new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["message"] = "Bạn chưa yêu cầu gửi mã OTP."
                    });
                }

                await using var verifyConn = await Db.OpenAsync();
                string? codeHash = null;
                DateTime expiresAt = DateTime.MinValue;
                int attempts = 0;
                await using (var otpCmd = new MySqlCommand(@"SELECT code_hash, expires_at, attempts
                    FROM otp_verifications WHERE otp_id = @otp_id AND verified_at IS NULL", verifyConn))
                {
                    otpCmd.Parameters.AddWithValue("@otp_id", otpId);
                    await using var reader = await otpCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        codeHash = reader.GetString("code_hash");
                        expiresAt = reader.GetDateTime("expires_at");
                        attempts = reader.GetInt32("attempts");
                    }
                }

                if (codeHash == null || expiresAt <= DateTime.UtcNow)
                {
                    return new JsonResult(new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["expired"] = true,
                        ["message"] = "Mã OTP đã hết hạn. Vui lòng bấm \"Gửi lại mã OTP\"."
                    });
                }

                if (attempts >= OtpMaxAttempts)
                    return new JsonResult(new Dictionary<string, object?> { ["success"] = false, ["message"] = "Bạn đã nhập sai OTP quá số lần cho phép." });

                if (!PasswordHelper.Verify(inputCode, codeHash))
                {
                    await using var failCmd = new MySqlCommand("UPDATE otp_verifications SET attempts = attempts + 1 WHERE otp_id = @otp_id", verifyConn);
                    failCmd.Parameters.AddWithValue("@otp_id", otpId);
                    await failCmd.ExecuteNonQueryAsync();
                    return new JsonResult(new Dictionary<string, object?>
                    {
                        ["success"] = false,
                        ["message"] = "Mã xác nhận không chính xác."
                    });
                }

                await using (var successCmd = new MySqlCommand(
                    "UPDATE otp_verifications SET attempts = attempts + 1, verified_at = UTC_TIMESTAMP() WHERE otp_id = @otp_id", verifyConn))
                {
                    successCmd.Parameters.AddWithValue("@otp_id", otpId);
                    await successCmd.ExecuteNonQueryAsync();
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
            : (form["driver_phone"].ToString() ?? "").Trim();

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
                var operationArea = (form["operation_area"].ToString() ?? "").Trim();
                if (!int.TryParse(form["seat_count"], out var seatCount) || seatCount is < 1 or > 16)
                    throw new InvalidOperationException("Số ghế phải từ 1 đến 16.");

                await using (var insertDriverCmd = new MySqlCommand(
                    @"INSERT INTO driver_profiles (driver_id, vehicle_type, license_plate, operation_area, seat_count)
                      VALUES (@id, @vehicle, @plate, @operation_area, @seat_count)", conn, tx))
                {
                    insertDriverCmd.Parameters.AddWithValue("@id", userId);
                    insertDriverCmd.Parameters.AddWithValue("@vehicle", vehicleType);
                    insertDriverCmd.Parameters.AddWithValue("@plate", licensePlate);
                    insertDriverCmd.Parameters.AddWithValue("@operation_area", operationArea);
                    insertDriverCmd.Parameters.AddWithValue("@seat_count", seatCount);
                    await insertDriverCmd.ExecuteNonQueryAsync();
                }

                var uploadDir = Path.Combine(_driverDocumentRoot, "driver-documents");
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
                    if (file is null || file.Length == 0)
                        throw new InvalidOperationException($"Thiếu giấy tờ bắt buộc: {docName}");
                    if (file.Length > 5 * 1024 * 1024 || !await IsAllowedFileAsync(file))
                        throw new InvalidOperationException($"File {docName} không đúng định dạng JPG, PNG, PDF hoặc vượt quá 5MB.");

                    var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
                    var filename = $"{inputName}_{userId}_{Guid.NewGuid():N}.{ext}";
                    var destination = Path.Combine(uploadDir, filename);

                    await using (var stream = new FileStream(destination, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    await using var insertDocCmd = new MySqlCommand(
                        "INSERT INTO driver_documents (driver_id, doc_type, doc_name, file_path, mime_type, file_size, status) VALUES (@driver_id, @doc_type, @doc_name, @file_path, @mime_type, @file_size, 'pending')", conn, tx);
                    insertDocCmd.Parameters.AddWithValue("@driver_id", userId);
                    insertDocCmd.Parameters.AddWithValue("@doc_type", inputName);
                    insertDocCmd.Parameters.AddWithValue("@doc_name", docName);
                    insertDocCmd.Parameters.AddWithValue("@file_path", $"driver-documents/{filename}");
                    insertDocCmd.Parameters.AddWithValue("@mime_type", file.ContentType);
                    insertDocCmd.Parameters.AddWithValue("@file_size", file.Length);
                    await insertDocCmd.ExecuteNonQueryAsync();
                }

                SuccessMessage = "Đăng ký làm tài xế thành công! Hồ sơ đang chờ Admin phê duyệt.";
            }

            await tx.CommitAsync();

            // Đăng ký thành công -> dọn dẹp dữ liệu OTP trong session
            HttpContext.Session.Remove("otp_id");
            HttpContext.Session.Remove("otp_target");
            HttpContext.Session.Remove("otp_verified");
            HttpContext.Session.Remove("otp_role");
        }
        catch
        {
            await tx.RollbackAsync();
            ErrorMessage = "Không thể hoàn tất đăng ký. Vui lòng kiểm tra đủ thông tin và giấy tờ hợp lệ.";
        }

        return Page();
    }

    private static async Task<bool> IsAllowedFileAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".pdf")) return false;

        var header = new byte[8];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header);
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var isPdf = read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        return ext switch
        {
            ".jpg" or ".jpeg" => isJpeg,
            ".png" => isPng,
            ".pdf" => isPdf,
            _ => false,
        };
    }
}
