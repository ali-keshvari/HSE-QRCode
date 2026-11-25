using System;
using System.Collections.Generic;
using System.Linq;

namespace HSEQRCode.Data
{
    /// <summary>
    /// Parameterized SQL Server view introspection queries and safe identifier quoting helpers.
    /// </summary>
    public static class SqlViewMetadataQueries
    {
        /// <summary>
        /// Lists all user-defined SQL Server views across non-system schemas.
        /// </summary>
        public const string GetAllViewsSql = @"
SELECT
    s.name AS SchemaName,
    v.name AS ViewName,
    s.name + N'.' + v.name AS FullViewName
FROM sys.views AS v
INNER JOIN sys.schemas AS s ON v.schema_id = s.schema_id
WHERE v.is_ms_shipped = 0
  AND s.name NOT IN (N'sys', N'INFORMATION_SCHEMA')
ORDER BY s.name, v.name;";

        /// <summary>
        /// Checks whether a view exists in INFORMATION_SCHEMA.VIEWS.
        /// Expects parameters: @SchemaName (nvarchar), @ViewName (nvarchar).
        /// </summary>
        public const string ViewExistsSql = @"
SELECT COUNT(1)
FROM INFORMATION_SCHEMA.VIEWS
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @ViewName;";

        /// <summary>
        /// Returns column metadata for a specific SQL Server view.
        /// Expects parameters: @SchemaName (nvarchar), @ViewName (nvarchar).
        /// </summary>
        public const string GetViewColumnsSql = @"
SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.is_nullable AS IsNullable,
    CAST(c.max_length AS int) AS MaxLength,
    c.column_id AS ColumnOrder
FROM sys.views AS v
INNER JOIN sys.schemas AS s ON v.schema_id = s.schema_id
INNER JOIN sys.columns AS c ON v.object_id = c.object_id
INNER JOIN sys.types AS t ON c.user_type_id = t.user_type_id
WHERE s.name = @SchemaName
  AND v.name = @ViewName
ORDER BY c.column_id;";

        /// <summary>
        /// Safely wraps a single SQL Server identifier in square brackets, escaping any closing brackets.
        /// </summary>
        public static string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("SQL identifier cannot be null or empty.", nameof(identifier));
            }

            string trimmed = identifier.Trim();

            // Strip outer brackets if caller passed an already-bracketed single identifier
            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Replace("]]", "]");
            }

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 128)
            {
                throw new ArgumentException("SQL identifier length is invalid.", nameof(identifier));
            }

            foreach (char ch in trimmed)
            {
                if (char.IsControl(ch) || ch == '\0')
                {
                    throw new ArgumentException("SQL identifier contains invalid control characters.", nameof(identifier));
                }
            }

            return "[" + trimmed.Replace("]", "]]") + "]";
        }

        /// <summary>
        /// Parses a view name (e.g., "gen.vw_FireExtinguishers_1001" or "vw_Test") into schema and view names.
        /// </summary>
        public static void ParseViewName(string fullViewName, out string schemaName, out string viewName)
        {
            if (string.IsNullOrWhiteSpace(fullViewName))
            {
                throw new ArgumentException("View name cannot be null or empty.", nameof(fullViewName));
            }

            string trimmed = fullViewName.Trim();
            int dotIndex = FindUnquotedDot(trimmed);

            if (dotIndex < 0)
            {
                schemaName = "dbo";
                viewName = UnquoteIdentifier(trimmed);
            }
            else
            {
                string rawSchema = trimmed.Substring(0, dotIndex);
                string rawView = trimmed.Substring(dotIndex + 1);

                if (FindUnquotedDot(rawView) >= 0)
                {
                    throw new ArgumentException("Only two-part view names (schema.view) are supported.", nameof(fullViewName));
                }

                schemaName = UnquoteIdentifier(rawSchema);
                viewName = UnquoteIdentifier(rawView);
            }
        }

        /// <summary>
        /// Quotes a two-part SQL Server object name (e.g., "[gen].[vw_FireExtinguishers_1001]").
        /// </summary>
        public static string QuoteTwoPartName(string fullViewName)
        {
            string schemaName;
            string viewName;
            ParseViewName(fullViewName, out schemaName, out viewName);
            return QuoteIdentifier(schemaName) + "." + QuoteIdentifier(viewName);
        }

        /// <summary>
        /// Builds a safe SELECT statement for the given view and column list.
        /// </summary>
        public static string BuildSelectSql(string fullViewName, IEnumerable<string> columnNames)
        {
            if (columnNames == null)
            {
                throw new ArgumentNullException(nameof(columnNames));
            }

            List<string> quotedCols = columnNames.Select(QuoteIdentifier).ToList();
            if (quotedCols.Count == 0)
            {
                throw new ArgumentException("At least one column name is required.", nameof(columnNames));
            }

            return "SELECT " + string.Join(", ", quotedCols) + " FROM " + QuoteTwoPartName(fullViewName);
        }

        private static string UnquoteIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier segment cannot be empty.", nameof(identifier));
            }

            string trimmed = identifier.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Replace("]]", "]");
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new ArgumentException("Identifier segment cannot be empty.", nameof(identifier));
            }

            return trimmed;
        }

        private static int FindUnquotedDot(string value)
        {
            bool inBrackets = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch == '[')
                {
                    inBrackets = true;
                }
                else if (ch == ']')
                {
                    if (i + 1 < value.Length && value[i + 1] == ']')
                    {
                        i++;
                    }
                    else
                    {
                        inBrackets = false;
                    }
                }
                else if (ch == '.' && !inBrackets)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
