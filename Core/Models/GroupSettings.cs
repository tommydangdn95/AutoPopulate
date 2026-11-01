namespace Core.Models
{
    /// <summary>
    /// Thiết lập dùng chung cho cả đoàn (một tờ khai).
    /// </summary>
    public class GroupSettings
    {
        public NationalityOption Nationality { get; set; } = NationalityOption.Taiwan;
        public DateTime ArrivalDate { get; set; } = DateTime.Today;
        public string Email { get; set; } = "";
    }
}
