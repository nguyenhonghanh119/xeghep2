using XeGhepApp.Data;
using XeGhepApp.Endpoints;

var builder = WebApplication.CreateBuilder(args);

Db.Configure(builder.Configuration.GetConnectionString("XeGhepDb")
    ?? "Server=127.0.0.1;Port=3306;Database=xeghep_db;User ID=root;Password=;CharSet=utf8mb4;");

builder.Services.AddRazorPages();

// Session dùng cho login.php / dang-ky.php (đăng nhập, OTP), tương đương session_start() trong PHP
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Phục vụ style.css, assets/common.js, uploads/docs/... từ wwwroot (giống thư mục gốc web root trong PHP)
app.UseStaticFiles();

app.UseSession();
app.UseRouting();

app.MapRazorPages();

// Các endpoint AJAX JSON thuần (start-trip.php, complete-trip.php, save-profile.php,
// upload-document.php, delete-document.php) - giữ nguyên đường dẫn *.php để JS phía
// client (fetch('start-trip.php') v.v.) không cần chỉnh sửa gì.
DriverApiEndpoints.Map(app);

app.Run();