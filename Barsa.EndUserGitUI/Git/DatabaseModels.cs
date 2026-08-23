using System;
using System.Collections.Generic;

namespace Barsa.EndUserGitUI.Git
{
    public enum DatabaseObjectType
    {
        Table,
        View,
        Procedure,
        Function,
        Trigger
    }

    public enum ColumnChangeType
    {
        Added,
        Deleted,
        Renamed,
        Modified
    }

    public class DatabaseColumnInfo
    {
        public string ColumnName { get; set; }
        public int Ordinal { get; set; }
        public string DataType { get; set; }
        public int MaxLength { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public bool IsNullable { get; set; }
        public bool IsComputed { get; set; }
    }

    public class DatabaseObjectInfo
    {
        public DatabaseObjectType Type { get; set; }
        public string Schema { get; set; }
        public string Name { get; set; }
        public string Definition { get; set; }
        public List<DatabaseColumnInfo> Columns { get; set; }

        public DatabaseObjectInfo()
        {
            Columns = new List<DatabaseColumnInfo>();
        }
    }

    public class DatabaseColumnChange
    {
        public ColumnChangeType ChangeType { get; set; }
        public string OldName { get; set; }
        public string NewName { get; set; }
        public string OldDataType { get; set; }
        public string NewDataType { get; set; }
        public int OldMaxLength { get; set; }
        public int NewMaxLength { get; set; }
        public bool OldNullable { get; set; }
        public bool NewNullable { get; set; }
        public int OldOrdinal { get; set; }
        public int NewOrdinal { get; set; }

        public string Status
        {
            get
            {
                switch (ChangeType)
                {
                    case ColumnChangeType.Added: return "A";
                    case ColumnChangeType.Deleted: return "D";
                    case ColumnChangeType.Renamed: return "R";
                    default: return "M";
                }
            }
        }
    }

    public class DatabaseChange
    {
        public string Status { get; set; }
        public DatabaseObjectType ObjectType { get; set; }
        public string Schema { get; set; }
        public string Name { get; set; }
        public List<DatabaseColumnChange> ColumnChanges { get; set; }

        public DatabaseChange()
        {
            ColumnChanges = new List<DatabaseColumnChange>();
        }
    }

    public class DatabaseScanResult
    {
        public List<DatabaseObjectInfo> Objects { get; set; }
        public List<DatabaseChange> Changes { get; set; }

        public DatabaseScanResult()
        {
            Objects = new List<DatabaseObjectInfo>();
            Changes = new List<DatabaseChange>();
        }
    }
}
