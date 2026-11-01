using ClosedXML.Excel;
using Core.Abstractions;
using Core.Models;
using static Infrastructure.Excel.ExcelColumns;

namespace Infrastructure.Excel
{
    /// <summary>
    /// Ghi danh sách khách ra file Excel theo đúng mẫu (để đọc lại bằng ExcelPassengerReader).
    /// </summary>
    public class ExcelPassengerWriter : IPassengerWriter
    {
        public void Write(string path, IReadOnlyList<Passenger> passengers)
        {
            using var workbook = ExcelTemplateWriter.CreateWorkbook();
            var ws = workbook.Worksheet(SheetName);
            var columns = Template.ToList();
            int Col(Column c) => columns.IndexOf(c) + 1;

            for (var i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                var r = i + 2;
                ws.Cell(r, Col(Stt)).Value = i + 1;
                ws.Cell(r, Col(Surname)).Value = p.Surname;
                ws.Cell(r, Col(GivenName)).Value = p.GivenName;
                SetDate(ws.Cell(r, Col(DateOfBirth)), p.DateOfBirth);
                ws.Cell(r, Col(PassportType)).Value = p.PassportType;
                SetText(ws.Cell(r, Col(PassportNumber)), p.PassportNumber);
                SetDate(ws.Cell(r, Col(PassportExpiry)), p.PassportExpiry);
                ws.Cell(r, Col(VisaType)).Value = p.VisaType;
                SetText(ws.Cell(r, Col(VisaNumber)), p.VisaNumber);
                SetText(ws.Cell(r, Col(VisaCode)), p.VisaCode);
                SetDate(ws.Cell(r, Col(VisaIssueDate)), p.VisaIssueDate);
                SetDate(ws.Cell(r, Col(VisaValidFrom)), p.VisaValidFrom);
                SetDate(ws.Cell(r, Col(VisaExpiry)), p.VisaExpiry);
                ws.Cell(r, Col(VisaIssuedPlace)).Value = p.VisaIssuedPlace;
                ws.Cell(r, Col(Note)).Value = p.Note;
            }

            // Mẫu chỉ định dạng sẵn 20 dòng; file từ PDF có thể tới 100 dòng.
            if (passengers.Count > 0)
            {
                var range = ws.Range(2, 1, passengers.Count + 1, columns.Count);
                range.Style.Font.SetFontName("Arial");
                range.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            workbook.SaveAs(path);
        }

        private static void SetText(IXLCell cell, string value)
        {
            cell.Style.NumberFormat.Format = "@";
            cell.Value = value;
        }

        private static void SetDate(IXLCell cell, DateTime? value)
        {
            if (value is null) return;
            cell.Value = value.Value;
            cell.Style.DateFormat.Format = "dd/MM/yyyy";
        }
    }
}
