namespace XeGhepApp.Data;

public static class PasswordHelper
{
    // Hàm dùng để băm (mã hóa) mật khẩu khi đăng ký
    public static string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    // Hàm dùng để kiểm tra mật khẩu khi đăng nhập hoặc rút tiền
    public static bool Verify(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}