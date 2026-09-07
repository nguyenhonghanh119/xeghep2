namespace XeGhepAdmin.Models;

public class Trip
{
    public string MaChuyen { get; set; } = "";
    public string DiemDi { get; set; } = "";
    public string DiemDen { get; set; } = "";
    public string TaiXe { get; set; } = "";
    public int SoKhach { get; set; }
    public DateTime ThoiGian { get; set; }
    public int GheDaDat { get; set; }
    public int TongGhe { get; set; }
    public TrangThai TrangThai { get; set; }
}
