using Microsoft.AspNetCore.Mvc;
using VnPayXeGhep.Services;

namespace VnPayXeGhep.Controllers;

[Route("api/vnpay")]
public class VnPayController : Controller
{
    private readonly IVnPayService _vnPay;
    private readonly IPassengerService _passengerService;

    public VnPayController(IVnPayService vnPay, IPassengerService passengerService)
    {
        _vnPay = vnPay;
        _passengerService = passengerService;
    }

    // Trình duyệt của khách được VNPay redirect về đây sau khi thanh toán.
    // Thay thế vnpay_return.php: kiểm tra chữ ký rồi cập nhật booking/transaction thật trong DB.
    [HttpGet("/api/vnpay/return")]
    public async Task<IActionResult> Return()
    {
        var query = Request.Query;
        var vnpParams = query.Keys.ToDictionary(k => k, k => query[k].ToString());

        if (!vnpParams.TryGetValue("vnp_SecureHash", out var receivedHash))
        {
            ViewBag.Result = "invalid";
            ViewBag.Message = "Thiếu chữ ký giao dịch.";
            return View("VnPayResult");
        }

        var validSignature = _vnPay.ValidateSignature(vnpParams, receivedHash);
        vnpParams.TryGetValue("vnp_TxnRef", out var txnRef);
        vnpParams.TryGetValue("vnp_ResponseCode", out var responseCode);
        vnpParams.TryGetValue("vnp_Amount", out var amountStr);
        vnpParams.TryGetValue("vnp_OrderInfo", out var orderInfo);
        vnpParams.TryGetValue("vnp_TransactionNo", out var transNo);
        vnpParams.TryGetValue("vnp_BankCode", out var bankCode);
        vnpParams.TryGetValue("vnp_PayDate", out var payDate);

        ViewBag.TxnRef = txnRef;
        ViewBag.Amount = amountStr;
        ViewBag.OrderInfo = orderInfo;
        ViewBag.ResponseCode = responseCode;
        ViewBag.TransNo = transNo;
        ViewBag.BankCode = bankCode;
        ViewBag.PayDate = payDate;

        if (!validSignature)
        {
            ViewBag.Result = "invalid";
            ViewBag.Message = "Chữ ký không hợp lệ.";
            return View("VnPayResult");
        }

        long.TryParse(amountStr, out var amount);
        var (ok, message) = await _passengerService.ConfirmVnPayPaymentAsync(txnRef ?? "", responseCode ?? "", amount);

        ViewBag.Result = ok && responseCode == "00" ? "success" : "failed";
        ViewBag.Message = message;
        return View("VnPayResult");
    }

    // VNPay server-to-server gọi endpoint này để xác nhận kết quả (Instant Payment Notification).
    // Thay thế vnpay_ipn.php: trả JSON RspCode theo đúng chuẩn VNPay yêu cầu.
    [HttpGet("/api/vnpay/ipn")]
    public async Task<IActionResult> Ipn()
    {
        var query = Request.Query;
        var vnpParams = query.Keys.ToDictionary(k => k, k => query[k].ToString());

        if (!vnpParams.TryGetValue("vnp_SecureHash", out var receivedHash))
            return Ok(new { RspCode = "99", Message = "Missing signature" });

        if (!_vnPay.ValidateSignature(vnpParams, receivedHash))
            return Ok(new { RspCode = "97", Message = "Invalid signature" });

        if (!vnpParams.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrEmpty(txnRef))
            return Ok(new { RspCode = "01", Message = "Order not found" });

        vnpParams.TryGetValue("vnp_ResponseCode", out var responseCode);
        long.TryParse(vnpParams.GetValueOrDefault("vnp_Amount"), out var amount);

        var (ok, message) = await _passengerService.ConfirmVnPayPaymentAsync(txnRef, responseCode ?? "", amount);

        if (message.Contains("khớp")) return Ok(new { RspCode = "04", Message = "Invalid amount" });
        if (message.Contains("Không tìm thấy")) return Ok(new { RspCode = "01", Message = "Order not found" });
        if (message.Contains("trước đó")) return Ok(new { RspCode = "02", Message = "Order already confirmed" });

        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }
}
