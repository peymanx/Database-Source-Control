using System;
using System.Collections.Generic;
using System.Text;

namespace Barsa.EndUserGitUI.Git
{
    public static class CommitMessageGenerator
    {
        public static string Generate(List<DatabaseChange> changes)
        {
            if (changes == null || changes.Count == 0)
                return "بدون تغییر";

            List<string> objectMessages = new List<string>();
            List<string> columnMessages = new List<string>();

            foreach (DatabaseChange change in changes)
            {
                if (change.ObjectType == DatabaseObjectType.Table &&
                    change.ColumnChanges != null &&
                    change.ColumnChanges.Count > 0)
                {
                    foreach (DatabaseColumnChange column in change.ColumnChanges)
                    {
                        columnMessages.Add(
                            GenerateColumnMessage(change, column)
                        );
                    }
                }
                else
                {
                    objectMessages.Add(
                        GenerateObjectMessage(change)
                    );
                }
            }

            const int maxDetails = 6;

            StringBuilder sb = new StringBuilder();

            // ==========================================
            // موجودیت‌ها
            // ==========================================
            if (objectMessages.Count > 0)
            {
                sb.AppendLine("این موجودیت ها تغییر کرده اند:");

                int objectCount = Math.Min(
                    objectMessages.Count,
                    maxDetails
                );

                for (int i = 0; i < objectCount; i++)
                {
                    sb.AppendLine(objectMessages[i]);
                }

                if (objectMessages.Count > maxDetails)
                {
                    sb.AppendLine(
                        "و " +
                        (objectMessages.Count - maxDetails) +
                        " موجودیت دیگر"
                    );
                }
            }

            // ==========================================
            // تغییرات فیلدها
            // ==========================================
            if (columnMessages.Count > 0)
            {
                if (objectMessages.Count > 0)
                    sb.AppendLine();

                int columnCount = Math.Min(
                    columnMessages.Count,
                    maxDetails
                );

                for (int i = 0; i < columnCount; i++)
                {
                    sb.AppendLine(columnMessages[i]);
                }

                if (columnMessages.Count > maxDetails)
                {
                    sb.AppendLine(
                        "و " +
                        (columnMessages.Count - maxDetails) +
                        " تغییر دیگر"
                    );
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// گزارش ساختاریافته و قابل جستجو برای انتهای Git Log.
        /// این متن داخل Body همان Commit ذخیره می‌شود
        /// تا تاریخچه Git نیز آن را نگه دارد.
        /// </summary>
        public static string GenerateDatabaseLog(
            List<DatabaseChange> changes)
        {
            if (changes == null || changes.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("تغییرات Database:");
            sb.AppendLine("============================");
            sb.AppendLine();

            foreach (DatabaseChange change in changes)
            {
                sb.AppendLine(
                    string.Format(
                        "{0}    {1}    {2}.{3}",
                        change.Status,
                        TypeName(change.ObjectType),
                        change.Schema,
                        change.Name
                    )
                );

                sb.AppendLine();

                if (change.ColumnChanges != null)
                {
                    foreach (
                        DatabaseColumnChange column
                        in change.ColumnChanges)
                    {
                        string columnName =
                            column.NewName ??
                            column.OldName ??
                            "";

                        if (column.ChangeType ==
                            ColumnChangeType.Renamed)
                        {
                            columnName =
                                column.OldName +
                                " -> " +
                                column.NewName;
                        }

                        sb.AppendLine(
                            string.Format(
                                "     {0}    Column    {1}",
                                column.Status,
                                columnName
                            )
                        );
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string GenerateColumnMessage(
            DatabaseChange change,
            DatabaseColumnChange column)
        {
            string entity = change.Name;

            switch (column.ChangeType)
            {
                case ColumnChangeType.Added:

                    return
                        "فیلد " +
                        column.NewName +
                        " به موجودیت " +
                        entity +
                        " اضافه شد";

                case ColumnChangeType.Deleted:

                    return
                        "فیلد " +
                        column.OldName +
                        " از موجودیت " +
                        entity +
                        " حذف شد";

                case ColumnChangeType.Renamed:

                    return
                        "نام فیلد " +
                        column.OldName +
                        " در موجودیت " +
                        entity +
                        " به " +
                        column.NewName +
                        " تغییر کرد";

                case ColumnChangeType.Modified:

                    return
                        "مشخصات فیلد " +
                        column.NewName +
                        " در موجودیت " +
                        entity +
                        " تغییر کرد";

                default:

                    return
                        "فیلد " +
                        (column.NewName ?? column.OldName) +
                        " در موجودیت " +
                        entity +
                        " تغییر کرد";
            }
        }

        private static string GenerateObjectMessage(
            DatabaseChange change)
        {
            string target =
                change.Schema +
                "." +
                change.Name;

            string type =
                TypeName(change.ObjectType);

            if (change.Status == "A")
            {
                return
                    type +
                    " " +
                    target +
                    " ایجاد شد";
            }

            if (change.Status == "D")
            {
                return
                    type +
                    " " +
                    target +
                    " حذف شد";
            }

            return
                type +
                " " +
                target +
                " تغییر کرد";
        }

        private static string TypeName(
            DatabaseObjectType type)
        {
            switch (type)
            {
                case DatabaseObjectType.Table:
                    return "جدول";

                case DatabaseObjectType.View:
                    return "View";

                case DatabaseObjectType.Procedure:
                    return "Procedure";

                case DatabaseObjectType.Function:
                    return "Function";

                case DatabaseObjectType.Trigger:
                    return "Trigger";

                default:
                    return "Object";
            }
        }
    }
}