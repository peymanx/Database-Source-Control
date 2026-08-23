using Barsa.EndUserGitUI.Git;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Barsa.EndUserGitUI
{
    public partial class GitStatusFRM : Form
    {
        public string _repositoryPath { get; set; }

        private bool _connected;
        private bool _auto_commit;

        public string _connection { get; set; }
        public GitStatusFRM(string path, string connection, bool auto_commit = false)
        {
            InitializeComponent();
            _repositoryPath = path;
            _connection = connection;
            _auto_commit = auto_commit;
          

        }


        private void ConnectToDB()
        {
            try
            {
                string path = _repositoryPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    MessageBox.Show("مسیر Repository را وارد کنید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                GitDatabase.ConnectionString = _connection;
                GitDatabase.ClearCache();
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                if (!GitHelper.IsRepository(path))
                {
                    string result = GitDatabase.Init(path);
                    
                }
                else
                {
                    
                }

                _repositoryPath = path;
                _connected = true;


                btnCommit.Enabled = true;
            

            }
            catch (Exception ex)
            {
                _connected = false;
                MessageBox.Show(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async void LogHistoryFRM_Load(object sender, EventArgs e)
        {
            await GitStatus();

            if(_auto_commit)
                await GitCommit();
        }

        private async Task GitStatus()
        {
            ConnectToDB();

            try
            {



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

                txtLogs.Text = sb.ToString();
                string message = GitDatabase.SuggestCommitMessage(_repositoryPath);
                txtCommitMessage.Text = message;
            }
            catch (Exception ex)
            {
                ProgressManager.Error(ex.Message);
                MessageBox.Show(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ProgressManager.Finish();


            }

            btnCommit.Enabled = !(txtCommitMessage.Text == "بدون تغییر");

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private async void btnCommit_Click(object sender, EventArgs e)
        {
          
            await GitCommit();
            

        }

        private async Task<bool> GitCommit()
        {
            if (!_connected)
            {
                MessageBox.Show("ابتدا به Repository متصل شوید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string message = txtCommitMessage.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("عنوان Commit را وارد کنید.", "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCommitMessage.Focus();
                return false;
            }

            DialogResult confirm = MessageBox.Show("تغییرات Database ذخیره و Commit شوند؟\r\n\r\nعنوان:\r\n" + message, "Commit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return false;

            try
            {

                GitDatabase.ConnectionString = _connection;
                _repositoryPath = _repositoryPath;
                txtLogs.Clear();

                ProgressManager.Start("Commit Database");
                ProgressManager.ReportStage("Commit", "شروع Commit؛ در صورت وجود Scan قبلی از همان نتیجه استفاده می‌شود.", 0);

                string result = await Task.Run(delegate
                {
                    return GitDatabase.SaveChanges(_repositoryPath, message);
                });

                txtLogs.AppendText("\r\nCommit انجام شد:\r\n" + result + Environment.NewLine);
                MessageBox.Show("تغییرات با موفقیت Commit شدند.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ProgressManager.Error(ex.Message);
                MessageBox.Show(ex.Message, "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ProgressManager.Finish();


            }

            this.Close();
            return true;
        }
    }
}
