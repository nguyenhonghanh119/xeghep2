namespace XeGhepAdmin.Models;

public class Customer
{
    public string MaKhach { get; set; } = "";       // USR-xxx
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public DateOnly NgayThamGia { get; set; }
    public int SoChuyen { get; set; }
    public TrangThai TrangThai { get; set; }
    public string? LyDoKhoa { get; set; }
}
