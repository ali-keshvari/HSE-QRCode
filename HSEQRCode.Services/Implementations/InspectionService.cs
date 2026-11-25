using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HSEQRCode.Data;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Domain.Enums;
using HSEQRCode.Services.Interfaces;
using HSEQRCode.Services.Models;
using Newtonsoft.Json;

namespace HSEQRCode.Services.Implementations
{
    public class InspectionService : IInspectionService
    {
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt"
        };

        private readonly HseDbContext _db;
        private readonly ISqlViewInspectorService _sqlViewInspectorService;
        private readonly IPersianDateService _persianDateService;
        private readonly IAuthService _authService;

        public InspectionService(
            HseDbContext db,
            ISqlViewInspectorService sqlViewInspectorService,
            IPersianDateService persianDateService,
            IAuthService authService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _sqlViewInspectorService = sqlViewInspectorService ?? throw new ArgumentNullException(nameof(sqlViewInspectorService));
            _persianDateService = persianDateService ?? throw new ArgumentNullException(nameof(persianDateService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task<PublicScanResult> GetPublicScanDataAsync(string indicator)
        {
            if (string.IsNullOrWhiteSpace(indicator))
            {
                return null;
            }

            string trimmed = indicator.Trim();
            string trimmedLower = trimmed.ToLowerInvariant();
            AssetIndicator assetIndicator = await _db.AssetIndicators
                .Include(a => a.DataSource)
                .FirstOrDefaultAsync(a => a.Indicator.ToLower() == trimmedLower)
                .ConfigureAwait(false);

            if (assetIndicator == null || assetIndicator.DataSource == null || !assetIndicator.DataSource.IsActive)
            {
                return null;
            }

            Dictionary<string, AssetFieldValueDto> baseValues = await ResolveBaseValuesAsync(assetIndicator, preferLive: false).ConfigureAwait(false);
            List<string> publicFieldNames = ParseStringListJson(assetIndicator.DataSource.PublicBaseFieldsJson);

            var result = new PublicScanResult
            {
                AssetIndicatorId = assetIndicator.Id,
                DataSourceId = assetIndicator.DataSourceId,
                DataSourceName = assetIndicator.DataSource.Name,
                Indicator = assetIndicator.Indicator,
                AssetKey = assetIndicator.AssetKey,
                QrPayloadUrl = assetIndicator.QrPayloadUrl
            };

            if (publicFieldNames.Count > 0)
            {
                foreach (string fieldName in publicFieldNames)
                {
                    AssetFieldValueDto val;
                    if (baseValues.TryGetValue(fieldName, out val) && val != null)
                    {
                        result.PublicBaseFields.Add(ToBaseFieldDisplayItem(val));
                    }
                }
            }

            InspectionRecord latestRecord = await _db.InspectionRecords
                .Include(r => r.Values.Select(v => v.CustomField))
                .Where(r => r.AssetIndicatorId == assetIndicator.Id)
                .OrderByDescending(r => r.InspectedAtUtc)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (latestRecord != null)
            {
                var optionCache = new Dictionary<int, Dictionary<string, string>>();
                var activePublicFields = latestRecord.Values
                    .Where(v => v.CustomField != null && !v.CustomField.IsDeleted && v.CustomField.IsPublicVisible)
                    .OrderBy(v => v.CustomField.DisplayOrder)
                    .ToList();

                foreach (var val in activePublicFields)
                {
                    if ((val.CustomField.FieldType == CustomFieldType.Select || val.CustomField.FieldType == CustomFieldType.MultiSelect) &&
                        !optionCache.ContainsKey(val.CustomFieldId))
                    {
                        List<SelectOptionDto> opts = await _sqlViewInspectorService.LoadSelectOptionsAsync(val.CustomField).ConfigureAwait(false);
                        optionCache[val.CustomFieldId] = opts.ToDictionary(o => o.Value ?? string.Empty, o => o.Text ?? o.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    }
                }

                result.LatestInspection = MapRecordSummary(latestRecord, publicOnly: true, optionLookup: optionCache);
            }

            return result;
        }

        public async Task<PersonnelInspectionResult> GetPersonnelInspectionDataAsync(string indicator, string username)
        {
            if (string.IsNullOrWhiteSpace(indicator))
            {
                return null;
            }

            string trimmed = indicator.Trim();
            string trimmedLower = trimmed.ToLowerInvariant();
            AssetIndicator assetIndicator = await _db.AssetIndicators
                .Include(a => a.DataSource)
                .FirstOrDefaultAsync(a => a.Indicator.ToLower() == trimmedLower)
                .ConfigureAwait(false);

            if (assetIndicator == null || assetIndicator.DataSource == null || !assetIndicator.DataSource.IsActive)
            {
                return null;
            }

            bool authorized = await _authService
                .IsUserAuthorizedForDataSourceAsync(assetIndicator.DataSourceId, username)
                .ConfigureAwait(false);

            if (!authorized)
            {
                throw new UnauthorizedAccessException("User is not authorized to inspect assets for this DataSource.");
            }

            Dictionary<string, AssetFieldValueDto> baseValues = await ResolveBaseValuesAsync(assetIndicator, preferLive: true).ConfigureAwait(false);

            var result = new PersonnelInspectionResult
            {
                AssetIndicatorId = assetIndicator.Id,
                DataSourceId = assetIndicator.DataSourceId,
                DataSourceName = assetIndicator.DataSource.Name,
                Indicator = assetIndicator.Indicator,
                AssetKey = assetIndicator.AssetKey,
                QrPayloadUrl = assetIndicator.QrPayloadUrl
            };

            foreach (var kvp in baseValues)
            {
                if (kvp.Value != null)
                {
                    result.BaseFields.Add(ToBaseFieldDisplayItem(kvp.Value));
                }
            }

            List<CustomField> customFields = await _db.CustomFields
                .Where(f => f.DataSourceId == assetIndicator.DataSourceId && !f.IsDeleted)
                .OrderBy(f => f.DisplayOrder)
                .ThenBy(f => f.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            var optionCache = new Dictionary<int, Dictionary<string, string>>();
            foreach (CustomField field in customFields)
            {
                var inputDef = new CustomFieldInputDefinition
                {
                    CustomFieldId = field.Id,
                    Name = field.Name,
                    Label = field.Label,
                    FieldType = field.FieldType,
                    IsRequired = field.IsRequired,
                    DisplayOrder = field.DisplayOrder,
                    IsPublicVisible = field.IsPublicVisible,
                    BooleanTrueLabel = string.IsNullOrWhiteSpace(field.BooleanTrueLabel) ? "بله" : field.BooleanTrueLabel,
                    BooleanFalseLabel = string.IsNullOrWhiteSpace(field.BooleanFalseLabel) ? "خیر" : field.BooleanFalseLabel,
                    IsGregorianDate = field.IsGregorianDate
                };

                if (field.FieldType == CustomFieldType.Select || field.FieldType == CustomFieldType.MultiSelect)
                {
                    inputDef.Options = await _sqlViewInspectorService.LoadSelectOptionsAsync(field).ConfigureAwait(false);
                    optionCache[field.Id] = inputDef.Options
                        .GroupBy(o => o.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().Text ?? g.Key, StringComparer.OrdinalIgnoreCase);
                }

                result.CustomFields.Add(inputDef);
            }

            List<InspectionRecord> history = await _db.InspectionRecords
                .Include(r => r.Values.Select(v => v.CustomField))
                .Where(r => r.AssetIndicatorId == assetIndicator.Id)
                .OrderByDescending(r => r.InspectedAtUtc)
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (InspectionRecord record in history)
            {
                result.InspectionHistory.Add(MapRecordSummary(record, publicOnly: false, optionLookup: optionCache));
            }

            return result;
        }

        public async Task<InspectionRecord> SubmitInspectionAsync(InspectionSubmissionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Indicator))
            {
                throw new ArgumentException("Indicator is required.", nameof(request));
            }

            string trimmedIndicator = request.Indicator.Trim();
            string trimmedLower = trimmedIndicator.ToLowerInvariant();
            AssetIndicator assetIndicator = await _db.AssetIndicators
                .Include(a => a.DataSource)
                .FirstOrDefaultAsync(a => a.Indicator.ToLower() == trimmedLower)
                .ConfigureAwait(false);

            if (assetIndicator == null || assetIndicator.DataSource == null || !assetIndicator.DataSource.IsActive)
            {
                throw new InvalidOperationException("Asset indicator not found or DataSource is inactive.");
            }

            bool authorized = await _authService
                .IsUserAuthorizedForDataSourceAsync(assetIndicator.DataSourceId, request.Username)
                .ConfigureAwait(false);

            if (!authorized)
            {
                throw new UnauthorizedAccessException("User is not authorized to submit inspections for this DataSource.");
            }

            AppUser user = await _authService.GetActiveUserAsync(request.Username).ConfigureAwait(false);
            string inspectorUsername = user != null ? user.Username : request.Username.Trim();
            string inspectorFullName = user != null ? user.FullName : inspectorUsername;

            List<CustomField> customFields = await _db.CustomFields
                .Where(f => f.DataSourceId == assetIndicator.DataSourceId && !f.IsDeleted)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            var record = new InspectionRecord
            {
                DataSourceId = assetIndicator.DataSourceId,
                AssetIndicatorId = assetIndicator.Id,
                AssetKey = assetIndicator.AssetKey,
                InspectorUsername = inspectorUsername,
                InspectorFullName = inspectorFullName,
                InspectedAtUtc = DateTime.UtcNow,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };

            foreach (CustomField field in customFields)
            {
                var recordValue = new InspectionRecordValue
                {
                    CustomFieldId = field.Id
                };

                string scalarRaw = null;
                if (request.ScalarValues != null)
                {
                    request.ScalarValues.TryGetValue(field.Id, out scalarRaw);
                }

                switch (field.FieldType)
                {
                    case CustomFieldType.Text:
                    {
                        string text = string.IsNullOrWhiteSpace(scalarRaw) ? null : scalarRaw.Trim();
                        if (field.IsRequired && string.IsNullOrEmpty(text))
                        {
                            throw new InvalidOperationException("فیلد اجباری '" + field.Label + "' تکمیل نشده است.");
                        }

                        recordValue.TextValue = text;
                        break;
                    }
                    case CustomFieldType.Number:
                    {
                        string numStr = _persianDateService.NormalizeDigits(scalarRaw)?.Trim();
                        if (string.IsNullOrEmpty(numStr))
                        {
                            if (field.IsRequired)
                            {
                                throw new InvalidOperationException("فیلد عددی اجباری '" + field.Label + "' تکمیل نشده است.");
                            }
                        }
                        else
                        {
                            decimal numVal;
                            if (!decimal.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out numVal))
                            {
                                throw new InvalidOperationException("مقدار عددی فیلد '" + field.Label + "' نامعتبر است.");
                            }

                            recordValue.NumberValue = numVal;
                        }
                        break;
                    }
                    case CustomFieldType.Date:
                    {
                        string dateStr = _persianDateService.NormalizeDigits(scalarRaw)?.Trim();
                        if (string.IsNullOrEmpty(dateStr))
                        {
                            if (field.IsRequired)
                            {
                                throw new InvalidOperationException("فیلد تاریخ اجباری '" + field.Label + "' تکمیل نشده است.");
                            }
                        }
                        else
                        {
                            DateTime dt;
                            if (field.IsGregorianDate)
                            {
                                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                                {
                                    throw new InvalidOperationException("تاریخ میلادی فیلد '" + field.Label + "' نامعتبر است.");
                                }
                            }
                            else
                            {
                                if (!_persianDateService.TryParseJalali(dateStr, out dt))
                                {
                                    throw new InvalidOperationException("تاریخ شمسی فیلد '" + field.Label + "' نامعتبر است.");
                                }
                            }

                            recordValue.DateValue = dt;
                        }
                        break;
                    }
                    case CustomFieldType.Boolean:
                    {
                        bool? parsedBool = ParseNullableBool(scalarRaw);
                        if (!parsedBool.HasValue && field.IsRequired)
                        {
                            throw new InvalidOperationException("فیلد بله/خیر اجباری '" + field.Label + "' انتخاب نشده است.");
                        }

                        recordValue.BooleanValue = parsedBool;
                        break;
                    }
                    case CustomFieldType.Select:
                    {
                        string selected = string.IsNullOrWhiteSpace(scalarRaw) ? null : scalarRaw.Trim();
                        if (field.IsRequired && string.IsNullOrEmpty(selected))
                        {
                            throw new InvalidOperationException("فیلد انتخابی اجباری '" + field.Label + "' انتخاب نشده است.");
                        }

                        recordValue.TextValue = selected;
                        break;
                    }
                    case CustomFieldType.MultiSelect:
                    {
                        List<string> selectedList = null;
                        if (request.MultiSelectValues != null)
                        {
                            request.MultiSelectValues.TryGetValue(field.Id, out selectedList);
                        }

                        selectedList = (selectedList ?? new List<string>())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        if (field.IsRequired && selectedList.Count == 0)
                        {
                            throw new InvalidOperationException("فیلد چندانتخابی اجباری '" + field.Label + "' انتخاب نشده است.");
                        }

                        recordValue.SelectedValuesJson = selectedList.Count > 0
                            ? JsonConvert.SerializeObject(selectedList)
                            : null;
                        break;
                    }
                    case CustomFieldType.Attachment:
                    {
                        UploadedFileInput file = null;
                        if (request.Files != null)
                        {
                            request.Files.TryGetValue(field.Id, out file);
                        }

                        if (file == null || file.InputStream == null || string.IsNullOrWhiteSpace(file.FileName))
                        {
                            if (field.IsRequired)
                            {
                                throw new InvalidOperationException("فایل پیوست اجباری '" + field.Label + "' بارگذاری نشده است.");
                            }
                        }
                        else
                        {
                            SaveUploadedFile(file, request.UploadRootPath, recordValue);
                        }
                        break;
                    }
                }

                record.Values.Add(recordValue);
            }

            _db.InspectionRecords.Add(record);
            await _db.SaveChangesAsync().ConfigureAwait(false);
            return record;
        }

        public async Task<InspectionRecordValue> GetAttachmentForDownloadAsync(int recordValueId, string username)
        {
            InspectionRecordValue val = await _db.InspectionRecordValues
                .Include(v => v.CustomField)
                .Include(v => v.InspectionRecord)
                .FirstOrDefaultAsync(v => v.Id == recordValueId)
                .ConfigureAwait(false);

            if (val == null || val.CustomField == null || val.InspectionRecord == null || string.IsNullOrWhiteSpace(val.StoredFileName))
            {
                return null;
            }

            if (val.CustomField.IsPublicVisible && !val.CustomField.IsDeleted)
            {
                return val;
            }

            bool authorized = await _authService
                .IsUserAuthorizedForDataSourceAsync(val.InspectionRecord.DataSourceId, username)
                .ConfigureAwait(false);

            if (!authorized)
            {
                throw new UnauthorizedAccessException("User is not authorized to download this attachment.");
            }

            return val;
        }

        private async Task<Dictionary<string, AssetFieldValueDto>> ResolveBaseValuesAsync(AssetIndicator assetIndicator, bool preferLive)
        {
            if (preferLive)
            {
                try
                {
                    PivotedAssetRowDto liveRow = await _sqlViewInspectorService
                        .QuerySingleAssetAsync(assetIndicator.DataSource, assetIndicator.AssetKey)
                        .ConfigureAwait(false);

                    if (liveRow != null && liveRow.BaseValues != null && liveRow.BaseValues.Count > 0)
                    {
                        return new Dictionary<string, AssetFieldValueDto>(liveRow.BaseValues, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch
                {
                    // Fall back to cached snapshot if live view query fails
                }
            }

            if (!string.IsNullOrWhiteSpace(assetIndicator.CachedBaseFieldsJson))
            {
                try
                {
                    var cached = JsonConvert.DeserializeObject<Dictionary<string, AssetFieldValueDto>>(assetIndicator.CachedBaseFieldsJson);
                    if (cached != null)
                    {
                        return new Dictionary<string, AssetFieldValueDto>(cached, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed cache and try live
                }
            }

            PivotedAssetRowDto fallbackLive = await _sqlViewInspectorService
                .QuerySingleAssetAsync(assetIndicator.DataSource, assetIndicator.AssetKey)
                .ConfigureAwait(false);

            return fallbackLive?.BaseValues != null
                ? new Dictionary<string, AssetFieldValueDto>(fallbackLive.BaseValues, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, AssetFieldValueDto>(StringComparer.OrdinalIgnoreCase);
        }

        private InspectionRecordSummary MapRecordSummary(
            InspectionRecord record,
            bool publicOnly,
            Dictionary<int, Dictionary<string, string>> optionLookup)
        {
            DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(record.InspectedAtUtc, DateTimeKind.Utc),
                TimeZoneInfo.Local);

            var summary = new InspectionRecordSummary
            {
                RecordId = record.Id,
                InspectorUsername = record.InspectorUsername,
                InspectorFullName = record.InspectorFullName,
                InspectedAtUtc = record.InspectedAtUtc,
                InspectedAtJalali = _persianDateService.ToJalaliString(localTime, includeTime: true),
                Notes = record.Notes
            };

            var values = record.Values
                .Where(v => v.CustomField != null && !v.CustomField.IsDeleted && (!publicOnly || v.CustomField.IsPublicVisible))
                .OrderBy(v => v.CustomField.DisplayOrder)
                .ThenBy(v => v.Id);

            foreach (InspectionRecordValue val in values)
            {
                summary.Values.Add(MapValueSummary(val, optionLookup));
            }

            return summary;
        }

        private InspectionFieldValueSummary MapValueSummary(
            InspectionRecordValue val,
            Dictionary<int, Dictionary<string, string>> optionLookup)
        {
            CustomField field = val.CustomField;
            var item = new InspectionFieldValueSummary
            {
                ValueId = val.Id,
                CustomFieldId = val.CustomFieldId,
                FieldName = field.Name,
                FieldLabel = field.Label,
                FieldType = field.FieldType,
                StoredFileName = val.StoredFileName,
                OriginalFileName = val.OriginalFileName,
                ContentType = val.ContentType
            };

            Dictionary<string, string> optMap = null;
            if (optionLookup != null)
            {
                optionLookup.TryGetValue(field.Id, out optMap);
            }

            switch (field.FieldType)
            {
                case CustomFieldType.Text:
                    item.DisplayValue = val.TextValue ?? string.Empty;
                    break;
                case CustomFieldType.Number:
                    item.DisplayValue = val.NumberValue.HasValue
                        ? val.NumberValue.Value.ToString("0.####", CultureInfo.InvariantCulture)
                        : string.Empty;
                    break;
                case CustomFieldType.Date:
                    item.DisplayValue = _persianDateService.FormatDateForDisplay(val.DateValue, field.IsGregorianDate);
                    break;
                case CustomFieldType.Boolean:
                    if (val.BooleanValue.HasValue)
                    {
                        item.DisplayValue = val.BooleanValue.Value
                            ? (string.IsNullOrWhiteSpace(field.BooleanTrueLabel) ? "بله" : field.BooleanTrueLabel)
                            : (string.IsNullOrWhiteSpace(field.BooleanFalseLabel) ? "خیر" : field.BooleanFalseLabel);
                    }
                    else
                    {
                        item.DisplayValue = string.Empty;
                    }
                    break;
                case CustomFieldType.Select:
                    if (!string.IsNullOrWhiteSpace(val.TextValue))
                    {
                        string mappedText;
                        item.DisplayValue = optMap != null && optMap.TryGetValue(val.TextValue, out mappedText)
                            ? mappedText
                            : val.TextValue;
                    }
                    else
                    {
                        item.DisplayValue = string.Empty;
                    }
                    break;
                case CustomFieldType.MultiSelect:
                {
                    List<string> selected = ParseStringListJson(val.SelectedValuesJson);
                    var displayParts = new List<string>();
                    foreach (string code in selected)
                    {
                        string mappedText;
                        string label = optMap != null && optMap.TryGetValue(code, out mappedText) ? mappedText : code;
                        item.SelectedValues.Add(label);
                        displayParts.Add(label);
                    }

                    item.DisplayValue = string.Join("، ", displayParts);
                    break;
                }
                case CustomFieldType.Attachment:
                    item.DisplayValue = !string.IsNullOrWhiteSpace(val.OriginalFileName)
                        ? val.OriginalFileName
                        : (val.StoredFileName ?? string.Empty);
                    break;
            }

            return item;
        }

        private static void SaveUploadedFile(UploadedFileInput file, string uploadRootPath, InspectionRecordValue recordValue)
        {
            if (string.IsNullOrWhiteSpace(uploadRootPath))
            {
                throw new InvalidOperationException("Upload root path is not configured.");
            }

            string safeOriginalName = Path.GetFileName(file.FileName);
            string ext = Path.GetExtension(safeOriginalName) ?? string.Empty;
            if (!AllowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException("نوع فایل پیوست مجاز نیست: " + ext);
            }

            Directory.CreateDirectory(uploadRootPath);
            string storedName = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
            string fullPath = Path.Combine(uploadRootPath, storedName);

            using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.InputStream.CopyTo(fs);
            }

            recordValue.StoredFileName = storedName;
            recordValue.OriginalFileName = safeOriginalName;
            recordValue.ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        }

        private static BaseFieldDisplayItem ToBaseFieldDisplayItem(AssetFieldValueDto dto)
        {
            return new BaseFieldDisplayItem
            {
                FieldName = dto.FieldName,
                FieldType = dto.FieldType,
                DisplayValue = dto.DisplayValue ?? string.Empty,
                RawValue = dto.RawValue,
                IsGregorian = dto.IsGregorian
            };
        }

        private static bool? ParseNullableBool(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string trimmed = raw.Trim().ToLowerInvariant();
            if (trimmed == "true" || trimmed == "1" || trimmed == "on" || trimmed == "yes" || trimmed == "بله")
            {
                return true;
            }

            if (trimmed == "false" || trimmed == "0" || trimmed == "off" || trimmed == "no" || trimmed == "خیر")
            {
                return false;
            }

            return null;
        }

        private static List<string> ParseStringListJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }
    }
}
