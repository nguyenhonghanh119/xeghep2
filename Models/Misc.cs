namespace XeGhepAdmin.Models;

public class ThongBaoDaGui
{
    public string TieuDe { get; set; } = "";
    public string DoiTuong { get; set; } = ""; // "Tất cả" | "Khách hàng" | "Tài xế"
    public DateOnly NgayGui { get; set; }
}

public class NhatKyHoatDong
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string HanhDong { get; set; } = "";
    public string? DoiTuong { get; set; }
}

/// <summary>
/// MỚI: dòng nhật ký tài khoản được HỆ THỐNG TỰ ĐỘNG TẠO khi khách hàng / tài xế tự đăng ký
/// qua app (không cần admin thao tác thủ công) — phục vụ trang "Quản lý tài khoản".
/// </summary>
public class TaiKhoanTuDongTao
{
    public string MaThamChieu { get; set; } = "";     // USR-xxx / DRV-xxx
    public string Loai { get; set; } = "";              // "Khách hàng" | "Tài xế"
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public DateTime ThoiGianTao { get; set; }
    public bool TaoThanhCong { get; set; } = true;
}
