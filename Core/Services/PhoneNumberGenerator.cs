using System.Text;
using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// Sinh số điện thoại di động ngẫu nhiên 9 chữ số, không có số 0 ở đầu và không gồm mã quốc gia
    /// (site đã có ô mã quốc gia riêng, vd +886).
    /// </summary>
    public static class PhoneNumberGenerator
    {
        public const int Length = 9;

        public static string Generate(NationalityOption nationality, Random random) =>
            nationality.PhoneCountryCode switch
            {
                // Đài Loan: di động 09xx xxx xxx → bỏ số 0 = 9xx xxx xxx.
                "+886" => "9" + Digits(random, Length - 1),
                // Trung Quốc: đầu số di động 1[3-9].
                "+86" => "1" + "3456789"[random.Next(7)] + Digits(random, Length - 2),
                _ => (char)('1' + random.Next(9)) + Digits(random, Length - 1)
            };

        /// <summary>Gán số cho các khách đang để trống (hoặc tất cả nếu regenerateAll). Trả về số khách được gán.</summary>
        public static int Fill(IEnumerable<Passenger> passengers, NationalityOption nationality, Random random,
            bool regenerateAll = false)
        {
            var used = new HashSet<string>();
            var count = 0;
            foreach (var p in passengers)
            {
                if (!regenerateAll && !string.IsNullOrWhiteSpace(p.Phone))
                {
                    used.Add(p.Phone);
                    continue;
                }
                string phone;
                do phone = Generate(nationality, random); while (!used.Add(phone));
                p.Phone = phone;
                count++;
            }
            return count;
        }

        private static string Digits(Random random, int length)
        {
            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
                sb.Append((char)('0' + random.Next(10)));
            return sb.ToString();
        }
    }
}
