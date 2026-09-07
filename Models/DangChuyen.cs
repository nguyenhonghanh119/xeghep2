namespace XeGhepAdmin.Models;

/// <summary>
/// Thay cho 2 trang cũ "Quản lý tuyến đường" + "Quản lý giá":
/// mỗi tuyến cố định giờ có SẴN giá đề xuất đi kèm ngay khi đăng, không tách 2 màn hình riêng.
/// </summary>
public class TuyenGia
{
    public string DiemDi { get; set; } = "";
    public string DiemDen { get; set; } = "";
    public int KhoangCachKm { get; set; }

    /// <summary>Giá riêng cho tuyến này (ghi đè). Null = dùng công thức giá mặc định.</summary>
    public decimal? GiaRiengMoiGhe { get; set; }

    public int SoChuyenDaChay { get; set; }
    public TrangThai TrangThai { get; set; }
    public DateOnly CapNhatLanCuoi { get; set; }
}

/// <summary>Công thức giá mặc định — dùng để tính "giá đề xuất" hiển thị ngay khi đăng chuyến mới.</summary>
public class CauHinhGia
{
    public decimal GiaMoiKmMoiGhe { get; set; } = 1500;
    public decimal PhiMoCuoc { get; set; } = 10000;
    public decimal GiaToiThieuMoiChuyen { get; set; } = 50000;
    public decimal HeSoGioCaoDiem { get; set; } = 1.2m;

    public decimal TinhGiaDeXuat(int khoangCachKm)
    {
        var gia = khoangCachKm * GiaMoiKmMoiGhe + PhiMoCuoc;
        return Math.Max(gia, GiaToiThieuMoiChuyen);
    }
}
