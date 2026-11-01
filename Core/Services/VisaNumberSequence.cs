namespace Core.Services
{
    /// <summary>
    /// Sinh số thị thực cho cả đoàn từ số của khách đầu tiên:
    /// khách 1 = số gốc, khách 2 = số gốc + "1", khách 3 = số gốc + "2", ... khách 11 = số gốc + "10".
    /// VD gốc AA26276768 → AA26276768, AA262767681, AA262767682, ..., AA2627676810.
    /// </summary>
    public static class VisaNumberSequence
    {
        /// <param name="index">Vị trí khách trong danh sách, bắt đầu từ 0.</param>
        public static string For(string firstNumber, int index) =>
            index <= 0 ? firstNumber : firstNumber + index;

        /// <summary>Chuẩn hoá số người dùng nhập: chỉ giữ chữ và số, viết hoa.</summary>
        public static string Normalize(string input) =>
            new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
