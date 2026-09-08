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

