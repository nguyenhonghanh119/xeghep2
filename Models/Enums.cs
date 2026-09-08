namespace XeGhepAdmin.Models;

/// <summary>Trạng thái chung dùng cho nhiều thực thể (đơn giản hoá từ chuỗi trạng thái trong PHP gốc).</summary>
public enum TrangThai
{
    ChoDuyet,       // "Chờ duyệt" / "Pending"
    DangHoatDong,   // "Đang hoạt động" / "Đã duyệt" / "Approved"
    BiKhoa,         // "Bị khoá" / "Locked"
    DaHuy,          // "Đã huỷ" / "Cancelled"
    HoanThanh,      // "Hoàn thành" / "Done"
    DangChay,       // "Đang chạy" / "Running"
    SapKhoiHanh,    // "Sắp khởi hành" / "Upcoming"
    DaXacNhan,      // "Đã xác nhận"
    TuChoi          // "Từ chối" / "Rejected"
}

public enum PhuongThucThanhToan { Online, TienMat }
