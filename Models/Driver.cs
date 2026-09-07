namespace XeGhepAdmin.Models;

/// <summary>Tài xế — đã gộp thông tin phương tiện (Vehicle) trực tiếp vào đây theo yêu cầu bỏ trang riêng.</summary>
public class Driver
{
    public string MaTaiXe { get; set; } = "";       // DRV-xxx
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public TrangThai TrangThai { get; set; }
    public int SoChuyen { get; set; }
    public double? DanhGia { get; set; }             // null nếu chưa có chuyến nào
    public DateOnly? NgayNopHoSo { get; set; }
    public DateOnly? NgayBiKhoa { get; set; }
    public string? LyDoKhoa { get; set; }

    public Vehicle Vehicle { get; set; } = new();

    public string AnhDaiDien => string.IsNullOrEmpty(HoTen) ? "?" : HoTen.Trim().Split(' ').Last()[..1].ToUpper();
}
