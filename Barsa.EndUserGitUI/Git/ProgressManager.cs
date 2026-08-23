using System;
using System.Drawing;
using System.Windows.Forms;

namespace Barsa.EndUserGitUI.Git
{
    public static class ProgressManager
    {
        public static event Action<string> MessageReported;
        private static Form _form;
        private static Label _titleLabel;
        private static Label _detailLabel;
        private static ProgressBar _overallBar;
        private static ProgressBar _itemBar;
        private static Label _overallLabel;
        private static Label _itemLabel;
        private static TextBox _logBox;
        private static bool _started;

        public static void Start(string title)
        {
            if (_started) return;
            _started = true;

            _form = new Form();
            _form.Text = title;
            _form.StartPosition = FormStartPosition.CenterScreen;
            _form.FormBorderStyle = FormBorderStyle.FixedDialog;
            _form.ControlBox = false;
            _form.MinimizeBox = false;
            _form.MaximizeBox = false;
            _form.ClientSize = new Size(720, 390);
            _form.Font = new Font("Tahoma", 9F);
            _form.RightToLeft = RightToLeft.Yes;
            _form.RightToLeftLayout = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.RowCount = 8;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            _titleLabel = new Label();
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.Font = new Font("Tahoma", 11F, FontStyle.Bold);
            _titleLabel.TextAlign = ContentAlignment.MiddleRight;
            root.Controls.Add(_titleLabel, 0, 0);

            _detailLabel = new Label();
            _detailLabel.Dock = DockStyle.Fill;
            _detailLabel.TextAlign = ContentAlignment.MiddleRight;
            root.Controls.Add(_detailLabel, 0, 1);

            _overallLabel = new Label();
            _overallLabel.Dock = DockStyle.Fill;
            root.Controls.Add(_overallLabel, 0, 2);

            _overallBar = new ProgressBar();
            _overallBar.Dock = DockStyle.Fill;
            root.Controls.Add(_overallBar, 0, 3);

            _itemLabel = new Label();
            _itemLabel.Dock = DockStyle.Fill;
            root.Controls.Add(_itemLabel, 0, 4);

            _itemBar = new ProgressBar();
            _itemBar.Dock = DockStyle.Fill;
            root.Controls.Add(_itemBar, 0, 5);

            _logBox = new TextBox();
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.Dock = DockStyle.Fill;
            _logBox.RightToLeft = RightToLeft.Yes;
            root.Controls.Add(_logBox, 0, 6);

            Label footer = new Label();
            footer.Text = "لطفاً تا پایان عملیات پنجره را نبندید...";
            footer.Dock = DockStyle.Fill;
            footer.TextAlign = ContentAlignment.MiddleCenter;
            root.Controls.Add(footer, 0, 7);

            _form.Controls.Add(root);
            _titleLabel.Text = title;
            _detailLabel.Text = "در حال آماده‌سازی...";
            _overallLabel.Text = "پیشرفت کلی: 0%";
            _itemLabel.Text = "مرحله فعلی: 0%";
            _form.Show();
            _form.Refresh();
        }

        public static void ReportStage(string title, string detail, int overall)
        {
            int p = Clamp(overall);
            RunOnUi(delegate
            {
                if (_titleLabel != null) _titleLabel.Text = title ?? "";
                if (_detailLabel != null) _detailLabel.Text = detail ?? "";
                if (_overallBar != null) _overallBar.Value = p;
                if (_overallLabel != null) _overallLabel.Text = "پیشرفت کلی: " + p + "%";
                if (_itemBar != null) _itemBar.Value = 0;
                if (_itemLabel != null) _itemLabel.Text = "مرحله فعلی: 0%";
                AppendLog(title + " | " + detail);
            });
        }

        public static void ReportItem(string detail, int index, int count, int overall)
        {
            int item = count <= 0 ? 0 : (int)Math.Round(index * 100.0 / count);
            item = Clamp(item);
            int p = Clamp(overall);

            RunOnUi(delegate
            {
                if (_detailLabel != null) _detailLabel.Text = detail ?? "";
                if (_itemBar != null) _itemBar.Value = item;
                if (_itemLabel != null) _itemLabel.Text = "مرحله فعلی: " + item + "%  (" + index + " از " + count + ")";
                if (_overallBar != null) _overallBar.Value = p;
                if (_overallLabel != null) _overallLabel.Text = "پیشرفت کلی: " + p + "%";
                AppendLog(detail);
            });
        }

        public static void Report(string detail, int overall)
        {
            ReportStage("در حال انجام عملیات", detail, overall);
        }

        public static void Log(string message)
        {
            RunOnUi(delegate { AppendLog(message); });
        }

        public static void Finish()
        {
            RunOnUi(delegate
            {
                if (_overallBar != null) _overallBar.Value = 100;
                if (_overallLabel != null) _overallLabel.Text = "پیشرفت کلی: 100%";
                if (_itemBar != null) _itemBar.Value = 100;
                if (_itemLabel != null) _itemLabel.Text = "مرحله فعلی: 100%";
                AppendLog("عملیات به پایان رسید.");
                if (_form != null && !_form.IsDisposed)
                {
                    _form.Close();
                    _form.Dispose();
                }
                _form = null;
                _started = false;
            });
        }

        public static void Error(string message)
        {
            Log("خطا: " + message);
        }

        private static void AppendLog(string message)
        {
            try { if (MessageReported != null) MessageReported(DateTime.Now.ToString("HH:mm:ss") + " | " + (message ?? "")); } catch { }
            if (_logBox == null || _logBox.IsDisposed) return;
            _logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + " | " + (message ?? "") + Environment.NewLine);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
            _logBox.Refresh();
        }

        private static void RunOnUi(Action action)
        {
            if (_form == null || _form.IsDisposed) return;
            try
            {
                if (_form.InvokeRequired)
                    _form.BeginInvoke(action);
                else
                    action();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }
    }
}
