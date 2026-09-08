using Microsoft.AspNetCore.Mvc.RazorPages;
using XeGhepAdmin.Data;
using XeGhepAdmin.Models;

namespace XeGhepAdmin.Pages;

// TODO BACKEND:
// - GET  /api/admin/users?query=&status=      -> danh sách hành khách
// - POST /api/admin/users/:id/suspend          -> nút "Khoá tài khoản"
// - POST /api/admin/users/:id/reactivate        -> nút "Mở khoá"
public class KhachHangModel : PageModel
{
    private readonly SampleDataStore _data;
    public KhachHangModel(SampleDataStore data) => _data = data;

    public List<Customer> Customers => _data.Customers;

    public void OnGet()
    {
        ViewData["Title"] = "Khách hàng";
        ViewData["ActivePage"] = "khach-hang";
    }
}
