namespace XeGhepAdmin.Models;

/// <summary>
/// Vai trò quản trị viên. Đã BỎ vai trò "Kế toán" theo yêu cầu tái cấu trúc phân quyền
/// (nghiệp vụ kế toán/hoa hồng nay do "Quản trị viên (Toàn quyền)" và luồng tự động đảm nhiệm).
/// </summary>
public enum VaiTroQuanTri
{
    ToanQuyen,        // Quản trị viên (Toàn quyền)
    NhanVienHoTro,    // Nhân viên hỗ trợ
    QuanLyVanHanh     // Quản lý vận hành (thay thế vai trò Kế toán đã bỏ)
}

public class StaffAccount
{
    public string HoTen { get; set; } = "";
    public string Email { get; set; } = "";
    public VaiTroQuanTri VaiTro { get; set; }
    public TrangThai TrangThai { get; set; }

    /// <summary>MỚI: lý do tài khoản bị khoá — hiển thị khi TrangThai = BiKhoa.</summary>
    public string? LyDoKhoa { get; set; }
    public DateOnly? NgayBiKhoa { get; set; }
}
