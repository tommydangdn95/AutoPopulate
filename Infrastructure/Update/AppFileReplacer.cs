namespace Infrastructure.Update
{
    /// <summary>
    /// Thay file của app đang chạy. Windows không cho ghi đè file exe đang chạy nhưng cho đổi tên:
    /// đổi file/thư mục cũ thành "*.old", chuyển bản mới vào, lỗi giữa chừng thì khôi phục.
    /// Thư mục Data (cấu hình, dữ liệu người dùng) không bao giờ bị đụng tới.
    /// </summary>
    public static class AppFileReplacer
    {
        private const string OldSuffix = ".old";
        private const string DataFolderName = "Data";

        public static void Replace(string packageFolder, string appFolder)
        {
            var placed = new List<string>();
            var renamed = new List<(string Current, string Old)>();
            try
            {
                foreach (var source in Directory.GetFileSystemEntries(packageFolder))
                {
                    var name = Path.GetFileName(source);
                    if (name.Equals(DataFolderName, StringComparison.OrdinalIgnoreCase)) continue;

                    var target = Path.Combine(appFolder, name);
                    var old = target + OldSuffix;
                    DeleteEntry(old);
                    if (EntryExists(target))
                    {
                        MoveEntry(target, old);
                        renamed.Add((target, old));
                    }
                    MoveEntry(source, target);
                    placed.Add(target);
                }
            }
            catch
            {
                foreach (var target in placed)
                    TryDelete(target);
                foreach (var (current, old) in renamed)
                {
                    TryDelete(current);
                    MoveEntry(old, current);
                }
                throw;
            }
        }

        /// <summary>Xoá các "*.old" của lần cập nhật trước (bản cũ có thể chưa thoát hẳn nên thử lại vài lần).</summary>
        public static async Task CleanupAsync(string appFolder, string updatesFolder)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var pending = Directory.GetFileSystemEntries(appFolder, "*" + OldSuffix).ToList();
                if (Directory.Exists(updatesFolder)) pending.Add(updatesFolder);
                if (pending.Count == 0) return;

                foreach (var entry in pending)
                    TryDelete(entry);
                await Task.Delay(500);
            }
        }

        private static bool EntryExists(string path) => File.Exists(path) || Directory.Exists(path);

        private static void MoveEntry(string source, string target)
        {
            // Tiến trình vừa đóng (driver Playwright) có thể còn giữ file trong giây lát.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    if (Directory.Exists(source)) Directory.Move(source, target);
                    else File.Move(source, target);
                    return;
                }
                catch (Exception ex) when (attempt < 10 && ex is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private static void DeleteEntry(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }

        private static void TryDelete(string path)
        {
            try { DeleteEntry(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* thử lại lần sau */ }
        }
    }
}
