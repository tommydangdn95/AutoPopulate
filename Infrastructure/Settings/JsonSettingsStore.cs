using System.Text.Json;
using Core.Abstractions;
using Core.Models;

namespace Infrastructure.Settings
{
    /// <summary>
    /// Lưu cấu hình vào &lt;thư mục app&gt;\Data\settings.json.
    /// </summary>
    public class JsonSettingsStore : ISettingsStore
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        /// <summary>Vị trí cũ (trước khi chuyển sang lưu trong thư mục app), chỉ dùng để chuyển dữ liệu một lần.</summary>
        private static readonly string LegacyFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPopulate", "settings.json");

        public AppSettings Load()
        {
            var path = File.Exists(AppPaths.SettingsFile) ? AppPaths.SettingsFile
                : File.Exists(LegacyFile) ? LegacyFile
                : null;
            if (path is null) return new AppSettings();

            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings();
                if (path == LegacyFile) Save(settings);
                return settings;
            }
            catch (JsonException)
            {
                // File hỏng: dùng cấu hình mặc định, sẽ ghi đè khi lưu.
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, Options));
        }
    }
}
