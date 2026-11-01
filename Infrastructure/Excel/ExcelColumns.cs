using System.Globalization;
using System.Text;

namespace Infrastructure.Excel
{
    /// <summary>
    /// Tiêu đề cột của file Excel. Khi đọc, tiêu đề được so khớp sau khi bỏ dấu, bỏ "*",
    /// không phân biệt hoa thường, nên thứ tự cột có thể thay đổi.
    /// </summary>
    internal static class ExcelColumns
    {
        public const string SheetName = "DanhSachKhach";

        public sealed record Column(string Header, bool Required, double Width, string Note);

        public static readonly Column Stt = new("STT", false, 6, "");
        public static readonly Column Surname = new("Họ", false, 12, "Chữ đầu tiên trong HỌ TÊN trên visa. VD: CHEN MEI LING → Họ = CHEN");
        public static readonly Column GivenName = new("Tên", true, 16, "Phần còn lại của HỌ TÊN. VD: CHEN MEI LING → Tên = MEI LING");
        /// <summary>Không bắt buộc và không có trong file mẫu: trống thì app gán ngẫu nhiên Nam/Nữ.</summary>
        public static readonly Column Gender = new("Giới tính", false, 10, "Không bắt buộc – để trống thì app tự gán ngẫu nhiên Nam/Nữ.");
        public static readonly Column DateOfBirth = new("Ngày sinh", true, 13, "DD/MM/YYYY");
        public static readonly Column PassportType = new("Loại hộ chiếu", true, 26, "Chọn từ danh sách. Mặc định: P - Hộ chiếu phổ thông");
        public static readonly Column PassportNumber = new("Số hộ chiếu", true, 14, "Nhập dạng chữ để giữ số 0 ở đầu");
        public static readonly Column PassportExpiry = new("Ngày hết hạn HC", true, 15, "DD/MM/YYYY – mục 'THỜI HẠN ĐẾN' trên visa");
        public static readonly Column VisaType = new("Loại thị thực", true, 30, "Chọn từ danh sách. Visa điện tử → 'Thị thực điện tử (E-Visa)'");
        /// <summary>Mục "Số" trên visa – điền vào ô "Số" thị thực trên site (app tự bỏ "/EV").</summary>
        public static readonly Column VisaNumber = new("Số thị thực", true, 16, "Mục 'Số' trên visa, VD: 123456789/EV → app điền 123456789 (9 chữ số).");
        /// <summary>Mục "Mã" trên visa – chỉ lưu để đối chiếu.</summary>
        public static readonly Column VisaCode = new("Mã thị thực", false, 26, "Mục 'Mã' trên visa, VD: E260101TWN31234567890. Chỉ lưu để đối chiếu.");
        public static readonly Column VisaIssueDate = new("Ngày cấp TT", false, 13, "DD/MM/YYYY – không bắt buộc");
        public static readonly Column VisaValidFrom = new("TT hiệu lực từ", false, 14, "DD/MM/YYYY – app cảnh báo nếu ngày đến sớm hơn ngày này");
        public static readonly Column VisaExpiry = new("Ngày hết hạn TT", true, 15, "DD/MM/YYYY – mục 'ĐẾN NGÀY' trên visa");
        public static readonly Column VisaIssuedPlace = new("Nơi cấp", false, 14, "Không bắt buộc");
        public static readonly Column Note = new("Ghi chú", false, 24, "");

        public static IReadOnlyList<Column> All { get; } =
        [
            Stt, Surname, GivenName, Gender, DateOfBirth, PassportType, PassportNumber, PassportExpiry,
            VisaType, VisaNumber, VisaCode, VisaIssueDate, VisaValidFrom, VisaExpiry, VisaIssuedPlace, Note
        ];

        /// <summary>Các cột có trong file mẫu (không có Giới tính – app tự gán).</summary>
        public static IReadOnlyList<Column> Template { get; } = All.Where(c => c != Gender).ToList();

        public static string Normalize(string? header)
        {
            if (string.IsNullOrWhiteSpace(header)) return "";
            var text = header.Replace("*", "").Trim().ToLowerInvariant().Replace('đ', 'd');
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
