using Microsoft.Extensions.Diagnostics.HealthChecks;
using XeGhepApp.Data;

namespace XeGhepApp.Data;

/// <summary>
/// Health check MySQL cho endpoint tổng hợp /health (NFR-06), dùng chung Db.OpenAsync()
/// đã có sẵn thay vì mở thêm một kiểu kết nối khác.
/// </summary>
public class DbHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = await Db.OpenAsync();
            return HealthCheckResult.Healthy("Kết nối MySQL thành công.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Không thể kết nối MySQL.", ex);
        }
    }
}
