using XeGhepAdmin.Data;
using XeGhepApp.Data;
using XeGhepApp.Endpoints;

var builder = WebApplication.CreateBuilder(args);

Db.Configure(builder.Configuration.GetConnectionString("XeGhepDb")
    ?? "Server=127.0.0.1;Port=3306;Database=xeghep_db;User ID=root;Password=;CharSet=utf8mb4;");

builder.Services.AddRazorPages();

// Dữ liệu mẫu dùng chung cho toàn bộ khu quản trị
// TODO BACKEND: thay SampleDataStore bằng các service gọi API/DB thật.
builder.Services.AddSingleton<SampleDataStore>();

// Session dùng cho login.php / dang-ky.php
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
    app.UseHsts();
}

// Phục vụ style.css, assets/common.js, uploads/docs/...
app.UseStaticFiles();

app.UseSession();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

// Các endpoint AJAX JSON thuần
DriverApiEndpoints.Map(app);

app.Run();