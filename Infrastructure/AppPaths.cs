using System.Runtime.InteropServices;

namespace Infrastructure
{
    /// <summary>
    /// File app tạo ra nằm trong thư mục của app (bản đóng gói chạy dạng portable), trừ file tải về:
    /// <code>
    /// &lt;thư mục app&gt;\
    ///   App.exe
    ///   Data\settings.json      cấu hình đã lưu
    ///   Data\screenshots\       ảnh chụp khi gặp lỗi
    ///   Data\Imports\           file Excel tạo từ các file PDF visa
    ///   Data\ChromeProfile\     profile Chrome riêng của app
    /// C:\Users\&lt;user&gt;\Downloads\   file tải về từ trình duyệt do app mở (thư mục Downloads của Windows)
    /// </code>
    /// </summary>
    public static class AppPaths
    {
        public static string BaseFolder { get; } = AppContext.BaseDirectory;
        public static string DataFolder { get; } = Path.Combine(BaseFolder, "Data");
        public static string SettingsFile { get; } = Path.Combine(DataFolder, "settings.json");
        public static string ScreenshotFolder { get; } = Path.Combine(DataFolder, "screenshots");

        /// <summary>File Excel app tạo ra từ các file PDF visa.</summary>
        public static string ImportsFolder { get; } = Path.Combine(DataFolder, "Imports");

        /// <summary>
        /// File tải về từ trình duyệt do app mở (vd PDF tờ khai, QR): thư mục Downloads của Windows
        /// (mặc định C:\Users\&lt;user&gt;\Downloads; nếu người dùng đã chuyển thư mục Downloads thì theo vị trí mới).
        /// </summary>
        public static string DownloadsFolder { get; } = GetWindowsDownloadsFolder();

        /// <summary>Profile Chrome riêng của app (chế độ thường, không ẩn danh).</summary>
        public static string ChromeProfileFolder { get; } = Path.Combine(DataFolder, "ChromeProfile");

        private static readonly Guid DownloadsKnownFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr path);

        private static string GetWindowsDownloadsFolder()
        {
            if (SHGetKnownFolderPath(DownloadsKnownFolderId, 0, IntPtr.Zero, out var ptr) == 0)
            {
                try
                {
                    if (Marshal.PtrToStringUni(ptr) is { Length: > 0 } path)
                        return path;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
