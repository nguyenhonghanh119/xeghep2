namespace VnPayXeGhep.Models;

// Ánh xạ section "VnPay" trong appsettings.json — tương đương các biến $vnp_* trong config.php gốc
public class VnPayOptions
{
    public const string SectionName = "VnPay";

    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public int ExpireMinutes { get; set; } = 15;
    public string Version { get; set; } = "2.1.0";
    public string Locale { get; set; } = "vn";
    public string CurrCode { get; set; } = "VND";
}
