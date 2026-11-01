using ClosedXML.Excel;
using Core.Abstractions;
using Core.Models;

namespace Infrastructure.Excel
{
    /// <summary>
    /// Tạo file Excel mẫu trống (có tiêu đề, ghi chú và danh sách chọn).
    /// </summary>
    public class ExcelTemplateWriter : ITemplateWriter
    {
        private const string Font = "Arial";

        /// <summary>Số dòng trống kẻ sẵn trong file mẫu (người dùng thêm dòng tuỳ ý).</summary>
        private const int TemplateRows = 20;

        public void Write(string path)
        {
            using var workbook = CreateWorkbook();
            workbook.SaveAs(path);
        }

        /// <summary>Workbook mẫu trống (dùng chung cho file mẫu và file tạo từ PDF).</summary>
        internal static XLWorkbook CreateWorkbook()
        {
            var workbook = new XLWorkbook();
            WriteGuide(workbook.AddWorksheet("HuongDan"));
            var list = workbook.AddWorksheet(ExcelColumns.SheetName);
            var catalog = workbook.AddWorksheet("DanhMuc");

            WriteCatalog(catalog);
            WritePassengerSheet(list);

            list.SetTabActive();
            return workbook;
        }

        private static void WritePassengerSheet(IXLWorksheet ws)
        {
            var columns = ExcelColumns.Template;
            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var cell = ws.Cell(1, i + 1);
                cell.Value = column.Header + (column.Required ? " *" : "");
                cell.Style.Font.SetFontName(Font).Font.SetBold().Font.SetFontColor(XLColor.White);
                cell.Style.Fill.BackgroundColor = column.Required ? XLColor.FromHtml("#C00000") : XLColor.FromHtml("#1F3864");
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText();
                if (column.Note != "")
                    cell.CreateComment().AddText(column.Note);
                ws.Column(i + 1).Width = column.Width;
            }

            var lastRow = TemplateRows + 1;
            var body = ws.Range(2, 1, lastRow, columns.Count);
            body.Style.Font.SetFontName(Font);
            body.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            for (var r = 2; r <= lastRow; r++)
                ws.Cell(r, 1).Value = r - 1;

            int Col(ExcelColumns.Column c) => columns.ToList().IndexOf(c) + 1;

            foreach (var c in new[] { ExcelColumns.DateOfBirth, ExcelColumns.PassportExpiry, ExcelColumns.VisaIssueDate,
                                      ExcelColumns.VisaValidFrom, ExcelColumns.VisaExpiry })
                ws.Range(2, Col(c), lastRow, Col(c)).Style.DateFormat.Format = "dd/MM/yyyy";

            foreach (var c in new[] { ExcelColumns.PassportNumber, ExcelColumns.VisaNumber, ExcelColumns.VisaCode })
                ws.Range(2, Col(c), lastRow, Col(c)).Style.NumberFormat.Format = "@";

            ws.Range(2, Col(ExcelColumns.PassportType), lastRow, Col(ExcelColumns.PassportType))
                .CreateDataValidation().List($"=DanhMuc!$A$2:$A${Catalog.PassportTypes.Count + 1}", true);
            ws.Range(2, Col(ExcelColumns.VisaType), lastRow, Col(ExcelColumns.VisaType))
                .CreateDataValidation().List($"=DanhMuc!$B$2:$B${Catalog.VisaTypes.Count + 1}", true);

            ws.SheetView.FreezeRows(1);
            ws.Row(1).Height = 32;
        }

        private static void WriteCatalog(IXLWorksheet ws)
        {
            var lists = new (string Title, IEnumerable<string> Items)[]
            {
                ("Loại hộ chiếu", Catalog.PassportTypes),
                ("Loại thị thực", Catalog.VisaTypes)
            };
            for (var c = 0; c < lists.Length; c++)
            {
                var header = ws.Cell(1, c + 1);
                header.Value = lists[c].Title;
                header.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3864");
                var r = 2;
                foreach (var item in lists[c].Items)
                    ws.Cell(r++, c + 1).Value = item;
            }
            ws.Style.Font.SetFontName(Font);
            ws.Columns().AdjustToContents();
        }

        private static void WriteGuide(IXLWorksheet ws)
        {
            string[] lines =
            [
                "HƯỚNG DẪN ĐIỀN FILE DANH SÁCH KHÁCH",
                "",
                $"• Mỗi file = 1 đoàn = 1 tờ khai; mỗi dòng của sheet {ExcelColumns.SheetName} là 1 khách (không giới hạn số dòng).",
                "• Có thể bỏ qua Excel: bấm 'Chọn file PDF...' trong app và chọn các file PDF visa điện tử, app tự tạo file Excel.",
                "• Cột tiêu đề màu ĐỎ (có dấu *) là bắt buộc; màu XANH là không bắt buộc.",
                "• Ngày nhập theo định dạng DD/MM/YYYY.",
                "• Loại hộ chiếu / Loại thị thực: chọn từ danh sách thả xuống.",
                "• HỌ TÊN trên visa: chữ đầu tiên là Họ, phần còn lại là Tên (CHEN MEI LING → CHEN | MEI LING).",
                "• Số thị thực (bắt buộc): mục 'Số' trên visa, VD 123456789/EV – app tự bỏ '/EV' và điền 123456789.",
                "• Mã thị thực (không bắt buộc): mục 'Mã' trên visa, VD E260101TWN31234567890 – chỉ để đối chiếu.",
                "• Ngày hết hạn HC = 'THỜI HẠN ĐẾN' (hộ chiếu); Ngày hết hạn TT = 'ĐẾN NGÀY' (thị thực).",
                "• KHÔNG cần nhập Quốc tịch, Ngày đến, Email – chọn một lần cho cả đoàn trên màn hình chính của app.",
                "• KHÔNG cần nhập Số điện thoại – app tự sinh số 9 chữ số (không có số 0 đầu) cho từng khách.",
                "• KHÔNG cần nhập Giới tính – app tự gán ngẫu nhiên Nam/Nữ (sửa được trong bảng của app).",
                "• Rê chuột vào từng tiêu đề cột để xem ghi chú chi tiết."
            ];
            for (var i = 0; i < lines.Length; i++)
                ws.Cell(i + 1, 1).Value = lines[i];
            ws.Style.Font.SetFontName(Font);
            ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#1F3864"));
            ws.Column(1).Width = 110;
        }
    }
}
