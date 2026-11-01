using Core.Models;

namespace Core.Abstractions
{
    public sealed class PassengerReadResult
    {
        public List<Passenger> Passengers { get; } = [];

        /// <summary>Lỗi cấp file: không tìm thấy sheet, thiếu cột bắt buộc...</summary>
        public List<string> Errors { get; } = [];
    }

    public interface IPassengerReader
    {
        PassengerReadResult Read(string path);
    }

    public interface ITemplateWriter
    {
        void Write(string path);
    }

    /// <summary>Ghi danh sách khách ra file Excel theo mẫu.</summary>
    public interface IPassengerWriter
    {
        void Write(string path, IReadOnlyList<Passenger> passengers);
    }

    /// <summary>Kết quả đọc một file PDF thị thực điện tử.</summary>
    /// <param name="Problems">Các trường không đọc được (rỗng = đọc đủ).</param>
    public sealed record VisaPdfResult(string FilePath, Passenger? Passenger, IReadOnlyList<string> Problems);

    public interface IVisaPdfExtractor
    {
        VisaPdfResult Extract(string path);
    }

    public interface ISettingsStore
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }

    /// <summary>Lỗi hiển thị trên form của site.</summary>
    public sealed record SiteFieldError(string Field, string Message);

    public enum UserDecision
    {
        /// <summary>Người dùng đã sửa trên trình duyệt, kiểm tra lại rồi đi tiếp.</summary>
        Continue,
        /// <summary>Người dùng đã sửa dữ liệu trong app, điền lại khách này.</summary>
        Refill,
        /// <summary>Bỏ qua khách này, dùng chỗ trống cho khách kế tiếp.</summary>
        Skip,
        /// <summary>Dừng hẳn, giữ nguyên trình duyệt.</summary>
        Stop
    }

    public sealed record AttentionRequest(Passenger Passenger, string Title, IReadOnlyList<SiteFieldError> Errors,
        bool AllowSkip = true);

    /// <summary>
    /// Giao diện mà automation dùng để báo trạng thái và hỏi ý người dùng.
    /// </summary>
    public interface IAutomationHost
    {
        void Log(string message);

        /// <summary>Báo trạng thái; requiresUser = true khi cần người dùng thao tác (CAPTCHA, kiểm tra...).</summary>
        void ReportStatus(string message, bool requiresUser = false);

        void PassengerChanged(Passenger passenger);

        /// <summary>Trình duyệt vừa tải về một file (đã lưu bằng tên gốc tại path).</summary>
        void FileDownloaded(string path);

        Task<UserDecision> AskUserAsync(AttentionRequest request, CancellationToken cancellationToken);
    }

    public interface IDeclarationAutomation : IAsyncDisposable
    {
        Task RunAsync(GroupSettings group, IReadOnlyList<Passenger> passengers, IAutomationHost host,
            CancellationToken cancellationToken);
    }
}
