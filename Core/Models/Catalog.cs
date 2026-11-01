namespace Core.Models
{
    /// <summary>
    /// Danh mục lựa chọn, ghi đúng nhãn hiển thị trên site (giao diện tiếng Việt).
    /// </summary>
    public static class Catalog
    {
        public const string DefaultPassportType = "P - Hộ chiếu phổ thông";
        public const string EVisa = "Thị thực điện tử (E-Visa)";

        public static IReadOnlyList<string> PassportTypes { get; } =
        [
            DefaultPassportType,
            "PO - Hộ chiếu công vụ",
            "PD - Hộ chiếu ngoại giao"
        ];

        public static IReadOnlyList<string> VisaTypes { get; } =
        [
            EVisa,
            "Thị thực",
            "Miễn song phương",
            "Miễn đơn phương",
            "Giấy miễn thị thực",
            "Thẻ du lịch",
            "Miễn thị thực nhập cảnh vào khu kinh tế ven biển Phú Quốc",
            "Thẻ ABTC",
            "Thẻ tạm trú",
            "Thẻ thường trú"
        ];

        public static IReadOnlyList<KeyValuePair<Gender, string>> Genders { get; } =
        [
            new(Gender.Unknown, ""),
            new(Gender.Male, "Nam"),
            new(Gender.Female, "Nữ"),
            new(Gender.Other, "Khác")
        ];

        /// <summary>Các loại miễn thị thực: site không bắt buộc nhập số thị thực.</summary>
        public static bool IsVisaExemption(string? visaType) =>
            visaType is not null && visaType.StartsWith("Miễn", StringComparison.OrdinalIgnoreCase);

        public static string? MatchPassportType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var value = raw.Trim();
            var exact = PassportTypes.FirstOrDefault(t => t.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact;
            // Chấp nhận chỉ mã: "P", "PO", "PD".
            return PassportTypes.FirstOrDefault(t =>
                t.Split(" - ")[0].Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        public static string? MatchVisaType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var value = raw.Trim();
            if (value.Equals("EV", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("E-Visa", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Evisa", StringComparison.OrdinalIgnoreCase))
                return EVisa;
            return VisaTypes.FirstOrDefault(t => t.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        public static Gender ParseGender(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Gender.Unknown;
            return raw.Trim().ToLowerInvariant() switch
            {
                "nam" or "m" or "male" or "男" => Gender.Male,
                "nữ" or "nu" or "f" or "female" or "女" => Gender.Female,
                "khác" or "khac" or "other" => Gender.Other,
                _ => Gender.Unknown
            };
        }

        public static string GenderText(Gender gender) =>
            Genders.First(g => g.Key == gender).Value;
    }
}
