using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Barsa.EndUserGitUI.Git
{
    public static class GitHelper
    {
        public static string Init(string repositoryPath)
        {
            return Execute(repositoryPath, "init");
        }

        public static bool IsRepository(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath)) return false;
            return Directory.Exists(Path.Combine(repositoryPath, ".git"));
        }

        /// <summary>
        /// Git را برای Repository دیتابیس روی LF تنظیم می‌کند.
        /// این تنظیم جلوی Warningهای مکرر CRLF/LF در Windows را می‌گیرد.
        /// تنظیمات به صورت local در همان Repository ذخیره می‌شوند.
        /// </summary>
        public static void ConfigureLineEndings(string repositoryPath)
        {
            if (!IsRepository(repositoryPath)) return;

            // Database source files are generated as LF. Do not let the
            // Windows global/user Git configuration convert them to CRLF.
            Execute(repositoryPath, "config core.autocrlf false");
            Execute(repositoryPath, "config core.eol lf");
        }

        public static string Add(string repositoryPath)
        {
            ConfigureLineEndings(repositoryPath);
            return Execute(repositoryPath, "add .");
        }

        /// <summary>
        /// فقط pathهای تغییرکرده را در یک invocation به Git می‌دهد.
        /// pathspec-from-file محدودیت طول command line ویندوز را هم دور می‌زند.
        /// </summary>
        public static string AddPaths(string repositoryPath, IEnumerable<string> paths)
        {
            if (paths == null) return string.Empty;

            List<string> normalized = new List<string>();
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                string p = path.Replace('\\', '/').TrimStart('/');
                if (!normalized.Contains(p, StringComparer.OrdinalIgnoreCase))
                    normalized.Add(p);
            }

            if (normalized.Count == 0) return string.Empty;

            // Important: configure the repository itself, not the user's
            // global Git config. This is safe for other repositories.
            ConfigureLineEndings(repositoryPath);
            return ExecuteWithInput(repositoryPath, "add --pathspec-from-file=- --pathspec-file-nul", normalized);
        }

        public static string Commit(string repositoryPath, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Commit message is required.");
            return ExecuteWithTextInput(repositoryPath, "commit -F -", message);
        }

        public static string Status(string repositoryPath)
        {
            return Execute(repositoryPath, "status --short");
        }

        public static string Log(string repositoryPath)
        {
            return Execute(repositoryPath, "log --pretty=format:\"%h|%an|%ad%n%B%x1e\" --date=format:\"%Y-%m-%d %H:%M:%S\"");
        }

        private static string ExecuteWithTextInput(string repositoryPath, string arguments, string input)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
                throw new ArgumentException("Repository path is required.");
            if (!Directory.Exists(repositoryPath))
                throw new DirectoryNotFoundException(repositoryPath);

            ProcessStartInfo psi = CreateStartInfo(repositoryPath, arguments);
            psi.RedirectStandardInput = true;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                using (StreamWriter writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                {
                    writer.Write(input ?? string.Empty);
                }

                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);

                string output = outputTask.Result;
                string error = errorTask.Result;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Git Error:\r\n" + error);
                return output;
            }
        }

        private static string ExecuteWithInput(string repositoryPath, string arguments, IEnumerable<string> inputLines)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
                throw new ArgumentException("Repository path is required.");
            if (!Directory.Exists(repositoryPath))
                throw new DirectoryNotFoundException(repositoryPath);

            ProcessStartInfo psi = CreateStartInfo(repositoryPath, arguments);
            psi.RedirectStandardInput = true;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();

                using (StreamWriter writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                {
                    foreach (string line in inputLines)
                    {
                        writer.Write(line);
                        writer.Write('\0');
                    }
                }

                // Read STDOUT and STDERR concurrently.
                // Git can emit thousands of CRLF warnings to STDERR; reading
                // one pipe completely before the other can deadlock when the
                // STDERR pipe buffer becomes full. That is the main reason
                // git add could appear to hang for a very long time.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);

                string output = outputTask.Result;
                string error = errorTask.Result;

                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Git Error:\r\n" + error);

                return output;
            }
        }

        private static string Execute(string repositoryPath, string arguments)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
                throw new ArgumentException("Repository path is required.");
            if (!Directory.Exists(repositoryPath))
                throw new DirectoryNotFoundException(repositoryPath);

            ProcessStartInfo psi = CreateStartInfo(repositoryPath, arguments);

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();

                // Read STDOUT and STDERR concurrently.
                // Git can emit thousands of CRLF warnings to STDERR; reading
                // one pipe completely before the other can deadlock when the
                // STDERR pipe buffer becomes full. That is the main reason
                // git add could appear to hang for a very long time.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                process.WaitForExit();
                Task.WaitAll(outputTask, errorTask);

                string output = outputTask.Result;
                string error = errorTask.Result;

                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Git Error:\r\n" + error);

                return output;
            }
        }

        private static ProcessStartInfo CreateStartInfo(string repositoryPath, string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "git.exe";
            psi.Arguments = arguments;
            psi.WorkingDirectory = repositoryPath;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            return psi;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
