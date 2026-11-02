using System.Globalization;
using ClosedXML.Excel;
using Core.Abstractions;
using Core.Models;
using Core.Validation;
using static Infrastructure.Excel.ExcelColumns;

namespace Infrastructure.Excel
{
    public class ExcelPassengerReader : IPassengerReader
    {
        private static readonly string[] DateFormats =
            ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "yyyy-MM-dd"];

        public PassengerReadResult Read(string path)
        {
            var result = new PassengerReadResult();

            // FileShare.ReadWrite để đọc được cả khi file đang mở trong Excel.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = new XLWorkbook(stream);

            // Mẫu của app (sheet "DanhSachKhach", file tạo từ PDF) → mẫu xin thị thực ("INF_NHAN_SU") → mẫu app theo tiêu đề.
            var sheet = workbook.Worksheets.FirstOrDefault(w => Normalize(w.Name) == Normalize(SheetName));
            if (sheet is null && VisaApplicationListReader.FindSheet(workbook) is { } applicationSheet)
                return VisaApplicationListReader.Read(applicationSheet);
            sheet ??= workbook.Worksheets.FirstOrDefault(w => FindHeaderRow(w) is not null);
            if (sheet is null)
            {
                result.Errors.Add($"Không nhận ra mẫu file: cần sheet \"{VisaApplicationListReader.SheetName}\" (danh sách khách) " +
                                  $"hoặc sheet \"{SheetName}\" (file app tạo từ PDF).");
                return result;
            }

            var headerRow = FindHeaderRow(sheet);
            if (headerRow is null)
            {
                result.Errors.Add($"Sheet \"{sheet.Name}\" không có dòng tiêu đề chứa cột \"{PassportNumber.Header}\".");
                return result;
            }

            var columns = new Dictionary<Column, int>();
            foreach (var cell in headerRow.CellsUsed())
            {
                var key = Normalize(cell.GetString());
                var column = All.FirstOrDefault(c => Normalize(c.Header) == key);
                if (column is not null && !columns.ContainsKey(column))
                    columns[column] = cell.Address.ColumnNumber;
            }

            var missing = All.Where(c => c.Required && !columns.ContainsKey(c)).Select(c => c.Header).ToList();
            if (missing.Count > 0)
            {
                result.Errors.Add($"Thiếu cột bắt buộc: {string.Join(", ", missing)}.");
                return result;
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
            for (var r = headerRow.RowNumber() + 1; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var reader = new RowReader(row, columns);

                // Bỏ qua dòng trống (chỉ có STT hoặc không có gì).
                if (reader.Text(GivenName) == "" && reader.Text(Surname) == "" && reader.Text(PassportNumber) == "")
                    continue;

                var passenger = reader.ToPassenger(r);
                passenger.No = result.Passengers.Count + 1;
                result.Passengers.Add(passenger);
            }

            return result;
        }

        private static IXLRow? FindHeaderRow(IXLWorksheet sheet)
        {
            var key = Normalize(PassportNumber.Header);
            return sheet.RowsUsed()
                .Take(10)
                .FirstOrDefault(row => row.CellsUsed().Any(c => Normalize(c.GetString()) == key));
        }

        private sealed class RowReader(IXLRow row, Dictionary<Column, int> columns)
        {
            private readonly List<ValidationIssue> _issues = [];

            private IXLCell? Cell(Column column) =>
                columns.TryGetValue(column, out var index) ? row.Cell(index) : null;

            public string Text(Column column)
            {
                var cell = Cell(column);
                if (cell is null || cell.IsEmpty()) return "";
                var value = cell.Value;
                // Số hộ chiếu/số thị thực nhập dạng số: tránh dạng 1.09E+08.
                if (value.IsNumber)
                    return value.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture);
                return cell.GetFormattedString().Trim();
            }

            private DateTime? Date(Column column, string field)
            {
                var cell = Cell(column);
                if (cell is null || cell.IsEmpty()) return null;

                var value = cell.Value;
                if (value.IsDateTime) return value.GetDateTime().Date;
                if (value.IsNumber)
                {
                    try { return DateTime.FromOADate(value.GetNumber()).Date; }
                    catch (ArgumentException) { /* rơi xuống báo lỗi */ }
                }

                var text = cell.GetFormattedString().Trim();
                if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    return parsed.Date;

                _issues.Add(new(field, $"{column.Header}: \"{text}\" không đúng định dạng DD/MM/YYYY."));
                return null;
            }

            public Passenger ToPassenger(int excelRow)
            {
                var p = new Passenger
                {
                    ExcelRow = excelRow,
                    Surname = Text(Surname).ToUpperInvariant(),
                    GivenName = Text(GivenName).ToUpperInvariant(),
                    PassportNumber = Text(PassportNumber).Replace(" ", "").ToUpperInvariant(),
                    VisaNumber = NormalizeVisaNumber(Text(VisaNumber)),
                    VisaCode = Text(VisaCode),
                    VisaIssuedPlace = Text(VisaIssuedPlace),
                    Note = Text(Note)
                };

                // File Excel tạo từ PDF ghi tên file PDF gốc ở đầu cột Ghi chú: "xxx.pdf | ...".
                var firstNotePart = p.Note.Split(" | ")[0].Trim();
                if (firstNotePart.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    p.SourceFile = Path.GetFileName(firstNotePart);
                    p.SourcePath = firstNotePart;
                }

                p.DateOfBirth = Date(DateOfBirth, nameof(p.DateOfBirth));
                p.PassportExpiry = Date(PassportExpiry, nameof(p.PassportExpiry));
                p.VisaIssueDate = Date(VisaIssueDate, nameof(p.VisaIssueDate));
                p.VisaValidFrom = Date(VisaValidFrom, nameof(p.VisaValidFrom));
                p.VisaExpiry = Date(VisaExpiry, nameof(p.VisaExpiry));

                var genderText = Text(ExcelColumns.Gender);
                p.Gender = Catalog.ParseGender(genderText);
                if (genderText != "" && p.Gender == Core.Models.Gender.Unknown)
                    _issues.Add(new(nameof(p.Gender), $"Giới tính \"{genderText}\" không hợp lệ (Nam/Nữ/Khác)."));

                var passportTypeText = Text(PassportType);
                if (passportTypeText == "")
                    p.PassportType = Catalog.DefaultPassportType;
                else if (Catalog.MatchPassportType(passportTypeText) is { } passportType)
                    p.PassportType = passportType;
                else
                {
                    p.PassportType = "";
                    _issues.Add(new(nameof(p.PassportType), $"Loại hộ chiếu \"{passportTypeText}\" không có trong danh mục."));
                }

                var visaTypeText = Text(VisaType);
                if (Catalog.MatchVisaType(visaTypeText) is { } visaType)
                    p.VisaType = visaType;
                else
                {
                    p.VisaType = "";
                    if (visaTypeText != "")
                        _issues.Add(new(nameof(p.VisaType), $"Loại thị thực \"{visaTypeText}\" không có trong danh mục."));
                }

                p.ParseIssues.AddRange(_issues);
                return p;
            }

            /// <summary>"123456789/EV" → "123456789"; bỏ khoảng trắng.</summary>
            private static string NormalizeVisaNumber(string raw)
            {
                var value = raw.Replace(" ", "").ToUpperInvariant();
                var slash = value.IndexOf('/');
                return slash > 0 ? value[..slash] : value;
            }
        }
    }
}
