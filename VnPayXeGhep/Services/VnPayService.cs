using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VnPayXeGhep.Models;

namespace VnPayXeGhep.Services;

public interface IVnPayService
{
    /// <summary>
    /// Dựng URL thanh toán VNPay (thay cho phần dựng $inputData + ksort + hash_hmac trong
    /// vnpay_create_payment.php / khach-thanh-toan.php gốc).
    /// </summary>
    string BuildPaymentUrl(string txnRef, decimal amountVnd, string orderInfo, string ipAddress, string? bankCode = null);

    /// <summary>
    /// Kiểm tra chữ ký vnp_SecureHash trả về từ VNPay (return URL hoặc IPN),
    /// tương đương đoạn so sánh $secureHash == $vnp_SecureHash trong vnpay_return.php / vnpay_ipn.php.
    /// </summary>
    bool ValidateSignature(IDictionary<string, string> vnpParams, string receivedHash);
}

public class VnPayService : IVnPayService
{
    private readonly VnPayOptions _options;

    public VnPayService(IOptions<VnPayOptions> options)
    {
        _options = options.Value;
    }

    public string BuildPaymentUrl(string txnRef, decimal amountVnd, string orderInfo, string ipAddress, string? bankCode = null)
    {
        var now = DateTime.Now;
        var expire = now.AddMinutes(_options.ExpireMinutes);

        var inputData = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = _options.Version,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = ((long)(amountVnd * 100)).ToString(), // VNPay yêu cầu nhân 100
            ["vnp_Command"] = "pay",
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = _options.CurrCode,
            ["vnp_IpAddr"] = ipAddress,
            ["vnp_Locale"] = _options.Locale,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TxnRef"] = txnRef,
            ["vnp_ExpireDate"] = expire.ToString("yyyyMMddHHmmss"),
        };

        if (!string.IsNullOrEmpty(bankCode))
        {
            inputData["vnp_BankCode"] = bankCode;
        }

        var (query, hashData) = BuildQueryAndHashData(inputData);
        var secureHash = HmacSha512(_options.HashSecret, hashData);

        return $"{_options.PaymentUrl}?{query}vnp_SecureHash={secureHash}";
    }

    public bool ValidateSignature(IDictionary<string, string> vnpParams, string receivedHash)
    {
        // Chỉ lấy các field bắt đầu bằng vnp_ và bỏ vnp_SecureHash / vnp_SecureHashType,
        // giống hệt logic trong vnpay_return.php / vnpay_ipn.php gốc.
        var filtered = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in vnpParams)
        {
            if (kv.Key.StartsWith("vnp_", StringComparison.Ordinal)
                && kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
            {
                filtered[kv.Key] = kv.Value;
            }
        }

        var (_, hashData) = BuildQueryAndHashData(filtered);
        var computedHash = HmacSha512(_options.HashSecret, hashData);
        return string.Equals(computedHash, receivedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static (string query, string hashData) BuildQueryAndHashData(SortedDictionary<string, string> data)
{
    var query = new StringBuilder();
    var hashData = new StringBuilder();
    var first = true;

    foreach (var kv in data)
    {
        // Bỏ qua nếu giá trị rỗng hoặc null
        if (string.IsNullOrEmpty(kv.Value))
        {
            continue;
        }

        if (!first)
        {
            hashData.Append('&');
        }

        // Sử dụng WebUtility.UrlEncode chuẩn cho VNPay .NET
        string keyEncoded = System.Net.WebUtility.UrlEncode(kv.Key);
        string valueEncoded = System.Net.WebUtility.UrlEncode(kv.Value);

        hashData.Append(keyEncoded).Append('=').Append(valueEncoded);
        query.Append(keyEncoded).Append('=').Append(valueEncoded).Append('&');

        first = false;
    }

    return (query.ToString(), hashData.ToString());
}

    private static string HmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
