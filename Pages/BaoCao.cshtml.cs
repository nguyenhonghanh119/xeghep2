using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET /api/admin/reports/summary?from=&to=          -> 4 stat-card + biểu đồ
// - GET /api/admin/reports/export?type=csv&from=&to=  -> nút "Xuất báo cáo"
public class BaoCaoModel : PageModel
{
    private readonly SampleDataStore _data;
    public BaoCaoModel(SampleDataStore data) => _data = data;

    public void OnGet()
    {
        ViewData["Title"] = "Báo cáo & thống kê";
        ViewData["ActivePage"] = "bao-cao";
    }

    public void OnPostXuatBaoCao()
    {
        // TODO BACKEND: gọi GET /api/admin/reports/export?type=csv&from=&to= và trả về FileResult (CSV/Excel).
    }
}
