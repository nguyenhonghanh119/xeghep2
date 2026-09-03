using MySqlConnector;

namespace XeGhepApp.Data;

/// <summary>
/// Tương đương db.php / khối kết nối PDO lặp lại ở đầu mỗi file PHP gốc.
/// Gọi Db.Configure(...) một lần ở Program.cs, sau đó Db.OpenAsync() ở bất kỳ đâu
/// để lấy một MySqlConnection đã mở, giống cách các file .php cũ tạo $pdo.
/// </summary>
public static class Db
{
    private static string _connectionString = string.Empty;

    public static void Configure(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static async Task<MySqlConnection> OpenAsync()
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }
}

/// <summary>
/// Giả lập ID tài xế đang đăng nhập (Trần Văn Hùng có user_id = 2),
/// y hệt biến $driver_id = 2; được hard-code trong tất cả các file PHP gốc.
/// Khi có đăng nhập thật, thay DriverId bằng giá trị lấy từ session.
/// </summary>
public static class Constants
{
    public const int DriverId = 2;
}