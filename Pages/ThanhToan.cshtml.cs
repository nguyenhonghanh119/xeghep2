using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND — LUỒNG CHIA HOA HỒNG & RÚT TIỀN TỰ ĐỘNG:
// - GET  /api/admin/settings/commission        -> tỉ lệ hoa hồng + auto_payout + auto_withdraw + hạn mức
// - PUT  /api/admin/settings/commission         -> nút "Lưu cài đặt"
// - Khi khách thanh toán TRỰC TUYẾN thành công (webhook cổng thanh toán POST /api/payments/webhook):
//     1) hoa_hong = tong_tien * commission_rate; tai_xe_nhan = tong_tien - hoa_hong
//     2) nếu auto_payout = true: cộng tai_xe_nhan vào driver_wallet.balance NGAY LẬP TỨC (1 transaction DB)
// - Khi tài xế bấm "Rút tiền" trên app (POST /api/driver/withdrawals):
//     1) nếu auto_withdraw = true VÀ số tiền <= hạn mức tự động duyệt:
//          hệ thống TỰ ĐỘNG duyệt + gọi cổng chuyển khoản (banking API) + trừ ví + ghi log — KHÔNG cần admin thao tác
//     2) nếu vượt hạn mức: chuyển sang hàng chờ để admin xem lại thủ công (an toàn chống gian lận số tiền lớn)
// - GET  /api/admin/transactions?method=&status=       -> bảng "Giao dịch thanh toán"
// - GET  /api/admin/withdrawals?auto=true                -> bảng "Lịch sử rút tiền (tự động)"
public class ThanhToanModel : PageModel
{
    private readonly SampleDataStore _data;
    public ThanhToanModel(SampleDataStore data) => _data = data;

    public CauHinhHoaHong HoaHong => _data.HoaHong;
    public List<GiaoDich> GiaoDichs => _data.GiaoDichs;
    public List<YeuCauRutTien> Withdrawals => _data.Withdrawals;

    public decimal TongGmv => GiaoDichs.Sum(g => g.TongTien);
    public decimal TongHoaHong => GiaoDichs.Sum(g => g.HoaHong);
    public decimal DaChuyenTuDong => Withdrawals.Where(w => w.DaChuyenTuDong).Sum(w => w.SoTien);
    public decimal ChoXemLaiThuCong => Withdrawals.Where(w => !w.DaChuyenTuDong).Sum(w => w.SoTien);

    public void OnGet()
    {
        ViewData["Title"] = "Thanh toán";
        ViewData["ActivePage"] = "thanh-toan";
    }
}
