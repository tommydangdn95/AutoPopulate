using System.Runtime.InteropServices;

namespace App
{
    /// <summary>
    /// Nút nằm trên thanh tiêu đề của cửa sổ chính, cạnh các nút thu nhỏ/phóng to/đóng.
    /// WinForms không cho đặt control vào thanh tiêu đề của Windows, nên đây là một cửa sổ nhỏ không viền
    /// thuộc cửa sổ chính, đặt đè lên thanh tiêu đề và đi theo khi cửa sổ chính di chuyển.
    /// </summary>
    internal sealed class TitleBarButton : Form
    {
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int DwmwaCaptionButtonBounds = 5;
        private const int LogicalWidth = 170;

        private readonly Form _owner;
        private readonly Button _button = new();

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);

        public TitleBarButton(Form owner, string text, Action onClick)
        {
            _owner = owner;
            Text = text;
            Owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(237, 125, 49);

            _button.Dock = DockStyle.Fill;
            _button.Text = text;
            _button.FlatStyle = FlatStyle.Flat;
            _button.FlatAppearance.BorderSize = 0;
            _button.BackColor = Color.FromArgb(237, 125, 49);
            _button.ForeColor = Color.White;
            _button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _button.Cursor = Cursors.Hand;
            _button.TabStop = false;
            _button.Click += (_, _) => onClick();
            Controls.Add(_button);

            owner.Move += (_, _) => Reposition();
            owner.SizeChanged += (_, _) => Reposition();
            Shown += (_, _) => Reposition();
        }

        public void SetText(string text)
        {
            Text = text;
            _button.Text = text;
        }

        // Bấm nút không lấy focus của cửa sổ chính.
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
                return parameters;
            }
        }

        private void Reposition()
        {
            if (_owner.WindowState == FormWindowState.Minimized) return;

            var scale = _owner.DeviceDpi / 96f;
            var gap = (int)(8 * scale);
            var width = (int)(LogicalWidth * scale);
            int right, top, height;
            if (DwmGetWindowAttribute(_owner.Handle, DwmwaCaptionButtonBounds, out var caption, Marshal.SizeOf<NativeRect>()) == 0
                && caption.Right > caption.Left)
            {
                right = _owner.Left + caption.Left;
                top = _owner.Top + caption.Top;
                height = caption.Bottom - caption.Top;
            }
            else
            {
                right = _owner.Right - (int)(150 * scale);
                top = _owner.Top + (int)(4 * scale);
                height = SystemInformation.CaptionHeight;
            }

            // Khi phóng to, thanh tiêu đề nhô ra ngoài mép trên màn hình: chỉ dùng phần nhìn thấy.
            var screenTop = Screen.FromControl(_owner).Bounds.Top;
            if (top < screenTop)
            {
                height -= screenTop - top;
                top = screenTop;
            }

            var inset = (int)(4 * scale);
            Bounds = new Rectangle(right - width - gap, top + inset, width, Math.Max(height - 2 * inset, (int)(14 * scale)));
        }
    }
}
