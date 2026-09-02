using Microsoft.AspNetCore.Mvc;
using VnPayXeGhep.Models;
using VnPayXeGhep.Services;

namespace VnPayXeGhep.Controllers;

[ApiController]
[Route("api/passenger")]
public class PassengerApiController : ControllerBase
{
    private readonly IPassengerService _service;

    public PassengerApiController(IPassengerService service)
    {
        _service = service;
    }

    private int PassengerId => DemoSession.CurrentPassengerId;

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var profile = await _service.GetProfileAsync(PassengerId);
        if (profile == null) return NotFound();
        return Ok(profile);
    }

    [HttpGet("trips/search")]
    public async Task<IActionResult> SearchTrips(string? from, string? to, DateTime? date, int seats = 1)
    {
        var results = await _service.SearchTripsAsync(from, to, date, seats);
        return Ok(results);
    }

    [HttpGet("trips/{tripId}")]
    public async Task<IActionResult> GetTrip(string tripId)
    {
        var trip = await _service.GetTripDetailAsync(tripId);
        if (trip == null) return NotFound(new { message = "Không tìm thấy chuyến đi." });
        return Ok(trip);
    }

    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] BookingCreateRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _service.CreateBookingAsync(PassengerId, req, ip);
        if (!result.Success) return BadRequest(new { message = result.Error });
        return Ok(result);
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings(string? status)
    {
        var bookings = await _service.GetMyBookingsAsync(PassengerId);
        if (!string.IsNullOrEmpty(status))
            bookings = bookings.Where(b => b.Tab == status).ToList();
        return Ok(bookings);
    }

    [HttpPost("bookings/{id}/cancel")]
    public async Task<IActionResult> CancelBooking(string id)
    {
        var (ok, error) = await _service.CancelBookingAsync(PassengerId, id);
        if (!ok) return BadRequest(new { message = error });
        return Ok(new { message = "Đã huỷ chuyến. Nếu đã thanh toán online, tiền sẽ được hoàn trong 1-3 ngày làm việc." });
    }

    [HttpPost("bookings/{tripId}/review")]
    public async Task<IActionResult> AddReview(string tripId, [FromBody] ReviewCreateRequest req)
    {
        var (ok, error) = await _service.AddReviewAsync(PassengerId, tripId, req);
        if (!ok) return BadRequest(new { message = error });
        return Ok(new { message = "Cảm ơn bạn đã đánh giá!" });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest req)
    {
        var (ok, error) = await _service.UpdateProfileAsync(PassengerId, req);
        if (!ok) return BadRequest(new { message = error });
        return Ok(new { message = "Đã lưu thay đổi." });
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        return Ok(await _service.GetPaymentMethodsAsync(PassengerId));
    }

    [HttpPost("payment-methods")]
    public async Task<IActionResult> AddPaymentMethod([FromBody] AddPaymentMethodRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Provider) || string.IsNullOrWhiteSpace(req.AccountNumber))
            return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin phương thức thanh toán." });
        var item = await _service.AddPaymentMethodAsync(PassengerId, req);
        return Ok(item);
    }

    [HttpDelete("payment-methods/{id}")]
    public async Task<IActionResult> RemovePaymentMethod(int id)
    {
        var (ok, error) = await _service.RemovePaymentMethodAsync(PassengerId, id);
        if (!ok) return BadRequest(new { message = error });
        return Ok(new { message = "Đã xoá." });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions()
    {
        return Ok(await _service.GetTransactionHistoryAsync(PassengerId));
    }

    // Khách tự nhập số tiền + thông tin ngân hàng để rút từ ví — xử lý tự động thành công ngay.
    [HttpPost("wallet/withdraw")]
    public async Task<IActionResult> WithdrawWallet([FromBody] WithdrawRequest req)
    {
        var (ok, error, newBalance) = await _service.WithdrawFromWalletAsync(PassengerId, req);
        if (!ok) return BadRequest(new { message = error });
        return Ok(new { message = "Đã rút tiền thành công về tài khoản của bạn.", walletBalance = newBalance });
    }

    [HttpGet("wallet/withdrawals")]
    public async Task<IActionResult> GetWithdrawals()
    {
        return Ok(await _service.GetWithdrawalHistoryAsync(PassengerId));
    }
}
