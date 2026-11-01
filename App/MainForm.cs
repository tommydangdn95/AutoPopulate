using System.ComponentModel;
using System.Globalization;
using System.Media;
using Core.Abstractions;
using Core.Models;
using Core.Services;
using Core.Validation;
using Infrastructure;
using Infrastructure.Automation;

namespace App
{
    public partial class MainForm : Form, IAutomationHost
    {
        /// <summary>Số ngày (tính từ hôm nay) hiển thị trong ô "Ngày đến dự kiến".</summary>
        private const int ArrivalDayOptions = 4;

        private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "ddMMyyyy"];

        private readonly ISettingsStore _settingsStore;
        private readonly IPassengerReader _reader;
        private readonly IVisaPdfExtractor _pdfExtractor;
        private readonly IPassengerWriter _passengerWriter;
        private readonly AppSettings _settings;
        private readonly Random _random = new();

        private BindingList<Passenger> _passengers = [];
        private IDeclarationAutomation? _automation;
        private CancellationTokenSource? _cts;
        private TaskCompletionSource<UserDecision>? _pendingDecision;
        private Passenger? _pendingPassenger;
        private bool _running;
        private bool _closing;
        private bool _initialized;
        /// <summary>Kết quả kiểm tra gần nhất; nút "Bắt đầu khai" chỉ bật khi true.</summary>
        private bool _dataValid;

        /// <summary>Nguồn của danh sách khách đang hiển thị.</summary>
        private enum DataOrigin { None, Excel, Pdf }
        private DataOrigin _origin = DataOrigin.None;

        /// <summary>Số/ngày hết hạn thị thực đọc từ file – để trả lại khi người dùng xoá ô nhập tay.</summary>
        private Dictionary<Passenger, (string Number, DateTime? Expiry)> _originalVisa = [];

        private sealed record ArrivalOption(DateTime Date)
        {
            public override string ToString()
            {
                var offset = (Date - DateTime.Today).Days;
                var suffix = offset switch { 0 => "Hôm nay", 1 => "Ngày mai", _ => $"+{offset} ngày" };
                return $"{Date:dd/MM/yyyy} ({suffix})";
            }
        }

        public MainForm(ISettingsStore settingsStore, IPassengerReader reader,
            IVisaPdfExtractor pdfExtractor, IPassengerWriter passengerWriter)
        {
            _settingsStore = settingsStore;
            _reader = reader;
            _pdfExtractor = pdfExtractor;
            _passengerWriter = passengerWriter;
            _settings = settingsStore.Load();

            InitializeComponent();
            SetupGridColumns();
            LoadSettingsToUi();
            SetupStartButtonStyle();
            _initialized = true;
            RunValidation(showSummary: false);
        }

        // ------------------------------------------------------------------ khởi tạo

        private readonly DataGridViewTextBoxColumn colPhone = new()
        {
            DataPropertyName = nameof(Passenger.Phone),
            HeaderText = "SĐT (tự sinh)",
            Name = "colPhone",
            MaxInputLength = 14
        };

        private void SetupGridColumns()
        {
            colGender.DataSource = Catalog.Genders.ToList();
            colGender.ValueMember = "Key";
            colGender.DisplayMember = "Value";
            colPassportType.DataSource = new[] { "" }.Concat(Catalog.PassportTypes).ToList();
            colVisaType.DataSource = new[] { "" }.Concat(Catalog.VisaTypes).ToList();
            // Cột ngày: khi sửa hiện lịch để chọn (Delete/Backspace để xoá ngày không bắt buộc).
            foreach (var column in new[] { colDob, colPassportExpiry, colVisaIssueDate, colVisaValidFrom, colVisaExpiry })
                column.CellTemplate = new CalendarCell();
            gridPassengers.DataSource = _passengers;
            gridPassengers.CellMouseDown += gridPassengers_CellMouseDown;
            gridPassengers.EditingControlShowing += gridPassengers_EditingControlShowing;
            SetupDeleteShortcuts();
            // SĐT do app tự sinh cho từng khách (không có trong Excel), vẫn sửa tay được.
            gridPassengers.Columns.Insert(colMessage.Index, colPhone);

            // Kiểu Standard vẽ theo theme Windows và bỏ qua BackColor của ô, nên ô lỗi không
            // hiện màu đỏ. Kiểu Flat tôn trọng BackColor.
            foreach (var column in new[] { colGender, colPassportType, colVisaType })
                column.FlatStyle = FlatStyle.Flat;
        }

        /// <summary>
        /// Mặc định ô ComboBox của DataGridView cần bấm 2 lần (lần 1 vào chế độ sửa, lần 2 mới xổ).
        /// Bấm vào ô dropdown thì vào chế độ sửa và xổ danh sách ngay.
        /// </summary>
        private void gridPassengers_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var cell = gridPassengers[e.ColumnIndex, e.RowIndex];

            // Ô ngày: bấm một lần là vào chế độ chọn ngày.
            if (cell is CalendarCell && !cell.ReadOnly)
            {
                if (gridPassengers.CurrentCell != cell)
                    gridPassengers.CurrentCell = cell;
                BeginInvoke(() =>
                {
                    if (gridPassengers.CurrentCell == cell)
                        gridPassengers.BeginEdit(false);
                });
                return;
            }

            if (gridPassengers.Columns[e.ColumnIndex] is not DataGridViewComboBoxColumn { ReadOnly: false }) return;

            if (gridPassengers.CurrentCell != cell)
                gridPassengers.CurrentCell = cell;
            BeginInvoke(() =>
            {
                if (gridPassengers.CurrentCell == cell && gridPassengers.BeginEdit(false) &&
                    gridPassengers.EditingControl is ComboBox combo)
                    combo.DroppedDown = true;
            });
        }

        private void gridPassengers_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is not ComboBox combo) return;
            // Danh sách xổ đủ rộng để đọc hết nhãn dài (vd "Miễn thị thực ... Phú Quốc").
            var widest = combo.Items.Cast<object>()
                .Select(i => TextRenderer.MeasureText(combo.GetItemText(i), combo.Font).Width)
                .DefaultIfEmpty(0).Max();
            combo.DropDownWidth = Math.Max(combo.Width, widest + SystemInformation.VerticalScrollBarWidth + 8);
            combo.MaxDropDownItems = 12;
            // Chọn xong thì ghi giá trị ngay, không cần rời ô.
            combo.SelectionChangeCommitted -= Combo_SelectionChangeCommitted;
            combo.SelectionChangeCommitted += Combo_SelectionChangeCommitted;
        }

        private void Combo_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            // Trong SelectionChangeCommitted, ComboBox.Text (giá trị DataGridView đọc khi commit) vẫn là
            // lựa chọn cũ → commit ngay sẽ ghi giá trị cũ và mất lựa chọn lần đầu. Hoãn đến khi ComboBox
            // cập nhật xong rồi mới ghi.
            BeginInvoke(() =>
            {
                if (!gridPassengers.IsCurrentCellInEditMode) return;
                gridPassengers.CommitEdit(DataGridViewDataErrorContexts.Commit);
                gridPassengers.EndEdit();
            });
        }

        /// <summary>
        /// Nút "Bắt đầu khai" có màu nền riêng nên WinForms không tự làm xám khi disable;
        /// đổi màu theo trạng thái Enabled để người dùng biết không bấm được.
        /// </summary>
        private void SetupStartButtonStyle()
        {
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.EnabledChanged += (_, _) => ApplyStartButtonStyle();
            ApplyStartButtonStyle();
        }

        private void ApplyStartButtonStyle()
        {
            btnStart.BackColor = btnStart.Enabled ? Color.FromArgb(31, 56, 100) : Color.FromArgb(200, 200, 200);
            btnStart.ForeColor = btnStart.Enabled ? Color.White : Color.FromArgb(120, 120, 120);
            btnStart.Cursor = btnStart.Enabled ? Cursors.Hand : Cursors.No;
        }

        private void LoadSettingsToUi()
        {
            txtEmail.Text = _settings.CompanyEmail;

            cboNationality.Items.AddRange(NationalityOption.All.ToArray<object>());
            cboNationality.SelectedItem = NationalityOption.All.FirstOrDefault(n => n.DisplayName == _settings.Nationality)
                                          ?? NationalityOption.Taiwan;

            ResetArrivalOptions();
            // Mở app là bắt đầu mới: không tự nạp lại file của lần trước (bảng khách trống).
        }

        private void ResetArrivalOptions()
        {
            cboArrival.Items.Clear();
            for (var i = 0; i < ArrivalDayOptions; i++)
                cboArrival.Items.Add(new ArrivalOption(DateTime.Today.AddDays(i)));
            cboArrival.SelectedIndex = 0;
        }

        private void SaveSettings()
        {
            _settings.CompanyEmail = txtEmail.Text.Trim();
            _settings.Nationality = SelectedNationality.DisplayName;
            _settingsStore.Save(_settings);
        }

        private NationalityOption SelectedNationality =>
            cboNationality.SelectedItem as NationalityOption ?? NationalityOption.Taiwan;

        private GroupSettings BuildGroup() => new()
        {
            Nationality = SelectedNationality,
            ArrivalDate = (cboArrival.SelectedItem as ArrivalOption)?.Date ?? DateTime.Today,
            Email = txtEmail.Text.Trim()
        };

        // ------------------------------------------------------------------ Excel

        private void LoadExcel(string path, DataOrigin origin)
        {
            PassengerReadResult result;
            try
            {
                result = _reader.Read(path);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"Không đọc được file Excel (file có đang mở trong Excel?):\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            txtExcel.Text = path;
            PhoneNumberGenerator.Fill(result.Passengers, SelectedNationality, _random);
            GenderGenerator.FillMissing(result.Passengers, _random);
            _passengers = new BindingList<Passenger>(result.Passengers);
            gridPassengers.DataSource = _passengers;
            _origin = origin;
            _originalVisa = _passengers.ToDictionary(p => p, p => (p.VisaNumber, p.VisaExpiry));
            ResetManualVisaInputs();
            ApplySearchFilter();

            if (result.Errors.Count > 0)
            {
                foreach (var error in result.Errors) Log(error);
                MessageBox.Show(this, string.Join("\n", result.Errors), "File Excel không hợp lệ",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Log($"Đã nạp {_passengers.Count} khách từ {Path.GetFileName(path)}.");
            RunValidation(showSummary: true);
        }

        /// <summary>Số file PDF tối đa chọn trong một lần.</summary>
        private const int MaxPdfFiles = 100;

        private async void btnBrowse_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "File visa PDF (*.pdf)|*.pdf",
                Multiselect = true,
                Title = $"Chọn file PDF visa (tối đa {MaxPdfFiles} file)"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var pdfs = dialog.FileNames.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
            if (pdfs.Count == 0) return;
            if (pdfs.Count > MaxPdfFiles)
            {
                MessageBox.Show(this, $"Chỉ chọn tối đa {MaxPdfFiles} file PDF mỗi lần (đang chọn {pdfs.Count}).",
                    "Quá nhiều file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await ImportPdfsAsync(pdfs);
        }

        /// <summary>
        /// Đọc các file PDF visa → ghi ra file Excel trong Data\Imports → nạp file Excel đó vào bảng.
        /// </summary>
        private async Task ImportPdfsAsync(IReadOnlyList<string> pdfs)
        {
            UseWaitCursor = true;
            grpSettings.Enabled = false;
            SetStatus($"Đang đọc {pdfs.Count} file PDF...", Color.Navy);
            Log($"Đọc {pdfs.Count} file PDF visa...");

            try
            {
                // Thứ tự khách = "Date modified" giảm dần (mới nhất → cũ nhất), như sắp xếp trong Windows Explorer.
                // Không dùng ngày tạo: file giải nén từ zip/chép sang thư mục khác có ngày tạo gần như trùng nhau.
                pdfs = pdfs.OrderByDescending(File.GetLastWriteTime).ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).ToList();
                ShowImportProgress(0, pdfs.Count);
                var progress = new Progress<int>(done => ShowImportProgress(done, pdfs.Count));
                var results = await Task.Run(() =>
                {
                    var list = new List<VisaPdfResult>(pdfs.Count);
                    foreach (var pdf in pdfs)
                    {
                        list.Add(_pdfExtractor.Extract(pdf));
                        ((IProgress<int>)progress).Report(list.Count);
                    }
                    return list;
                });

                foreach (var r in results.Where(r => r.Problems.Count > 0))
                    Log($"⚠ {Path.GetFileName(r.FilePath)}: " +
                        (r.Passenger is null ? string.Join("; ", r.Problems) : "không đọc được " + string.Join(", ", r.Problems)));

                var passengers = results.Where(r => r.Passenger is not null).Select(r => r.Passenger!).ToList();
                var unreadable = results.Count - passengers.Count;
                if (passengers.Count == 0)
                {
                    SetStatus("Không đọc được dữ liệu từ file PDF nào (xem khung log).", Color.Firebrick);
                    MessageBox.Show(this, "Không đọc được dữ liệu từ file PDF nào. Xem chi tiết trong khung log.",
                        "Đọc PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var excelPath = Path.Combine(AppPaths.ImportsFolder, $"DanhSachKhach_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                await Task.Run(() => _passengerWriter.Write(excelPath, passengers));
                Log($"Đã tạo file Excel từ {passengers.Count} file PDF: {excelPath}");

                LoadExcel(excelPath, DataOrigin.Pdf);
                SaveSettings();

                var partial = results.Count(r => r.Passenger is not null && r.Problems.Count > 0);
                if (unreadable > 0 || partial > 0)
                    MessageBox.Show(this,
                        $"Đã nhập {passengers.Count}/{results.Count} file PDF." +
                        (partial > 0 ? $"\n• {partial} file thiếu một số trường: ô đỏ trong bảng – sửa trực tiếp trong bảng, hoặc kiểm tra lại file PDF (tên file ở cột Ghi chú)." : "") +
                        (unreadable > 0 ? $"\n• {unreadable} file không đọc được (xem khung log)." : ""),
                        "Đọc PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, $"Không ghi được file Excel:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                grpSettings.Enabled = true;
                UseWaitCursor = false;
                progressImport.Visible = false;
                lblImport.Visible = false;
            }
        }

        private void ShowImportProgress(int done, int total)
        {
            var percent = total == 0 ? 100 : done * 100 / total;
            progressImport.Maximum = 100;
            progressImport.Value = percent;
            progressImport.Visible = true;
            lblImport.Text = $"Đang đọc file PDF: {done}/{total} ({percent}%)";
            lblImport.Visible = true;
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            if (File.Exists(txtExcel.Text))
                LoadExcel(txtExcel.Text, _origin == DataOrigin.None ? DataOrigin.Excel : _origin);
            else
                btnBrowse_Click(sender, e);
        }

        /// <summary>
        /// Chọn 1 file Excel danh sách khách: mẫu xin thị thực (sheet INF_NHAN_SU) hoặc file app đã tạo từ PDF.
        /// </summary>
        private void btnBrowseExcel_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "File Excel danh sách khách (*.xlsx)|*.xlsx",
                Title = "Chọn file Excel danh sách khách",
                InitialDirectory = File.Exists(txtExcel.Text) ? Path.GetDirectoryName(txtExcel.Text) : AppPaths.DownloadsFolder
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            LoadExcel(dialog.FileName, DataOrigin.Excel);
            SaveSettings();
        }

        // ------------------------------------------------------------------ kiểm tra dữ liệu

        /// <summary>
        /// Kiểm tra lại toàn bộ dữ liệu, cập nhật màu ô, dòng trạng thái và nút "Bắt đầu khai".
        /// </summary>
        /// <param name="showSummary">true: ghi chi tiết từng lỗi ra khung log.</param>
        /// <returns>true nếu không còn lỗi.</returns>
        private bool RunValidation(bool showSummary)
        {
            gridPassengers.EndEdit();
            var active = _passengers.ToList();
            var groupErrors = PassengerValidator.Validate(active, BuildGroup(), DateTime.Today);
            gridPassengers.Refresh();

            var invalid = active.Where(p => p.HasErrors).ToList();
            var warnings = active.Count(p => p.Issues.Any(i => i.IsWarning));
            _dataValid = groupErrors.Count == 0 && invalid.Count == 0;
            btnStart.Enabled = _dataValid && !_running;

            if (showSummary)
            {
                foreach (var error in groupErrors) Log("⚠ " + error);
                foreach (var p in invalid)
                    Log($"⚠ STT {p.No} ({p.FullName}){p.SourceSuffix}: {string.Join("; ", p.Issues.Where(i => !i.IsWarning).Select(i => i.Message))}");
            }

            // Khi đang chạy, dòng trạng thái dành cho tiến trình điền form.
            if (!_running)
            {
                var errors = new List<string>(groupErrors);
                if (invalid.Count > 0)
                    errors.Add($"{invalid.Count} khách có lỗi (ô màu đỏ – rê chuột để xem chi tiết).");
                var summary = _dataValid
                    ? $"✔ Dữ liệu hợp lệ: sẽ khai {active.Count} khách. Có thể bấm \"Bắt đầu khai\"."
                    : "Chưa thể bắt đầu khai:\n" + string.Join("  ", errors.Select(e => "• " + e));
                if (warnings > 0) summary += $"  ({warnings} khách có cảnh báo – ô màu vàng.)";
                SetStatus(summary, _dataValid ? Color.DarkGreen : Color.Firebrick);
            }

            return _dataValid;
        }

        private void groupInput_Changed(object? sender, EventArgs e)
        {
            if (!_initialized || _running) return;

            // Đổi quốc tịch thì sinh lại SĐT theo đầu số của quốc tịch mới.
            if (sender == cboNationality && _passengers.Count > 0)
            {
                PhoneNumberGenerator.Fill(_passengers, SelectedNationality, _random, regenerateAll: true);
                Log($"Đã sinh lại SĐT cho {_passengers.Count} khách theo quốc tịch {SelectedNationality.DisplayName}.");
            }
            RunValidation(showSummary: false);
        }

        private void btnValidate_Click(object sender, EventArgs e) => RunValidation(showSummary: true);

        // ------------------------------------------------------------------ xoá khách

        /// <summary>Phím Delete và menu chuột phải "Xoá khách" trên bảng.</summary>
        private void SetupDeleteShortcuts()
        {
            var menu = new ContextMenuStrip();
            var deleteItem = new ToolStripMenuItem("🗑 Xoá khách", null, (_, _) => DeleteSelectedPassengers())
            {
                ShortcutKeyDisplayString = "Delete"
            };
            menu.Items.Add(deleteItem);
            menu.Opening += (_, e) => e.Cancel = _running || _passengers.Count == 0;
            gridPassengers.ContextMenuStrip = menu;

            // Chuột phải vào dòng chưa chọn → chọn dòng đó trước khi mở menu.
            gridPassengers.CellMouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
                if (!gridPassengers.Rows[e.RowIndex].Selected && !gridPassengers[Math.Max(e.ColumnIndex, 0), e.RowIndex].Selected)
                {
                    gridPassengers.ClearSelection();
                    gridPassengers.CurrentCell = gridPassengers[Math.Max(e.ColumnIndex, 0), e.RowIndex];
                }
            };

            gridPassengers.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Delete || gridPassengers.IsCurrentCellInEditMode) return;
                DeleteSelectedPassengers();
                e.Handled = true;
            };
        }

        private void btnDelete_Click(object? sender, EventArgs e) => DeleteSelectedPassengers();

        /// <summary>
        /// Xoá các khách đang chọn khỏi bảng (chỉ trong app – file Excel/PDF không đổi), đánh lại STT và kiểm tra lại.
        /// </summary>
        private void DeleteSelectedPassengers()
        {
            if (_running) return;
            if (gridPassengers.IsCurrentCellInEditMode)
                gridPassengers.EndEdit();

            var indexes = gridPassengers.SelectedCells.Cast<DataGridViewCell>().Select(c => c.RowIndex)
                .Concat(gridPassengers.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index))
                .Where(i => i >= 0 && i < _passengers.Count && gridPassengers.Rows[i].Visible)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (indexes.Count == 0)
            {
                MessageBox.Show(this, "Chọn dòng khách cần xoá trong bảng trước.", "Xoá khách",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = indexes.Select(i => _passengers[i]).ToList();
            var lines = selected.Take(10).Select(p => $"• STT {p.No} – {p.FullName}{p.SourceSuffix}").ToList();
            if (selected.Count > 10) lines.Add($"… và {selected.Count - 10} khách khác");
            var answer = MessageBox.Show(this,
                $"Xoá {selected.Count} khách khỏi danh sách?\n\n{string.Join("\n", lines)}\n\n" +
                "(Chỉ xoá trong app – file Excel/PDF không thay đổi. Bấm \"Làm mới\" sẽ nạp lại từ file Excel.)",
                "Xoá khách", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            gridPassengers.CurrentCell = null;
            foreach (var i in indexes.OrderByDescending(i => i))
                _passengers.RemoveAt(i);
            for (var i = 0; i < _passengers.Count; i++)
                _passengers[i].No = i + 1;
            _passengers.ResetBindings();

            Log($"Đã xoá {selected.Count} khách: {string.Join(", ", selected.Select(p => p.FullName))}. Còn {_passengers.Count} khách.");
            ApplyManualVisaInfo(); // STT thay đổi → đánh lại số thị thực theo thứ tự mới
            ApplySearchFilter();
            RunValidation(showSummary: false);
        }

        // ------------------------------------------------------------------ số / hạn thị thực nhập tay (file Excel)

        /// <summary>Hai ô Số thị thực / Hết hạn TT chỉ dùng được khi danh sách nạp từ file Excel.</summary>
        private void UpdateManualVisaControls()
        {
            var enabled = _origin == DataOrigin.Excel && !_running;
            txtVisaNumber.Enabled = enabled;
            dtpVisaExpiry.Enabled = enabled;
        }

        /// <summary>Nạp danh sách mới: xoá hai ô nhập tay (không áp số của đoàn trước sang đoàn mới).</summary>
        private void ResetManualVisaInputs()
        {
            _suppressManualVisa = true;
            txtVisaNumber.Clear();
            dtpVisaExpiry.Value = DateTime.Today;
            dtpVisaExpiry.Checked = false;
            _suppressManualVisa = false;
            UpdateManualVisaControls();
        }

        private bool _suppressManualVisa;

        private void manualVisa_Changed(object? sender, EventArgs e)
        {
            if (_suppressManualVisa || !_initialized || _running) return;
            ApplyManualVisaInfo();
            ApplySearchFilter();
            RunValidation(showSummary: false);
        }

        /// <summary>
        /// Áp số thị thực (theo quy tắc VisaNumberSequence) và ngày hết hạn thị thực nhập tay cho cả đoàn.
        /// Ô trống / bỏ chọn ngày → trả lại giá trị đọc từ file.
        /// </summary>
        private void ApplyManualVisaInfo()
        {
            if (_origin != DataOrigin.Excel) return;

            var firstNumber = VisaNumberSequence.Normalize(txtVisaNumber.Text);
            var expiry = dtpVisaExpiry.Checked ? dtpVisaExpiry.Value.Date : (DateTime?)null;
            for (var i = 0; i < _passengers.Count; i++)
            {
                var p = _passengers[i];
                var original = _originalVisa.TryGetValue(p, out var o) ? o : (Number: p.VisaNumber, Expiry: p.VisaExpiry);

                p.VisaNumber = firstNumber == "" ? original.Number : VisaNumberSequence.For(firstNumber, i);
                p.VisaExpiry = expiry ?? original.Expiry;
                if (firstNumber != "") p.ParseIssues.RemoveAll(x => x.Field == nameof(Passenger.VisaNumber));
                if (expiry is not null) p.ParseIssues.RemoveAll(x => x.Field == nameof(Passenger.VisaExpiry));
            }
            _passengers.ResetBindings();
        }

        // ------------------------------------------------------------------ tìm kiếm

        private void txtSearch_TextChanged(object? sender, EventArgs e) => ApplySearchFilter();

        private void txtSearch_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            txtSearch.Clear();
            e.SuppressKeyPress = true;
        }

        /// <summary>
        /// Ẩn các dòng không khớp ô tìm kiếm. Khớp (không phân biệt hoa thường, bỏ khoảng trắng thừa) theo
        /// Họ và tên, Số hộ chiếu, Số thị thực. Chỉ ẩn trên bảng – mọi khách vẫn được kiểm tra và khai.
        /// </summary>
        private void ApplySearchFilter()
        {
            var keyword = string.Join(' ', txtSearch.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (gridPassengers.IsCurrentCellInEditMode)
                gridPassengers.EndEdit();

            // Không ẩn được dòng đang là dòng hiện tại → bỏ chọn ô hiện tại trước.
            var currency = (CurrencyManager)BindingContext![_passengers];
            currency.SuspendBinding();
            gridPassengers.CurrentCell = null;

            var shown = 0;
            for (var i = 0; i < _passengers.Count && i < gridPassengers.RowCount; i++)
            {
                var p = _passengers[i];
                var match = keyword == ""
                            || p.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            || p.PassportNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            || p.VisaNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                gridPassengers.Rows[i].Visible = match;
                if (match) shown++;
            }
            currency.ResumeBinding();

            lblSearchResult.Text = keyword == "" ? "" : $"Tìm thấy {shown}/{_passengers.Count} khách";
            lblSearchResult.ForeColor = shown == 0 && keyword != "" ? Color.Firebrick : Color.DimGray;
        }

        // ------------------------------------------------------------------ chạy

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;
            SaveSettings();

            foreach (var p in _passengers)
            {
                p.State = PassengerState.Pending;
                p.StatusMessage = "";
            }

            if (!RunValidation(showSummary: true))
            {
                MessageBox.Show(this, "Dữ liệu còn lỗi. Sửa các ô màu đỏ trực tiếp trong bảng trước khi chạy.\n" +
                                      $"(Hoặc sửa file Excel \"{Path.GetFileName(txtExcel.Text)}\" rồi bấm \"Làm mới\".)\n\n" +
                                      "Rê chuột vào ô để xem chi tiết lỗi.", "Chưa thể chạy",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var group = BuildGroup();
            _automation ??= new PrearrivalAutomation(_settings);
            _cts = new CancellationTokenSource();
            SetRunning(true);
            var active = _passengers.ToList();
            Log($"Bắt đầu khai {active.Count} khách – {group.Nationality.DisplayName} – ngày đến {group.ArrivalDate:dd/MM/yyyy}.");

            try
            {
                await _automation.RunAsync(group, active, this, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Đã dừng. Trình duyệt vẫn mở.", Color.DimGray);
                Log("Đã dừng theo yêu cầu.");
            }
            catch (Exception ex)
            {
                Log("LỖI: " + ex);
                SetStatus("Lỗi: " + ex.Message.Split('\n')[0], Color.Firebrick);
                MessageBox.Show(this, ex.Message, "Lỗi khi điều khiển trình duyệt", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetRunning(false);
                _cts.Dispose();
                _cts = null;
                gridPassengers.Refresh();
            }
        }

        private void SetRunning(bool running)
        {
            _running = running;
            foreach (Control c in new Control[] { txtEmail, cboNationality, cboArrival, btnBrowse, btnBrowseExcel,
                                                  btnReload, btnValidate, btnDelete, btnNewGroup })
                c.Enabled = !running;
            btnStart.Enabled = !running && _dataValid;
            btnStop.Enabled = running;
            UpdateManualVisaControls();
            if (!running) SetDecisionButtons(false, false);
        }

        private async void btnNewGroup_Click(object sender, EventArgs e)
        {
            if (_automation is not null)
            {
                var answer = MessageBox.Show(this, "Đóng trình duyệt của đoàn hiện tại?", "Khai đoàn mới",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes)
                {
                    await _automation.DisposeAsync();
                    _automation = null;
                }
            }

            _passengers = [];
            gridPassengers.DataSource = _passengers;
            _origin = DataOrigin.None;
            _originalVisa = [];
            ResetManualVisaInputs();
            txtSearch.Clear();
            txtExcel.Text = "";
            ResetArrivalOptions();
            SaveSettings();
            RunValidation(showSummary: false);
            SetStatus("Khai đoàn mới: chọn quốc tịch, ngày đến rồi bấm \"Chọn file PDF...\" hoặc \"Chọn file Excel...\".", Color.Black);
            Log("---- Đoàn mới ----");
        }

        // ------------------------------------------------------------------ quyết định của người dùng

        private void SetDecisionButtons(bool enabled, bool allowSkip)
        {
            btnContinue.Enabled = enabled;
            btnRefill.Enabled = enabled;
            btnSkip.Enabled = enabled && allowSkip;
        }

        private void CompleteDecision(UserDecision decision)
        {
            var pending = _pendingDecision;
            if (pending is null) return;
            _pendingDecision = null;
            _pendingPassenger = null;
            SetDecisionButtons(false, false);
            pending.TrySetResult(decision);
        }

        private void btnContinue_Click(object sender, EventArgs e) => CompleteDecision(UserDecision.Continue);

        private void btnRefill_Click(object sender, EventArgs e)
        {
            RunValidation(showSummary: false);
            if (_pendingPassenger is { HasErrors: true } p)
            {
                MessageBox.Show(this, $"Khách STT {p.No} vẫn còn lỗi:\n" +
                                      string.Join("\n", p.Issues.Where(i => !i.IsWarning).Select(i => "• " + i.Message)),
                    "Chưa điền lại được", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CompleteDecision(UserDecision.Refill);
        }

        private void btnSkip_Click(object sender, EventArgs e) => CompleteDecision(UserDecision.Skip);

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_pendingDecision is not null)
                CompleteDecision(UserDecision.Stop);
            else
                _cts?.Cancel();
        }

        // ------------------------------------------------------------------ IAutomationHost

        public void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => Log(message));
                return;
            }
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        public void ReportStatus(string message, bool requiresUser = false)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ReportStatus(message, requiresUser));
                return;
            }
            SetStatus(message, requiresUser ? Color.DarkOrange : Color.Navy);
            Log(message);
            if (requiresUser) SystemSounds.Exclamation.Play();
        }

        public void FileDownloaded(string path)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => FileDownloaded(path));
                return;
            }
            Log($"Đã lưu file tải về: {path}");
            SetStatus($"✔ Đã tải về: {Path.GetFileName(path)}\n{path}", Color.DarkGreen);
            // Mở thư mục và chọn sẵn file (lịch sử tải của Chrome do app điều khiển chỉ hiện tên GUID).
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Không mở được Explorer: đường dẫn đã có trong log.
            }
        }

        public void PassengerChanged(Passenger passenger)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => PassengerChanged(passenger));
                return;
            }
            var index = _passengers.IndexOf(passenger);
            if (index < 0) return;
            _passengers.ResetItem(index);
            if (index < gridPassengers.RowCount && gridPassengers.Rows[index].Visible)
                gridPassengers.FirstDisplayedScrollingRowIndex = index;
        }

        public async Task<UserDecision> AskUserAsync(AttentionRequest request, CancellationToken cancellationToken)
        {
            var details = string.Join("\n", request.Errors.Select(e => $"• {e.Field}: {e.Message}"));
            SetStatus($"⚠ {request.Title}\n{details}", Color.Firebrick);
            Log($"⚠ {request.Title}: {details.Replace("\n", " ")}");
            SystemSounds.Exclamation.Play();

            var index = _passengers.IndexOf(request.Passenger);
            if (index >= 0)
            {
                // Khách cần sửa đang bị ẩn bởi ô tìm kiếm → xoá tìm kiếm để thấy dòng đó.
                if (!gridPassengers.Rows[index].Visible)
                    txtSearch.Clear();
                gridPassengers.ClearSelection();
                gridPassengers.Rows[index].Selected = true;
                gridPassengers.FirstDisplayedScrollingRowIndex = index;
            }

            _pendingPassenger = request.Passenger;
            _pendingDecision = new TaskCompletionSource<UserDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetDecisionButtons(true, request.AllowSkip);

            using var registration = cancellationToken.Register(() => _pendingDecision?.TrySetCanceled());
            var decision = await _pendingDecision.Task;
            SetStatus("Tiếp tục...", Color.Navy);
            return decision;
        }

        private void SetStatus(string message, Color color)
        {
            lblStatus.ForeColor = color;
            lblStatus.Text = message;
        }

        // ------------------------------------------------------------------ bảng

        private Passenger? PassengerAt(int rowIndex) =>
            rowIndex >= 0 && rowIndex < _passengers.Count ? _passengers[rowIndex] : null;

        private void gridPassengers_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (PassengerAt(e.RowIndex) is not { } p) return;

            var style = e.CellStyle!;
            style.BackColor = p.State switch
            {
                PassengerState.Filled => Color.Honeydew,
                PassengerState.Skipped => Color.Gainsboro,
                PassengerState.Filling => Color.LightYellow,
                PassengerState.NeedsAttention => Color.MistyRose,
                _ => gridPassengers.DefaultCellStyle.BackColor
            };

            var field = gridPassengers.Columns[e.ColumnIndex].DataPropertyName;
            var issues = p.Issues.Where(i => i.Field == field).ToList();
            if (issues.Any(i => !i.IsWarning))
                style.BackColor = Color.LightCoral;
            else if (issues.Count > 0)
                style.BackColor = Color.Khaki;

            // Cột Thông báo: chữ đỏ khi dòng có lỗi (dữ liệu hoặc trên site), cam khi chỉ có cảnh báo.
            if (e.ColumnIndex == colMessage.Index)
            {
                if (p.HasErrors || p.State == PassengerState.NeedsAttention)
                {
                    style.ForeColor = Color.Red;
                    style.Font = new Font(gridPassengers.Font, FontStyle.Bold);
                }
                else if (p.Issues.Any(i => i.IsWarning) && p.State is PassengerState.Pending)
                    style.ForeColor = Color.DarkOrange;
            }
        }

        private void gridPassengers_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.ColumnIndex < 0 || PassengerAt(e.RowIndex) is not { } p) return;
            var field = gridPassengers.Columns[e.ColumnIndex].DataPropertyName;
            var messages = p.Issues.Where(i => i.Field == field).Select(i => i.Message).ToList();
            if (messages.Count > 0)
                e.ToolTipText = string.Join("\n", messages);
        }

        private void gridPassengers_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (gridPassengers.Columns[e.ColumnIndex].DefaultCellStyle.Format != "dd/MM/yyyy") return;

            var text = e.Value?.ToString()?.Trim() ?? "";
            if (text == "")
            {
                e.Value = null;
                e.ParsingApplied = true;
            }
            else if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                e.Value = date;
                e.ParsingApplied = true;
            }
        }

        private void gridPassengers_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (PassengerAt(e.RowIndex) is { } p)
            {
                p.Surname = p.Surname.Trim().ToUpperInvariant();
                p.GivenName = p.GivenName.Trim().ToUpperInvariant();
                p.PassportNumber = p.PassportNumber.Trim().ToUpperInvariant();
                p.VisaNumber = p.VisaNumber.Trim().ToUpperInvariant();
                // Giá trị đã sửa tay trong bảng thì bỏ lỗi đọc Excel của ô đó.
                var field = gridPassengers.Columns[e.ColumnIndex].DataPropertyName;
                p.ParseIssues.RemoveAll(i => i.Field == field);
            }
            BeginInvoke(() => RunValidation(showSummary: false));
        }

        private void gridPassengers_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            SetStatus("Giá trị vừa nhập không hợp lệ (ngày nhập dạng DD/MM/YYYY).", Color.Firebrick);
        }

        // ------------------------------------------------------------------ đóng app

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closing || _automation is null)
            {
                if (!_closing) SaveSettings();
                return;
            }

            e.Cancel = true;
            _closing = true;
            SaveSettings();
            _pendingDecision?.TrySetResult(UserDecision.Stop);
            _cts?.Cancel();
            await _automation.DisposeAsync();
            _automation = null;
            Close();
        }
    }
}
