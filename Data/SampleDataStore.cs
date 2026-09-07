using XeGhepAdmin.Models;

namespace XeGhepAdmin.Data;

/// <summary>
/// Dữ liệu MẪU giữ trong bộ nhớ — thay thế các mảng hard-code trong PHP gốc để demo giao diện.
/// TODO BACKEND: xoá toàn bộ file này khi đã có DB thật; mỗi PageModel nên gọi service/API tương ứng
/// (đường dẫn API gợi ý được ghi chú ngay tại nơi sử dụng dữ liệu trong từng PageModel).
/// Đăng ký dạng Singleton trong Program.cs để dữ liệu demo giữ nguyên trong suốt vòng đời ứng dụng.
/// </summary>
public class SampleDataStore
{
    public int SoHoSoTaiXeChoDuyet => Drivers.Count(d => d.TrangThai == TrangThai.ChoDuyet);

    public List<Driver> Drivers { get; } = new()
    {
        new Driver{ MaTaiXe="DRV-201", HoTen="Nguyễn Văn Tùng", SoDienThoai="090 112 2345", TrangThai=TrangThai.ChoDuyet,
            NgayNopHoSo=new DateOnly(2026,8,18),
            Vehicle = new Vehicle{ BienSo="30G-112.45", LoaiXe="Toyota Innova", SoCho=7, HanDangKiem=null } },
        new Driver{ MaTaiXe="DRV-202", HoTen="Đặng Quốc Bảo", SoDienThoai="098 556 7890", TrangThai=TrangThai.ChoDuyet,
            NgayNopHoSo=new DateOnly(2026,8,17),
            Vehicle = new Vehicle{ BienSo="29H-556.78", LoaiXe="Kia Sedona", SoCho=7, HanDangKiem=null } },
        new Driver{ MaTaiXe="DRV-101", HoTen="Trần Văn Hùng", SoDienThoai="091 234 5678", TrangThai=TrangThai.DangHoatDong,
            SoChuyen=312, DanhGia=4.9,
            Vehicle = new Vehicle{ BienSo="30F-689.21", LoaiXe="Kia Carnival", SoCho=7, HanDangKiem=new DateOnly(2027,3,14) } },
        new Driver{ MaTaiXe="DRV-102", HoTen="Phạm Đức Long", SoDienThoai="097 345 1122", TrangThai=TrangThai.DangHoatDong,
            SoChuyen=198, DanhGia=4.7,
            Vehicle = new Vehicle{ BienSo="29A-334.09", LoaiXe="Toyota Innova", SoCho=7, HanDangKiem=new DateOnly(2026,9,2) } },
        new Driver{ MaTaiXe="DRV-050", HoTen="Vũ Anh Khoa", SoDienThoai="094 000 1122", TrangThai=TrangThai.BiKhoa,
            SoChuyen=44, DanhGia=3.9, NgayBiKhoa=new DateOnly(2026,7,2), LyDoKhoa="Huỷ chuyến liên tục",
            Vehicle = new Vehicle{ BienSo="—", LoaiXe="—", SoCho=0 } },
    };

    public List<Customer> Customers { get; } = new()
    {
        new Customer{ MaKhach="USR-301", HoTen="Nguyễn Thị Lan", SoDienThoai="091 888 2233", NgayThamGia=new DateOnly(2024,2,14), SoChuyen=47, TrangThai=TrangThai.DangHoatDong },
        new Customer{ MaKhach="USR-302", HoTen="Lê Quốc Anh", SoDienThoai="090 771 4432", NgayThamGia=new DateOnly(2024,5,3), SoChuyen=112, TrangThai=TrangThai.DangHoatDong },
        new Customer{ MaKhach="USR-303", HoTen="Vũ Minh Đức", SoDienThoai="098 223 9981", NgayThamGia=new DateOnly(2023,11,21), SoChuyen=9, TrangThai=TrangThai.BiKhoa, LyDoKhoa="Vi phạm chính sách nền tảng" },
        new Customer{ MaKhach="USR-304", HoTen="Trịnh Thu Hà", SoDienThoai="096 550 1123", NgayThamGia=new DateOnly(2025,1,9), SoChuyen=23, TrangThai=TrangThai.DangHoatDong },
    };

    public List<Trip> Trips { get; } = new()
    {
        new Trip{ MaChuyen="TRIP-001", DiemDi="Hà Nội", DiemDen="Ninh Bình", TaiXe="Trần Văn Hùng", SoKhach=2, ThoiGian=new DateTime(2026,8,22,6,30,0), GheDaDat=3, TongGhe=3, TrangThai=TrangThai.SapKhoiHanh },
        new Trip{ MaChuyen="TRIP-002", DiemDi="Hà Nội", DiemDen="Hải Phòng", TaiXe="Đỗ Thị Hoa", SoKhach=1, ThoiGian=new DateTime(2026,8,19,14,2,0), GheDaDat=1, TongGhe=6, TrangThai=TrangThai.DangChay },
        new Trip{ MaChuyen="TRIP-003", DiemDi="Hà Nội", DiemDen="Hải Phòng", TaiXe="Trần Văn Hùng", SoKhach=1, ThoiGian=new DateTime(2026,8,9,14,0,0), GheDaDat=1, TongGhe=4, TrangThai=TrangThai.HoanThanh },
        new Trip{ MaChuyen="TRIP-004", DiemDi="Hà Nội", DiemDen="Ninh Bình", TaiXe="Phạm Đức Long", SoKhach=0, ThoiGian=new DateTime(2026,8,1,8,0,0), GheDaDat=0, TongGhe=4, TrangThai=TrangThai.DaHuy },
    };

    public List<Booking> Bookings { get; } = new()
    {
        new Booking{ MaDatCho="BK-5510", MaChuyen="TRIP-001", Khach="Nguyễn Thị Lan", GheDat=2, PhuongThuc=PhuongThucThanhToan.Online, TrangThai=TrangThai.DaXacNhan },
        new Booking{ MaDatCho="BK-5511", MaChuyen="TRIP-002", Khach="Lê Quốc Anh", GheDat=1, PhuongThuc=PhuongThucThanhToan.TienMat, TrangThai=TrangThai.ChoDuyet },
        new Booking{ MaDatCho="BK-5498", MaChuyen="TRIP-081", Khach="Vũ Minh Đức", GheDat=1, PhuongThuc=PhuongThucThanhToan.Online, TrangThai=TrangThai.DaHuy },
    };

    public List<Review> Reviews { get; } = new()
    {
        new Review{ Khach="Nguyễn Thị Lan", TaiXe="Trần Văn Hùng", MaChuyen="TRIP-097", SoSao=5, NhanXet="Tài xế thân thiện, xe sạch sẽ.", Ngay=new DateOnly(2026,8,18) },
        new Review{ Khach="Lê Quốc Anh", TaiXe="Đỗ Thị Hoa", MaChuyen="TRIP-002", SoSao=4, NhanXet="Đón trễ 10 phút.", Ngay=new DateOnly(2026,8,19) },
        new Review{ Khach="Trịnh Thu Hà", TaiXe="Phạm Đức Long", MaChuyen="TRIP-081", SoSao=3, NhanXet="Xe hơi cũ, tài xế lái ổn.", Ngay=new DateOnly(2026,8,17) },
    };

    public List<Complaint> Complaints { get; } = new()
    {
        new Complaint{ MaKhieuNai="CP-021", Khach="Vũ Minh Đức", TaiXe="Trần Văn Hùng", MaChuyen="TRIP-081", NoiDung="Tài xế huỷ chuyến sát giờ khởi hành", DaXuLy=false },
    };

    public List<GiaoDich> GiaoDichs { get; } = new()
    {
        new GiaoDich{ ThoiGian=new DateTime(2026,8,19,15,41,0), MaChuyen="TRIP-118", Khach="Nguyễn Thị Lan", TaiXe="Trần Văn Hùng", PhuongThuc=PhuongThucThanhToan.Online, TongTien=120000, HoaHong=12000, TaiXeNhan=108000, TrangThaiHienThi="Đã cộng vào ví tài xế" },
        new GiaoDich{ ThoiGian=new DateTime(2026,8,19,14,2,0), MaChuyen="TRIP-002", Khach="Lê Quốc Anh", TaiXe="Đỗ Thị Hoa", PhuongThuc=PhuongThucThanhToan.TienMat, TongTien=130000, HoaHong=13000, TaiXeNhan=117000, TrangThaiHienThi="Chờ đối soát tiền mặt" },
        new GiaoDich{ ThoiGian=new DateTime(2026,8,18,9,12,0), MaChuyen="TRIP-097", Khach="Trịnh Thu Hà", TaiXe="Phạm Đức Long", PhuongThuc=PhuongThucThanhToan.Online, TongTien=390000, HoaHong=39000, TaiXeNhan=351000, TrangThaiHienThi="Đã cộng vào ví tài xế" },
        new GiaoDich{ ThoiGian=new DateTime(2026,8,17,20,3,0), MaChuyen="TRIP-081", Khach="Vũ Minh Đức", TaiXe="Trần Văn Hùng", PhuongThuc=PhuongThucThanhToan.Online, TongTien=100000, HoaHong=10000, TaiXeNhan=null, TrangThaiHienThi="Đã hoàn tiền khách" },
    };

    /// <summary>Lịch sử rút tiền — nay xử lý & chuyển khoản TỰ ĐỘNG nên không còn hàng "chờ duyệt".</summary>
    public List<YeuCauRutTien> Withdrawals { get; } = new()
    {
        new YeuCauRutTien{ MaYeuCau="WD-401", TaiXe="Trần Văn Hùng", NganHang="Vietcombank ****4821", SoTien=1_000_000, ThoiGianYeuCau=new DateTime(2026,8,15,10,0,0), ThoiGianChuyenTuDong=new DateTime(2026,8,15,10,0,4), DaChuyenTuDong=true },
        new YeuCauRutTien{ MaYeuCau="WD-402", TaiXe="Đỗ Thị Hoa", NganHang="MB Bank ****7710", SoTien=650_000, ThoiGianYeuCau=new DateTime(2026,8,18,9,30,0), ThoiGianChuyenTuDong=new DateTime(2026,8,18,9,30,3), DaChuyenTuDong=true },
        new YeuCauRutTien{ MaYeuCau="WD-403", TaiXe="Phạm Đức Long", NganHang="Techcombank ****2290", SoTien=6_200_000, ThoiGianYeuCau=new DateTime(2026,8,19,16,0,0), DaChuyenTuDong=false }, // vượt hạn mức -> vẫn cần xem lại
    };

    public CauHinhHoaHong HoaHong { get; } = new();

    public CauHinhGia GiaMacDinh { get; } = new();

    /// <summary>Gộp "Quản lý tuyến đường" + "Quản lý giá" cũ thành 1 danh sách duy nhất.</summary>
    public List<TuyenGia> Tuyens { get; } = new()
    {
        new TuyenGia{ DiemDi="Hà Nội", DiemDen="Ninh Bình", KhoangCachKm=95, GiaRiengMoiGhe=130000, SoChuyenDaChay=318, TrangThai=TrangThai.DangHoatDong, CapNhatLanCuoi=new DateOnly(2026,8,12) },
        new TuyenGia{ DiemDi="Hà Nội", DiemDen="Hải Phòng", KhoangCachKm=105, GiaRiengMoiGhe=120000, SoChuyenDaChay=256, TrangThai=TrangThai.DangHoatDong, CapNhatLanCuoi=new DateOnly(2026,8,8) },
        new TuyenGia{ DiemDi="Hà Nội", DiemDen="Sapa", KhoangCachKm=315, GiaRiengMoiGhe=280000, SoChuyenDaChay=64, TrangThai=TrangThai.BiKhoa, CapNhatLanCuoi=new DateOnly(2026,6,1) },
    };

    public List<StaffAccount> StaffAccounts { get; } = new()
    {
        new StaffAccount{ HoTen="Admin Quản trị", Email="quantri@xeghep.vn", VaiTro=VaiTroQuanTri.ToanQuyen, TrangThai=TrangThai.DangHoatDong },
        new StaffAccount{ HoTen="Nguyễn Thảo Vy", Email="vy.nguyen@xeghep.vn", VaiTro=VaiTroQuanTri.NhanVienHoTro, TrangThai=TrangThai.DangHoatDong },
        new StaffAccount{ HoTen="Hoàng Minh Khôi", Email="khoi.hoang@xeghep.vn", VaiTro=VaiTroQuanTri.QuanLyVanHanh, TrangThai=TrangThai.BiKhoa, LyDoKhoa="Đăng nhập sai vị trí bất thường, tạm khoá chờ xác minh", NgayBiKhoa=new DateOnly(2026,8,5) },
    };

    public List<ThongBaoDaGui> ThongBaos { get; } = new()
    {
        new ThongBaoDaGui{ TieuDe="Ưu đãi mã KHACHMOI cho khách mới", DoiTuong="Khách hàng", NgayGui=new DateOnly(2026,8,15) },
        new ThongBaoDaGui{ TieuDe="Cập nhật chính sách hoa hồng 10%", DoiTuong="Tài xế", NgayGui=new DateOnly(2026,8,1) },
    };

    public List<NhatKyHoatDong> ActivityLogs { get; } = new()
    {
        new NhatKyHoatDong{ ThoiGian=new DateTime(2026,8,19,15,52,0), NguoiThucHien="Hệ thống (tự động)", HanhDong="Chuyển khoản rút tiền tự động", DoiTuong="WD-401" },
        new NhatKyHoatDong{ ThoiGian=new DateTime(2026,8,19,9,10,0), NguoiThucHien="Admin Quản trị", HanhDong="Khoá tài khoản khách hàng", DoiTuong="USR-303" },
        new NhatKyHoatDong{ ThoiGian=new DateTime(2026,8,18,21,3,0), NguoiThucHien="Nguyễn Thảo Vy", HanhDong="Duyệt hồ sơ tài xế", DoiTuong="DRV-198" },
        new NhatKyHoatDong{ ThoiGian=new DateTime(2026,8,17,8,40,0), NguoiThucHien="Admin Quản trị", HanhDong="Cập nhật tỉ lệ hoa hồng: 9% → 10%", DoiTuong=null },
    };

    /// <summary>MỚI — nhật ký các tài khoản do HỆ THỐNG TỰ TẠO khi người dùng/tài xế tự đăng ký qua app.</summary>
    public List<TaiKhoanTuDongTao> TaiKhoanTuDong { get; } = new()
    {
        new TaiKhoanTuDongTao{ MaThamChieu="USR-305", Loai="Khách hàng", HoTen="Bùi Anh Thư", SoDienThoai="032 441 8890", ThoiGianTao=new DateTime(2026,8,19,20,12,0), TaoThanhCong=true },
        new TaiKhoanTuDongTao{ MaThamChieu="DRV-203", Loai="Tài xế", HoTen="Ngô Xuân Bách", SoDienThoai="076 220 1190", ThoiGianTao=new DateTime(2026,8,19,18,45,0), TaoThanhCong=true },
        new TaiKhoanTuDongTao{ MaThamChieu="USR-306", Loai="Khách hàng", HoTen="Đinh Thị Mai", SoDienThoai="098 112 4470", ThoiGianTao=new DateTime(2026,8,19,17,3,0), TaoThanhCong=true },
        new TaiKhoanTuDongTao{ MaThamChieu="DRV-204", Loai="Tài xế", HoTen="Lương Văn Đạt", SoDienThoai="091 900 2231", ThoiGianTao=new DateTime(2026,8,18,11,20,0), TaoThanhCong=true },
    };
}
