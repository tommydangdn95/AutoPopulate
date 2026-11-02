using Core.Abstractions;

namespace App
{
    /// <summary>
    /// Thông báo có phiên bản mới, kèm nội dung thay đổi. Chọn "Cập nhật ngay" thì tải về
    /// (có thanh tiến trình, huỷ được); tải xong đóng lại với DialogResult.OK và PackageFolder.
    /// </summary>
    internal sealed class UpdateForm : Form
    {
        private readonly UpdateInfo _update;
        private readonly IUpdateService _service;
        private readonly Button _updateNow = new() { Text = "Cập nhật ngay", AutoSize = true, MinimumSize = new Size(130, 34) };
        private readonly Button _later = new() { Text = "Để lần sau", AutoSize = true, MinimumSize = new Size(110, 34) };
        private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Visible = false };
        private CancellationTokenSource? _download;

        public string? PackageFolder { get; private set; }

        public UpdateForm(UpdateInfo update, IUpdateService service)
        {
            _update = update;
            _service = service;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Có phiên bản mới";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(620, 420);

            var title = new Label
            {
                AutoSize = true,
                Margin = new Padding(12, 12, 12, 4),
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                Text = $"AutoPopulate {update.Version} đã có (bạn đang dùng {service.CurrentVersion})."
            };
            var notesTitle = new Label { AutoSize = true, Margin = new Padding(12, 4, 12, 4), Text = "Nội dung thay đổi:" };
            var notes = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 0, 12, 8),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = string.IsNullOrWhiteSpace(update.Notes)
                    ? "(Không có mô tả)"
                    : update.Notes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine)
            };

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(12, 0, 12, 12)
            };
            buttons.Controls.Add(_updateNow);
            buttons.Controls.Add(_later);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(notesTitle, 0, 1);
            layout.Controls.Add(notes, 0, 2);
            layout.Controls.Add(_progress, 0, 3);
            layout.Controls.Add(buttons, 0, 4);
            Controls.Add(layout);

            AcceptButton = _updateNow;
            _updateNow.Click += async (_, _) => await DownloadAsync();
            _later.Click += (_, _) =>
            {
                if (_download is not null) _download.Cancel();
                else Close();
            };
            FormClosing += (_, e) =>
            {
                // Đang tải mà bấm X: huỷ tải trước, DownloadAsync sẽ đưa form về trạng thái ban đầu.
                if (_download is null) return;
                _download.Cancel();
                e.Cancel = true;
            };
        }

        private async Task DownloadAsync()
        {
            _download = new CancellationTokenSource();
            _updateNow.Enabled = false;
            _later.Text = "Huỷ";
            _progress.Visible = true;
            _progress.Value = 0;
            try
            {
                var progress = new Progress<int>(p => _progress.Value = Math.Clamp(p, 0, 100));
                PackageFolder = await _service.DownloadAsync(_update, progress, _download.Token);
                _download = null;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException)
            {
                ResetAfterDownload();
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
            {
                ResetAfterDownload();
                MessageBox.Show(this, $"Không tải được bản cập nhật:\n{ex.Message}", "Cập nhật",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ResetAfterDownload()
        {
            _download = null;
            _updateNow.Enabled = true;
            _later.Text = "Để lần sau";
            _progress.Visible = false;
        }
    }
}
