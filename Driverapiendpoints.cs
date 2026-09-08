using MySqlConnector;
using XeGhepApp.Data;

namespace XeGhepApp.Endpoints;

/// <summary>
/// Request body dùng cho start-trip.php / complete-trip.php (JSON: { "trip_id": "..." })
/// </summary>
public sealed class TripIdRequest
{
    public string? trip_id { get; set; }
}

/// <summary>
/// Request body dùng cho save-profile.php (JSON: { fullName, phone, vehicle, plate })
/// </summary>
public sealed class SaveProfileRequest
{
    public string? fullName { get; set; }
    public string? phone { get; set; }
    public string? vehicle { get; set; }
    public string? plate { get; set; }
}

/// <summary>
/// Request body dùng cho delete-document.php (JSON: { doc_id, doc_type })
/// </summary>
public sealed class DeleteDocumentRequest
{
    public int doc_id { get; set; }
    public string? doc_type { get; set; }
}

public static class DriverApiEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/start-trip.php", StartTripAsync);
        app.MapPost("/complete-trip.php", CompleteTripAsync);
        app.MapPost("/save-profile.php", SaveProfileAsync);
        app.MapPost("/upload-document.php", UploadDocumentAsync);
        app.MapPost("/delete-document.php", DeleteDocumentAsync);
    }

    // ================= start-trip.php =================
    // Cập nhật trips.status -> 'running' và bookings.status -> 'running'
    // (cho các vé đã 'approved' của chuyến đó).
    private static async Task<IResult> StartTripAsync(TripIdRequest? body)
    {
        var driverId = Constants.DriverId;
        var tripId = body?.trip_id;

        if (string.IsNullOrWhiteSpace(tripId))
        {
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Thiếu trip_id"
            }, statusCode: 400);
        }

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            string? currentStatus = null;
            await using (var cmd = new MySqlCommand(
                "SELECT status FROM trips WHERE trip_id = @trip_id AND driver_id = @driver_id FOR UPDATE", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    currentStatus = reader.GetString("status");
                }
            }

            if (currentStatus is null)
            {
                await tx.RollbackAsync();
                return Results.Json(new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["message"] = "Không tìm thấy chuyến đi hoặc bạn không có quyền"
                }, statusCode: 404);
            }

            if (currentStatus != "upcoming")
            {
                await tx.RollbackAsync();
                return Results.Json(new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["message"] = "Chuyến đi đã được bắt đầu hoặc không còn ở trạng thái chờ khởi hành"
                }, statusCode: 409);
            }

            await using (var cmd = new MySqlCommand("UPDATE trips SET status = 'running' WHERE trip_id = @trip_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                await cmd.ExecuteNonQueryAsync();
            }

            int affectedBookings;
            await using (var cmd = new MySqlCommand(
                "UPDATE bookings SET status = 'running' WHERE trip_id = @trip_id AND status = 'approved'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                affectedBookings = await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["message"] = "Đã bắt đầu chuyến",
                ["trip_id"] = tripId,
                ["trip_status"] = "running",
                ["bookings_updated"] = affectedBookings
            });
        }
        catch (Exception e)
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Lỗi khi cập nhật CSDL: " + e.Message
            }, statusCode: 500);
        }
    }

    // ================= complete-trip.php =================
    // trips.status -> 'done', bookings.status -> 'done', tạo transactions cho từng vé,
    // cộng ví tài xế nếu thanh toán online + auto_payout, cập nhật driver_profiles.total_trips.
    private static async Task<IResult> CompleteTripAsync(TripIdRequest? body)
    {
        var driverId = Constants.DriverId;
        var tripId = body?.trip_id;

        if (string.IsNullOrWhiteSpace(tripId))
        {
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Thiếu trip_id"
            }, statusCode: 400);
        }

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            string? currentStatus = null;
            await using (var cmd = new MySqlCommand(
                "SELECT status FROM trips WHERE trip_id = @trip_id AND driver_id = @driver_id FOR UPDATE", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    currentStatus = reader.GetString("status");
                }
            }

            if (currentStatus is null)
            {
                await tx.RollbackAsync();
                return Results.Json(new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["message"] = "Không tìm thấy chuyến đi hoặc bạn không có quyền"
                }, statusCode: 404);
            }

            if (currentStatus != "running")
            {
                await tx.RollbackAsync();
                return Results.Json(new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["message"] = "Chuyến đi chưa ở trạng thái đang chạy"
                }, statusCode: 409);
            }

            await using (var cmd = new MySqlCommand("UPDATE trips SET status = 'done' WHERE trip_id = @trip_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Lấy các vé đang 'running' để chuyển sang 'done'
            var bookings = new List<(long BookingId, long PassengerId, int Seats, decimal TotalAmount, string PaymentMethod)>();
            await using (var cmd = new MySqlCommand(
                "SELECT booking_id, passenger_id, seats, total_amount, payment_method FROM bookings WHERE trip_id = @trip_id AND status = 'running'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    bookings.Add((
                        reader.GetInt64("booking_id"),
                        reader.GetInt64("passenger_id"),
                        reader.GetInt32("seats"),
                        reader.GetDecimal("total_amount"),
                        reader.GetString("payment_method")
                    ));
                }
            }

            await using (var cmd = new MySqlCommand(
                "UPDATE bookings SET status = 'done' WHERE trip_id = @trip_id AND status = 'running'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Tỉ lệ hoa hồng hệ thống
            decimal commissionRate = 10m;
            await using (var cmd = new MySqlCommand("SELECT setting_value FROM system_settings WHERE setting_key = 'commission_rate'", conn, tx))
            {
                var val = await cmd.ExecuteScalarAsync();
                if (val is not null && decimal.TryParse(val.ToString(), out var parsed))
                {
                    commissionRate = parsed;
                }
            }

            bool autoPayout = false;
            await using (var cmd = new MySqlCommand("SELECT setting_value FROM system_settings WHERE setting_key = 'auto_payout'", conn, tx))
            {
                var val = await cmd.ExecuteScalarAsync();
                autoPayout = val?.ToString() == "true";
            }

            decimal walletCredit = 0;
            int txCreated = 0;

            foreach (var b in bookings)
            {
                long existingCount;
                await using (var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM transactions WHERE booking_id = @booking_id", conn, tx))
                {
                    checkCmd.Parameters.AddWithValue("@booking_id", b.BookingId);
                    existingCount = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);
                }

                if (existingCount > 0) continue; // đã có giao dịch trước đó, bỏ qua

                var totalAmount = b.TotalAmount;
                var commission = Math.Round(totalAmount * commissionRate / 100m, 2, MidpointRounding.AwayFromZero);
                var driverReceive = Math.Round(totalAmount - commission, 2, MidpointRounding.AwayFromZero);
                var isOnline = b.PaymentMethod == "online";
                var txStatus = isOnline ? "approved" : "pending_cash_audit";
                var note = isOnline
                    ? $"Chuyến {tripId} thanh toán online — cộng ví tài xế"
                    : $"Chờ đối soát tiền mặt — chuyến {tripId}";

                await using (var insertCmd = new MySqlCommand(@"INSERT INTO transactions
                    (booking_id, trip_id, passenger_id, driver_id, total_amount, commission_amount, driver_receive, payment_method, status, note)
                    VALUES (@booking_id, @trip_id, @passenger_id, @driver_id, @total_amount, @commission_amount, @driver_receive, @payment_method, @status, @note)", conn, tx))
                {
                    insertCmd.Parameters.AddWithValue("@booking_id", b.BookingId);
                    insertCmd.Parameters.AddWithValue("@trip_id", tripId);
                    insertCmd.Parameters.AddWithValue("@passenger_id", b.PassengerId);
                    insertCmd.Parameters.AddWithValue("@driver_id", driverId);
                    insertCmd.Parameters.AddWithValue("@total_amount", totalAmount);
                    insertCmd.Parameters.AddWithValue("@commission_amount", commission);
                    insertCmd.Parameters.AddWithValue("@driver_receive", driverReceive);
                    insertCmd.Parameters.AddWithValue("@payment_method", b.PaymentMethod);
                    insertCmd.Parameters.AddWithValue("@status", txStatus);
                    insertCmd.Parameters.AddWithValue("@note", note);
                    await insertCmd.ExecuteNonQueryAsync();
                }
                txCreated++;

                if (isOnline && autoPayout)
                {
                    walletCredit += driverReceive;
                }
            }

            if (walletCredit > 0)
            {
                await using var walletCmd = new MySqlCommand(
                    "UPDATE driver_profiles SET wallet_balance = wallet_balance + @amount WHERE driver_id = @driver_id", conn, tx);
                walletCmd.Parameters.AddWithValue("@amount", walletCredit);
                walletCmd.Parameters.AddWithValue("@driver_id", driverId);
                await walletCmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new MySqlCommand(
                "UPDATE driver_profiles SET total_trips = total_trips + 1 WHERE driver_id = @driver_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["message"] = "Đã hoàn thành chuyến",
                ["trip_id"] = tripId,
                ["trip_status"] = "done",
                ["transactions_created"] = txCreated,
                ["wallet_credit"] = walletCredit
            });
        }
        catch (Exception e)
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Lỗi khi cập nhật CSDL: " + e.Message
            }, statusCode: 500);
        }
    }

    // ================= save-profile.php =================
    private static async Task<IResult> SaveProfileAsync(SaveProfileRequest? body)
    {
        var driverId = Constants.DriverId;
        var fullName = (body?.fullName ?? "").Trim();
        var phone = (body?.phone ?? "").Trim();
        var vehicle = (body?.vehicle ?? "").Trim();
        var plate = (body?.plate ?? "").Trim();

        if (fullName == "" || phone == "" || vehicle == "" || plate == "")
        {
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Vui lòng điền đầy đủ thông tin"
            }, statusCode: 400);
        }

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using (var checkCmd = new MySqlCommand(
                "SELECT user_id FROM users WHERE phone = @phone AND user_id != @driver_id", conn, tx))
            {
                checkCmd.Parameters.AddWithValue("@phone", phone);
                checkCmd.Parameters.AddWithValue("@driver_id", driverId);
                await using var reader = await checkCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    await tx.RollbackAsync();
                    return Results.Json(new Dictionary<string, object?>
                    {
                        ["ok"] = false,
                        ["message"] = "Số điện thoại đã được sử dụng bởi tài khoản khác"
                    }, statusCode: 409);
                }
            }

            await using (var cmd = new MySqlCommand(
                "UPDATE users SET full_name = @full_name, phone = @phone WHERE user_id = @driver_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@full_name", fullName);
                cmd.Parameters.AddWithValue("@phone", phone);
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new MySqlCommand(
                "UPDATE driver_profiles SET vehicle_type = @vehicle, license_plate = @plate WHERE driver_id = @driver_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@vehicle", vehicle);
                cmd.Parameters.AddWithValue("@plate", plate);
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["message"] = "Đã lưu thay đổi",
                ["fullName"] = fullName,
                ["phone"] = phone,
                ["vehicle"] = vehicle,
                ["plate"] = plate
            });
        }
        catch (Exception e)
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Lỗi khi cập nhật CSDL: " + e.Message
            }, statusCode: 500);
        }
    }

    // ================= upload-document.php =================
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png", "pdf" };
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private static async Task<IResult> UploadDocumentAsync(HttpRequest request, IWebHostEnvironment env)
    {
        var driverId = Constants.DriverId;

        if (!request.HasFormContentType)
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Phương thức không hợp lệ" }, statusCode: 405);
        }

        var form = await request.ReadFormAsync();
        var docType = (form["doc_type"].ToString() ?? "").Trim();
        var docName = (form["doc_name"].ToString() ?? "").Trim();

        if (docType == "" || docName == "")
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Thiếu loại giấy tờ" }, statusCode: 400);
        }

        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Vui lòng chọn file hợp lệ" }, statusCode: 400);
        }

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Chỉ chấp nhận file JPG, PNG hoặc PDF" }, statusCode: 400);
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "File tối đa 5MB" }, statusCode: 400);
        }

        // Thư mục lưu file thật: wwwroot/uploads/docs/
        var uploadDir = Path.Combine(env.WebRootPath, "uploads", "docs");
        Directory.CreateDirectory(uploadDir);

        var safeType = System.Text.RegularExpressions.Regex.Replace(docType, "[^a-zA-Z0-9_-]", "");
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fileName = $"driver{driverId}_{safeType}_{unixTime}.{ext}";
        var destPath = Path.Combine(uploadDir, fileName);
        var publicPath = $"/uploads/docs/{fileName}"; // đường dẫn lưu vào DB (tương đối theo web root)

        try
        {
            await using (var stream = new FileStream(destPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
        }
        catch
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Không thể lưu file lên server" }, statusCode: 500);
        }

        try
        {
            await using var conn = await Db.OpenAsync();

            long? existingDocId = null;
            await using (var checkCmd = new MySqlCommand(
                "SELECT doc_id FROM driver_documents WHERE driver_id = @driver_id AND doc_type = @doc_type", conn))
            {
                checkCmd.Parameters.AddWithValue("@driver_id", driverId);
                checkCmd.Parameters.AddWithValue("@doc_type", docType);
                var result = await checkCmd.ExecuteScalarAsync();
                if (result is not null) existingDocId = Convert.ToInt64(result);
            }

            long docId;
            if (existingDocId.HasValue)
            {
                await using var updateCmd = new MySqlCommand(@"UPDATE driver_documents
                    SET doc_name = @doc_name, file_path = @file_path, status = 'pending', created_at = CURRENT_TIMESTAMP
                    WHERE doc_id = @doc_id", conn);
                updateCmd.Parameters.AddWithValue("@doc_name", docName);
                updateCmd.Parameters.AddWithValue("@file_path", publicPath);
                updateCmd.Parameters.AddWithValue("@doc_id", existingDocId.Value);
                await updateCmd.ExecuteNonQueryAsync();
                docId = existingDocId.Value;
            }
            else
            {
                await using var insertCmd = new MySqlCommand(@"INSERT INTO driver_documents (driver_id, doc_type, doc_name, file_path, status)
                    VALUES (@driver_id, @doc_type, @doc_name, @file_path, 'pending')", conn);
                insertCmd.Parameters.AddWithValue("@driver_id", driverId);
                insertCmd.Parameters.AddWithValue("@doc_type", docType);
                insertCmd.Parameters.AddWithValue("@doc_name", docName);
                insertCmd.Parameters.AddWithValue("@file_path", publicPath);
                await insertCmd.ExecuteNonQueryAsync();
                docId = insertCmd.LastInsertedId;
            }

            // Lưu ý: PHP gốc trả về khoá "doc_id" (không phải "docId") — giữ nguyên để đúng hành vi gốc.
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["message"] = "Đã tải lên, chờ admin duyệt",
                ["doc_id"] = docId,
                ["doc_type"] = docType,
                ["doc_name"] = docName,
                ["status"] = "pending",
                ["file_path"] = publicPath
            });
        }
        catch (Exception e)
        {
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Lỗi khi lưu CSDL: " + e.Message
            }, statusCode: 500);
        }
    }

    // ================= delete-document.php =================
    private static async Task<IResult> DeleteDocumentAsync(DeleteDocumentRequest? body, IWebHostEnvironment env)
    {
        var driverId = Constants.DriverId;
        var docId = body?.doc_id ?? 0;

        if (docId <= 0)
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Thiếu doc_id hợp lệ." });
        }

        await using var conn = await Db.OpenAsync();
        try
        {
            string? filePath = null;
            await using (var cmd = new MySqlCommand(
                "SELECT file_path FROM driver_documents WHERE doc_id = @doc_id AND driver_id = @driver_id", conn))
            {
                cmd.Parameters.AddWithValue("@doc_id", docId);
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return Results.Json(new Dictionary<string, object?>
                    {
                        ["ok"] = false,
                        ["message"] = "Không tìm thấy giấy tờ hoặc bạn không có quyền xóa."
                    });
                }
                filePath = reader.IsDBNull(reader.GetOrdinal("file_path")) ? null : reader.GetString("file_path");
            }

            int deletedRows;
            await using (var tx = await conn.BeginTransactionAsync())
            {
                await using (var delCmd = new MySqlCommand(
                    "DELETE FROM driver_documents WHERE doc_id = @doc_id AND driver_id = @driver_id", conn, tx))
                {
                    delCmd.Parameters.AddWithValue("@doc_id", docId);
                    delCmd.Parameters.AddWithValue("@driver_id", driverId);
                    deletedRows = await delCmd.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }

            if (deletedRows == 0)
            {
                return Results.Json(new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["message"] = "Xóa không thành công, vui lòng thử lại."
                });
            }

            // Xóa file vật lý trên server (nếu có), không làm hỏng response nếu lỗi
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var fullPath = Path.Combine(env.WebRootPath, filePath.TrimStart('/'));
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                }
                catch { /* bỏ qua lỗi xoá file vật lý, giống @unlink() trong PHP */ }
            }

            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["message"] = "Đã xóa giấy tờ.",
                ["docId"] = docId
            });
        }
        catch (Exception)
        {
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Lỗi CSDL khi xóa giấy tờ."
            }, statusCode: 500);
        }
    }
}