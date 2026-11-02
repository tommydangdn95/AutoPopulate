using System.Text;
using System.Text.RegularExpressions;

namespace Core.Review
{
    /// <summary>Thông tin một hành khách đọc từ trang "Xem lại và nộp".</summary>
    public sealed record ReviewCard(int Number, IReadOnlyDictionary<string, string> Fields);

    /// <summary>Nhãn các trường trên trang "Xem lại và nộp" (giao diện tiếng Việt).</summary>
    public static class ReviewLabels
    {
        public const string ArrivalDate = "Ngày đến dự kiến (DD/MM/YYYY GMT+7)";
        public const string PassportType = "Loại hộ chiếu";
        public const string PassportNumber = "Số hộ chiếu";
        public const string PassportExpiry = "Ngày hết hạn (DD/MM/YYYY)";
        public const string FullName = "Họ và tên";
        public const string Gender = "Giới tính";
        public const string DateOfBirth = "Ngày sinh (DD/MM/YYYY)";
        public const string Nationality = "Quốc tịch";
        public const string PhoneCountryCode = "Mã điện thoại quốc gia";
        public const string Phone = "Số điện thoại";
        public const string Email = "Địa chỉ thư điện tử";
        public const string Relationship = "Mối quan hệ với người khai";
        public const string VisaType = "Loại thị thực";
        public const string VisaNumber = "Số thị thực";
        public const string VisaIssueDate = "Ngày cấp thị thực (DD/MM/YYYY)";
        public const string VisaExpiry = "Ngày hết hạn thị thực (DD/MM/YYYY)";
        public const string VisaIssuedPlace = "Nơi cấp";

        public static IReadOnlyList<string> All { get; } =
        [
            ArrivalDate, PassportType, PassportNumber, PassportExpiry, FullName, Gender, DateOfBirth, Nationality,
            PhoneCountryCode, Phone, Email, Relationship, VisaType, VisaNumber, VisaIssueDate, VisaExpiry, VisaIssuedPlace
        ];
    }

    /// <summary>
    /// Tách chữ của trang "Xem lại và nộp" thành từng hành khách. Mỗi trường hiển thị dạng
    /// một dòng nhãn, dòng kế tiếp là giá trị; mỗi khách bắt đầu bằng "Thông tin hành khách thứ #n".
    /// </summary>
    public static partial class ReviewPageParser
    {
        [GeneratedRegex(@"hành khách thứ\s*#\s*(\d+)", RegexOptions.IgnoreCase)]
        private static partial Regex CardHeading();

        private const string TripSectionHeading = "Thông tin chuyến đi";

        private static readonly Dictionary<string, string> LabelByKey =
            ReviewLabels.All.ToDictionary(Key, label => label);

        public static List<ReviewCard> Parse(string pageText)
        {
            var cards = new List<ReviewCard>();
            Dictionary<string, string>? fields = null;
            var number = 0;
            var lines = pageText.Normalize(NormalizationForm.FormC)
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l != "")
                .ToList();

            void Flush()
            {
                if (fields is not null)
                    cards.Add(new ReviewCard(number, fields));
                fields = null;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var heading = CardHeading().Match(lines[i]);
                if (heading.Success)
                {
                    Flush();
                    number = int.Parse(heading.Groups[1].Value);
                    fields = new Dictionary<string, string>();
                    continue;
                }
                if (fields is null) continue;

                // Hết phần hành khách: phần chuyến đi có thể trùng nhãn nên không đọc tiếp.
                if (Key(lines[i]) == Key(TripSectionHeading))
                {
                    Flush();
                    continue;
                }

                if (!LabelByKey.TryGetValue(Key(lines[i]), out var label) || fields.ContainsKey(label))
                    continue;

                // Dòng kế tiếp là giá trị, trừ khi nó là nhãn khác (giá trị trống).
                var next = i + 1 < lines.Count ? lines[i + 1] : "";
                if (LabelByKey.ContainsKey(Key(next)) || CardHeading().IsMatch(next))
                {
                    fields[label] = "";
                    continue;
                }
                fields[label] = next;
                i++;
            }
            Flush();
            return cards;
        }

        private static string Key(string text) =>
            text.Normalize(NormalizationForm.FormC).Replace("*", "").Trim().ToUpperInvariant();
    }
}
