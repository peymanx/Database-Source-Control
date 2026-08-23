using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Barsa.EndUserGitUI.Git
{
    public static class GitDatabase
    {
        public static string ConnectionString { get; set; }

        private static List<DatabaseObjectInfo> _cachedObjects;
        private static List<DatabaseChange> _cachedChanges;
        private static string _cachedRepository;
        private static string _cachedConnection;

        public static string Init(string repositoryPath)
        {
            ValidateRepositoryPath(repositoryPath);
            EnsureDirectory(repositoryPath);
            if (GitHelper.IsRepository(repositoryPath))
                return "Repository قبلاً ایجاد شده است.";

            ProgressManager.ReportStage("Git", "در حال ایجاد Repository...", 2);
            string result = GitHelper.Init(repositoryPath);
            ProgressManager.Log("Repository ایجاد شد.");
            return result;
        }

        public static List<DatabaseChange> GetChanges(string repositoryPath)
        {
            ValidateRepositoryPath(repositoryPath);
            ValidateConnectionString();
            // Status باید همیشه وضعیت واقعی Database را بخواند؛
            // Cache قدیمی می‌توانست باعث شود تغییرات تازه تا باز و بسته کردن برنامه دیده نشوند.
            EnsureScan(repositoryPath, true);
            return new List<DatabaseChange>(_cachedChanges);
        }

        public static void ClearCache()
        {
            _cachedObjects = null;
            _cachedChanges = null;
            _cachedRepository = null;
            _cachedConnection = null;
        }

        public static string SuggestCommitMessage(string repositoryPath)
        {
            List<DatabaseChange> changes = GetChanges(repositoryPath);
            return CommitMessageGenerator.Generate(changes);
        }

        public static string SaveChanges(string repositoryPath, string commitMessage)
        {
            ValidateRepositoryPath(repositoryPath);
            ValidateConnectionString();
            if (string.IsNullOrWhiteSpace(commitMessage))
                throw new ArgumentException("Commit message is required.");

            // اگر Status قبلاً اجرا شده باشد از همان Scan استفاده می‌کنیم تا Database بزرگ دوباره Scan نشود.
            // اگر Cache متعلق به این Repository/Connection نباشد، Scan انجام می‌شود.
            EnsureScan(repositoryPath, false);

            if (_cachedChanges == null || _cachedChanges.Count == 0)
                return "No changes.";

            ProgressManager.ReportStage("مرحله 4 از 7", "در حال ذخیره فقط فایل‌های SQL تغییرکرده...", 65);
            List<string> changedPaths = SaveDatabaseFiles(repositoryPath, _cachedObjects, _cachedChanges);

            ProgressManager.ReportStage("مرحله 5 از 7", "در حال ذخیره Snapshot متادیتا...", 78);
            SaveSnapshot(repositoryPath, _cachedObjects);
            changedPaths.Add(ToRepositoryPath(Path.Combine(".dbgit", "snapshot.json")));

            ProgressManager.ReportStage("مرحله 6 از 7", "در حال اجرای Git Add فقط روی فایل‌های تغییرکرده...", 88);
            GitHelper.AddPaths(repositoryPath, changedPaths);
            ProgressManager.Log("Git Add فقط روی " + changedPaths.Count + " فایل/مسیر تغییرکرده انجام شد.");

            ProgressManager.ReportStage("مرحله 7 از 7", "در حال Commit کردن تغییرات...", 94);
            string result = GitHelper.Commit(repositoryPath, commitMessage);
            ProgressManager.Log("Git Commit با موفقیت انجام شد.");

            _cachedObjects = null;
            _cachedChanges = null;
            return result;
        }

        public static string Status(string repositoryPath)
        {
            ValidateRepositoryPath(repositoryPath);
            return GitHelper.Status(repositoryPath).TrimEnd();
        }

        public static string Log(string repositoryPath)
        {
            ValidateRepositoryPath(repositoryPath);
            string result = GitHelper.Log(repositoryPath);
            if (string.IsNullOrWhiteSpace(result)) return "";

            string[] records = result.Split(new[] { '\u001e' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder output = new StringBuilder();
            foreach (string rawRecord in records)
            {
                string record = rawRecord.Trim('\r', '\n');
                if (string.IsNullOrWhiteSpace(record)) continue;

                string[] lines = record.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                if (lines.Length == 0) continue;

                string[] parts = lines[0].Split(new[] { '|' }, 3);
                if (parts.Length == 3)
                {
                    output.AppendLine("Commit : " + parts[0]);
                    output.AppendLine("Author : " + parts[1]);
                    output.AppendLine("Date   : " + parts[2]);
                    output.AppendLine();

                    bool firstMessageLine = true;
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (firstMessageLine && string.IsNullOrWhiteSpace(lines[i]))
                            continue;
                        output.AppendLine(firstMessageLine ? "Message: " + lines[i] : lines[i]);
                        firstMessageLine = false;
                    }
                    output.AppendLine("----------------------------------------");
                }
                else
                {
                    output.AppendLine(record);
                }
            }
            return output.ToString().TrimEnd();
        }

        private static void EnsureScan(string repositoryPath, bool force)
        {
            if (!force && _cachedObjects != null && _cachedChanges != null &&
                string.Equals(_cachedRepository, repositoryPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_cachedConnection, ConnectionString, StringComparison.Ordinal))
            {
                ProgressManager.ReportStage("Cache", "نتیجه Scan قبلی استفاده می‌شود؛ Scan دوباره انجام نمی‌شود.", 60);
                return;
            }

            ProgressManager.ReportStage("مرحله 1 از 7", "در حال خواندن Database...", 5);
            DateTime start = DateTime.Now;
            List<DatabaseObjectInfo> current = DatabaseDiff.Scan(ConnectionString);
            ProgressManager.Log("Scan Database تمام شد. زمان: " + (DateTime.Now - start).TotalSeconds.ToString("0.0") + " ثانیه");

            ProgressManager.ReportStage("مرحله 2 از 7", "در حال خواندن Snapshot قبلی...", 40);
            List<DatabaseObjectInfo> oldSnapshot = LoadSnapshot(repositoryPath);
            ProgressManager.Log("Snapshot قبلی شامل " + oldSnapshot.Count + " Object است.");

            ProgressManager.ReportStage("مرحله 3 از 7", "در حال مقایسه Snapshot با Database فعلی...", 55);
            List<DatabaseChange> changes = DatabaseDiff.Compare(oldSnapshot, current);
            ProgressManager.Log("تعداد تغییرات پیدا شده: " + changes.Count);

            _cachedObjects = current;
            _cachedChanges = changes;
            _cachedRepository = repositoryPath;
            _cachedConnection = ConnectionString;
        }

        private static List<string> SaveDatabaseFiles(string repositoryPath, List<DatabaseObjectInfo> objects, List<DatabaseChange> changes)
        {
            string databasePath = Path.Combine(repositoryPath, "database");
            EnsureDirectory(databasePath);

            Dictionary<string, DatabaseObjectInfo> currentMap = objects.ToDictionary(ObjectKey, StringComparer.OrdinalIgnoreCase);
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // فقط Objectهایی که واقعاً تغییر کرده‌اند روی دیسک نوشته می‌شوند.
            // در Databaseهای بزرگ این بخش از O(total objects) به O(changed objects) تبدیل می‌شود.
            int index = 0;
            int total = changes == null ? 0 : changes.Count;

            foreach (DatabaseChange change in changes ?? new List<DatabaseChange>())
            {
                string key = change.ObjectType + "|" + change.Schema + "|" + change.Name;
                DatabaseObjectInfo obj;
                string relativePath = GetRelativeObjectPath(change.ObjectType, change.Schema, change.Name);
                if (relativePath == null) continue;

                string filePath = Path.Combine(repositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                paths.Add(relativePath);

                if (change.Status == "D")
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        ProgressManager.Log("فایل Object حذف شد: " + relativePath);
                    }
                }
                else if (currentMap.TryGetValue(key, out obj))
                {
                    string folderPath = Path.GetDirectoryName(filePath);
                    EnsureDirectory(folderPath);

                    string content = obj.Type == DatabaseObjectType.Table
                        ? GenerateTableScript(obj)
                        : (obj.Definition ?? "");

                    // فایل‌های SQL را همیشه با LF ذخیره می‌کنیم تا با Git و .gitattributes هماهنگ باشند.
                    content = NormalizeForGit(content);
                    File.WriteAllText(filePath, content, new UTF8Encoding(false));
                }

                index++;
                if (index == 1 || index == total || index % 25 == 0)
                    ProgressManager.ReportItem("پردازش تغییر: " + change.Schema + "." + change.Name, index, Math.Max(1, total), 65 + (int)(index * 10.0 / Math.Max(1, total)));
            }

            return paths.ToList();
        }

        private static string GetRelativeObjectPath(DatabaseObjectType type, string schema, string name)
        {
            string folder = GetFolder(type);
            if (folder == null) return null;
            return "database/" + folder + "/" + MakeSafeFileName(schema + "." + name) + ".sql";
        }

        private static string ObjectKey(DatabaseObjectInfo obj)
        {
            return obj.Type + "|" + obj.Schema + "|" + obj.Name;
        }

        private static string ToRepositoryPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string NormalizeForGit(string content)
        {
            if (string.IsNullOrEmpty(content)) return content ?? string.Empty;
            return content.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string GetFolder(DatabaseObjectType type)
        {
            switch (type)
            {
                case DatabaseObjectType.Table: return "tables";
                case DatabaseObjectType.View: return "views";
                case DatabaseObjectType.Procedure: return "procedures";
                case DatabaseObjectType.Function: return "functions";
                case DatabaseObjectType.Trigger: return "triggers";
                default: return null;
            }
        }

        private static string GenerateTableScript(DatabaseObjectInfo table)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-- Table: [" + table.Schema + "].[" + table.Name + "]");
            sb.AppendLine();
            sb.AppendLine("CREATE TABLE [" + table.Schema + "].[" + table.Name + "]");
            sb.AppendLine("(");
            for (int i = 0; i < table.Columns.Count; i++)
            {
                DatabaseColumnInfo c = table.Columns[i];
                sb.Append("    [" + c.ColumnName + "] " + FormatSqlDataType(c));
                if (c.IsComputed) sb.Append(" /* COMPUTED */");
                else sb.Append(c.IsNullable ? " NULL" : " NOT NULL");
                if (i < table.Columns.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine(");");
            return sb.ToString();
        }

        private static string FormatSqlDataType(DatabaseColumnInfo c)
        {
            string type = c.DataType ?? "";
            if (type.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) || type.Equals("nchar", StringComparison.OrdinalIgnoreCase))
                return type + (c.MaxLength == -1 ? "(MAX)" : "(" + (c.MaxLength / 2) + ")");
            if (type.Equals("varchar", StringComparison.OrdinalIgnoreCase) || type.Equals("char", StringComparison.OrdinalIgnoreCase) || type.Equals("varbinary", StringComparison.OrdinalIgnoreCase) || type.Equals("binary", StringComparison.OrdinalIgnoreCase))
                return type + (c.MaxLength == -1 ? "(MAX)" : "(" + c.MaxLength + ")");
            if (type.Equals("decimal", StringComparison.OrdinalIgnoreCase) || type.Equals("numeric", StringComparison.OrdinalIgnoreCase))
                return type + "(" + c.Precision + "," + c.Scale + ")";
            return type;
        }

        private static void SaveSnapshot(string repositoryPath, List<DatabaseObjectInfo> snapshot)
        {
            string folder = Path.Combine(repositoryPath, ".dbgit");
            EnsureDirectory(folder);
            string file = Path.Combine(folder, "snapshot.json");
            string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            File.WriteAllText(file, NormalizeForGit(json), new UTF8Encoding(false));
            ProgressManager.Log("snapshot.json ذخیره شد. حجم: " + new FileInfo(file).Length.ToString("N0") + " bytes");
        }

        private static List<DatabaseObjectInfo> LoadSnapshot(string repositoryPath)
        {
            string file = Path.Combine(repositoryPath, ".dbgit", "snapshot.json");
            if (!File.Exists(file))
            {
                ProgressManager.Log("Snapshot قبلی وجود ندارد؛ این Commit به‌عنوان اولین Snapshot در نظر گرفته می‌شود.");
                return new List<DatabaseObjectInfo>();
            }

            DateTime start = DateTime.Now;
            string json = File.ReadAllText(file, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return new List<DatabaseObjectInfo>();
            List<DatabaseObjectInfo> snapshot = JsonConvert.DeserializeObject<List<DatabaseObjectInfo>>(json);
            ProgressManager.Log("خواندن Snapshot در " + (DateTime.Now - start).TotalSeconds.ToString("0.0") + " ثانیه انجام شد.");
            return snapshot ?? new List<DatabaseObjectInfo>();
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "_");
            return name;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static void ValidateRepositoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Repository path is required.");
        }

        private static void ValidateConnectionString()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString)) throw new InvalidOperationException("ConnectionString تنظیم نشده است.");
        }
    }
}
