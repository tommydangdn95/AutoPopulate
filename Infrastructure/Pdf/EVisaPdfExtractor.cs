using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Infrastructure.Pdf
{
    /// <summary>
    /// Đọc file PDF thị thực điện tử (E-Visa) của Cục Quản lý xuất nhập cảnh.
    /// So khớp trên chữ đã bỏ dấu tiếng Việt, nên không phụ thuộc cách PDF lưu dấu.
    /// </summary>
    public static partial class EVisaPdfParser
    {
        // "Số: 123456789/EV"
        [GeneratedRegex(@"\b(\d{6,12})\s*/\s*EV\b")]
        private static partial Regex VisaNumberRegex();

        // "Mã: E260101TWN31234567890"
        [GeneratedRegex(@"\b(E\d{6}[A-Z]{3}\d{6,20})\b")]
        private static partial Regex VisaCodeRegex();

        // "THỊ THỰC CÓ GIÁ TRỊ TỪ NGÀY 25/11/2026"
        [GeneratedRegex(@"GIA TRI TU NGAY\s*:?\s*(\d{1,2}/\d{1,2}/\d{4})")]
        private static partial Regex ValidFromRegex();

        // "ĐẾN NGÀY 22/02/2027"
        [GeneratedRegex(@"DEN NGAY\s*:?\s*(\d{1,2}/\d{1,2}/\d{4})")]
        private static partial Regex VisaExpiryRegex();

        // "HỌ TÊN: CHEN MEI LING" (dừng trước chữ thường như "Full name")
        [GeneratedRegex(@"HO TEN\s*:\s*([A-Z]+(?![a-z])(?:[ \t]+[A-Z]+(?![a-z]))*)")]
        private static partial Regex FullNameRegex();

        // "NGÀY THÁNG NĂM SINH: 26/01/2000"
        [GeneratedRegex(@"NAM SINH\s*:\s*(\d{1,2}/\d{1,2}/\d{4})")]
        private static partial Regex DateOfBirthRegex();

        // "SỐ HỘ CHIẾU: 312345678"
        [GeneratedRegex(@"SO HO CHIEU\s*:\s*([A-Z0-9]{5,15})")]
        private static partial Regex PassportNumberRegex();

        // "THỜI HẠN ĐẾN: 07/10/2030" (hạn hộ chiếu)
        [GeneratedRegex(@"THOI HAN DEN\s*:\s*(\d{1,2}/\d{1,2}/\d{4})")]
        private static partial Regex PassportExpiryRegex();

        // "MANG HỘ CHIẾU CỦA NƯỚC/LÃNH THỔ: Taiwan(Đài Loan)"
        [GeneratedRegex(@"LANH THO\s*:\s*([A-Za-z][A-Za-z ()]*?)\s*(?:Holding|\r?\n|$)")]
        private static partial Regex NationalityRegex();

        private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy"];

        /// <summary>Tách dữ liệu từ chữ của PDF. Trả về khách (có thể thiếu trường) và danh sách trường thiếu.</summary>
        public static (Passenger Passenger, List<string> Problems) Parse(string pdfText, string sourcePath)
        {
            var text = StripDiacritics(pdfText);
            var problems = new List<string>();
            var p = new Passenger
            {
                VisaType = Catalog.EVisa,
                PassportType = Catalog.DefaultPassportType
            };

            if (!text.Contains("THI THUC DIEN TU") && !text.Contains("ELECTRONIC VISA"))
                problems.Add("không giống thị thực điện tử");

            var fullName = Match(FullNameRegex(), text);
            if (fullName is null) problems.Add("Họ tên");
            else
            {
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                p.Surname = parts[0];
                p.GivenName = string.Join(' ', parts.Skip(1));
            }

            p.DateOfBirth = Date(DateOfBirthRegex(), text, "Ngày sinh", problems);
            p.PassportNumber = Match(PassportNumberRegex(), text) ?? Missing("Số hộ chiếu", problems);
            p.PassportExpiry = Date(PassportExpiryRegex(), text, "Ngày hết hạn hộ chiếu", problems);
            p.VisaNumber = Match(VisaNumberRegex(), text) ?? Missing("Số thị thực", problems);
            p.VisaCode = Match(VisaCodeRegex(), text) ?? "";
            p.VisaValidFrom = Date(ValidFromRegex(), text, "Ngày hiệu lực thị thực", problems);
            p.VisaExpiry = Date(VisaExpiryRegex(), text, "Ngày hết hạn thị thực", problems);

            // Đường dẫn đầy đủ của PDF ở đầu Ghi chú: giữ được file nguồn của khách qua file Excel trung gian.
            p.SourcePath = sourcePath;
            p.SourceFile = Path.GetFileName(sourcePath);
            var note = new List<string> { sourcePath };
            if (Match(NationalityRegex(), text) is { } nationality)
                note.Add($"Quốc tịch trên visa: {nationality.Trim()}");
            if (problems.Count > 0)
                note.Add("Thiếu: " + string.Join(", ", problems));
            p.Note = string.Join(" | ", note);

            return (p, problems);
        }

        private static string? Match(Regex regex, string text)
        {
            var m = regex.Match(text);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string Missing(string field, List<string> problems)
        {
            problems.Add(field);
            return "";
        }

        private static DateTime? Date(Regex regex, string text, string field, List<string> problems)
        {
            if (Match(regex, text) is { } value &&
                DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;
            problems.Add(field);
            return null;
        }

        internal static string StripDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(ch switch { 'Đ' => 'D', 'đ' => 'd', _ => ch });
            }
            return sb.ToString();
        }
    }

    public class EVisaPdfExtractor : IVisaPdfExtractor
    {
        public VisaPdfResult Extract(string path)
        {
            string text;
            try
            {
                using var document = PdfDocument.Open(path);
                var sb = new StringBuilder();
                foreach (var page in document.GetPages())
                    sb.AppendLine(ContentOrderTextExtractor.GetText(page));
                text = sb.ToString();
            }
            catch (Exception ex)
            {
                return new VisaPdfResult(path, null, [$"không mở được file PDF ({ex.Message.Split('\n')[0]})"]);
            }

            if (string.IsNullOrWhiteSpace(text))
                return new VisaPdfResult(path, null, ["PDF không có lớp chữ (có thể là ảnh scan) – cần nhập tay"]);

            var (passenger, problems) = EVisaPdfParser.Parse(text, path);
            return new VisaPdfResult(path, passenger, problems);
        }
    }
}
