namespace Core.Models
{
    /// <summary>
    /// Quốc tịch chọn cho cả đoàn ở bước "Chọn Quốc tịch của bạn".
    /// </summary>
    /// <param name="DisplayName">Nhãn đúng như trong dropdown của site (tiếng Việt).</param>
    /// <param name="SearchText">Chuỗi gõ vào ô tìm kiếm để lọc dropdown.</param>
    /// <param name="PhoneCountryCode">Mã quốc gia điện thoại site tự điền theo quốc tịch.</param>
    public sealed record NationalityOption(string DisplayName, string SearchText, string PhoneCountryCode)
    {
        public static readonly NationalityOption Taiwan = new("Trung Quốc (Đài Loan)", "Đài Loan", "+886");
        public static readonly NationalityOption China = new("Trung Quốc", "Trung Quốc", "+86");

        public static IReadOnlyList<NationalityOption> All { get; } = [Taiwan, China];

        public override string ToString() => $"{DisplayName} ({PhoneCountryCode})";
    }
}
