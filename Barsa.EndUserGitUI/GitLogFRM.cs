using Barsa.EndUserGitUI.Git;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Barsa.EndUserGitUI
{
    public partial class GitLogFRM : Form
    {
        public string RepositoryPath { get; set; }
        public GitLogFRM(string path)
        {
            InitializeComponent();
            RepositoryPath = path;
        }

        private void LogHistoryFRM_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(RepositoryPath)) return;
            string result = GitDatabase.Log(RepositoryPath.Trim());
            txtLogs.Text = string.IsNullOrWhiteSpace(result) ? "هیچ Commitای ثبت نشده است." : "Git Log:\r\n\r\n" + result;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            txtLogs.RightToLeft = txtLogs.RightToLeft == RightToLeft.Yes ? RightToLeft.No : RightToLeft.Yes;

        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLogs.Clear();

        }
    }
}
