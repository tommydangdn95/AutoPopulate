using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Validation
{
    /// <summary>
    /// Kiểm tra dữ liệu trước khi mở trình duyệt. Các quy tắc chép theo
    /// validator phía client của site, để lỗi được phát hiện ngay trong app.
    /// </summary>
    public static partial class PassengerValidator
    {
        [GeneratedRegex("^[A-Za-z0-9 ]+$")]
        private static partial Regex LatinName();

        [GeneratedRegex("^[A-Za-z0-9]{1,15}$")]
        private static partial Regex PassportPattern();

        [GeneratedRegex("^[A-Za-z0-9]{1,25}$")]
        private static partial Regex VisaPattern();

        /// <summary>Ngày hết hạn TT phải cách ngày TT hiệu lực từ ít nhất số ngày này.</summary>
        public const int MinVisaValidityDays = 10;

        /// <summary>Site chỉ nhận số thị thực điện tử (EV) đúng 9 chữ số.</summary>
        [GeneratedRegex("^[0-9]{9}$")]
        private static partial Regex EVisaNumberPattern();

        [GeneratedRegex("^[0-9]{3,14}$")]
        private static partial Regex PhonePattern();

        [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
        private static partial Regex EmailPattern();

        /// <summary>
        /// Kiểm tra cả đoàn. Trả về lỗi cấp đoàn; lỗi từng khách ghi vào Passenger.Issues.
        /// </summary>
        public static List<string> Validate(IReadOnlyList<Passenger> passengers, GroupSettings group, DateTime today)
        {
            var groupErrors = new List<string>();

            if (passengers.Count == 0)
                groupErrors.Add("Chưa có khách nào – bấm \"Chọn file PDF...\" hoặc \"Chọn file Excel...\".");
            if (string.IsNullOrWhiteSpace(group.Email) || !EmailPattern().IsMatch(group.Email.Trim()))
                groupErrors.Add("Email công ty chưa nhập hoặc sai định dạng.");

            var passports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in passengers)
            {
                ValidateOne(p, group, today);

                if (!string.IsNullOrEmpty(p.PassportNumber) && !passports.Add(p.PassportNumber))
                    p.Issues.Add(new(nameof(p.PassportNumber), "Số hộ chiếu bị trùng với khách khác."));
                if (!string.IsNullOrEmpty(p.VisaNumber) && !visas.Add(p.VisaNumber))
                    p.Issues.Add(new(nameof(p.VisaNumber), "Số thị thực bị trùng với khách khác."));

                if (p.State is PassengerState.Pending or PassengerState.Invalid)
                    p.State = p.HasErrors ? PassengerState.Invalid : PassengerState.Pending;
                if (p.State is PassengerState.Pending or PassengerState.Invalid)
                    p.StatusMessage = string.Join("; ", p.Issues.Select(i => i.Message));
            }

            return groupErrors;
        }

        private static void ValidateOne(Passenger p, GroupSettings group, DateTime today)
        {
            var issues = p.Issues;
            issues.Clear();
            issues.AddRange(p.ParseIssues);

            void Error(string field, string message) => issues.Add(new(field, message));
            void Warn(string field, string message) => issues.Add(new(field, message, IsWarning: true));
            bool HasParseIssue(string field) => p.ParseIssues.Any(i => i.Field == field);

            var arrival = group.ArrivalDate.Date;

            if (!string.IsNullOrEmpty(p.Surname) && !LatinName().IsMatch(p.Surname))
                Error(nameof(p.Surname), "Họ chỉ được chứa chữ không dấu, số và khoảng trắng.");

            if (string.IsNullOrWhiteSpace(p.GivenName))
                Error(nameof(p.GivenName), "Thiếu Tên.");
            else if (!LatinName().IsMatch(p.GivenName))
                Error(nameof(p.GivenName), "Tên chỉ được chứa chữ không dấu, số và khoảng trắng.");

            if (p.Gender == Gender.Unknown && !HasParseIssue(nameof(p.Gender)))
                Error(nameof(p.Gender), "Thiếu Giới tính (Nam/Nữ/Khác).");

            if (p.DateOfBirth is null)
            {
                if (!HasParseIssue(nameof(p.DateOfBirth))) Error(nameof(p.DateOfBirth), "Thiếu Ngày sinh.");
            }
            else if (p.DateOfBirth.Value.Date > today)
                Error(nameof(p.DateOfBirth), "Ngày sinh không được sau ngày hôm nay.");

            if (string.IsNullOrWhiteSpace(p.PassportType) && !HasParseIssue(nameof(p.PassportType)))
                Error(nameof(p.PassportType), "Thiếu Loại hộ chiếu.");

            if (string.IsNullOrWhiteSpace(p.PassportNumber))
                Error(nameof(p.PassportNumber), "Thiếu Số hộ chiếu.");
            else if (!PassportPattern().IsMatch(p.PassportNumber))
                Error(nameof(p.PassportNumber), "Số hộ chiếu chỉ gồm chữ/số, tối đa 15 ký tự.");

            if (p.PassportExpiry is null)
            {
                if (!HasParseIssue(nameof(p.PassportExpiry))) Error(nameof(p.PassportExpiry), "Thiếu Ngày hết hạn hộ chiếu.");
            }
            else if (p.PassportExpiry.Value.Date < today)
                Error(nameof(p.PassportExpiry), "Hộ chiếu đã hết hạn.");

            ValidateVisa(p, arrival, Error, Warn, HasParseIssue);

            if (string.IsNullOrWhiteSpace(p.Phone))
                Error(nameof(p.Phone), "Chưa có số điện thoại.");
            else if (!PhonePattern().IsMatch(p.Phone))
                Error(nameof(p.Phone), "Số điện thoại chỉ gồm 3–14 chữ số (không có mã quốc gia).");
        }

        private static void ValidateVisa(Passenger p, DateTime arrival,
            Action<string, string> error, Action<string, string> warn, Func<string, bool> hasParseIssue)
        {
            if (string.IsNullOrWhiteSpace(p.VisaType))
            {
                if (!hasParseIssue(nameof(p.VisaType))) error(nameof(p.VisaType), "Thiếu Loại thị thực.");
                return;
            }

            var isEVisa = p.VisaType == Catalog.EVisa;
            var exempt = Catalog.IsVisaExemption(p.VisaType);

            if (string.IsNullOrWhiteSpace(p.VisaNumber))
            {
                if (!exempt) error(nameof(p.VisaNumber), "Thiếu Số thị thực (mục \"Số\" trên visa).");
            }
            else if (isEVisa && !EVisaNumberPattern().IsMatch(p.VisaNumber))
                error(nameof(p.VisaNumber),
                    "Thị thực điện tử: Số thị thực phải đúng 9 chữ số – mục \"Số\" trên visa (VD 123456789/EV → 123456789).");
            else if (!VisaPattern().IsMatch(p.VisaNumber))
                error(nameof(p.VisaNumber), "Số thị thực chỉ gồm chữ/số, tối đa 25 ký tự.");

            if (p.VisaIssueDate is { } issue && issue.Date > arrival)
                error(nameof(p.VisaIssueDate), "Ngày cấp thị thực không được sau ngày đến.");

            if (p.VisaValidFrom is { } from && from.Date > arrival)
                warn(nameof(p.VisaValidFrom), $"Visa có hiệu lực từ {from:dd/MM/yyyy}, sau ngày đến {arrival:dd/MM/yyyy}.");

            if (p.VisaExpiry is null)
            {
                if (!exempt && !hasParseIssue(nameof(p.VisaExpiry)))
                    error(nameof(p.VisaExpiry), "Thiếu Ngày hết hạn thị thực.");
                return;
            }

            var expiry = p.VisaExpiry.Value.Date;
            if (p.VisaValidFrom is { } validFrom)
            {
                if (expiry < validFrom.Date)
                    error(nameof(p.VisaExpiry),
                        $"Ngày hết hạn TT ({expiry:dd/MM/yyyy}) không được trước ngày TT hiệu lực từ ({validFrom:dd/MM/yyyy}).");
                else if ((expiry - validFrom.Date).TotalDays < MinVisaValidityDays)
                    error(nameof(p.VisaExpiry),
                        $"Ngày hết hạn TT phải sau ngày TT hiệu lực từ ít nhất {MinVisaValidityDays} ngày " +
                        $"(sớm nhất {validFrom.Date.AddDays(MinVisaValidityDays):dd/MM/yyyy}).");
            }
            if (expiry < arrival)
                error(nameof(p.VisaExpiry), "Thị thực hết hạn trước ngày đến.");
            if (isEVisa && p.VisaIssueDate is { } issued && expiry > issued.Date.AddDays(90))
                error(nameof(p.VisaExpiry), "Thị thực điện tử có thời hạn tối đa 90 ngày kể từ ngày cấp.");
            if (!exempt && p.PassportExpiry is { } passportExpiry && expiry > passportExpiry.Date.AddDays(-30))
                error(nameof(p.VisaExpiry), "Thị thực phải hết hạn trước ngày hết hạn hộ chiếu ít nhất 30 ngày.");
        }
    }
}
