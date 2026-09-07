namespace XeGhepAdmin.Models;

public class Review
{
    public string Khach { get; set; } = "";
    public string TaiXe { get; set; } = "";
    public string MaChuyen { get; set; } = "";
    public int SoSao { get; set; }
    public string NhanXet { get; set; } = "";
    public DateOnly Ngay { get; set; }
}

public class Complaint
{
    public string MaKhieuNai { get; set; } = "";
    public string Khach { get; set; } = "";
    public string TaiXe { get; set; } = "";
    public string MaChuyen { get; set; } = "";
    public string NoiDung { get; set; } = "";
    public bool DaXuLy { get; set; }
}
