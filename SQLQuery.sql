SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_computed
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.columns c ON c.object_id = t.object_id
LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name, c.column_id;


SELECT
    CASE o.type
        WHEN 'V' THEN 1
        WHEN 'P' THEN 2
        WHEN 'FN' THEN 3
        WHEN 'IF' THEN 3
        WHEN 'TF' THEN 3
        ELSE 0
    END AS ObjectType,
    s.name AS SchemaName,
    o.name AS ObjectName,
    ISNULL(sm.definition, '') AS Definition
FROM sys.objects o
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules sm ON sm.object_id = o.object_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('V'--, 'P', 'FN', 'IF', 'TF'
  )
ORDER BY s.name, o.name;

DECLARE @TableName NVARCHAR(128) = N'dyn41';

SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnId,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.precision AS Precision,
    c.scale AS Scale,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.columns c
    ON c.object_id = t.object_id
INNER JOIN sys.types ty
    ON ty.user_type_id = c.user_type_id
WHERE t.name = @TableName
ORDER BY
    c.column_id;