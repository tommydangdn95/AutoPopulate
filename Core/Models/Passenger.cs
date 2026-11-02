using Core.Validation;

namespace Core.Models
{
    public enum PassengerState
    {
        Pending,
        Invalid,
        Filling,
        NeedsAttention,
        Filled,
        Skipped,
        /// <summary>Trang "Xem lại và nộp" khớp với file nguồn.</summary>
        Verified,
        /// <summary>Trang "Xem lại và nộp" khác với file nguồn.</summary>
        VerifyFailed
    }

    /// <summary>
    /// Một hành khách, tương ứng một dòng trong file Excel.
    /// </summary>
    public class Passenger
    {
        /// <summary>Số thứ tự khách (bắt đầu từ 1), trùng cột STT trong file Excel.</summary>
        public int No { get; set; }

        /// <summary>Số dòng trong file Excel.</summary>
        public int ExcelRow { get; set; }

        /// <summary>Tên file PDF visa gốc nếu khách được nhập từ PDF (rỗng nếu nhập tay trong Excel).</summary>
        public string SourceFile { get; set; } = "";

        /// <summary>Đường dẫn file nguồn của khách: file PDF riêng, hoặc file Excel danh sách.</summary>
        public string SourcePath { get; set; } = "";

        /// <summary>Nguồn dữ liệu để ghi vào thông báo lỗi, vd " [file VietNam_eVisa_xxx.pdf]".</summary>
        public string SourceSuffix => SourceFile == "" ? "" : $" [file {SourceFile}]";

        public string Surname { get; set; } = "";
        public string GivenName { get; set; } = "";
        public Gender Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public string PassportType { get; set; } = Catalog.DefaultPassportType;
        public string PassportNumber { get; set; } = "";
        public DateTime? PassportExpiry { get; set; }

        public string VisaType { get; set; } = Catalog.EVisa;
        /// <summary>Mục "Số" trên visa, bỏ hậu tố "/EV" (vd 123456789) – điền vào ô "Số" thị thực trên site.</summary>
        public string VisaNumber { get; set; } = "";
        /// <summary>Mục "Mã" trên visa (vd E260101TWN31234567890), chỉ lưu để đối chiếu.</summary>
        public string VisaCode { get; set; } = "";
        public DateTime? VisaIssueDate { get; set; }
        public DateTime? VisaValidFrom { get; set; }
        public DateTime? VisaExpiry { get; set; }
        public string VisaIssuedPlace { get; set; } = "";

        /// <summary>Số điện thoại của khách (app tự sinh 9 chữ số, không có số 0 đầu, không gồm mã quốc gia).</summary>
        public string Phone { get; set; } = "";

        public string Note { get; set; } = "";

        public PassengerState State { get; set; }
        public string StatusMessage { get; set; } = "";

        /// <summary>Lỗi khi đọc Excel (sai định dạng ngày, giá trị không có trong danh mục...).</summary>
        public List<ValidationIssue> ParseIssues { get; } = [];

        /// <summary>Kết quả kiểm tra gần nhất (gồm cả ParseIssues).</summary>
        public List<ValidationIssue> Issues { get; } = [];

        public bool HasErrors => Issues.Any(i => !i.IsWarning);

        public string FullName => $"{Surname} {GivenName}".Trim();

        public string StateText => State switch
        {
            PassengerState.Pending => "Chờ",
            PassengerState.Invalid => "Lỗi dữ liệu",
            PassengerState.Filling => "Đang điền",
            PassengerState.NeedsAttention => "Cần sửa",
            PassengerState.Filled => "Đã điền",
            PassengerState.Skipped => "Bỏ qua",
            PassengerState.Verified => "Đã đối chiếu",
            PassengerState.VerifyFailed => "Sai so với nguồn",
            _ => State.ToString()
        };
    }
}
