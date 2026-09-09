using MySqlConnector;
using XeGhepApp.Data;
using System.Globalization;

namespace XeGhepApp.Endpoints;

/// <summary>
/// Request body dùng cho start-trip.php / complete-trip.php (JSON: { "trip_id": "..." })
/// </summary>
public sealed class TripIdRequest
{
    public string? trip_id { get; set; }
}

/// <summary>
/// Request body dùng cho save-profile.php.
/// </summary>
public sealed class SaveProfileRequest
{
    public string? fullName { get; set; }
    public string? phone { get; set; }
    public string? vehicle { get; set; }
    public string? plate { get; set; }
    public int seatCount { get; set; }
    public string? operationArea { get; set; }
}

/// <summary>
/// Request body dùng cho delete-document.php (JSON: { doc_id, doc_type })
/// </summary>
public sealed class DeleteDocumentRequest
{
    public int doc_id { get; set; }
    public string? doc_type { get; set; }
}

/// <summary>
/// Request body dùng cho accept-booking.php / reject-booking.php
/// </summary>
public sealed class BookingIdRequest
{
    public string? booking_id { get; set; }
}

public sealed class AssignmentResponseRequest
{
    public string? reason { get; set; }
}

public sealed class TripCancellationRequest
{
    public string? reason { get; set; }
}

public sealed class DriverReviewRequest
{
    public int rating { get; set; }
    public string? comment { get; set; }
}

public sealed class DriverComplaintRequest
{
    public string? content { get; set; }
}

public static class DriverApiEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/auth/refresh", RefreshSessionAsync).AllowAnonymous().RequireRateLimiting("auth");
        app.MapPost("/api/auth/logout", LogoutSessionAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/start-trip.php", StartTripAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/complete-trip.php", CompleteTripAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/save-profile.php", SaveProfileAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/upload-document.php", UploadDocumentAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/delete-document.php", DeleteDocumentAsync).RequireAuthorization("DriverOnly");
        app.MapGet("/api/driver/documents/{id:long}/file", DownloadDocumentAsync).RequireAuthorization("DriverOnly");
        
        // Đăng ký 2 API mới cho Yêu cầu đặt chỗ
        app.MapPost("/accept-booking.php", AcceptBookingAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/reject-booking.php", RejectBookingAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/bookings/{id}/accept", (string id, System.Security.Claims.ClaimsPrincipal user) =>
            AcceptBookingAsync(new BookingIdRequest { booking_id = id }, user)).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/bookings/{id}/reject", (string id, System.Security.Claims.ClaimsPrincipal user) =>
            RejectBookingAsync(new BookingIdRequest { booking_id = id }, user)).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/bookings/{id}/confirm-cash", ConfirmCashAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/trips/{id}/start", (string id, System.Security.Claims.ClaimsPrincipal user) =>
            StartTripAsync(new TripIdRequest { trip_id = id }, user)).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/trips/{id}/complete", (string id, System.Security.Claims.ClaimsPrincipal user) =>
            CompleteTripAsync(new TripIdRequest { trip_id = id }, user)).RequireAuthorization("DriverOnly");
        app.MapGet("/api/driver/assignments", GetAssignmentsAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/assignments/{id:long}/accept", AcceptAssignmentAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/assignments/{id:long}/reject", RejectAssignmentAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/trips/{id}/cancel-request", RequestTripCancellationAsync).RequireAuthorization("DriverOnly");
        app.MapGet("/api/driver/notifications", GetNotificationsAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/notifications/{id:long}/read", ReadNotificationAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/bookings/{id}/review", ReviewPassengerAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/bookings/{id}/complaints", CreateComplaintAsync).RequireAuthorization("DriverOnly");
        app.MapGet("/api/driver/complaints/mine", GetMyComplaintsAsync).RequireAuthorization("DriverOnly");
        app.MapPost("/api/driver/complaints/{id}/attachments", UploadComplaintAttachmentAsync).RequireAuthorization("DriverOnly");
    }

    private static async Task<IResult> RefreshSessionAsync(HttpContext context, DriverTokenService tokens)
    {
        var refreshToken = context.Request.Cookies["xeghep_refresh"];
        if (string.IsNullOrWhiteSpace(refreshToken)) return Results.Unauthorized();

        var result = await tokens.RefreshAsync(refreshToken,
            context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent);
        if (result is null) return Results.Unauthorized();

        tokens.WriteCookies(context.Response, context.Request.IsHttps,
            result.Value.accessToken, result.Value.refreshToken, result.Value.accessExpiresAt);
        return Results.Ok(new { message = "Đã làm mới phiên đăng nhập." });
    }

    private static async Task<IResult> LogoutSessionAsync(HttpContext context, DriverTokenService tokens)
    {
        if (int.TryParse(context.User.FindFirst("session_id")?.Value, out var sessionId))
            await tokens.LogoutAsync(sessionId);
        DriverTokenService.DeleteCookies(context.Response);
        context.Session.Clear();
        return Results.Ok(new { message = "Đã đăng xuất." });
    }

    // ================= accept-booking.php =================
    private static async Task<IResult> AcceptBookingAsync(BookingIdRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        var bookingId = body?.booking_id;

        if (string.IsNullOrWhiteSpace(bookingId))
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Thiếu booking_id" }, statusCode: 400);
        }

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            int seats = 0;
            string tripId = "";
            string paymentMethod = "";
            long passengerId = 0;
            long holdId = 0;
            DateTime holdExpiresAt = DateTime.MinValue;

            await using (var cmd = new MySqlCommand(
                @"SELECT b.seats, b.trip_id, b.payment_method, b.passenger_id,
                         h.hold_id, h.expires_at
                  FROM bookings b
                  JOIN trips t ON b.trip_id = t.trip_id
                  JOIN seat_holds h ON h.booking_id = b.booking_id
                  WHERE b.booking_id = @bid AND t.driver_id = @did
                    AND b.status = 'pending_approval' AND h.status = 'active'
                  FOR UPDATE", conn, tx))
            {
                cmd.Parameters.AddWithValue("@bid", bookingId);
                cmd.Parameters.AddWithValue("@did", driverId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    seats = reader.GetInt32("seats");
                    tripId = reader.GetString("trip_id");
                    paymentMethod = reader.GetString("payment_method");
                    passengerId = reader.GetInt64("passenger_id");
                    holdId = reader.GetInt64("hold_id");
                    holdExpiresAt = reader.GetDateTime("expires_at");
                }
                else
                {
                    await tx.RollbackAsync();
                    return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Yêu cầu không tồn tại hoặc đã được xử lý" }, statusCode: 404);
                }
            }

            if (holdExpiresAt <= DateTime.UtcNow)
            {
                await using var expireCmd = new MySqlCommand(@"UPDATE seat_holds SET status = 'expired' WHERE hold_id = @hold_id;
                    UPDATE bookings SET status = 'cancelled', cancelled_at = UTC_TIMESTAMP(), cancellation_reason = 'Seat hold expired'
                    WHERE booking_id = @bid", conn, tx);
                expireCmd.Parameters.AddWithValue("@hold_id", holdId);
                expireCmd.Parameters.AddWithValue("@bid", bookingId);
                await expireCmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Thời gian giữ ghế đã hết hạn" }, statusCode: 409);
            }

            await using (var cmd = new MySqlCommand(@"UPDATE trips
                SET available_seats = available_seats - @seats,
                    status = IF(available_seats - @seats = 0, 'full', status)
                WHERE trip_id = @tid AND available_seats >= @seats", conn, tx))
            {
                cmd.Parameters.AddWithValue("@seats", seats);
                cmd.Parameters.AddWithValue("@tid", tripId);
                if (await cmd.ExecuteNonQueryAsync() != 1)
                {
                    await tx.RollbackAsync();
                    return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Chuyến không còn đủ ghế" }, statusCode: 409);
                }
            }

            await using (var cmd = new MySqlCommand(@"UPDATE bookings
                SET status = 'confirmed', confirmed_at = UTC_TIMESTAMP(),
                    payment_status = IF(payment_method = 'cash', 'pending_cash', 'pending')
                WHERE booking_id = @bid;
                UPDATE seat_holds SET status = 'confirmed' WHERE hold_id = @hold_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@bid", bookingId);
                cmd.Parameters.AddWithValue("@hold_id", holdId);
                await cmd.ExecuteNonQueryAsync();
            }

            if (paymentMethod == "cash")
            {
                await using var paymentCmd = new MySqlCommand(@"INSERT IGNORE INTO payments
                    (booking_id, method, provider, idempotency_key, amount, status)
                    SELECT booking_id, 'cash', 'driver_cash', CONCAT('cash:', booking_id), total_amount, 'pending'
                    FROM bookings WHERE booking_id = @bid", conn, tx);
                paymentCmd.Parameters.AddWithValue("@bid", bookingId);
                await paymentCmd.ExecuteNonQueryAsync();
            }

            await using (var eventCmd = new MySqlCommand(@"INSERT INTO notifications
                (recipient_user_id, type, title, body, resource_type, resource_id)
                VALUES (@passenger_id, 'booking_confirmed', 'Đặt chỗ đã được duyệt',
                        CONCAT('Booking ', @bid, ' đã được tài xế xác nhận.'), 'booking', @bid);
                INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, created_at)
                VALUES (@driver_id, 'booking.confirm', 'booking', @bid, UTC_TIMESTAMP())", conn, tx))
            {
                eventCmd.Parameters.AddWithValue("@passenger_id", passengerId);
                eventCmd.Parameters.AddWithValue("@driver_id", driverId);
                eventCmd.Parameters.AddWithValue("@bid", bookingId);
                await eventCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["message"] = "Đã chấp nhận yêu cầu đặt chỗ"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Không thể xác nhận đặt chỗ." }, statusCode: 500);
        }
    }

    // ================= reject-booking.php =================
    private static async Task<IResult> RejectBookingAsync(BookingIdRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        var bookingId = body?.booking_id;

        if (string.IsNullOrWhiteSpace(bookingId))
        {
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Thiếu booking_id" }, statusCode: 400);
        }

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using var cmd = new MySqlCommand(@"UPDATE bookings b
                JOIN trips t ON b.trip_id = t.trip_id
                JOIN seat_holds h ON h.booking_id = b.booking_id
                SET b.status = 'rejected', h.status = 'released'
                WHERE b.booking_id = @bid AND t.driver_id = @did AND b.status = 'pending_approval'", conn, tx);
            cmd.Parameters.AddWithValue("@bid", bookingId);
            cmd.Parameters.AddWithValue("@did", driverId);
            
            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0)
            {
                await using var eventCmd = new MySqlCommand(@"INSERT INTO notifications
                    (recipient_user_id, type, title, body, resource_type, resource_id)
                    SELECT passenger_id, 'booking_rejected', 'Đặt chỗ bị từ chối',
                           CONCAT('Booking ', booking_id, ' đã bị tài xế từ chối.'), 'booking', booking_id
                    FROM bookings WHERE booking_id = @bid;
                    INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, created_at)
                    VALUES (@did, 'booking.reject', 'booking', @bid, UTC_TIMESTAMP())", conn, tx);
                eventCmd.Parameters.AddWithValue("@bid", bookingId);
                eventCmd.Parameters.AddWithValue("@did", driverId);
                await eventCmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return Results.Json(new Dictionary<string, object?> { ["ok"] = true, ["message"] = "Đã từ chối yêu cầu" });
            }
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Không tìm thấy yêu cầu hoặc bạn không có quyền" }, statusCode: 404);
        }
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Không thể từ chối đặt chỗ." }, statusCode: 500);
        }
    }

    // ================= start-trip.php =================
    private static async Task<IResult> StartTripAsync(TripIdRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
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

            if (currentStatus is not ("upcoming" or "full"))
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
                "UPDATE bookings SET status = 'running' WHERE trip_id = @trip_id AND status = 'paid' AND payment_status = 'paid'", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                affectedBookings = await cmd.ExecuteNonQueryAsync();
            }

            await using (var eventCmd = new MySqlCommand(@"INSERT INTO notifications
                (recipient_user_id, type, title, body, resource_type, resource_id)
                SELECT passenger_id, 'trip_running', 'Chuyến đi đã bắt đầu',
                       CONCAT('Chuyến ', @trip_id, ' đã bắt đầu.'), 'trip', @trip_id
                FROM bookings WHERE trip_id = @trip_id AND status = 'running';
                INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, created_at)
                VALUES (@driver_id, 'trip.start', 'trip', @trip_id, UTC_TIMESTAMP())", conn, tx))
            {
                eventCmd.Parameters.AddWithValue("@trip_id", tripId);
                eventCmd.Parameters.AddWithValue("@driver_id", driverId);
                await eventCmd.ExecuteNonQueryAsync();
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
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Không thể bắt đầu chuyến đi."
            }, statusCode: 500);
        }
    }

    // ================= complete-trip.php =================
    private static async Task<IResult> CompleteTripAsync(TripIdRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
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

            // Tự động xác nhận thanh toán tiền mặt khi tài xế hoàn thành chuyến:
            // "người dùng không cần xác nhận đến điểm khi thanh toán tiền mặt, chỉ cần tài xế xác nhận chuyến đi hoàn thành là được, tiền sẽ được cộng vào doanh thu"
            await using (var autoCashCmd = new MySqlCommand(@"UPDATE bookings
                SET payment_status = 'paid'
                WHERE trip_id = @trip_id AND payment_method = 'cash'
                  AND payment_status = 'pending_cash' AND status IN ('confirmed','paid','running','approved')", conn, tx))
            {
                autoCashCmd.Parameters.AddWithValue("@trip_id", tripId);
                await autoCashCmd.ExecuteNonQueryAsync();
            }

            long cashUnconfirmed = 0;

            await using (var cmd = new MySqlCommand("UPDATE trips SET status = 'done' WHERE trip_id = @trip_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                await cmd.ExecuteNonQueryAsync();
            }

            var bookings = new List<(string BookingId, long PassengerId, int Seats, decimal TotalAmount, string PaymentMethod)>();
            await using (var cmd = new MySqlCommand(
                "SELECT booking_id, passenger_id, seats, total_amount, payment_method FROM bookings WHERE trip_id = @trip_id AND payment_status = 'paid' AND status IN ('running','confirmed','approved')", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    bookings.Add((
                        reader.GetString("booking_id"),
                        reader.GetInt64("passenger_id"),
                        reader.GetInt32("seats"),
                        reader.GetDecimal("total_amount"),
                        reader.GetString("payment_method")
                    ));
                }
            }

            await using (var cmd = new MySqlCommand(
                "UPDATE bookings SET status = 'done' WHERE trip_id = @trip_id AND status IN ('running','confirmed','approved')", conn, tx))
            {
                cmd.Parameters.AddWithValue("@trip_id", tripId);
                await cmd.ExecuteNonQueryAsync();
            }

            decimal commissionRate = 10m;
            await using (var cmd = new MySqlCommand("SELECT setting_value FROM system_settings WHERE setting_key = 'commission_rate'", conn, tx))
            {
                var val = await cmd.ExecuteScalarAsync();
                if (val is not null && decimal.TryParse(val.ToString(), out var parsed))
                {
                    commissionRate = parsed;
                }
            }

            bool autoCashReconcile = false;
            await using (var cmd = new MySqlCommand("SELECT setting_value FROM system_settings WHERE setting_key = 'auto_cash_reconcile'", conn, tx))
            {
                var val = await cmd.ExecuteScalarAsync();
                autoCashReconcile = val?.ToString() == "true";
            }

            decimal walletCredit = 0;
            decimal walletHeld = 0;
            int txCreated = 0;

            await using (var walletInit = new MySqlCommand(
                "INSERT IGNORE INTO wallets (user_id, available_balance, held_balance) VALUES (@driver_id, 0, 0)", conn, tx))
            {
                walletInit.Parameters.AddWithValue("@driver_id", driverId);
                await walletInit.ExecuteNonQueryAsync();
            }

            foreach (var b in bookings)
            {
                long existingCount;
                await using (var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM transactions WHERE booking_id = @booking_id", conn, tx))
                {
                    checkCmd.Parameters.AddWithValue("@booking_id", b.BookingId);
                    existingCount = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);
                }

                if (existingCount > 0) continue; 

                var totalAmount = b.TotalAmount;
                var commission = Math.Round(totalAmount * commissionRate / 100m, 2, MidpointRounding.AwayFromZero);
                var driverReceive = Math.Round(totalAmount - commission, 2, MidpointRounding.AwayFromZero);
                var isOnline = b.PaymentMethod == "online";
                var txStatus = "approved";
                var note = isOnline
                    ? $"Chuyến {tripId} thanh toán online — cộng ví tài xế"
                    : $"Chuyến {tripId} thu tiền mặt — hoàn thành chuyến, cộng ví tài xế";

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

                // Escrow chỉ tồn tại với thanh toán online (tiền khách đã nằm trên hệ thống),
                // chuyến tiền mặt tài xế thu trực tiếp nên không có gì để giải ngân.
                if (isOnline)
                {
                    await using var escrowRelease = new MySqlCommand(@"UPDATE payment_escrows
                        SET status='released', commission_amount=@commission,
                            released_to_driver=@driver_receive, released_at=UTC_TIMESTAMP()
                        WHERE booking_id=@booking_id AND status='held'", conn, tx);
                    escrowRelease.Parameters.AddWithValue("@commission", commission);
                    escrowRelease.Parameters.AddWithValue("@driver_receive", driverReceive);
                    escrowRelease.Parameters.AddWithValue("@booking_id", b.BookingId);
                    if (await escrowRelease.ExecuteNonQueryAsync() != 1)
                        throw new InvalidOperationException($"Escrow của booking {b.BookingId} không ở trạng thái có thể giải ngân.");
                }

                // Cộng ví cho cả chuyến tiền mặt: đã đối soát (auto_cash_reconcile) thì vào số dư
                // khả dụng để tài xế rút được, còn chờ đối soát thì tạm giữ ở held_balance.
                var creditAvailable = txStatus == "approved" ? driverReceive : 0m;
                var creditHeld = txStatus == "approved" ? 0m : driverReceive;
                walletCredit += creditAvailable;
                walletHeld += creditHeld;

                decimal availableAfter;
                decimal heldAfter;
                await using (var walletUpdate = new MySqlCommand(@"UPDATE wallets
                    SET available_balance = available_balance + @available,
                        held_balance = held_balance + @held
                    WHERE user_id = @driver_id", conn, tx))
                {
                    walletUpdate.Parameters.AddWithValue("@available", creditAvailable);
                    walletUpdate.Parameters.AddWithValue("@held", creditHeld);
                    walletUpdate.Parameters.AddWithValue("@driver_id", driverId);
                    await walletUpdate.ExecuteNonQueryAsync();
                }
                await using (var walletRead = new MySqlCommand(
                    "SELECT available_balance, held_balance FROM wallets WHERE user_id = @driver_id", conn, tx))
                {
                    walletRead.Parameters.AddWithValue("@driver_id", driverId);
                    await using var reader = await walletRead.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    availableAfter = reader.GetDecimal("available_balance");
                    heldAfter = reader.GetDecimal("held_balance");
                }

                await using (var ledger = new MySqlCommand(@"INSERT INTO wallet_transactions
                    (user_id, type, amount, available_balance_after, held_balance_after,
                     reference_type, reference_id, status, note)
                    VALUES (@driver_id, @type, @amount, @available_after, @held_after,
                            'booking', @booking_id, @status, @note)", conn, tx))
                {
                    ledger.Parameters.AddWithValue("@driver_id", driverId);
                    ledger.Parameters.AddWithValue("@type", "trip_income");
                    ledger.Parameters.AddWithValue("@amount", driverReceive);
                    ledger.Parameters.AddWithValue("@available_after", availableAfter);
                    ledger.Parameters.AddWithValue("@held_after", heldAfter);
                    ledger.Parameters.AddWithValue("@booking_id", b.BookingId);
                    ledger.Parameters.AddWithValue("@status", txStatus == "approved" ? "posted" : "pending");
                    ledger.Parameters.AddWithValue("@note", note);
                    await ledger.ExecuteNonQueryAsync();
                }

                await using var notify = new MySqlCommand(@"INSERT INTO notifications
                    (recipient_user_id, type, title, body, resource_type, resource_id)
                    VALUES (@passenger_id, 'trip_done', 'Chuyến đi đã hoàn thành',
                            CONCAT('Chuyến ', @trip_id, ' đã hoàn thành.'), 'trip', @trip_id)", conn, tx);
                notify.Parameters.AddWithValue("@passenger_id", b.PassengerId);
                notify.Parameters.AddWithValue("@trip_id", tripId);
                await notify.ExecuteNonQueryAsync();
            }

            await using (var auditCmd = new MySqlCommand(@"INSERT INTO audit_logs
                (actor_user_id, action, resource_type, resource_id, created_at)
                VALUES (@driver_id, 'trip.complete', 'trip', @trip_id, UTC_TIMESTAMP())", conn, tx))
            {
                auditCmd.Parameters.AddWithValue("@driver_id", driverId);
                auditCmd.Parameters.AddWithValue("@trip_id", tripId);
                await auditCmd.ExecuteNonQueryAsync();
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
                ["message"] = cashUnconfirmed > 0
                    ? $"Đã hoàn thành chuyến. Còn {cashUnconfirmed} khách tiền mặt chưa bấm \"Xác nhận tiền mặt\" nên chưa được tính vào thu nhập."
                    : "Đã hoàn thành chuyến",
                ["trip_id"] = tripId,
                ["trip_status"] = "done",
                ["transactions_created"] = txCreated,
                ["cash_unconfirmed"] = cashUnconfirmed,
                ["wallet_credit"] = walletCredit,
                ["wallet_held"] = walletHeld
            });
        }
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Không thể hoàn thành chuyến đi."
            }, statusCode: 500);
        }
    }

    // ================= save-profile.php =================
    private static async Task<IResult> SaveProfileAsync(SaveProfileRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        var fullName = (body?.fullName ?? "").Trim();
        var phone = (body?.phone ?? "").Trim();
        var vehicle = (body?.vehicle ?? "").Trim();
        var plate = (body?.plate ?? "").Trim();
        var operationArea = (body?.operationArea ?? "").Trim();
        var seatCount = body?.seatCount ?? 0;

        if (fullName == "" || phone == "" || vehicle == "" || plate == "" || operationArea == "" || seatCount is < 1 or > 16)
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
                @"UPDATE driver_profiles SET vehicle_type = @vehicle, license_plate = @plate,
                  operation_area = @operation_area, seat_count = @seat_count WHERE driver_id = @driver_id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@vehicle", vehicle);
                cmd.Parameters.AddWithValue("@plate", plate);
                cmd.Parameters.AddWithValue("@operation_area", operationArea);
                cmd.Parameters.AddWithValue("@seat_count", seatCount);
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
                ["plate"] = plate,
                ["operationArea"] = operationArea,
                ["seatCount"] = seatCount
            });
        }
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Không thể cập nhật hồ sơ."
            }, statusCode: 500);
        }
    }

    // ================= upload-document.php =================
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png", "pdf" };
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private static async Task<IResult> UploadDocumentAsync(HttpRequest request, IWebHostEnvironment env, IConfiguration configuration, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);

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

        if (!await HasValidFileSignatureAsync(file, ext))
            return Results.Json(new Dictionary<string, object?> { ["ok"] = false, ["message"] = "Nội dung file không đúng định dạng." }, statusCode: 400);

        var uploadDir = Path.Combine(GetDriverDocumentRoot(env, configuration), "driver-documents");
        Directory.CreateDirectory(uploadDir);

        var safeType = System.Text.RegularExpressions.Regex.Replace(docType, "[^a-zA-Z0-9_-]", "");
        var fileName = $"driver{driverId}_{safeType}_{Guid.NewGuid():N}.{ext}";
        var destPath = Path.Combine(uploadDir, fileName);
        var storagePath = $"driver-documents/{fileName}";

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
                    SET doc_name = @doc_name, file_path = @file_path, mime_type = @mime_type,
                        file_size = @file_size, status = 'pending', rejection_reason = NULL, created_at = CURRENT_TIMESTAMP
                    WHERE doc_id = @doc_id", conn);
                updateCmd.Parameters.AddWithValue("@doc_name", docName);
                updateCmd.Parameters.AddWithValue("@file_path", storagePath);
                updateCmd.Parameters.AddWithValue("@mime_type", file.ContentType);
                updateCmd.Parameters.AddWithValue("@file_size", file.Length);
                updateCmd.Parameters.AddWithValue("@doc_id", existingDocId.Value);
                await updateCmd.ExecuteNonQueryAsync();
                docId = existingDocId.Value;
            }
            else
            {
                await using var insertCmd = new MySqlCommand(@"INSERT INTO driver_documents
                    (driver_id, doc_type, doc_name, file_path, mime_type, file_size, status)
                    VALUES (@driver_id, @doc_type, @doc_name, @file_path, @mime_type, @file_size, 'pending')", conn);
                insertCmd.Parameters.AddWithValue("@driver_id", driverId);
                insertCmd.Parameters.AddWithValue("@doc_type", docType);
                insertCmd.Parameters.AddWithValue("@doc_name", docName);
                insertCmd.Parameters.AddWithValue("@file_path", storagePath);
                insertCmd.Parameters.AddWithValue("@mime_type", file.ContentType);
                insertCmd.Parameters.AddWithValue("@file_size", file.Length);
                await insertCmd.ExecuteNonQueryAsync();
                docId = insertCmd.LastInsertedId;
            }

            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["message"] = "Đã tải lên, chờ admin duyệt",
                ["doc_id"] = docId,
                ["doc_type"] = docType,
                ["doc_name"] = docName,
                ["status"] = "pending",
                ["file_path"] = $"/api/driver/documents/{docId}/file"
            });
        }
        catch
        {
            return Results.Json(new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["message"] = "Không thể lưu tài liệu."
            }, statusCode: 500);
        }
    }

    // ================= delete-document.php =================
    private static async Task<IResult> DeleteDocumentAsync(DeleteDocumentRequest? body, IWebHostEnvironment env, IConfiguration configuration, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
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

            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var storageRoot = GetDriverDocumentRoot(env, configuration);
                    var relativePath = filePath.Replace('\\', '/').TrimStart('/');
                    var fullPath = Path.GetFullPath(Path.Combine(storageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                    if (fullPath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)) File.Delete(fullPath);
                }
                catch { }
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

    private static async Task<bool> HasValidFileSignatureAsync(IFormFile file, string extension)
    {
        var header = new byte[8];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header);
        var jpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var png = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var pdf = read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        return extension switch { "jpg" or "jpeg" => jpeg, "png" => png, "pdf" => pdf, _ => false };
    }

    private static async Task<IResult> DownloadDocumentAsync(long id, IWebHostEnvironment env, IConfiguration configuration, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        await using var conn = await Db.OpenAsync();
        await using var cmd = new MySqlCommand(@"SELECT file_path, mime_type, doc_name FROM driver_documents
            WHERE doc_id = @id AND driver_id = @driver_id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return Results.NotFound(new { message = "Không tìm thấy tài liệu." });
        var storagePath = reader.GetString("file_path").Replace('\\', '/').TrimStart('/');
        var contentType = reader.IsDBNull(reader.GetOrdinal("mime_type")) ? "application/octet-stream" : reader.GetString("mime_type");
        var downloadName = reader.GetString("doc_name") + Path.GetExtension(storagePath);
        var storageRoot = GetDriverDocumentRoot(env, configuration);
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return Results.NotFound(new { message = "File tài liệu không còn tồn tại." });
        return Results.File(fullPath, contentType, downloadName);
    }

    private static string GetDriverDocumentRoot(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredRoot = configuration["Storage:DriverDocumentsRoot"] ?? "Data";
        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredRoot));
    }

    private static async Task<IResult> ConfirmCashAsync(string id, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            long passengerId;
            string paymentStatus;
            await using (var cmd = new MySqlCommand(@"SELECT b.passenger_id, p.status
                FROM bookings b
                JOIN trips t ON t.trip_id = b.trip_id
                JOIN payments p ON p.booking_id = b.booking_id AND p.method = 'cash'
                WHERE b.booking_id = @booking_id AND t.driver_id = @driver_id
                  AND b.payment_method = 'cash' AND b.status IN ('confirmed','paid')
                FOR UPDATE", conn, tx))
            {
                cmd.Parameters.AddWithValue("@booking_id", id);
                cmd.Parameters.AddWithValue("@driver_id", driverId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return Results.Json(new { ok = false, message = "Không tìm thấy booking tiền mặt hợp lệ." }, statusCode: 404);
                passengerId = reader.GetInt64("passenger_id");
                paymentStatus = reader.GetString("status");
            }

            if (paymentStatus == "paid")
            {
                await tx.CommitAsync();
                return Results.Json(new { ok = true, message = "Khoản tiền đã được xác nhận trước đó." });
            }

            await using var update = new MySqlCommand(@"UPDATE payments
                SET status = 'paid', paid_at = UTC_TIMESTAMP()
                WHERE booking_id = @booking_id AND method = 'cash' AND status = 'pending';
                UPDATE bookings SET status = 'paid', payment_status = 'paid', paid_at = UTC_TIMESTAMP()
                WHERE booking_id = @booking_id AND status = 'confirmed';
                INSERT INTO notifications (recipient_user_id, type, title, body, resource_type, resource_id)
                VALUES (@passenger_id, 'payment_paid', 'Đã xác nhận tiền mặt',
                        CONCAT('Tài xế đã xác nhận thanh toán booking ', @booking_id, '.'), 'booking', @booking_id);
                INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, created_at)
                VALUES (@driver_id, 'payment.cash_confirm', 'booking', @booking_id, UTC_TIMESTAMP())", conn, tx);
            update.Parameters.AddWithValue("@booking_id", id);
            update.Parameters.AddWithValue("@passenger_id", passengerId);
            update.Parameters.AddWithValue("@driver_id", driverId);
            await update.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            return Results.Json(new { ok = true, message = "Đã xác nhận nhận tiền mặt." });
        }
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new { ok = false, message = "Không thể xác nhận tiền mặt." }, statusCode: 500);
        }
    }

    private static async Task<IResult> GetAssignmentsAsync(System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        var items = new List<Dictionary<string, object?>>();
        await using var conn = await Db.OpenAsync();
        await using var cmd = new MySqlCommand(@"SELECT a.assignment_id, a.trip_id, a.status, a.rejection_reason,
            a.assigned_at, a.responded_at, t.route_from, t.route_to, t.pickup_location,
            t.dropoff_location, t.departure_time, t.price_per_seat, t.total_seats
            FROM trip_assignments a JOIN trips t ON t.trip_id = a.trip_id
            WHERE a.driver_id = @driver_id ORDER BY a.assigned_at DESC", conn);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new Dictionary<string, object?>
            {
                ["assignment_id"] = reader.GetInt64("assignment_id"),
                ["trip_id"] = reader.GetString("trip_id"),
                ["status"] = reader.GetString("status"),
                ["route_from"] = reader.GetString("route_from"),
                ["route_to"] = reader.GetString("route_to"),
                ["pickup_location"] = reader.GetString("pickup_location"),
                ["dropoff_location"] = reader.GetString("dropoff_location"),
                ["departure_time"] = reader.GetDateTime("departure_time"),
                ["price_per_seat"] = reader.GetDecimal("price_per_seat"),
                ["total_seats"] = reader.GetInt32("total_seats"),
                ["rejection_reason"] = reader.IsDBNull(reader.GetOrdinal("rejection_reason")) ? null : reader.GetString("rejection_reason"),
            });
        }
        return Results.Ok(items);
    }

    private static async Task<IResult> AcceptAssignmentAsync(long id, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            string? tripId = null;
            await using (var select = new MySqlCommand(@"SELECT trip_id FROM trip_assignments
                WHERE assignment_id = @id AND driver_id = @driver_id AND status = 'pending' FOR UPDATE", conn, tx))
            {
                select.Parameters.AddWithValue("@id", id);
                select.Parameters.AddWithValue("@driver_id", driverId);
                tripId = (string?)await select.ExecuteScalarAsync();
            }
            if (tripId == null) return Results.NotFound(new { message = "Không tìm thấy phân công đang chờ." });

            await using var update = new MySqlCommand(@"UPDATE trip_assignments
                SET status = 'accepted', responded_at = UTC_TIMESTAMP() WHERE assignment_id = @id;
                UPDATE trips SET driver_id = @driver_id, status = 'upcoming', driver_responded_at = UTC_TIMESTAMP()
                WHERE trip_id = @trip_id AND status = 'scheduled';
                INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, created_at)
                VALUES (@driver_id, 'assignment.accept', 'assignment', @id, UTC_TIMESTAMP())", conn, tx);
            update.Parameters.AddWithValue("@id", id);
            update.Parameters.AddWithValue("@driver_id", driverId);
            update.Parameters.AddWithValue("@trip_id", tripId);
            await update.ExecuteNonQueryAsync();
            await UpdateAcceptanceStatsAsync(conn, tx, driverId, accepted: true);
            await tx.CommitAsync();
            return Results.Ok(new { message = "Đã nhận chuyến được phân công.", trip_id = tripId });
        }
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new { message = "Không thể nhận chuyến." }, statusCode: 500);
        }
    }

    private static async Task<IResult> RejectAssignmentAsync(long id, AssignmentResponseRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        var reason = body?.reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) return Results.BadRequest(new { message = "Vui lòng nhập lý do từ chối." });

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        string? tripId;
        await using (var select = new MySqlCommand(@"SELECT trip_id FROM trip_assignments
            WHERE assignment_id=@id AND driver_id=@driver_id AND status='pending' FOR UPDATE", conn, tx))
        {
            select.Parameters.AddWithValue("@id", id);
            select.Parameters.AddWithValue("@driver_id", driverId);
            tripId = (string?)await select.ExecuteScalarAsync();
        }
        if (tripId == null) return Results.NotFound(new { message = "Không tìm thấy phân công đang chờ." });

        await using var cmd = new MySqlCommand(@"UPDATE trip_assignments SET status='rejected', rejection_reason=@reason,
            responded_at=UTC_TIMESTAMP() WHERE assignment_id=@id;
            UPDATE trips SET driver_id=NULL, assigned_at=NULL WHERE trip_id=@trip_id AND status='scheduled' AND driver_id=@driver_id;
            INSERT INTO notifications (recipient_user_id, type, title, body, resource_type, resource_id)
            SELECT u.user_id, 'assignment_rejected', 'Tài xế từ chối chuyến',
                   CONCAT('Tài xế đã từ chối chuyến ', @trip_id, ': ', @reason), 'trip', @trip_id
            FROM users u LEFT JOIN admin_profiles ap ON ap.user_id=u.user_id
            WHERE u.role='admin' AND u.status='active' AND ap.admin_role IN ('full_access','operations');
            INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, after_data, created_at)
            VALUES (@driver_id, 'assignment.reject', 'assignment', @id, JSON_OBJECT('reason',@reason), UTC_TIMESTAMP())", conn, tx);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@trip_id", tripId);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        await cmd.ExecuteNonQueryAsync();
        await UpdateAcceptanceStatsAsync(conn, tx, driverId, accepted: false);
        await tx.CommitAsync();
        return Results.Ok(new { message = "Đã từ chối chuyến được phân công." });
    }

    private static async Task<IResult> RequestTripCancellationAsync(string id, TripCancellationRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        var reason = body?.reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) return Results.BadRequest(new { message = "Vui lòng nhập lý do xin hủy chuyến." });

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        string? status;
        await using (var select = new MySqlCommand("SELECT status FROM trips WHERE trip_id=@id AND driver_id=@driver_id FOR UPDATE", conn, tx))
        {
            select.Parameters.AddWithValue("@id", id);
            select.Parameters.AddWithValue("@driver_id", driverId);
            status = (string?)await select.ExecuteScalarAsync();
        }
        if (status == null) return Results.NotFound(new { message = "Không tìm thấy chuyến của bạn." });
        if (status is not ("scheduled" or "upcoming" or "full"))
            return Results.Conflict(new { message = "Chỉ có thể xin hủy trước khi chuyến bắt đầu." });

        await using var cmd = new MySqlCommand(@"UPDATE trip_assignments SET status='cancelled', rejection_reason=@reason,
            responded_at=UTC_TIMESTAMP() WHERE trip_id=@id AND driver_id=@driver_id AND status IN ('pending','accepted');
            UPDATE trips SET driver_id=NULL, status='scheduled', assigned_at=NULL, driver_responded_at=UTC_TIMESTAMP()
            WHERE trip_id=@id AND driver_id=@driver_id;
            INSERT INTO notifications (recipient_user_id, type, title, body, resource_type, resource_id)
            SELECT u.user_id, 'trip_reassignment_requested', 'Cần phân công lại tài xế',
                   CONCAT('Tài xế xin rời chuyến ', @id, ': ', @reason), 'trip', @id
            FROM users u LEFT JOIN admin_profiles ap ON ap.user_id=u.user_id
            WHERE u.role='admin' AND u.status='active' AND ap.admin_role IN ('full_access','operations');
            INSERT INTO notifications (recipient_user_id, type, title, body, resource_type, resource_id)
            SELECT DISTINCT passenger_id, 'driver_reassignment', 'Chuyến đang đổi tài xế',
                   CONCAT('Chuyến ', @id, ' đang được phân công lại tài xế.'), 'trip', @id
            FROM bookings WHERE trip_id=@id AND status NOT IN ('cancelled','rejected');
            INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, after_data, created_at)
            VALUES (@driver_id, 'trip.reassignment_request', 'trip', @id, JSON_OBJECT('reason',@reason), UTC_TIMESTAMP());
            INSERT INTO notifications (recipient_user_id, type, title, body, resource_type, resource_id)
            SELECT u.user_id, 'driver_cancellation_review', 'Cần xem xét tài khoản tài xế',
                   CONCAT('Tài xế #', @driver_id, ' đã xin rời từ 3 chuyến trong 30 ngày. Hãy xem xét cảnh báo hoặc khóa tài khoản.'),
                   'user', CAST(@driver_id AS CHAR)
            FROM users u LEFT JOIN admin_profiles ap ON ap.user_id=u.user_id
            WHERE u.role='admin' AND u.status='active' AND ap.admin_role IN ('full_access','operations')
              AND (SELECT COUNT(*) FROM audit_logs WHERE actor_user_id=@driver_id
                   AND action='trip.reassignment_request' AND created_at >= UTC_TIMESTAMP() - INTERVAL 30 DAY) >= 3", conn, tx);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        await cmd.ExecuteNonQueryAsync();
        await UpdateAcceptanceStatsAsync(conn, tx, driverId, accepted: false);
        await tx.CommitAsync();
        return Results.Ok(new { message = "Đã gửi yêu cầu; chuyến được chuyển về trạng thái chờ Admin phân công lại." });
    }

    private static async Task<IResult> GetNotificationsAsync(int page, int pageSize, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 50);
        await using var conn = await Db.OpenAsync();
        await using var cmd = new MySqlCommand(@"SELECT notification_id, type, title, body, resource_type, resource_id,
            read_at, created_at FROM notifications WHERE recipient_user_id=@driver_id
            ORDER BY created_at DESC LIMIT @take OFFSET @skip", conn);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        cmd.Parameters.AddWithValue("@take", pageSize);
        cmd.Parameters.AddWithValue("@skip", (page - 1) * pageSize);
        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(new
        {
            id = reader.GetInt64("notification_id"),
            type = reader.GetString("type"),
            title = reader.GetString("title"),
            body = reader.GetString("body"),
            resource_type = reader.IsDBNull(reader.GetOrdinal("resource_type")) ? null : reader.GetString("resource_type"),
            resource_id = reader.IsDBNull(reader.GetOrdinal("resource_id")) ? null : reader.GetString("resource_id"),
            read = !reader.IsDBNull(reader.GetOrdinal("read_at")),
            created_at = reader.GetDateTime("created_at")
        });
        return Results.Ok(new { page, pageSize, items });
    }

    private static async Task<IResult> ReadNotificationAsync(long id, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        await using var conn = await Db.OpenAsync();
        await using var cmd = new MySqlCommand(@"UPDATE notifications SET read_at=COALESCE(read_at,UTC_TIMESTAMP())
            WHERE notification_id=@id AND recipient_user_id=@driver_id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        return await cmd.ExecuteNonQueryAsync() == 1 ? Results.Ok(new { message = "Đã đọc." }) : Results.NotFound();
    }

    private static async Task<IResult> ReviewPassengerAsync(string id, DriverReviewRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        if (body == null || body.rating is < 1 or > 5) return Results.BadRequest(new { message = "Số sao phải từ 1 đến 5." });
        await using var conn = await Db.OpenAsync();
        try
        {
            await using var cmd = new MySqlCommand(@"INSERT INTO reviews
                (booking_id, trip_id, passenger_id, driver_id, reviewer_id, reviewee_id, reviewer_role, rating, comment)
                SELECT b.booking_id, b.trip_id, b.passenger_id, @driver_id, @driver_id, b.passenger_id, 'driver', @rating, @comment
                FROM bookings b JOIN trips t ON t.trip_id=b.trip_id
                WHERE b.booking_id=@id AND t.driver_id=@driver_id AND b.status='done'", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@driver_id", driverId);
            cmd.Parameters.AddWithValue("@rating", body.rating);
            cmd.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(body.comment) ? DBNull.Value : body.comment.Trim());
            if (await cmd.ExecuteNonQueryAsync() != 1) return Results.NotFound(new { message = "Chỉ đánh giá được hành khách của chuyến đã hoàn thành." });
            return Results.Ok(new { message = "Đã gửi đánh giá hành khách." });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Results.Conflict(new { message = "Bạn đã đánh giá hành khách của đặt chỗ này." });
        }
    }

    private static async Task<IResult> CreateComplaintAsync(string id, DriverComplaintRequest? body, System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        var content = body?.content?.Trim();
        if (string.IsNullOrWhiteSpace(content)) return Results.BadRequest(new { message = "Vui lòng nhập nội dung khiếu nại." });
        if (content.Length > 2000) return Results.BadRequest(new { message = "Nội dung khiếu nại không được vượt quá 2.000 ký tự." });

        await using var conn = await Db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            string? tripId = null;
            int passengerId = 0;
            await using (var select = new MySqlCommand(@"SELECT b.trip_id, b.passenger_id
                FROM bookings b JOIN trips t ON t.trip_id=b.trip_id
                WHERE b.booking_id=@id AND t.driver_id=@driver_id FOR UPDATE", conn, tx))
            {
                select.Parameters.AddWithValue("@id", id);
                select.Parameters.AddWithValue("@driver_id", driverId);
                await using var reader = await select.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    tripId = reader.GetString("trip_id");
                    passengerId = reader.GetInt32("passenger_id");
                }
            }
            if (tripId == null) return Results.NotFound(new { message = "Không tìm thấy hành khách thuộc chuyến của bạn." });

            long sequence;
            await using (var selectSequence = new MySqlCommand(
                "SELECT current_value FROM id_sequences WHERE sequence_name='complaint' FOR UPDATE", conn, tx))
                sequence = Convert.ToInt64(await selectSequence.ExecuteScalarAsync()) + 1;
            await using (var updateSequence = new MySqlCommand(
                "UPDATE id_sequences SET current_value=@value WHERE sequence_name='complaint'", conn, tx))
            {
                updateSequence.Parameters.AddWithValue("@value", sequence);
                await updateSequence.ExecuteNonQueryAsync();
            }

            var complaintId = $"CP-{sequence:D6}";
            await using var insert = new MySqlCommand(@"INSERT INTO complaints
                (complaint_id, booking_id, trip_id, created_by, target_user_id, content, status)
                VALUES (@complaint_id, @booking_id, @trip_id, @driver_id, @passenger_id, @content, 'pending');
                INSERT INTO notifications (recipient_user_id, type, title, body, resource_type, resource_id)
                SELECT user_id, 'complaint_created', 'Khiếu nại mới',
                       CONCAT('Khiếu nại ', @complaint_id, ' cần được xử lý.'), 'complaint', @complaint_id
                FROM users WHERE role='admin' AND status='active';
                INSERT INTO audit_logs (actor_user_id, action, resource_type, resource_id, created_at)
                VALUES (@driver_id, 'complaint.create', 'complaint', @complaint_id, UTC_TIMESTAMP())", conn, tx);
            insert.Parameters.AddWithValue("@complaint_id", complaintId);
            insert.Parameters.AddWithValue("@booking_id", id);
            insert.Parameters.AddWithValue("@trip_id", tripId);
            insert.Parameters.AddWithValue("@driver_id", driverId);
            insert.Parameters.AddWithValue("@passenger_id", passengerId);
            insert.Parameters.AddWithValue("@content", content);
            await insert.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            return Results.Ok(new { message = "Đã gửi khiếu nại.", complaint_id = complaintId });
        }
        catch
        {
            await tx.RollbackAsync();
            return Results.Json(new { message = "Không thể gửi khiếu nại." }, statusCode: 500);
        }
    }

    private static async Task<IResult> GetMyComplaintsAsync(System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        await using var conn = await Db.OpenAsync();
        await using var cmd = new MySqlCommand(@"SELECT complaint_id, booking_id, trip_id, content, status,
            decision, resolution_note, created_at FROM complaints WHERE created_by=@driver_id
            ORDER BY created_at DESC LIMIT 100", conn);
        cmd.Parameters.AddWithValue("@driver_id", driverId);
        var items = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(new
        {
            complaint_id = reader.GetString("complaint_id"),
            booking_id = reader.IsDBNull(reader.GetOrdinal("booking_id")) ? null : reader.GetString("booking_id"),
            trip_id = reader.GetString("trip_id"),
            content = reader.GetString("content"),
            status = reader.GetString("status"),
            decision = reader.IsDBNull(reader.GetOrdinal("decision")) ? null : reader.GetString("decision"),
            resolution_note = reader.IsDBNull(reader.GetOrdinal("resolution_note")) ? null : reader.GetString("resolution_note"),
            created_at = reader.GetDateTime("created_at")
        });
        return Results.Ok(items);
    }

    private static async Task<IResult> UploadComplaintAttachmentAsync(
        string id, HttpRequest request, IWebHostEnvironment env, IConfiguration configuration,
        System.Security.Claims.ClaimsPrincipal user)
    {
        var driverId = CurrentUser.DriverId(user);
        if (!request.HasFormContentType)
            return Results.BadRequest(new { message = "Yêu cầu không hợp lệ." });

        await using var conn = await Db.OpenAsync();
        await using (var owned = new MySqlCommand(
            "SELECT COUNT(*) FROM complaints WHERE complaint_id=@id AND created_by=@driver_id", conn))
        {
            owned.Parameters.AddWithValue("@id", id);
            owned.Parameters.AddWithValue("@driver_id", driverId);
            if (Convert.ToInt64(await owned.ExecuteScalarAsync()) != 1)
                return Results.NotFound(new { message = "Không tìm thấy khiếu nại." });
        }

        var form = await request.ReadFormAsync();
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { message = "Vui lòng chọn file bằng chứng." });
        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext) || file.Length > MaxUploadBytes || !await HasValidFileSignatureAsync(file, ext))
            return Results.BadRequest(new { message = "Chỉ chấp nhận JPG, PNG, PDF đúng định dạng và tối đa 5MB." });

        var configuredRoot = configuration["Storage:ComplaintAttachmentsRoot"] ?? "../VnPayXeGhep/Data";
        var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, configuredRoot));
        var uploadDir = Path.Combine(root, "complaint-attachments");
        Directory.CreateDirectory(uploadDir);
        var safeId = System.Text.RegularExpressions.Regex.Replace(id, "[^a-zA-Z0-9_-]", "");
        var fileName = $"{safeId}_{Guid.NewGuid():N}.{ext}";
        var destination = Path.Combine(uploadDir, fileName);
        await using (var stream = new FileStream(destination, FileMode.CreateNew))
            await file.CopyToAsync(stream);

        try
        {
            await using var insert = new MySqlCommand(@"INSERT INTO complaint_attachments
                (complaint_id, storage_path, original_name, mime_type, file_size, created_at)
                VALUES (@id, @path, @name, @mime, @size, UTC_TIMESTAMP())", conn);
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@path", $"complaint-attachments/{fileName}");
            insert.Parameters.AddWithValue("@name", Path.GetFileName(file.FileName));
            insert.Parameters.AddWithValue("@mime", file.ContentType);
            insert.Parameters.AddWithValue("@size", file.Length);
            await insert.ExecuteNonQueryAsync();
            return Results.Ok(new { message = "Đã đính kèm bằng chứng." });
        }
        catch
        {
            File.Delete(destination);
            return Results.Json(new { message = "Không thể lưu bằng chứng." }, statusCode: 500);
        }
    }

    private static async Task UpdateAcceptanceStatsAsync(
        MySqlConnection conn, MySqlTransaction tx, int driverId, bool accepted)
    {
        var now = DateTime.UtcNow;
        var periods = new[]
        {
            (type: "day", value: now.ToString("yyyy-MM-dd"), previous: now.AddDays(-1).ToString("yyyy-MM-dd")),
            (type: "week", value: $"{ISOWeek.GetYear(now)}-W{ISOWeek.GetWeekOfYear(now):D2}",
                previous: $"{ISOWeek.GetYear(now.AddDays(-7))}-W{ISOWeek.GetWeekOfYear(now.AddDays(-7)):D2}"),
            (type: "month", value: now.ToString("yyyy-MM"), previous: now.AddMonths(-1).ToString("yyyy-MM")),
        };

        foreach (var period in periods)
        {
            long? statId = null;
            var acceptedCount = 0;
            var rejectedCount = 0;
            await using (var select = new MySqlCommand(@"SELECT stat_id, accepted_count, rejected_count
                FROM driver_acceptance_stats WHERE driver_id=@driver_id AND period_type=@period_type
                  AND period_value=@period_value ORDER BY stat_id LIMIT 1 FOR UPDATE", conn, tx))
            {
                select.Parameters.AddWithValue("@driver_id", driverId);
                select.Parameters.AddWithValue("@period_type", period.type);
                select.Parameters.AddWithValue("@period_value", period.value);
                await using var reader = await select.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    statId = reader.GetInt64("stat_id");
                    acceptedCount = reader.GetInt32("accepted_count");
                    rejectedCount = reader.GetInt32("rejected_count");
                }
            }

            acceptedCount += accepted ? 1 : 0;
            rejectedCount += accepted ? 0 : 1;
            var rate = Math.Round(acceptedCount * 100m / (acceptedCount + rejectedCount), 2);

            if (statId.HasValue)
            {
                await using var update = new MySqlCommand(@"UPDATE driver_acceptance_stats
                    SET accepted_count=@accepted, rejected_count=@rejected, acceptance_rate=@rate
                    WHERE stat_id=@stat_id", conn, tx);
                update.Parameters.AddWithValue("@accepted", acceptedCount);
                update.Parameters.AddWithValue("@rejected", rejectedCount);
                update.Parameters.AddWithValue("@rate", rate);
                update.Parameters.AddWithValue("@stat_id", statId.Value);
                await update.ExecuteNonQueryAsync();
                continue;
            }

            decimal previousRate;
            await using (var previous = new MySqlCommand(@"SELECT COALESCE(MAX(acceptance_rate), 0)
                FROM driver_acceptance_stats WHERE driver_id=@driver_id AND period_type=@period_type
                  AND period_value=@previous_value", conn, tx))
            {
                previous.Parameters.AddWithValue("@driver_id", driverId);
                previous.Parameters.AddWithValue("@period_type", period.type);
                previous.Parameters.AddWithValue("@previous_value", period.previous);
                previousRate = Convert.ToDecimal(await previous.ExecuteScalarAsync());
            }

            await using var insert = new MySqlCommand(@"INSERT INTO driver_acceptance_stats
                (driver_id, period_type, period_value, accepted_count, rejected_count, acceptance_rate, prev_period_rate)
                VALUES (@driver_id, @period_type, @period_value, @accepted, @rejected, @rate, @previous_rate)", conn, tx);
            insert.Parameters.AddWithValue("@driver_id", driverId);
            insert.Parameters.AddWithValue("@period_type", period.type);
            insert.Parameters.AddWithValue("@period_value", period.value);
            insert.Parameters.AddWithValue("@accepted", acceptedCount);
            insert.Parameters.AddWithValue("@rejected", rejectedCount);
            insert.Parameters.AddWithValue("@rate", rate);
            insert.Parameters.AddWithValue("@previous_rate", previousRate);
            await insert.ExecuteNonQueryAsync();
        }
    }
}
