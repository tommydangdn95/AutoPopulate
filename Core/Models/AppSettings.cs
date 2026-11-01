namespace Core.Models
{
    /// <summary>
    /// Cấu hình lưu lại giữa các lần mở app.
    /// </summary>
    public class AppSettings
    {
        public string CompanyEmail { get; set; } = "";
        public string Nationality { get; set; } = NationalityOption.Taiwan.DisplayName;

        public string HomeUrl { get; set; } = "https://prearrival.immigration.gov.vn/home-page";

        /// <summary>Trình duyệt dùng để chạy: "msedge" hoặc "chrome" (đã cài sẵn trên máy).</summary>
        public string BrowserChannel { get; set; } = "chrome";

        /// <summary>Độ trễ (ms) giữa các thao tác trên trình duyệt.</summary>
        public int SlowMoMs { get; set; } = 80;
    }
}
