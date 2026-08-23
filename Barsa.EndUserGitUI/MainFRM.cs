using Barsa.EndUserGitUI.Git;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Barsa.EndUserGitUI
{
    public partial class MainFRM : Form
    {
        private string _repositoryPath;
        private bool _connected;

        public MainFRM()
        {
            InitializeComponent();
            ProgressManager.MessageReported += ProgressManager_MessageReported;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ProgressManager.MessageReported -= ProgressManager_MessageReported;
            base.OnFormClosed(e);
        }

        private void ProgressManager_MessageReported(string message)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string>(ProgressManager_MessageReported), message);
                    return;
                }
                txtLogs.AppendText(message + Environment.NewLine);
                txtLogs.SelectionStart = txtLogs.TextLength;
                txtLogs.ScrollToCaret();
            }
            catch { }
        }

        private void MainFRM_Load(object sender, EventArgs e)
        {
            GitDatabase.ConnectionString = txtConnectionString.Text;
            btnStatus.Enabled = false;
            btnCommit.Enabled = false;
            btnLog.Enabled = false;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                string path = txtRepositoryPath.Text.Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    ShowMessage("مسیر Repository را وارد کنید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                GitDatabase.ConnectionString = txtConnectionString.Text;
                GitDatabase.ClearCache();
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                if (!GitHelper.IsRepository(path))
                {
                    string result = GitDatabase.Init(path);
                    txtLogs.Text = "Repository ایجاد شد:\r\n\r\n" + result + "\r\n";
                }
                else
                {
                    txtLogs.Text = "Repository با موفقیت شناسایی شد.\r\n";
                }

                _repositoryPath = path;
                _connected = true;
                btnConnect.BackColor = Color.LightGray;
                btnConnect.Text = "متصل شد";
                btnConnect.Enabled = false;
                btnStatus.Enabled = true;
                btnCommit.Enabled = true;
                btnLog.Enabled = true;
            }
            catch (Exception ex)
            {
                _connected = false;
                ShowMessage(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnStatus_Click(object sender, EventArgs e)
        {
            if (!_connected)
            {
                ShowMessage("ابتدا به Repository متصل شوید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SetBusy(true);
                GitDatabase.ConnectionString = txtConnectionString.Text;
                _repositoryPath = txtRepositoryPath.Text.Trim();
                txtLogs.Clear();

                ProgressManager.Start("بررسی تغییرات Database");
                ProgressManager.ReportStage("Status", "شروع بررسی تغییرات Database...", 0);

                List<DatabaseChange> changes = await Task.Run(delegate
                {
                    return GitDatabase.GetChanges(_repositoryPath);
                });

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("تغییرات Database:");
                sb.AppendLine("============================");
                sb.AppendLine();

                if (changes == null || changes.Count == 0)
                {
                    sb.AppendLine("تغییری وجود ندارد.");
                    btnCommit.Enabled = false;
                }
                else
                {
                    btnCommit.Enabled = true;

                    foreach (DatabaseChange change in changes)
                    {
                        sb.AppendLine(string.Format("{0}    {1}    {2}.{3}", change.Status, change.ObjectType, change.Schema, change.Name));
                        if (change.ColumnChanges != null)
                        {
                            foreach (DatabaseColumnChange column in change.ColumnChanges)
                            {
                                sb.AppendLine(string.Format("     {0}    Column    {1}", column.Status, column.NewName ?? column.OldName));
                            }
                        }
                        sb.AppendLine();
                    }
                }

                txtLogs.AppendText(sb.ToString());
                string message = GitDatabase.SuggestCommitMessage(_repositoryPath);
                txtCommitMessage.Text = message;
            }
            catch (Exception ex)
            {
                ProgressManager.Error(ex.Message);
                ShowMessage(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ProgressManager.Finish();
                SetBusy(false);
            }
        }

        private async void btnCommit_Click(object sender, EventArgs e)
        {
            if (!_connected)
            {
                ShowMessage("ابتدا به Repository متصل شوید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string message = txtCommitMessage.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                ShowMessage("عنوان Commit را وارد کنید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCommitMessage.Focus();
                return;
            }

            DialogResult confirm = ShowMessage("تغییرات Database ذخیره و Commit شوند؟\r\n\r\nعنوان:\r\n" + message, "Commit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                SetBusy(true);
                GitDatabase.ConnectionString = txtConnectionString.Text;
                _repositoryPath = txtRepositoryPath.Text.Trim();
                txtLogs.Clear();

                ProgressManager.Start("Commit Database");
                ProgressManager.ReportStage("Commit", "شروع Commit؛ در صورت وجود Scan قبلی از همان نتیجه استفاده می‌شود.", 0);

                string result = await Task.Run(delegate
                {
                    return GitDatabase.SaveChanges(_repositoryPath, message);
                });

                txtLogs.AppendText("\r\nCommit انجام شد:\r\n" + result + Environment.NewLine);
                ShowMessage("تغییرات با موفقیت Commit شدند.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ProgressManager.Error(ex.Message);
                ShowMessage(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ProgressManager.Finish();
                SetBusy(false);
            }
        }

        private void btnLog_Click(object sender, EventArgs e)
        {
            if (!_connected)
            {
                ShowMessage("ابتدا به Repository متصل شوید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string result = GitDatabase.Log(txtRepositoryPath.Text.Trim());
                txtLogs.Text = string.IsNullOrWhiteSpace(result) ? "هیچ Commitای ثبت نشده است." : "Git Log:\r\n\r\n" + result;
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                string path = txtRepositoryPath.Text.Trim();
                if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "مسیر Repository را انتخاب کنید";
                dialog.ShowNewFolderButton = true;
                if (Directory.Exists(txtRepositoryPath.Text)) dialog.SelectedPath = txtRepositoryPath.Text;
                if (dialog.ShowDialog() != DialogResult.OK) return;

                txtRepositoryPath.Text = dialog.SelectedPath;
                _repositoryPath = dialog.SelectedPath;
                GitDatabase.ClearCache();
                _connected = GitHelper.IsRepository(_repositoryPath);

                btnStatus.Enabled = _connected;
                btnCommit.Enabled = _connected;
                btnLog.Enabled = _connected;
                btnConnect.Enabled = true;
                btnConnect.Text = _connected ? "اتصال مجدد" : "اتصال";
                btnConnect.BackColor = SystemColors.Control;

                txtLogs.Text = _connected ? "Repository شناسایی شد:\r\n" + _repositoryPath : "این پوشه هنوز Git Repository نیست:\r\n" + _repositoryPath;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            txtLogs.RightToLeft = txtLogs.RightToLeft == RightToLeft.Yes ? RightToLeft.No : RightToLeft.Yes;
        }

        private void pictureBox3_Click(object sender, EventArgs e) { }

        private void SetBusy(bool busy)
        {
            btnStatus.Enabled = !busy && _connected;
            btnCommit.Enabled = !busy && _connected;
            btnLog.Enabled = !busy && _connected;
            btnConnect.Enabled = !busy && !_connected;
        }

        private static DialogResult ShowMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(message, title, buttons, icon);
        }
    }
}
