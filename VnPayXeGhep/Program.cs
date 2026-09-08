using Microsoft.EntityFrameworkCore;
using VnPayXeGhep.Data;
using VnPayXeGhep.Models;
using VnPayXeGhep.Services;

var builder = WebApplication.CreateBuilder(args);

// Tương đương require_once("./config.php") ở mỗi file PHP gốc: nạp cấu hình VNPay
// từ appsettings.json (section "VnPay").
builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection(VnPayOptions.SectionName));

// Tương đương db.php: kết nối MySQL (xeghep_db) qua EF Core + Pomelo.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Server=127.0.0.1;Database=xeghep_db;User=root;Password=hanh;";
// Dùng ServerVersion cố định (MySQL 8.0) thay vì AutoDetect để tránh phải mở kết nối
// thật tới DB ngay lúc khởi động ứng dụng / build. Đổi số phiên bản nếu bạn dùng MariaDB
// hoặc MySQL version khác.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21))));

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "XeGhep Passenger & VNPay API",
        Version = "v1",
        Description = "Chuyển đổi từ bộ demo PHP (config.php, vnpay_*.php, khach-*.php) sang ASP.NET Core."
    });
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IPassengerService, PassengerService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Bật Swagger ở mọi môi trường để dễ test API demo.
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles(); // phục vụ wwwroot/style.css, wwwroot/assets/common.js

app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Passenger}/{action=Index}/{id?}");

app.Run();
