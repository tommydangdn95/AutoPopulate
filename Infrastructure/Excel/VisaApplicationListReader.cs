using System.Globalization;
using ClosedXML.Excel;
using Core.Abstractions;
using Core.Models;
using Core.Validation;

namespace Infrastructure.Excel
{
    /// <summary>
    /// Đọc file Excel danh sách khách theo mẫu xin thị thực (sheet "INF_NHAN_SU"):
    /// STT | Họ và tên | Ngày, tháng, năm sinh | Giới tính (F/M) | Quốc tịch hiện nay | ... | Số hộ chiếu |
    /// Loại hộ chiếu (thực tế chứa ngày hết hạn hộ chiếu) | Mục đích nhập cảnh | Đề nghị từ ngày | Đến ngày | ...
    /// Các cột thông tin chung của đoàn (quốc tịch, mục đích, đề nghị từ/đến ngày) thường chỉ điền ở dòng đầu:
    /// dòng trống lấy giá trị của dòng phía trên.
    /// </summary>
    internal static class VisaApplicationListReader
    {
        public const string SheetName = "INF_NHAN_SU";
        private const string VisaOnArrivalType = "Thị thực";

        private static readonly string[] DateFormats =
            ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "M/d/yyyy"];

        private static class Keys
        {
            public const string FullName = "ho va ten";
            public const string DateOfBirth = "ngay thang nam sinh";
            public const string Gender = "gioi tinh";
            public const string Nationality = "quoc tich hien nay";
            public const string PassportNumber = "so ho chieu";
            public const string PassportType = "loai ho chieu";
            public const string PassportExpiry = "ngay het han ho chieu";
            public const string Purpose = "muc dich nhap canh";
            public const string ValidFrom = "de nghi tu ngay";
            public const string ValidTo = "den ngay";
            public const string VisaNumber = "so thi thuc";
            public const string VisaCode = "ma thi thuc";
        }

        /// <summary>Chuẩn hoá tiêu đề: bỏ "(*)", dấu câu, dấu tiếng Việt.</summary>
        private static string Key(string? header) =>
            ExcelColumns.Normalize((header ?? "").Replace("(*)", "").Replace(",", " ").Replace(".", " "));

        /// <summary>Tìm sheet theo mẫu xin thị thực (theo tên sheet hoặc tiêu đề "Họ và tên" + "Số hộ chiếu").</summary>
        public static IXLWorksheet? FindSheet(XLWorkbook workbook) =>
            workbook.Worksheets.FirstOrDefault(w => Key(w.Name) == Key(SheetName))
            ?? workbook.Worksheets.FirstOrDefault(w => HeaderRow(w) is not null);

        private static IXLRow? HeaderRow(IXLWorksheet sheet) =>
            sheet.RowsUsed().Take(10).FirstOrDefault(row =>
            {
                var keys = row.CellsUsed().Select(c => Key(c.GetString())).ToList();
                return keys.Contains(Keys.FullName) && keys.Contains(Keys.PassportNumber);
            });

        public static PassengerReadResult Read(IXLWorksheet sheet)
        {
            var result = new PassengerReadResult();
            var header = HeaderRow(sheet);
            if (header is null)
            {
                result.Errors.Add($"Sheet \"{sheet.Name}\" không có dòng tiêu đề chứa \"Họ và tên\" và \"Số hộ chiếu\".");
                return result;
            }

            var cols = new Dictionary<string, int>();
            foreach (var cell in header.CellsUsed())
                cols.TryAdd(Key(cell.GetString()), cell.Address.ColumnNumber);

            var missing = new[] { (Keys.FullName, "Họ và tên"), (Keys.DateOfBirth, "Ngày, tháng, năm sinh"), (Keys.PassportNumber, "Số hộ chiếu") }
                .Where(k => !cols.ContainsKey(k.Item1)).Select(k => k.Item2).ToList();
            if (missing.Count > 0)
            {
                result.Errors.Add($"Thiếu cột bắt buộc: {string.Join(", ", missing)}.");
                return result;
            }

            // Thông tin chung của đoàn: dòng trống dùng lại giá trị gần nhất phía trên.
            string nationality = "", purpose = "";
            DateTime? validFrom = null, validTo = null;

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();
            for (var r = header.RowNumber() + 1; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                IXLCell? Cell(string key) => cols.TryGetValue(key, out var c) ? row.Cell(c) : null;

                var fullName = Text(Cell(Keys.FullName)).ToUpperInvariant();
                var passport = Text(Cell(Keys.PassportNumber)).Replace(" ", "").ToUpperInvariant();
                if (fullName == "" && passport == "") continue;

                var issues = new List<ValidationIssue>();
                // Mẫu xin thị thực nhận tại cửa khẩu (vd "SBQT Đà Nẵng") → loại "Thị thực" (số dạng AA12345678),
                // không phải E-Visa (site chỉ nhận E-Visa đúng 9 chữ số).
                var p = new Passenger
                {
                    ExcelRow = r,
                    No = result.Passengers.Count + 1,
                    PassportNumber = passport,
                    VisaType = VisaOnArrivalType
                };

                var names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (names.Length > 0)
                {
                    p.Surname = names[0];
                    p.GivenName = string.Join(' ', names.Skip(1));
                }

                p.DateOfBirth = Date(Cell(Keys.DateOfBirth), "Ngày sinh", nameof(p.DateOfBirth), issues);

                var genderText = Text(Cell(Keys.Gender));
                p.Gender = Catalog.ParseGender(genderText);
                if (genderText != "" && p.Gender == Gender.Unknown)
                    issues.Add(new(nameof(p.Gender), $"Giới tính \"{genderText}\" không hợp lệ (F/M, Nam/Nữ)."));

                // Cột "Loại hộ chiếu" của mẫu này thực tế chứa ngày hết hạn hộ chiếu → đọc theo kiểu dữ liệu.
                var typeCell = Cell(Keys.PassportType);
                if (Cell(Keys.PassportExpiry) is { } expiryCell && !expiryCell.IsEmpty())
                    p.PassportExpiry = Date(expiryCell, "Ngày hết hạn hộ chiếu", nameof(p.PassportExpiry), issues);
                else if (typeCell is not null && !typeCell.IsEmpty() && LooksLikeDate(typeCell))
                    p.PassportExpiry = Date(typeCell, "Ngày hết hạn hộ chiếu", nameof(p.PassportExpiry), issues);
                if (typeCell is not null && !typeCell.IsEmpty() && !LooksLikeDate(typeCell))
                {
                    var typeText = Text(typeCell);
                    if (Catalog.MatchPassportType(typeText) is { } type) p.PassportType = type;
                    else issues.Add(new(nameof(p.PassportType), $"Loại hộ chiếu \"{typeText}\" không có trong danh mục."));
                }

                nationality = Carry(Text(Cell(Keys.Nationality)), nationality);
                purpose = Carry(Text(Cell(Keys.Purpose)), purpose);
                validFrom = Date(Cell(Keys.ValidFrom), "Đề nghị từ ngày", nameof(p.VisaValidFrom), issues) ?? validFrom;
                validTo = Date(Cell(Keys.ValidTo), "Đến ngày", nameof(p.VisaExpiry), issues) ?? validTo;
                p.VisaValidFrom = validFrom;
                p.VisaExpiry = validTo;

                // Mẫu xin thị thực chưa có số thị thực; nếu file có thêm cột này thì đọc.
                var visaNumber = Text(Cell(Keys.VisaNumber)).Replace(" ", "").ToUpperInvariant();
                var slash = visaNumber.IndexOf('/');
                p.VisaNumber = slash > 0 ? visaNumber[..slash] : visaNumber;
                p.VisaCode = Text(Cell(Keys.VisaCode));

                p.Note = string.Join(" | ", new[] { nationality, purpose }.Where(s => s != ""));
                p.ParseIssues.AddRange(issues);
                result.Passengers.Add(p);
            }

            return result;
        }

        private static string Carry(string value, string previous) => value != "" ? value : previous;

        private static string Text(IXLCell? cell)
        {
            if (cell is null || cell.IsEmpty()) return "";
            var value = cell.Value;
            if (value.IsNumber)
                return value.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture);
            return cell.GetFormattedString().Trim();
        }

        private static bool LooksLikeDate(IXLCell cell) =>
            cell.Value.IsDateTime ||
            DateTime.TryParseExact(cell.GetFormattedString().Trim(), DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _);

        private static DateTime? Date(IXLCell? cell, string label, string field, List<ValidationIssue> issues)
        {
            if (cell is null || cell.IsEmpty()) return null;
            var value = cell.Value;
            if (value.IsDateTime) return value.GetDateTime().Date;
            if (value.IsNumber)
            {
                try { return DateTime.FromOADate(value.GetNumber()).Date; }
                catch (ArgumentException) { /* báo lỗi bên dưới */ }
            }
            var text = cell.GetFormattedString().Trim();
            // Chữ dạng ngày Việt Nam trước (03/11/2026 = 3 tháng 11), rồi mới tới dạng Mỹ.
            if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date.Date;
            issues.Add(new(field, $"{label}: \"{text}\" không đúng định dạng DD/MM/YYYY."));
            return null;
        }
    }
}
