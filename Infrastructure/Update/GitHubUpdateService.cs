using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Core.Abstractions;

namespace Infrastructure.Update
{
    /// <summary>
    /// Cập nhật từ GitHub Releases. Mỗi bản phát hành có 2 file:
    /// AutoPopulate-vX.Y.Z-win-x64.zip và AutoPopulate-vX.Y.Z-win-x64.zip.sha256.
    /// </summary>
    public sealed class GitHubUpdateService(string feedUrl) : IUpdateService
    {
        private const string ZipSuffix = "-win-x64.zip";
        private const string Sha256Suffix = ".sha256";
        private const string ExeName = "AutoPopulate.exe";

        private static readonly HttpClient Http = CreateClient();

        private static string UpdatesFolder => Path.Combine(AppPaths.DataFolder, "Updates");

        public Version CurrentVersion { get; } = Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

        /// <summary>Bản đóng gói gộp thành một file exe, không có AutoPopulate.dll cạnh exe như khi chạy từ Visual Studio.</summary>
        public bool IsSupported =>
            File.Exists(Path.Combine(AppPaths.BaseFolder, ExeName)) &&
            !File.Exists(Path.Combine(AppPaths.BaseFolder, "AutoPopulate.dll"));

        public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await Http.GetAsync(feedUrl, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = json.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed)) return null;

            var version = Normalize(parsed);
            if (version <= CurrentVersion) return null;

            string? zipUrl = null, shaUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                var url = asset.GetProperty("browser_download_url").GetString();
                if (name.EndsWith(ZipSuffix, StringComparison.OrdinalIgnoreCase)) zipUrl = url;
                else if (name.EndsWith(ZipSuffix + Sha256Suffix, StringComparison.OrdinalIgnoreCase)) shaUrl = url;
            }
            // Thiếu file kiểm tra thì không cập nhật: không xác nhận được file tải về còn nguyên vẹn.
            if (zipUrl is null || shaUrl is null) return null;

            var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
            return new UpdateInfo(version, notes, zipUrl, shaUrl);
        }

        public async Task<string> DownloadAsync(UpdateInfo update, IProgress<int> percent, CancellationToken cancellationToken)
        {
            if (Directory.Exists(UpdatesFolder)) Directory.Delete(UpdatesFolder, recursive: true);
            Directory.CreateDirectory(UpdatesFolder);

            var shaText = await Http.GetStringAsync(update.Sha256Url, cancellationToken);
            var expected = shaText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

            var zipPath = Path.Combine(UpdatesFolder, "update.zip");
            using (var response = await Http.GetAsync(update.ZipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? 0;
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(zipPath);

                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    if (total > 0) percent.Report((int)(received * 100 / total));
                }
            }

            string actual;
            await using (var file = File.OpenRead(zipPath))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("File tải về bị lỗi (mã kiểm tra SHA256 không khớp). Vui lòng thử lại.");

            var extracted = Path.Combine(UpdatesFolder, "package");
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extracted), cancellationToken);

            // Zip chứa thư mục gốc AutoPopulate\.
            var packageFolder = Directory.GetFiles(extracted, ExeName, SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .FirstOrDefault();
            return packageFolder ?? throw new InvalidDataException($"Gói cập nhật không có {ExeName}.");
        }

        public void ApplyAndRestart(string packageFolder)
        {
            AppFileReplacer.Replace(packageFolder, AppPaths.BaseFolder);
            Process.Start(new ProcessStartInfo(Path.Combine(AppPaths.BaseFolder, ExeName))
            {
                UseShellExecute = true,
                WorkingDirectory = AppPaths.BaseFolder
            });
        }

        public Task CleanupAsync() => AppFileReplacer.CleanupAsync(AppPaths.BaseFolder, UpdatesFolder);

        private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AutoPopulate-Updater");
            return client;
        }
    }
}
