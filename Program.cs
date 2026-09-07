using XeGhepAdmin.Data;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages: mỗi trang admin cũ (admin-xxx.php) tương ứng 1 Razor Page (Pages/Xxx.cshtml + Xxx.cshtml.cs)
builder.Services.AddRazorPages();

// Dữ liệu mẫu dùng chung cho toàn bộ khu quản trị (thay cho biến $... hard-code trong PHP gốc).
// TODO BACKEND: thay SampleDataStore bằng các service gọi API/DB thật (xem comment "TODO BACKEND" trong từng PageModel).
builder.Services.AddSingleton<SampleDataStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
