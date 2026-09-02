using Microsoft.AspNetCore.Mvc;
using VnPayXeGhep.Services;

namespace VnPayXeGhep.Controllers;

// NOTE: Bản demo này giả lập hành khách đang đăng nhập là Nguyễn Thị Lan (user_id = 5),
// giống cách db.php gốc giả lập $driver_id = 2. Khi có hệ thống đăng nhập thật,
// thay hằng số này bằng user_id lấy từ session/JWT.
public static class DemoSession
{
    public const int CurrentPassengerId = 5;
}

[Route("")]
public class PassengerController : Controller
{
    private readonly IPassengerService _service;

    public PassengerController(IPassengerService service)
    {
        _service = service;
    }

    // Tương ứng khach-index.php — Tìm chuyến
    [HttpGet("")]
    [HttpGet("khach-index")]
    public async Task<IActionResult> Index(string? from, string? to, DateTime? date, int seats = 1)
    {
        from ??= "Hà Nội";
        var results = await _service.SearchTripsAsync(from, to, date, seats);
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Date = date?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.Seats = seats;
        await SetBadgeAsync();
        return View("Index", results);
    }

    // Tương ứng khach-dat-cho.php — Xác nhận đặt chỗ
    [HttpGet("khach-dat-cho")]
    public async Task<IActionResult> DatCho(string trip)
    {
        var detail = await _service.GetTripDetailAsync(trip);
        if (detail == null) return RedirectToAction(nameof(Index));
        await SetBadgeAsync();
        return View("DatCho", detail);
    }

    // Tương ứng khach-chuyen-cua-toi.php — Chuyến của tôi
    [HttpGet("khach-chuyen-cua-toi")]
    public async Task<IActionResult> ChuyenCuaToi()
    {
        var bookings = await _service.GetMyBookingsAsync(DemoSession.CurrentPassengerId);
        ViewBag.MyTripsBadge = bookings.Count(b => b.Tab is "k-upcoming" or "k-running");
        return View("ChuyenCuaToi", bookings);
    }

    // Tương ứng khach-ho-so.php — Hồ sơ & Thanh toán
    [HttpGet("khach-ho-so")]
    public async Task<IActionResult> HoSo()
    {
        var profile = await _service.GetProfileAsync(DemoSession.CurrentPassengerId);
        ViewBag.Withdrawals = await _service.GetWithdrawalHistoryAsync(DemoSession.CurrentPassengerId);
        ViewBag.Transactions = await _service.GetTransactionHistoryAsync(DemoSession.CurrentPassengerId);
        await SetBadgeAsync();
        return View("HoSo", profile);
    }

    private async Task SetBadgeAsync()
    {
        var bookings = await _service.GetMyBookingsAsync(DemoSession.CurrentPassengerId);
        ViewBag.MyTripsBadge = bookings.Count(b => b.Tab is "k-upcoming" or "k-running");
    }
}
