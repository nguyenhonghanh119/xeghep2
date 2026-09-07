namespace XeGhepAdmin.Models;

public class Booking
{
    public string MaDatCho { get; set; } = "";
    public string MaChuyen { get; set; } = "";
    public string Khach { get; set; } = "";
    public int GheDat { get; set; }
    public PhuongThucThanhToan PhuongThuc { get; set; }
    public TrangThai TrangThai { get; set; }
}
