using Core.Review;

namespace App
{
    /// <summary>
    /// Kết quả đối chiếu trang "Xem lại và nộp" với file nguồn. Cửa sổ không chặn app,
    /// để người dùng vừa xem kết quả vừa sửa trên Chrome. Bấm đúp một dòng để mở file nguồn của khách.
    /// </summary>
    internal sealed class ReviewResultForm : Form
    {
        private readonly ReviewReport _report;
        private readonly Action<string> _openSource;
        private readonly DataGridView _grid = new();
        private readonly CheckBox _onlyFailed = new();

        public ReviewResultForm(ReviewReport report, int passengerCount, Action<string> openSource)
        {
            _report = report;
            _openSource = openSource;

            // Kích thước tính theo DPI 96, tự co giãn trên màn hình độ phân giải cao.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Đối chiếu trang \"Xem lại và nộp\" với file nguồn";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1150, 620);
            TopMost = true;

            var failedPassengers = report.Checks.Where(c => !c.Ok).Select(c => c.Passenger).Distinct().Count();
            var summary = new Label
            {
                AutoSize = true,
                Margin = new Padding(10, 10, 10, 6),
                Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
                ForeColor = report.AllOk ? Color.DarkGreen : Color.Firebrick,
                Text = report.CardCount == 0
                    ? "✖ KHÔNG ĐỌC ĐƯỢC danh sách khách trên trang: site có thể đã đổi giao diện.\nApp chưa đối chiếu được. Hãy tự kiểm tra kỹ từng khách với file nguồn trước khi nộp."
                    : report.AllOk
                    ?$"✔ KHỚP: {report.CardCount} hành khách trên trang, {report.Checks.Count} trường đều đúng với nguồn. Có thể nộp."
                    : $"✖ CÓ SAI LỆCH: {report.FailedCount} trường, {failedPassengers} hành khách (trang có {report.CardCount} khách, danh sách có {passengerCount} khách).\n" +
                      "Sửa trên Chrome rồi quay lại trang Xem lại để đối chiếu lại. ĐỪNG nộp khi còn sai."
            };

            _onlyFailed.Text = "Chỉ hiện các trường sai";
            _onlyFailed.AutoSize = true;
            _onlyFailed.Margin = new Padding(10, 0, 10, 6);
            _onlyFailed.Checked = !report.AllOk;
            _onlyFailed.CheckedChanged += (_, _) => FillGrid();

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            _grid.Columns.Add("Passenger", "Khách");
            _grid.Columns.Add("Field", "Trường");
            _grid.Columns.Add("Web", "Trên trang web");
            _grid.Columns.Add("Expected", "Trong nguồn");
            _grid.Columns.Add("Result", "Kết quả");
            _grid.Columns.Add("Source", "Nguồn");
            _grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].Tag is ReviewCheck { Passenger.SourcePath: { Length: > 0 } path })
                    _openSource(path);
            };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(summary, 0, 0);
            layout.Controls.Add(_onlyFailed, 0, 1);
            layout.Controls.Add(_grid, 0, 2);
            Controls.Add(layout);

            // Dòng tóm tắt tự xuống dòng theo độ rộng cửa sổ.
            void FitSummary() => summary.MaximumSize = new Size(Math.Max(100, ClientSize.Width - 30), 0);
            Resize += (_, _) => FitSummary();
            Shown += (_, _) => FitSummary();
            FillGrid();
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            foreach (var check in _report.Checks.Where(c => !_onlyFailed.Checked || !c.Ok))
            {
                var who = check.Passenger is { } p
                    ? $"STT {p.No} – {p.FullName}"
                    : $"#{check.CardNumber} trên web";
                var index = _grid.Rows.Add(who, check.Field, check.WebValue, check.ExpectedValue, check.Ok ? "✔ Đúng" : "✖ SAI", check.Source);
                var row = _grid.Rows[index];
                row.Tag = check;
                row.DefaultCellStyle.BackColor = check.Ok ? Color.Honeydew : Color.MistyRose;
                if (!check.Ok) row.DefaultCellStyle.ForeColor = Color.Firebrick;
            }
        }
    }
}
