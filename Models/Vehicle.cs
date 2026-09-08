namespace XeGhepAdmin.Models;

/// <summary>
/// Thông tin phương tiện — trước đây là trang riêng "Quản lý phương tiện" (admin-phuong-tien.php),
/// nay gộp thành 1 phần của hồ sơ Tài xế (xem TaiXe.cshtml).
/// </summary>
public class Vehicle
{
    public string BienSo { get; set; } = "";
    public string LoaiXe { get; set; } = "";
    public int SoCho { get; set; }
    public DateOnly? HanDangKiem { get; set; }
    public bool BaoHiemConHan { get; set; } = true;

    /// <summary>Cảnh báo khi hạn đăng kiểm/bảo hiểm còn dưới 30 ngày (theo TODO cron job của bản gốc).</summary>
    public bool SapHetHan => HanDangKiem.HasValue &&
        HanDangKiem.Value.ToDateTime(TimeOnly.MinValue) < DateTime.Now.AddDays(30);
}
