using System.Globalization;

namespace App
{
    /// <summary>
    /// Ô ngày trong DataGridView: khi sửa hiện DateTimePicker để chọn ngày trên lịch.
    /// Giá trị có thể để trống (DateTime? = null): nhấn Delete/Backspace để xoá.
    /// </summary>
    internal sealed class CalendarCell : DataGridViewTextBoxCell
    {
        public const string DateFormat = "dd/MM/yyyy";

        public override Type EditType => typeof(CalendarEditingControl);

        public override object? DefaultNewRowValue => null;

        public override void InitializeEditingControl(int rowIndex, object? initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            if (DataGridView?.EditingControl is CalendarEditingControl picker)
                picker.SetDate(GetValue(rowIndex) as DateTime?);
        }
    }

    internal sealed class CalendarEditingControl : DateTimePicker, IDataGridViewEditingControl
    {
        private bool _initializing;
        private bool _cleared;

        public CalendarEditingControl()
        {
            Format = DateTimePickerFormat.Custom;
            CustomFormat = CalendarCell.DateFormat;
        }

        public void SetDate(DateTime? date)
        {
            _initializing = true;
            Value = date ?? DateTime.Today;
            _initializing = false;
            SetCleared(date is null);
            EditingControlValueChanged = false;
        }

        /// <summary>Ô trống: hiển thị rỗng (lịch vẫn mở ở ngày hôm nay khi bấm xổ).</summary>
        private void SetCleared(bool cleared)
        {
            _cleared = cleared;
            CustomFormat = cleared ? " " : CalendarCell.DateFormat;
        }

        private void NotifyChanged()
        {
            EditingControlValueChanged = true;
            EditingControlDataGridView?.NotifyCurrentCellDirty(true);
        }

        protected override void OnValueChanged(EventArgs eventargs)
        {
            base.OnValueChanged(eventargs);
            if (_initializing) return;
            SetCleared(false);
            NotifyChanged();
        }

        protected override void OnCloseUp(EventArgs eventargs)
        {
            base.OnCloseUp(eventargs);
            // Ô đang trống mà chọn đúng ngày đang hiển thị trên lịch thì ValueChanged không chạy.
            if (_cleared)
            {
                SetCleared(false);
                NotifyChanged();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Delete or Keys.Back)
            {
                SetCleared(true);
                NotifyChanged();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        // ------------------------------------------------------------------ IDataGridViewEditingControl

        public object EditingControlFormattedValue
        {
            get => _cleared ? "" : Value.ToString(CalendarCell.DateFormat, CultureInfo.InvariantCulture);
            set
            {
                if (value is not string text) return;
                if (string.IsNullOrWhiteSpace(text))
                    SetCleared(true);
                else if (DateTime.TryParseExact(text, CalendarCell.DateFormat, CultureInfo.InvariantCulture,
                             DateTimeStyles.None, out var date))
                {
                    Value = date;
                    SetCleared(false);
                }
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => EditingControlFormattedValue;

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
        {
            Font = dataGridViewCellStyle.Font;
        }

        public DataGridView? EditingControlDataGridView { get; set; }

        public int EditingControlRowIndex { get; set; }

        public bool EditingControlValueChanged { get; set; }

        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey) =>
            (keyData & Keys.KeyCode) switch
            {
                Keys.Left or Keys.Up or Keys.Down or Keys.Right or Keys.Home or Keys.End
                    or Keys.PageDown or Keys.PageUp or Keys.Delete or Keys.Back => true,
                _ => !dataGridViewWantsInputKey
            };

        public Cursor EditingPanelCursor => Cursor;

        public void PrepareEditingControlForEdit(bool selectAll)
        {
        }

        public bool RepositionEditingControlOnValueChange => false;
    }
}
