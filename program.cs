using XeGhepApp.Data;
using XeGhepApp.Endpoints;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Configuration.AddJsonFile("db.json", optional: true, reloadOnChange: true);

Db.Configure(builder.Configuration.GetConnectionString("XeGhepDb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Thiếu chuỗi kết nối MySQL XeGhepDb/DefaultConnection."));

builder.Services.AddRazorPages();
builder.Services.AddSharedAuthentication(builder.Configuration);

// Session dùng cho login.php / dang-ky.php (đăng nhập, OTP), tương đương session_start() trong PHP
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});
builder.Services.AddHealthChecks()
    .AddCheck<XeGhepApp.Data.DbHealthCheck>("mysql");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Phục vụ style.css, assets/common.js, uploads/docs/... từ wwwroot (giống thư mục gốc web root trong PHP)
app.UseStaticFiles();

app.UseSession();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CookieCsrfMiddleware>();
app.UseAuthorization();

app.MapRazorPages();

// Các endpoint AJAX JSON thuần (start-trip.php, complete-trip.php, save-profile.php,
// upload-document.php, delete-document.php) - giữ nguyên đường dẫn *.php để JS phía
// client (fetch('start-trip.php') v.v.) không cần chỉnh sửa gì.
DriverApiEndpoints.Map(app);
app.MapHealthChecks("/health");
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async () =>
{
    try
    {
        await using var conn = await Db.OpenAsync();
        var otpProvider = app.Configuration["Otp:Provider"];
        if (string.IsNullOrWhiteSpace(otpProvider))
            return Results.Json(new { status = "not_ready", database = "mysql", otp = "missing_configuration" }, statusCode: 503);
        return Results.Ok(new { status = "ready", database = "mysql", otp = otpProvider.ToLowerInvariant() });
    }
    catch
    {
        return Results.Json(new { status = "not_ready" }, statusCode: 503);
    }
});

app.Run();
