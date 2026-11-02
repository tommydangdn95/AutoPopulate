using System.Text;
using Core.Models;

namespace Core.Review
{
    /// <summary>Giá trị mong đợi của một trường. Value = null: chỉ cần trên web có giá trị (vd Ngày cấp do site tự điền).</summary>
    public sealed record ExpectedField(string Label, string? Value, string Source);

    public sealed record ExpectedPassenger(Passenger Passenger, IReadOnlyList<ExpectedField> Fields, string? SourceProblem);

    /// <summary>Kết quả đối chiếu một trường.</summary>
    public sealed record ReviewCheck(Passenger? Passenger, int? CardNumber, string Field, string WebValue,
        string ExpectedValue, string Source, bool Ok);

    public sealed class ReviewReport
    {
        public List<ReviewCheck> Checks { get; } = [];

        public bool AllOk => Checks.Count > 0 && Checks.All(c => c.Ok);
        public int FailedCount => Checks.Count(c => !c.Ok);

        /// <summary>Số hành khách đọc được trên trang web.</summary>
        public int CardCount { get; set; }
    }

    /// <summary>
    /// So từng trường trên trang "Xem lại và nộp" với giá trị mong đợi. Ghép khách theo số hộ chiếu;
    /// khách còn lại (có thể do sai số hộ chiếu) ghép theo thứ tự để chỉ ra đúng trường sai.
    /// </summary>
    public static class ReviewVerifier
    {
        public const string NotOnPage = "(không thấy trên trang)";

        public static ReviewReport Verify(IReadOnlyList<ReviewCard> cards, IReadOnlyList<ExpectedPassenger> expected,
            Func<ReviewCard, ExpectedPassenger, IEnumerable<ExpectedField>>? extraFields = null)
        {
            var report = new ReviewReport { CardCount = cards.Count };
            var freeExpected = expected.ToList();
            var pairs = new List<(ReviewCard Card, ExpectedPassenger? Expected)>();

            foreach (var card in cards)
            {
                var passport = Normalize(card.Fields.GetValueOrDefault(ReviewLabels.PassportNumber, ""));
                var match = freeExpected.FirstOrDefault(e => Normalize(ExpectedValue(e, ReviewLabels.PassportNumber)) == passport && passport != "");
                if (match is not null) freeExpected.Remove(match);
                pairs.Add((card, match));
            }

            for (var i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Expected is not null) continue;
                if (freeExpected.Count == 0) break;
                pairs[i] = (pairs[i].Card, freeExpected[0]);
                freeExpected.RemoveAt(0);
            }

            foreach (var (card, item) in pairs)
            {
                if (item is null)
                {
                    report.Checks.Add(new ReviewCheck(null, card.Number, "Hành khách",
                        card.Fields.GetValueOrDefault(ReviewLabels.FullName, ""), "(không có trong danh sách của app)", "Bảng app", false));
                    continue;
                }

                if (item.SourceProblem is not null)
                    report.Checks.Add(new ReviewCheck(item.Passenger, card.Number, "File nguồn", "", item.SourceProblem, "File nguồn", false));

                var fields = extraFields is null ? item.Fields : item.Fields.Concat(extraFields(card, item));
                foreach (var field in fields)
                {
                    var found = card.Fields.TryGetValue(field.Label, out var web);
                    var webValue = found ? web! : "";
                    var ok = field.Value is null
                        ? found && Normalize(webValue) != ""
                        : found && Normalize(webValue) == Normalize(field.Value);
                    report.Checks.Add(new ReviewCheck(item.Passenger, card.Number, field.Label,
                        found ? webValue : NotOnPage, field.Value ?? "(phải có giá trị)", field.Source, ok));
                }
            }

            // Khách trong danh sách nhưng không có trên trang.
            foreach (var missing in freeExpected)
                report.Checks.Add(new ReviewCheck(missing.Passenger, null, "Hành khách", NotOnPage,
                    missing.Passenger.FullName, "Bảng app", false));

            return report;
        }

        private static string ExpectedValue(ExpectedPassenger e, string label) =>
            e.Fields.FirstOrDefault(f => f.Label == label)?.Value ?? "";

        /// <summary>Chuẩn hoá để so sánh: dấu "—" là trống, gộp khoảng trắng, không phân biệt hoa thường.</summary>
        public static string Normalize(string? value)
        {
            var text = (value ?? "").Normalize(NormalizationForm.FormC).Trim();
            if (text is "—" or "-") return "";
            return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        }
    }
}
