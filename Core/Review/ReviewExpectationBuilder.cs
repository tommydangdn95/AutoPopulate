using Core.Abstractions;
using Core.Models;

namespace Core.Review
{
    /// <summary>
    /// Dựng giá trị mong đợi cho từng khách: đọc LẠI file nguồn (PDF/Excel), không dùng dữ liệu trong bảng,
    /// để phát hiện cả trường hợp bảng bị sửa nhầm. Trường file nguồn không có thì lấy giá trị trong app.
    /// </summary>
    public sealed class ReviewExpectationBuilder(IVisaPdfExtractor pdfExtractor, IPassengerReader excelReader)
    {
        private const string AppSource = "Nhập trong app";

        public List<ExpectedPassenger> Build(IReadOnlyList<Passenger> passengers, GroupSettings group)
        {
            var excelCache = new Dictionary<string, PassengerReadResult?>(StringComparer.OrdinalIgnoreCase);
            return passengers.Select(p => BuildOne(p, group, excelCache)).ToList();
        }

        private ExpectedPassenger BuildOne(Passenger p, GroupSettings group, Dictionary<string, PassengerReadResult?> excelCache)
        {
            var (source, sourceName, problem) = ReadSource(p, excelCache);
            var fields = new List<ExpectedField>();

            // Trường lấy từ file nguồn; file không có (hoặc không đọc được) thì so với giá trị trong app.
            void FromSource(string label, string? sourceValue, string appValue)
            {
                fields.Add(string.IsNullOrWhiteSpace(sourceValue)
                    ? new ExpectedField(label, appValue, AppSource)
                    : new ExpectedField(label, sourceValue, sourceName));
            }
            void FromApp(string label, string value) => fields.Add(new ExpectedField(label, value, AppSource));

            FromSource(ReviewLabels.FullName, source?.FullName, p.FullName);
            FromSource(ReviewLabels.DateOfBirth, Date(source?.DateOfBirth), Date(p.DateOfBirth));
            FromSource(ReviewLabels.PassportNumber, source?.PassportNumber, p.PassportNumber);
            FromSource(ReviewLabels.PassportExpiry, Date(source?.PassportExpiry), Date(p.PassportExpiry));
            FromSource(ReviewLabels.VisaNumber, source?.VisaNumber, p.VisaNumber);
            FromSource(ReviewLabels.VisaExpiry, Date(source?.VisaExpiry), Date(p.VisaExpiry));
            FromSource(ReviewLabels.Gender,
                source is { Gender: not Gender.Unknown } ? Catalog.GenderText(source.Gender) : null,
                Catalog.GenderText(p.Gender));

            FromApp(ReviewLabels.PassportType, p.PassportType);
            FromApp(ReviewLabels.VisaType, p.VisaType);
            FromApp(ReviewLabels.Nationality, group.Nationality.DisplayName);
            FromApp(ReviewLabels.PhoneCountryCode, group.Nationality.PhoneCountryCode);
            FromApp(ReviewLabels.Phone, p.Phone);
            FromApp(ReviewLabels.Email, group.Email);
            if (!string.IsNullOrWhiteSpace(p.VisaIssuedPlace))
                FromApp(ReviewLabels.VisaIssuedPlace, p.VisaIssuedPlace);

            // Ngày cấp do site tự điền hoặc lấy theo ngày đến: chỉ cần có giá trị.
            fields.Add(new ExpectedField(ReviewLabels.VisaIssueDate, null, "Site tự điền"));

            return new ExpectedPassenger(p, fields, problem);
        }

        /// <summary>Các trường chỉ áp dụng theo vị trí trên trang: ngày đến (khách #1), mối quan hệ (khách #2 trở đi).</summary>
        public static IEnumerable<ExpectedField> PositionFields(ReviewCard card, GroupSettings group)
        {
            if (card.Number <= 1)
                yield return new ExpectedField(ReviewLabels.ArrivalDate, group.ArrivalDate.ToString("dd/MM/yyyy"), AppSource);
            else
                yield return new ExpectedField(ReviewLabels.Relationship, Catalog.DefaultRelationship, AppSource);
        }

        private (Passenger? Source, string Name, string? Problem) ReadSource(Passenger p,
            Dictionary<string, PassengerReadResult?> excelCache)
        {
            var path = p.SourcePath;
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(path))
                return (null, "", "Khách này không có file nguồn.");
            if (!File.Exists(path))
                return (null, fileName, $"Không tìm thấy file nguồn: {path}");

            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var result = pdfExtractor.Extract(path);
                var problem = result.Problems.Count > 0 ? $"File PDF thiếu: {string.Join(", ", result.Problems)}" : null;
                return (result.Passenger, $"PDF: {fileName}", problem);
            }

            if (!excelCache.TryGetValue(path, out var rows))
            {
                try { rows = excelReader.Read(path); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { rows = null; }
                excelCache[path] = rows;
            }
            if (rows is null)
                return (null, fileName, $"Không đọc được file Excel: {path}");

            var row = rows.Passengers.FirstOrDefault(r => r.ExcelRow == p.ExcelRow);
            return row is null
                ? (null, fileName, $"Không thấy dòng {p.ExcelRow} trong file Excel: {path}")
                : (row, $"Excel: {fileName} (dòng {p.ExcelRow})", null);
        }

        private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy") ?? "";
    }
}
