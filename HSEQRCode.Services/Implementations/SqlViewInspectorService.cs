using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using HSEQRCode.Data;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Domain.Enums;
using HSEQRCode.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSEQRCode.Services.Implementations
{
    public class SqlViewInspectorService : ISqlViewInspectorService
    {
        private readonly string _connectionString;
        private readonly IPersianDateService _persianDateService;

        public SqlViewInspectorService(IPersianDateService persianDateService)
            : this(ConfigurationManager.ConnectionStrings["HseDbConnection"]?.ConnectionString, persianDateService)
        {
        }

        public SqlViewInspectorService(string connectionString, IPersianDateService persianDateService)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'HseDbConnection' is not configured.");
            }

            _connectionString = connectionString;
            _persianDateService = persianDateService ?? throw new ArgumentNullException(nameof(persianDateService));
        }

        public async Task<List<ViewMetadataDto>> GetAvailableViewsAsync()
        {
            var result = new List<ViewMetadataDto>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = new SqlCommand(SqlViewMetadataQueries.GetAllViewsSql, conn))
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new ViewMetadataDto
                        {
                            SchemaName = reader.GetString(0),
                            ViewName = reader.GetString(1),
                            FullViewName = reader.GetString(2)
                        });
                    }
                }
            }

            return result;
        }

        public async Task<ViewMetadataDto> InspectViewMetadataAsync(string viewName)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                return await EnsureViewAndGetMetadataAsync(conn, viewName).ConfigureAwait(false);
            }
        }

        public async Task<List<EavFieldDefinitionDto>> DiscoverEavFieldsAsync(DataSource dataSource)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            if (!dataSource.IsEav)
            {
                return new List<EavFieldDefinitionDto>();
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                ViewMetadataDto metadata = await EnsureViewAndGetMetadataAsync(conn, dataSource.ViewName).ConfigureAwait(false);
                HashSet<string> validColumns = BuildColumnSet(metadata);

                string fieldNameCol = RequireWhitelistedColumn(validColumns, dataSource.EavFieldNameColumn, nameof(dataSource.EavFieldNameColumn));
                string fieldTypeCol = RequireWhitelistedColumn(validColumns, dataSource.EavFieldTypeColumn, nameof(dataSource.EavFieldTypeColumn));

                string fieldIdCol = !string.IsNullOrWhiteSpace(dataSource.EavFieldIdColumn)
                    ? RequireWhitelistedColumn(validColumns, dataSource.EavFieldIdColumn, nameof(dataSource.EavFieldIdColumn))
                    : null;

                string displayOrderCol = !string.IsNullOrWhiteSpace(dataSource.EavDisplayOrderColumn)
                    ? RequireWhitelistedColumn(validColumns, dataSource.EavDisplayOrderColumn, nameof(dataSource.EavDisplayOrderColumn))
                    : null;

                bool hasIsRequired = validColumns.Contains("IsRequired");

                var selectCols = new List<string>();
                if (fieldIdCol != null)
                {
                    selectCols.Add(SqlViewMetadataQueries.QuoteIdentifier(fieldIdCol) + " AS [CategoryFieldId]");
                }

                selectCols.Add(SqlViewMetadataQueries.QuoteIdentifier(fieldNameCol) + " AS [FieldName]");
                selectCols.Add(SqlViewMetadataQueries.QuoteIdentifier(fieldTypeCol) + " AS [FieldType]");

                if (hasIsRequired)
                {
                    selectCols.Add(SqlViewMetadataQueries.QuoteIdentifier("IsRequired") + " AS [IsRequired]");
                }

                if (displayOrderCol != null)
                {
                    selectCols.Add(SqlViewMetadataQueries.QuoteIdentifier(displayOrderCol) + " AS [DisplayOrder]");
                }

                string orderBy = displayOrderCol != null
                    ? SqlViewMetadataQueries.QuoteIdentifier(displayOrderCol) + ", " + SqlViewMetadataQueries.QuoteIdentifier(fieldNameCol)
                    : SqlViewMetadataQueries.QuoteIdentifier(fieldNameCol);

                string sql = "SELECT DISTINCT " + string.Join(", ", selectCols) +
                             " FROM " + SqlViewMetadataQueries.QuoteTwoPartName(metadata.FullViewName) +
                             " ORDER BY " + orderBy + ";";

                var list = new List<EavFieldDefinitionDto>();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var def = new EavFieldDefinitionDto
                        {
                            CategoryFieldId = fieldIdCol != null ? ReadIntSafe(reader, "CategoryFieldId") : 0,
                            FieldName = ReadStringSafe(reader, "FieldName"),
                            FieldType = (BaseFieldType)ReadIntSafe(reader, "FieldType", (int)BaseFieldType.Text),
                            IsRequired = hasIsRequired && ReadBoolSafe(reader, "IsRequired"),
                            DisplayOrder = displayOrderCol != null ? ReadIntSafe(reader, "DisplayOrder") : 0
                        };

                        if (!string.IsNullOrWhiteSpace(def.FieldName))
                        {
                            list.Add(def);
                        }
                    }
                }

                return list;
            }
        }

        public async Task<List<PivotedAssetRowDto>> QueryAssetsAsync(DataSource dataSource)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                ViewMetadataDto metadata = await EnsureViewAndGetMetadataAsync(conn, dataSource.ViewName).ConfigureAwait(false);
                HashSet<string> validColumns = BuildColumnSet(metadata);

                string uniqueKeyCol = RequireWhitelistedColumn(validColumns, dataSource.UniqueKeyColumn, nameof(dataSource.UniqueKeyColumn));

                List<string> allCols = metadata.Columns.Select(c => c.ColumnName).ToList();
                string sql = SqlViewMetadataQueries.BuildSelectSql(metadata.FullViewName, allCols);

                if (dataSource.IsEav)
                {
                    string assetIdCol = !string.IsNullOrWhiteSpace(dataSource.EavAssetIdColumn)
                        ? RequireWhitelistedColumn(validColumns, dataSource.EavAssetIdColumn, nameof(dataSource.EavAssetIdColumn))
                        : uniqueKeyCol;

                    string orderBy = SqlViewMetadataQueries.QuoteIdentifier(assetIdCol);
                    if (!string.IsNullOrWhiteSpace(dataSource.EavDisplayOrderColumn))
                    {
                        string displayOrderCol = RequireWhitelistedColumn(validColumns, dataSource.EavDisplayOrderColumn, nameof(dataSource.EavDisplayOrderColumn));
                        orderBy += ", " + SqlViewMetadataQueries.QuoteIdentifier(displayOrderCol);
                    }

                    sql += " ORDER BY " + orderBy;
                }

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    List<Dictionary<string, object>> rows = await ReadAllRowsAsync(reader).ConfigureAwait(false);
                    return dataSource.IsEav
                        ? PivotEavRows(dataSource, metadata, validColumns, rows)
                        : MapFlatRows(dataSource, metadata, validColumns, rows);
                }
            }
        }

        public async Task<PivotedAssetRowDto> QuerySingleAssetAsync(DataSource dataSource, string assetKey)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            if (string.IsNullOrWhiteSpace(assetKey))
            {
                return null;
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                ViewMetadataDto metadata = await EnsureViewAndGetMetadataAsync(conn, dataSource.ViewName).ConfigureAwait(false);
                HashSet<string> validColumns = BuildColumnSet(metadata);

                string uniqueKeyCol = RequireWhitelistedColumn(validColumns, dataSource.UniqueKeyColumn, nameof(dataSource.UniqueKeyColumn));
                List<string> allCols = metadata.Columns.Select(c => c.ColumnName).ToList();

                string sql = SqlViewMetadataQueries.BuildSelectSql(metadata.FullViewName, allCols) +
                             " WHERE " + SqlViewMetadataQueries.QuoteIdentifier(uniqueKeyCol) + " = @AssetKey";

                if (dataSource.IsEav && !string.IsNullOrWhiteSpace(dataSource.EavDisplayOrderColumn))
                {
                    string displayOrderCol = RequireWhitelistedColumn(validColumns, dataSource.EavDisplayOrderColumn, nameof(dataSource.EavDisplayOrderColumn));
                    sql += " ORDER BY " + SqlViewMetadataQueries.QuoteIdentifier(displayOrderCol);
                }

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@AssetKey", SqlDbType.NVarChar, 200) { Value = assetKey.Trim() });

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        List<Dictionary<string, object>> rows = await ReadAllRowsAsync(reader).ConfigureAwait(false);
                        if (rows.Count == 0)
                        {
                            return null;
                        }

                        List<PivotedAssetRowDto> mapped = dataSource.IsEav
                            ? PivotEavRows(dataSource, metadata, validColumns, rows)
                            : MapFlatRows(dataSource, metadata, validColumns, rows);

                        return mapped.FirstOrDefault();
                    }
                }
            }
        }

        public async Task<List<SelectOptionDto>> LoadSelectOptionsAsync(CustomField field)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (field.OptionsSourceType == OptionsSourceType.Static)
            {
                return ParseStaticOptions(field.StaticOptionsJson);
            }

            if (field.OptionsSourceType != OptionsSourceType.SqlView)
            {
                return new List<SelectOptionDto>();
            }

            if (string.IsNullOrWhiteSpace(field.OptionsViewName) ||
                string.IsNullOrWhiteSpace(field.OptionsValueColumn) ||
                string.IsNullOrWhiteSpace(field.OptionsDisplayColumn))
            {
                return new List<SelectOptionDto>();
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                ViewMetadataDto metadata = await EnsureViewAndGetMetadataAsync(conn, field.OptionsViewName).ConfigureAwait(false);
                HashSet<string> validColumns = BuildColumnSet(metadata);

                string valueCol = RequireWhitelistedColumn(validColumns, field.OptionsValueColumn, nameof(field.OptionsValueColumn));
                string displayCol = RequireWhitelistedColumn(validColumns, field.OptionsDisplayColumn, nameof(field.OptionsDisplayColumn));

                string quotedVal = SqlViewMetadataQueries.QuoteIdentifier(valueCol);
                string quotedDisp = SqlViewMetadataQueries.QuoteIdentifier(displayCol);

                string sql = "SELECT DISTINCT " + quotedVal + " AS [OptionValue], " + quotedDisp + " AS [OptionText]" +
                             " FROM " + SqlViewMetadataQueries.QuoteTwoPartName(metadata.FullViewName) +
                             " ORDER BY " + quotedDisp + ";";

                var options = new List<SelectOptionDto>();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        string val = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture);
                        string text = reader.IsDBNull(1) ? val : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(val) || !string.IsNullOrEmpty(text))
                        {
                            options.Add(new SelectOptionDto
                            {
                                Value = val,
                                Text = text
                            });
                        }
                    }
                }

                return options;
            }
        }

        private async Task<ViewMetadataDto> EnsureViewAndGetMetadataAsync(SqlConnection conn, string viewName)
        {
            string schemaName;
            string parsedViewName;
            try
            {
                SqlViewMetadataQueries.ParseViewName(viewName, out schemaName, out parsedViewName);
            }
            catch (ArgumentException ex)
            {
                throw new SecurityException("Invalid SQL view name.", ex);
            }

            if (string.Equals(schemaName, "sys", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(schemaName, "INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException("Access to system schema views is prohibited.");
            }

            using (var existsCmd = new SqlCommand(SqlViewMetadataQueries.ViewExistsSql, conn))
            {
                existsCmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.NVarChar, 128) { Value = schemaName });
                existsCmd.Parameters.Add(new SqlParameter("@ViewName", SqlDbType.NVarChar, 128) { Value = parsedViewName });

                int count = Convert.ToInt32(await existsCmd.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
                if (count <= 0)
                {
                    throw new SecurityException("SQL view '" + schemaName + "." + parsedViewName + "' is not whitelisted or does not exist.");
                }
            }

            var dto = new ViewMetadataDto
            {
                SchemaName = schemaName,
                ViewName = parsedViewName,
                FullViewName = schemaName + "." + parsedViewName
            };

            using (var colsCmd = new SqlCommand(SqlViewMetadataQueries.GetViewColumnsSql, conn))
            {
                colsCmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.NVarChar, 128) { Value = schemaName });
                colsCmd.Parameters.Add(new SqlParameter("@ViewName", SqlDbType.NVarChar, 128) { Value = parsedViewName });

                using (var reader = await colsCmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        dto.Columns.Add(new ViewColumnDto
                        {
                            ColumnName = reader.GetString(0),
                            DataType = reader.GetString(1),
                            IsNullable = reader.GetBoolean(2),
                            MaxLength = reader.GetInt32(3),
                            ColumnOrder = reader.GetInt32(4)
                        });
                    }
                }
            }

            if (dto.Columns.Count == 0)
            {
                throw new SecurityException("SQL view '" + dto.FullViewName + "' has no inspectable columns.");
            }

            return dto;
        }

        private static HashSet<string> BuildColumnSet(ViewMetadataDto metadata)
        {
            return new HashSet<string>(
                metadata.Columns.Select(c => c.ColumnName),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string RequireWhitelistedColumn(HashSet<string> validColumns, string columnName, string paramName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new InvalidOperationException("Required column mapping '" + paramName + "' is not configured.");
            }

            string trimmed = columnName.Trim();
            if (!validColumns.Contains(trimmed))
            {
                throw new SecurityException("Column '" + trimmed + "' is not present in the whitelisted SQL view.");
            }

            return trimmed;
        }

        private static async Task<List<Dictionary<string, object>>> ReadAllRowsAsync(SqlDataReader reader)
        {
            var rows = new List<Dictionary<string, object>>();
            int fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var row = new Dictionary<string, object>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    object val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[names[i]] = val;
                }

                rows.Add(row);
            }

            return rows;
        }

        private List<PivotedAssetRowDto> PivotEavRows(
            DataSource dataSource,
            ViewMetadataDto metadata,
            HashSet<string> validColumns,
            List<Dictionary<string, object>> rows)
        {
            string uniqueKeyCol = RequireWhitelistedColumn(validColumns, dataSource.UniqueKeyColumn, nameof(dataSource.UniqueKeyColumn));
            string assetIdCol = !string.IsNullOrWhiteSpace(dataSource.EavAssetIdColumn)
                ? RequireWhitelistedColumn(validColumns, dataSource.EavAssetIdColumn, nameof(dataSource.EavAssetIdColumn))
                : uniqueKeyCol;
            string fieldNameCol = RequireWhitelistedColumn(validColumns, dataSource.EavFieldNameColumn, nameof(dataSource.EavFieldNameColumn));
            string fieldTypeCol = RequireWhitelistedColumn(validColumns, dataSource.EavFieldTypeColumn, nameof(dataSource.EavFieldTypeColumn));

            var eavSystemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                fieldNameCol,
                fieldTypeCol,
                "TextValue",
                "NumberValue",
                "DateValue",
                "BooleanValue",
                "IsGregorian",
                "StoredFileName",
                "OriginalFileName",
                "ContentType",
                "IsRequired"
            };

            if (!string.IsNullOrWhiteSpace(dataSource.EavFieldIdColumn))
            {
                eavSystemColumns.Add(dataSource.EavFieldIdColumn.Trim());
            }

            if (!string.IsNullOrWhiteSpace(dataSource.EavDisplayOrderColumn))
            {
                eavSystemColumns.Add(dataSource.EavDisplayOrderColumn.Trim());
            }

            var columnTypeLookup = metadata.Columns.ToDictionary(c => c.ColumnName, c => c.DataType, StringComparer.OrdinalIgnoreCase);

            var grouped = rows.GroupBy(r =>
            {
                object idObj;
                if (r.TryGetValue(assetIdCol, out idObj) && idObj != null)
                {
                    return Convert.ToString(idObj, CultureInfo.InvariantCulture);
                }

                object keyObj;
                return r.TryGetValue(uniqueKeyCol, out keyObj) && keyObj != null
                    ? Convert.ToString(keyObj, CultureInfo.InvariantCulture)
                    : string.Empty;
            });

            var result = new List<PivotedAssetRowDto>();
            foreach (var group in grouped)
            {
                Dictionary<string, object> firstRow = group.First();
                string assetKey = GetRowString(firstRow, uniqueKeyCol);
                if (string.IsNullOrWhiteSpace(assetKey))
                {
                    continue;
                }

                var assetRow = new PivotedAssetRowDto
                {
                    AssetKey = assetKey
                };

                // Extract non-EAV base asset columns (AssetKey, AssetTag, DisplayName, CategoryName, DepartmentId, CreatedAt, etc.)
                foreach (var col in metadata.Columns)
                {
                    if (eavSystemColumns.Contains(col.ColumnName))
                    {
                        continue;
                    }

                    object rawObj;
                    firstRow.TryGetValue(col.ColumnName, out rawObj);
                    string sqlDataType = columnTypeLookup.ContainsKey(col.ColumnName) ? columnTypeLookup[col.ColumnName] : "nvarchar";
                    assetRow.BaseValues[col.ColumnName] = CreateFlatFieldValue(col.ColumnName, sqlDataType, rawObj);
                }

                // Pivot EAV rows
                foreach (var row in group)
                {
                    string fieldName = GetRowString(row, fieldNameCol);
                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        continue;
                    }

                    int fieldTypeInt = GetRowInt(row, fieldTypeCol, (int)BaseFieldType.Text);
                    BaseFieldType fieldType = Enum.IsDefined(typeof(BaseFieldType), fieldTypeInt)
                        ? (BaseFieldType)fieldTypeInt
                        : BaseFieldType.Text;

                    assetRow.BaseValues[fieldName] = CreateEavFieldValue(fieldName, fieldType, row);
                }

                result.Add(assetRow);
            }

            return result;
        }

        private List<PivotedAssetRowDto> MapFlatRows(
            DataSource dataSource,
            ViewMetadataDto metadata,
            HashSet<string> validColumns,
            List<Dictionary<string, object>> rows)
        {
            string uniqueKeyCol = RequireWhitelistedColumn(validColumns, dataSource.UniqueKeyColumn, nameof(dataSource.UniqueKeyColumn));
            var columnTypeLookup = metadata.Columns.ToDictionary(c => c.ColumnName, c => c.DataType, StringComparer.OrdinalIgnoreCase);

            var result = new List<PivotedAssetRowDto>();
            foreach (var row in rows)
            {
                string assetKey = GetRowString(row, uniqueKeyCol);
                if (string.IsNullOrWhiteSpace(assetKey))
                {
                    continue;
                }

                var assetRow = new PivotedAssetRowDto
                {
                    AssetKey = assetKey
                };

                foreach (var col in metadata.Columns)
                {
                    object rawObj;
                    row.TryGetValue(col.ColumnName, out rawObj);
                    string sqlDataType = columnTypeLookup.ContainsKey(col.ColumnName) ? columnTypeLookup[col.ColumnName] : "nvarchar";
                    assetRow.BaseValues[col.ColumnName] = CreateFlatFieldValue(col.ColumnName, sqlDataType, rawObj);
                }

                result.Add(assetRow);
            }

            return result;
        }

        private AssetFieldValueDto CreateEavFieldValue(string fieldName, BaseFieldType fieldType, Dictionary<string, object> row)
        {
            var dto = new AssetFieldValueDto
            {
                FieldName = fieldName,
                FieldType = fieldType
            };

            switch (fieldType)
            {
                case BaseFieldType.Text:
                {
                    string text = GetRowString(row, "TextValue");
                    dto.RawValue = text;
                    dto.DisplayValue = text ?? string.Empty;
                    break;
                }
                case BaseFieldType.Date:
                {
                    bool isGregorian = GetRowBool(row, "IsGregorian");
                    dto.IsGregorian = isGregorian;
                    DateTime? dt = GetRowDateTime(row, "DateValue");
                    dto.RawValue = dt.HasValue ? dt.Value.ToString("O", CultureInfo.InvariantCulture) : null;
                    dto.DisplayValue = _persianDateService.FormatDateForDisplay(dt, isGregorian);
                    break;
                }
                case BaseFieldType.Number:
                {
                    object numObj = GetRowObject(row, "NumberValue");
                    if (numObj != null)
                    {
                        decimal dec = Convert.ToDecimal(numObj, CultureInfo.InvariantCulture);
                        dto.RawValue = dec.ToString(CultureInfo.InvariantCulture);
                        dto.DisplayValue = dec.ToString("0.####", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        dto.DisplayValue = string.Empty;
                    }
                    break;
                }
                case BaseFieldType.Boolean:
                {
                    object boolObj = GetRowObject(row, "BooleanValue");
                    if (boolObj != null)
                    {
                        bool val = Convert.ToBoolean(boolObj, CultureInfo.InvariantCulture);
                        dto.RawValue = val ? "true" : "false";
                        dto.DisplayValue = val ? "بله" : "خیر";
                    }
                    else
                    {
                        dto.DisplayValue = string.Empty;
                    }
                    break;
                }
                case BaseFieldType.Attachment:
                {
                    dto.StoredFileName = GetRowString(row, "StoredFileName");
                    dto.OriginalFileName = GetRowString(row, "OriginalFileName");
                    dto.ContentType = GetRowString(row, "ContentType");
                    dto.RawValue = dto.StoredFileName;
                    dto.DisplayValue = !string.IsNullOrWhiteSpace(dto.OriginalFileName)
                        ? dto.OriginalFileName
                        : (dto.StoredFileName ?? string.Empty);
                    break;
                }
            }

            return dto;
        }

        private AssetFieldValueDto CreateFlatFieldValue(string fieldName, string sqlDataType, object rawObj)
        {
            BaseFieldType fieldType = InferBaseFieldType(sqlDataType);
            var dto = new AssetFieldValueDto
            {
                FieldName = fieldName,
                FieldType = fieldType
            };

            if (rawObj == null || rawObj == DBNull.Value)
            {
                dto.DisplayValue = string.Empty;
                return dto;
            }

            switch (fieldType)
            {
                case BaseFieldType.Date:
                {
                    DateTime dt = Convert.ToDateTime(rawObj, CultureInfo.InvariantCulture);
                    dto.IsGregorian = false;
                    dto.RawValue = dt.ToString("O", CultureInfo.InvariantCulture);
                    dto.DisplayValue = _persianDateService.FormatDateForDisplay(dt, false);
                    break;
                }
                case BaseFieldType.Boolean:
                {
                    bool val = Convert.ToBoolean(rawObj, CultureInfo.InvariantCulture);
                    dto.RawValue = val ? "true" : "false";
                    dto.DisplayValue = val ? "بله" : "خیر";
                    break;
                }
                case BaseFieldType.Number:
                {
                    decimal dec = Convert.ToDecimal(rawObj, CultureInfo.InvariantCulture);
                    dto.RawValue = dec.ToString(CultureInfo.InvariantCulture);
                    dto.DisplayValue = dec.ToString("0.####", CultureInfo.InvariantCulture);
                    break;
                }
                default:
                {
                    string text = Convert.ToString(rawObj, CultureInfo.InvariantCulture);
                    dto.RawValue = text;
                    dto.DisplayValue = text ?? string.Empty;
                    break;
                }
            }

            return dto;
        }

        private static BaseFieldType InferBaseFieldType(string sqlDataType)
        {
            if (string.IsNullOrWhiteSpace(sqlDataType))
            {
                return BaseFieldType.Text;
            }

            switch (sqlDataType.Trim().ToLowerInvariant())
            {
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                case "datetimeoffset":
                    return BaseFieldType.Date;
                case "bit":
                    return BaseFieldType.Boolean;
                case "int":
                case "bigint":
                case "smallint":
                case "tinyint":
                case "decimal":
                case "numeric":
                case "float":
                case "real":
                case "money":
                case "smallmoney":
                    return BaseFieldType.Number;
                default:
                    return BaseFieldType.Text;
            }
        }

        private static List<SelectOptionDto> ParseStaticOptions(string staticOptionsJson)
        {
            if (string.IsNullOrWhiteSpace(staticOptionsJson))
            {
                return new List<SelectOptionDto>();
            }

            try
            {
                JToken token = JToken.Parse(staticOptionsJson);
                if (token.Type != JTokenType.Array)
                {
                    return new List<SelectOptionDto>();
                }

                var list = new List<SelectOptionDto>();
                foreach (JToken item in token)
                {
                    if (item.Type == JTokenType.String)
                    {
                        string val = item.Value<string>();
                        list.Add(new SelectOptionDto { Value = val, Text = val });
                    }
                    else if (item.Type == JTokenType.Object)
                    {
                        string value = item["Value"]?.Value<string>() ?? item["value"]?.Value<string>();
                        string text = item["Text"]?.Value<string>() ?? item["text"]?.Value<string>() ?? value;
                        if (!string.IsNullOrEmpty(value) || !string.IsNullOrEmpty(text))
                        {
                            list.Add(new SelectOptionDto { Value = value ?? text, Text = text ?? value });
                        }
                    }
                }

                return list;
            }
            catch (JsonException)
            {
                return new List<SelectOptionDto>();
            }
        }

        private static object GetRowObject(Dictionary<string, object> row, string key)
        {
            object val;
            return row.TryGetValue(key, out val) ? val : null;
        }

        private static string GetRowString(Dictionary<string, object> row, string key)
        {
            object val = GetRowObject(row, key);
            return val == null || val == DBNull.Value ? null : Convert.ToString(val, CultureInfo.InvariantCulture);
        }

        private static int GetRowInt(Dictionary<string, object> row, string key, int defaultValue = 0)
        {
            object val = GetRowObject(row, key);
            if (val == null || val == DBNull.Value)
            {
                return defaultValue;
            }

            return Convert.ToInt32(val, CultureInfo.InvariantCulture);
        }

        private static bool GetRowBool(Dictionary<string, object> row, string key)
        {
            object val = GetRowObject(row, key);
            return val != null && val != DBNull.Value && Convert.ToBoolean(val, CultureInfo.InvariantCulture);
        }

        private static DateTime? GetRowDateTime(Dictionary<string, object> row, string key)
        {
            object val = GetRowObject(row, key);
            if (val == null || val == DBNull.Value)
            {
                return null;
            }

            return Convert.ToDateTime(val, CultureInfo.InvariantCulture);
        }

        private static int ReadIntSafe(SqlDataReader reader, string columnName, int defaultValue = 0)
        {
            int idx = reader.GetOrdinal(columnName);
            return reader.IsDBNull(idx) ? defaultValue : Convert.ToInt32(reader.GetValue(idx), CultureInfo.InvariantCulture);
        }

        private static string ReadStringSafe(SqlDataReader reader, string columnName)
        {
            int idx = reader.GetOrdinal(columnName);
            return reader.IsDBNull(idx) ? null : Convert.ToString(reader.GetValue(idx), CultureInfo.InvariantCulture);
        }

        private static bool ReadBoolSafe(SqlDataReader reader, string columnName)
        {
            int idx = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(idx) && Convert.ToBoolean(reader.GetValue(idx), CultureInfo.InvariantCulture);
        }
    }
}
