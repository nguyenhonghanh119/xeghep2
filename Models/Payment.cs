namespace XeGhepAdmin.Models;

public class GiaoDich
{
    public DateTime ThoiGian { get; set; }
    public string MaChuyen { get; set; } = "";
    public string Khach { get; set; } = "";
    public string TaiXe { get; set; } = "";
    public PhuongThucThanhToan PhuongThuc { get; set; }
    public decimal TongTien { get; set; }
    public decimal HoaHong { get; set; }
    public decimal? TaiXeNhan { get; set; }
    public string TrangThaiHienThi { get; set; } = ""; // "Đã cộng vào ví tài xế", "Đã hoàn tiền khách"...
}

/// <summary>
/// Yêu cầu rút tiền của tài xế. Trước đây admin phải bấm "Duyệt/Từ chối" thủ công;
/// nay hệ thống RÚT TIỀN TỰ ĐỘNG — mục này chỉ còn là NHẬT KÝ các lần đã tự động chuyển khoản.
/// </summary>
public class YeuCauRutTien
{
    public string MaYeuCau { get; set; } = "";
    public string TaiXe { get; set; } = "";
    public string NganHang { get; set; } = "";
    public decimal SoTien { get; set; }
    public DateTime ThoiGianYeuCau { get; set; }
    public DateTime? ThoiGianChuyenTuDong { get; set; }
    public bool DaChuyenTuDong { get; set; }
}

public class CauHinhHoaHong
{
    public decimal TiLePhanTram { get; set; } = 10;
    public bool TuDongCongViTaiXe { get; set; } = true;
    public bool DoiSoatTienMatTuDong { get; set; } = true;

    /// <summary>MỚI: bật thì mọi yêu cầu rút tiền hợp lệ của tài xế được xử lý & chuyển khoản tự động, không cần admin duyệt.</summary>
    public bool TuDongDuyetRutTien { get; set; } = true;

    public decimal HanMucTuDongDuyet { get; set; } = 5_000_000m; // trên mức này vẫn cần admin xem lại thủ công
}
