namespace App
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dateStyle = new DataGridViewCellStyle();
            DataGridViewCellStyle messageStyle = new DataGridViewCellStyle();
            messageStyle.WrapMode = DataGridViewTriState.True;
            grpSettings = new GroupBox();
            tlpSettings = new TableLayoutPanel();
            lblEmail = new Label();
            txtEmail = new TextBox();
            lblVisaNumber = new Label();
            txtVisaNumber = new TextBox();
            lblVisaExpiry = new Label();
            dtpVisaExpiry = new DateTimePicker();
            lblNationality = new Label();
            cboNationality = new ComboBox();
            lblArrival = new Label();
            cboArrival = new ComboBox();
            lblExcel = new Label();
            txtExcel = new TextBox();
            btnBrowse = new Button();
            btnBrowseExcel = new Button();
            flpActions = new FlowLayoutPanel();
            btnReload = new Button();
            btnValidate = new Button();
            btnDelete = new Button();
            lblSearch = new Label();
            txtSearch = new TextBox();
            lblSearchResult = new Label();
            progressImport = new ProgressBar();
            lblImport = new Label();
            btnStart = new Button();
            btnNewGroup = new Button();
            splitMain = new SplitContainer();
            gridPassengers = new DataGridView();
            colRow = new DataGridViewTextBoxColumn();
            colState = new DataGridViewTextBoxColumn();
            colSurname = new DataGridViewTextBoxColumn();
            colGivenName = new DataGridViewTextBoxColumn();
            colGender = new DataGridViewComboBoxColumn();
            colDob = new DataGridViewTextBoxColumn();
            colPassportType = new DataGridViewComboBoxColumn();
            colPassportNumber = new DataGridViewTextBoxColumn();
            colPassportExpiry = new DataGridViewTextBoxColumn();
            colVisaType = new DataGridViewComboBoxColumn();
            colVisaNumber = new DataGridViewTextBoxColumn();
            colVisaIssueDate = new DataGridViewTextBoxColumn();
            colVisaValidFrom = new DataGridViewTextBoxColumn();
            colVisaExpiry = new DataGridViewTextBoxColumn();
            colVisaIssuedPlace = new DataGridViewTextBoxColumn();
            colMessage = new DataGridViewTextBoxColumn();
            txtLog = new TextBox();
            pnlAttention = new Panel();
            flpAttention = new FlowLayoutPanel();
            btnContinue = new Button();
            btnRefill = new Button();
            btnSkip = new Button();
            btnStop = new Button();
            btnVerifyReview = new Button();
            lblStatus = new Label();
            grpSettings.SuspendLayout();
            tlpSettings.SuspendLayout();
            flpActions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridPassengers).BeginInit();
            pnlAttention.SuspendLayout();
            flpAttention.SuspendLayout();
            SuspendLayout();
            //
            // grpSettings
            //
            grpSettings.Controls.Add(btnNewGroup);
            grpSettings.Controls.Add(tlpSettings);
            grpSettings.Dock = DockStyle.Top;
            grpSettings.Location = new Point(0, 0);
            grpSettings.Name = "grpSettings";
            grpSettings.Padding = new Padding(8);
            grpSettings.Size = new Size(1384, 172);
            grpSettings.TabIndex = 0;
            grpSettings.TabStop = false;
            grpSettings.Text = "Thiết lập đoàn";
            //
            // tlpSettings
            //
            tlpSettings.ColumnCount = 6;
            tlpSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tlpSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));
            tlpSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            tlpSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));
            tlpSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            tlpSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            tlpSettings.Controls.Add(lblEmail, 0, 0);
            tlpSettings.Controls.Add(txtEmail, 1, 0);
            tlpSettings.Controls.Add(lblVisaNumber, 2, 0);
            tlpSettings.Controls.Add(txtVisaNumber, 3, 0);
            tlpSettings.Controls.Add(lblNationality, 0, 1);
            tlpSettings.Controls.Add(cboNationality, 1, 1);
            tlpSettings.Controls.Add(lblVisaExpiry, 2, 1);
            tlpSettings.Controls.Add(dtpVisaExpiry, 3, 1);
            tlpSettings.Controls.Add(lblArrival, 0, 2);
            tlpSettings.Controls.Add(cboArrival, 1, 2);
            tlpSettings.Controls.Add(lblExcel, 0, 3);
            tlpSettings.Controls.Add(txtExcel, 1, 3);
            tlpSettings.Controls.Add(btnBrowse, 4, 3);
            tlpSettings.Controls.Add(btnBrowseExcel, 5, 3);
            tlpSettings.Dock = DockStyle.Fill;
            tlpSettings.Location = new Point(8, 24);
            tlpSettings.Name = "tlpSettings";
            tlpSettings.RowCount = 4;
            tlpSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tlpSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tlpSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tlpSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            tlpSettings.Size = new Size(1368, 140);
            tlpSettings.TabIndex = 0;
            //
            // lblEmail
            //
            lblEmail.Anchor = AnchorStyles.Left;
            lblEmail.AutoSize = true;
            lblEmail.Name = "lblEmail";
            lblEmail.TabIndex = 0;
            lblEmail.Text = "Email công ty *";
            //
            // txtEmail
            //
            txtEmail.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtEmail.Name = "txtEmail";
            txtEmail.PlaceholderText = "ops@congty.com";
            txtEmail.TabIndex = 1;
            txtEmail.TextChanged += groupInput_Changed;
            //
            // lblVisaNumber
            //
            lblVisaNumber.Anchor = AnchorStyles.Left;
            lblVisaNumber.AutoSize = true;
            lblVisaNumber.Name = "lblVisaNumber";
            lblVisaNumber.TabIndex = 2;
            lblVisaNumber.Text = "Số thị thực (khách 1)";
            //
            // txtVisaNumber
            //
            txtVisaNumber.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtVisaNumber.CharacterCasing = CharacterCasing.Upper;
            txtVisaNumber.Enabled = false;
            txtVisaNumber.MaxLength = 22;
            txtVisaNumber.Name = "txtVisaNumber";
            txtVisaNumber.PlaceholderText = "Chỉ dùng khi nạp file Excel – vd AA26276768";
            txtVisaNumber.TabIndex = 3;
            txtVisaNumber.TextChanged += manualVisa_Changed;
            //
            // lblVisaExpiry
            //
            lblVisaExpiry.Anchor = AnchorStyles.Left;
            lblVisaExpiry.AutoSize = true;
            lblVisaExpiry.Name = "lblVisaExpiry";
            lblVisaExpiry.TabIndex = 4;
            lblVisaExpiry.Text = "Hết hạn thị thực";
            //
            // dtpVisaExpiry
            //
            dtpVisaExpiry.Anchor = AnchorStyles.Left;
            dtpVisaExpiry.Size = new Size(160, 23);
            dtpVisaExpiry.Checked = false;
            dtpVisaExpiry.CustomFormat = "dd/MM/yyyy";
            dtpVisaExpiry.Enabled = false;
            dtpVisaExpiry.Format = DateTimePickerFormat.Custom;
            dtpVisaExpiry.Name = "dtpVisaExpiry";
            dtpVisaExpiry.ShowCheckBox = true;
            dtpVisaExpiry.TabIndex = 5;
            dtpVisaExpiry.ValueChanged += manualVisa_Changed;
            //
            // lblNationality
            //
            lblNationality.Anchor = AnchorStyles.Left;
            lblNationality.AutoSize = true;
            lblNationality.Name = "lblNationality";
            lblNationality.TabIndex = 4;
            lblNationality.Text = "Quốc tịch";
            //
            // cboNationality
            //
            cboNationality.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboNationality.DropDownStyle = ComboBoxStyle.DropDownList;
            cboNationality.Name = "cboNationality";
            cboNationality.TabIndex = 5;
            cboNationality.SelectedIndexChanged += groupInput_Changed;
            //
            // lblArrival
            //
            lblArrival.Anchor = AnchorStyles.Left;
            lblArrival.AutoSize = true;
            lblArrival.Name = "lblArrival";
            lblArrival.TabIndex = 6;
            lblArrival.Text = "Ngày đến dự kiến";
            //
            // cboArrival
            //
            cboArrival.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboArrival.DropDownStyle = ComboBoxStyle.DropDownList;
            cboArrival.Name = "cboArrival";
            cboArrival.TabIndex = 7;
            cboArrival.SelectedIndexChanged += groupInput_Changed;
            //
            // lblExcel
            //
            lblExcel.Anchor = AnchorStyles.Left;
            lblExcel.AutoSize = true;
            lblExcel.Name = "lblExcel";
            lblExcel.TabIndex = 8;
            lblExcel.Text = "File dữ liệu";
            //
            // txtExcel
            //
            txtExcel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            tlpSettings.SetColumnSpan(txtExcel, 3);
            txtExcel.Name = "txtExcel";
            txtExcel.ReadOnly = true;
            txtExcel.TabIndex = 9;
            //
            // btnBrowse
            //
            btnBrowse.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Size = new Size(124, 28);
            btnBrowse.TabIndex = 10;
            btnBrowse.Text = "Chọn file PDF...";
            btnBrowse.UseVisualStyleBackColor = true;
            btnBrowse.Click += btnBrowse_Click;
            //
            // btnBrowseExcel
            //
            btnBrowseExcel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            btnBrowseExcel.Name = "btnBrowseExcel";
            btnBrowseExcel.Size = new Size(124, 28);
            btnBrowseExcel.TabIndex = 11;
            btnBrowseExcel.Text = "Chọn file Excel...";
            btnBrowseExcel.UseVisualStyleBackColor = true;
            btnBrowseExcel.Click += btnBrowseExcel_Click;
            //
            // flpActions
            //
            flpActions.Controls.Add(btnReload);
            flpActions.Controls.Add(btnValidate);
            flpActions.Controls.Add(btnDelete);
            flpActions.Controls.Add(lblSearch);
            flpActions.Controls.Add(txtSearch);
            flpActions.Controls.Add(lblSearchResult);
            flpActions.Controls.Add(progressImport);
            flpActions.Controls.Add(lblImport);
            flpActions.Dock = DockStyle.Top;
            flpActions.Location = new Point(0, 132);
            flpActions.Name = "flpActions";
            flpActions.Padding = new Padding(6, 4, 6, 4);
            flpActions.Size = new Size(1384, 46);
            flpActions.TabIndex = 1;
            //
            // btnReload
            //
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(140, 34);
            btnReload.TabIndex = 0;
            btnReload.Text = "Làm mới";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += btnReload_Click;
            //
            // btnValidate
            //
            btnValidate.Name = "btnValidate";
            btnValidate.Size = new Size(140, 34);
            btnValidate.TabIndex = 2;
            btnValidate.Text = "Kiểm tra dữ liệu";
            btnValidate.UseVisualStyleBackColor = true;
            btnValidate.Click += btnValidate_Click;
            //
            // btnDelete
            //
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(140, 34);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "🗑 Xoá khách";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            //
            // lblSearch
            //
            lblSearch.AutoSize = true;
            lblSearch.Margin = new Padding(24, 12, 3, 0);
            lblSearch.Name = "lblSearch";
            lblSearch.TabIndex = 2;
            lblSearch.Text = "🔍 Tìm kiếm:";
            //
            // txtSearch
            //
            txtSearch.Margin = new Padding(3, 8, 3, 3);
            txtSearch.Name = "txtSearch";
            txtSearch.PlaceholderText = "Họ tên, số hộ chiếu, số thị thực... (Esc để xoá)";
            txtSearch.Size = new Size(320, 23);
            txtSearch.TabIndex = 3;
            txtSearch.TextChanged += txtSearch_TextChanged;
            txtSearch.KeyDown += txtSearch_KeyDown;
            //
            // lblSearchResult
            //
            lblSearchResult.AutoSize = true;
            lblSearchResult.ForeColor = Color.DimGray;
            lblSearchResult.Margin = new Padding(6, 12, 3, 0);
            lblSearchResult.Name = "lblSearchResult";
            lblSearchResult.TabIndex = 4;
            //
            // progressImport
            //
            progressImport.Margin = new Padding(24, 9, 3, 3);
            progressImport.Name = "progressImport";
            progressImport.Size = new Size(320, 22);
            progressImport.TabIndex = 3;
            progressImport.Visible = false;
            //
            // lblImport
            //
            lblImport.AutoSize = true;
            lblImport.Margin = new Padding(6, 12, 3, 0);
            lblImport.Name = "lblImport";
            lblImport.TabIndex = 4;
            lblImport.Visible = false;
            //
            // btnStart
            //
            btnStart.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnStart.BackColor = Color.FromArgb(31, 56, 100);
            btnStart.Enabled = false;
            btnStart.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnStart.ForeColor = Color.White;
            btnStart.Location = new Point(1176, 70);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(196, 42);
            btnStart.TabIndex = 2;
            btnStart.Text = "▶ Bắt đầu khai";
            btnStart.UseVisualStyleBackColor = false;
            btnStart.Click += btnStart_Click;
            //
            // btnNewGroup
            //
            btnNewGroup.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNewGroup.Location = new Point(1228, 22);
            btnNewGroup.Name = "btnNewGroup";
            btnNewGroup.Size = new Size(146, 34);
            btnNewGroup.TabIndex = 1;
            btnNewGroup.Text = "Khai đoàn mới";
            btnNewGroup.UseVisualStyleBackColor = true;
            btnNewGroup.Click += btnNewGroup_Click;
            //
            // splitMain
            //
            splitMain.Dock = DockStyle.Fill;
            splitMain.Location = new Point(0, 178);
            splitMain.Name = "splitMain";
            splitMain.Orientation = Orientation.Horizontal;
            //
            // splitMain.Panel1
            //
            splitMain.Panel1.Controls.Add(gridPassengers);
            //
            // splitMain.Panel2
            //
            splitMain.Panel2.Controls.Add(txtLog);
            splitMain.Size = new Size(1384, 513);
            splitMain.SplitterDistance = 370;
            splitMain.TabIndex = 2;
            //
            // gridPassengers
            //
            gridPassengers.AllowUserToAddRows = false;
            gridPassengers.AllowUserToDeleteRows = false;
            gridPassengers.AutoGenerateColumns = false;
            gridPassengers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            gridPassengers.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            gridPassengers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridPassengers.Columns.AddRange(new DataGridViewColumn[] { colRow, colState, colSurname, colGivenName, colGender, colDob, colPassportType, colPassportNumber, colPassportExpiry, colVisaType, colVisaNumber, colVisaIssueDate, colVisaValidFrom, colVisaExpiry, colVisaIssuedPlace, colMessage });
            gridPassengers.Dock = DockStyle.Fill;
            gridPassengers.Location = new Point(0, 0);
            gridPassengers.Name = "gridPassengers";
            gridPassengers.RowHeadersWidth = 24;
            gridPassengers.Size = new Size(1384, 370);
            gridPassengers.TabIndex = 0;
            gridPassengers.CellEndEdit += gridPassengers_CellEndEdit;
            gridPassengers.CellFormatting += gridPassengers_CellFormatting;
            gridPassengers.CellParsing += gridPassengers_CellParsing;
            gridPassengers.CellToolTipTextNeeded += gridPassengers_CellToolTipTextNeeded;
            gridPassengers.DataError += gridPassengers_DataError;
            //
            // dateStyle
            //
            dateStyle.Format = "dd/MM/yyyy";
            dateStyle.NullValue = "";
            //
            // colRow
            //
            colRow.DataPropertyName = "No";
            colRow.HeaderText = "STT";
            colRow.Name = "colRow";
            colRow.ReadOnly = true;
            colRow.Width = 50;
            //
            // colState
            //
            colState.DataPropertyName = "StateText";
            colState.HeaderText = "Trạng thái";
            colState.Name = "colState";
            colState.ReadOnly = true;
            colState.Width = 85;
            //
            // colSurname
            //
            colSurname.DataPropertyName = "Surname";
            colSurname.HeaderText = "Họ";
            colSurname.Name = "colSurname";
            colSurname.Width = 70;
            //
            // colGivenName
            //
            colGivenName.DataPropertyName = "GivenName";
            colGivenName.HeaderText = "Tên *";
            colGivenName.Name = "colGivenName";
            colGivenName.Width = 100;
            //
            // colGender
            //
            colGender.DataPropertyName = "Gender";
            colGender.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colGender.HeaderText = "Giới tính *";
            colGender.Name = "colGender";
            colGender.MinimumWidth = 90;
            //
            // colDob
            //
            colDob.DataPropertyName = "DateOfBirth";
            colDob.DefaultCellStyle = dateStyle;
            colDob.HeaderText = "Ngày sinh *";
            colDob.Name = "colDob";
            colDob.Width = 85;
            //
            // colPassportType
            //
            colPassportType.DataPropertyName = "PassportType";
            colPassportType.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colPassportType.HeaderText = "Loại hộ chiếu *";
            colPassportType.Name = "colPassportType";
            colPassportType.MinimumWidth = 200;
            //
            // colPassportNumber
            //
            colPassportNumber.DataPropertyName = "PassportNumber";
            colPassportNumber.HeaderText = "Số hộ chiếu *";
            colPassportNumber.Name = "colPassportNumber";
            colPassportNumber.Width = 90;
            //
            // colPassportExpiry
            //
            colPassportExpiry.DataPropertyName = "PassportExpiry";
            colPassportExpiry.DefaultCellStyle = dateStyle;
            colPassportExpiry.HeaderText = "Hết hạn HC *";
            colPassportExpiry.Name = "colPassportExpiry";
            colPassportExpiry.Width = 85;
            //
            // colVisaType
            //
            colVisaType.DataPropertyName = "VisaType";
            colVisaType.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colVisaType.HeaderText = "Loại thị thực *";
            colVisaType.Name = "colVisaType";
            colVisaType.MinimumWidth = 230;
            //
            // colVisaNumber
            //
            colVisaNumber.DataPropertyName = "VisaNumber";
            colVisaNumber.HeaderText = "Số thị thực *";
            colVisaNumber.Name = "colVisaNumber";
            colVisaNumber.Width = 90;
            //
            // colVisaIssueDate
            //
            colVisaIssueDate.DataPropertyName = "VisaIssueDate";
            colVisaIssueDate.DefaultCellStyle = dateStyle;
            colVisaIssueDate.HeaderText = "Ngày cấp TT";
            colVisaIssueDate.Name = "colVisaIssueDate";
            colVisaIssueDate.Width = 85;
            //
            // colVisaValidFrom
            //
            colVisaValidFrom.DataPropertyName = "VisaValidFrom";
            colVisaValidFrom.DefaultCellStyle = dateStyle;
            colVisaValidFrom.HeaderText = "TT hiệu lực từ";
            colVisaValidFrom.Name = "colVisaValidFrom";
            colVisaValidFrom.Width = 85;
            //
            // colVisaExpiry
            //
            colVisaExpiry.DataPropertyName = "VisaExpiry";
            colVisaExpiry.DefaultCellStyle = dateStyle;
            colVisaExpiry.HeaderText = "Hết hạn TT *";
            colVisaExpiry.Name = "colVisaExpiry";
            colVisaExpiry.Width = 85;
            //
            // colVisaIssuedPlace
            //
            colVisaIssuedPlace.DataPropertyName = "VisaIssuedPlace";
            colVisaIssuedPlace.HeaderText = "Nơi cấp";
            colVisaIssuedPlace.Name = "colVisaIssuedPlace";
            colVisaIssuedPlace.Width = 70;
            //
            // colMessage
            //
            colMessage.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colMessage.DataPropertyName = "StatusMessage";
            colMessage.HeaderText = "Thông báo";
            colMessage.DefaultCellStyle = messageStyle;
            colMessage.MinimumWidth = 300;
            colMessage.Name = "colMessage";
            colMessage.ReadOnly = true;
            //
            // txtLog
            //
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.Location = new Point(0, 0);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(1384, 139);
            txtLog.TabIndex = 0;
            //
            // pnlAttention
            //
            pnlAttention.BackColor = Color.FromArgb(242, 242, 242);
            pnlAttention.Controls.Add(btnStart);
            pnlAttention.Controls.Add(flpAttention);
            pnlAttention.Controls.Add(lblStatus);
            pnlAttention.Dock = DockStyle.Bottom;
            pnlAttention.Location = new Point(0, 691);
            pnlAttention.Name = "pnlAttention";
            pnlAttention.Size = new Size(1384, 120);
            pnlAttention.TabIndex = 3;
            //
            // flpAttention
            //
            flpAttention.Controls.Add(btnContinue);
            flpAttention.Controls.Add(btnRefill);
            flpAttention.Controls.Add(btnSkip);
            flpAttention.Controls.Add(btnStop);
            flpAttention.Controls.Add(btnVerifyReview);
            flpAttention.Dock = DockStyle.Fill;
            flpAttention.Location = new Point(0, 70);
            flpAttention.Name = "flpAttention";
            flpAttention.Padding = new Padding(6, 2, 6, 2);
            flpAttention.Size = new Size(1384, 50);
            flpAttention.TabIndex = 1;
            //
            // btnContinue
            //
            btnContinue.Enabled = false;
            btnContinue.Name = "btnContinue";
            btnContinue.Size = new Size(280, 36);
            btnContinue.TabIndex = 0;
            btnContinue.Text = "Tiếp tục (đã sửa trên trình duyệt)";
            btnContinue.UseVisualStyleBackColor = true;
            btnContinue.Click += btnContinue_Click;
            //
            // btnRefill
            //
            btnRefill.Enabled = false;
            btnRefill.Name = "btnRefill";
            btnRefill.Size = new Size(280, 36);
            btnRefill.TabIndex = 1;
            btnRefill.Text = "Điền lại khách này (đã sửa trong bảng)";
            btnRefill.UseVisualStyleBackColor = true;
            btnRefill.Click += btnRefill_Click;
            //
            // btnSkip
            //
            btnSkip.Enabled = false;
            btnSkip.Name = "btnSkip";
            btnSkip.Size = new Size(170, 36);
            btnSkip.TabIndex = 2;
            btnSkip.Text = "Bỏ qua khách này";
            btnSkip.UseVisualStyleBackColor = true;
            btnSkip.Click += btnSkip_Click;
            //
            // btnStop
            //
            btnStop.Enabled = false;
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(200, 36);
            btnStop.TabIndex = 3;
            btnStop.Text = "⏸ Dừng điền (giữ Chrome)";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            //
            // btnVerifyReview
            //
            btnVerifyReview.Name = "btnVerifyReview";
            btnVerifyReview.Size = new Size(220, 36);
            btnVerifyReview.TabIndex = 4;
            btnVerifyReview.Text = "🔎 Đối chiếu trang Xem lại";
            btnVerifyReview.UseVisualStyleBackColor = true;
            btnVerifyReview.Click += btnVerifyReview_Click;
            //
            // lblStatus
            //
            lblStatus.Dock = DockStyle.Top;
            lblStatus.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblStatus.Location = new Point(0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Padding = new Padding(8, 6, 8, 0);
            lblStatus.Size = new Size(1384, 70);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "Bấm \"Chọn file PDF...\" (file visa) hoặc \"Chọn file Excel...\" (danh sách khách) để bắt đầu.";
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1384, 811);
            Controls.Add(splitMain);
            Controls.Add(pnlAttention);
            Controls.Add(flpActions);
            Controls.Add(grpSettings);
            MinimumSize = new Size(1100, 700);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "AutoPopulate – Khai báo thông tin trước nhập cảnh";
            FormClosing += MainForm_FormClosing;
            grpSettings.ResumeLayout(false);
            tlpSettings.ResumeLayout(false);
            tlpSettings.PerformLayout();
            flpActions.ResumeLayout(false);
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            splitMain.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridPassengers).EndInit();
            pnlAttention.ResumeLayout(false);
            flpAttention.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private GroupBox grpSettings;
        private TableLayoutPanel tlpSettings;
        private Label lblEmail;
        private TextBox txtEmail;
        private Label lblVisaNumber;
        private TextBox txtVisaNumber;
        private Label lblVisaExpiry;
        private DateTimePicker dtpVisaExpiry;
        private Label lblNationality;
        private ComboBox cboNationality;
        private Label lblArrival;
        private ComboBox cboArrival;
        private Label lblExcel;
        private TextBox txtExcel;
        private Button btnBrowse;
        private Button btnBrowseExcel;
        private FlowLayoutPanel flpActions;
        private Button btnReload;
        private Button btnValidate;
        private Button btnDelete;
        private Label lblSearch;
        private TextBox txtSearch;
        private Label lblSearchResult;
        private ProgressBar progressImport;
        private Label lblImport;
        private Button btnStart;
        private Button btnNewGroup;
        private SplitContainer splitMain;
        private DataGridView gridPassengers;
        private DataGridViewTextBoxColumn colRow;
        private DataGridViewTextBoxColumn colState;
        private DataGridViewTextBoxColumn colSurname;
        private DataGridViewTextBoxColumn colGivenName;
        private DataGridViewComboBoxColumn colGender;
        private DataGridViewTextBoxColumn colDob;
        private DataGridViewComboBoxColumn colPassportType;
        private DataGridViewTextBoxColumn colPassportNumber;
        private DataGridViewTextBoxColumn colPassportExpiry;
        private DataGridViewComboBoxColumn colVisaType;
        private DataGridViewTextBoxColumn colVisaNumber;
        private DataGridViewTextBoxColumn colVisaIssueDate;
        private DataGridViewTextBoxColumn colVisaValidFrom;
        private DataGridViewTextBoxColumn colVisaExpiry;
        private DataGridViewTextBoxColumn colVisaIssuedPlace;
        private DataGridViewTextBoxColumn colMessage;
        private TextBox txtLog;
        private Panel pnlAttention;
        private FlowLayoutPanel flpAttention;
        private Button btnContinue;
        private Button btnRefill;
        private Button btnSkip;
        private Button btnStop;
        private Button btnVerifyReview;
        private Label lblStatus;
    }
}
