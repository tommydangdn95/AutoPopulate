using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// Gán giới tính ngẫu nhiên (Nam/Nữ) cho khách chưa có giới tính – người dùng không cần nhập.
    /// </summary>
    public static class GenderGenerator
    {
        /// <summary>Trả về số khách được gán.</summary>
        public static int FillMissing(IEnumerable<Passenger> passengers, Random random)
        {
            var count = 0;
            foreach (var p in passengers.Where(p => p.Gender == Gender.Unknown))
            {
                p.Gender = random.Next(2) == 0 ? Gender.Male : Gender.Female;
                count++;
            }
            return count;
        }
    }
}
