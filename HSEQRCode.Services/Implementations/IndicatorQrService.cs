using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSEQRCode.Data;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Services.Interfaces;
using Newtonsoft.Json;
using QRCoder;

namespace HSEQRCode.Services.Implementations
{
    public class IndicatorQrService : IIndicatorQrService
    {
        private readonly HseDbContext _db;
        private readonly ISqlViewInspectorService _sqlViewInspectorService;

        public IndicatorQrService(HseDbContext db, ISqlViewInspectorService sqlViewInspectorService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _sqlViewInspectorService = sqlViewInspectorService ?? throw new ArgumentNullException(nameof(sqlViewInspectorService));
        }

        public async Task<int> GenerateIndicatorsForDataSourceAsync(int dataSourceId, string baseUrl)
        {
            DataSource dataSource = await _db.DataSources
                .FirstOrDefaultAsync(d => d.Id == dataSourceId)
                .ConfigureAwait(false);

            if (dataSource == null)
            {
                throw new InvalidOperationException("DataSource not found.");
            }

            string normalizedBaseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            List<PivotedAssetRowDto> assets = await _sqlViewInspectorService.QueryAssetsAsync(dataSource).ConfigureAwait(false);

            List<string> indicatorCols = ParseStringListJson(dataSource.IndicatorColumnsJson);
            if (indicatorCols.Count == 0 && !string.IsNullOrWhiteSpace(dataSource.UniqueKeyColumn))
            {
                indicatorCols.Add(dataSource.UniqueKeyColumn);
            }

            string separator = string.IsNullOrEmpty(dataSource.IndicatorSeparator) ? "-" : dataSource.IndicatorSeparator;
            string prefix = string.IsNullOrWhiteSpace(dataSource.IndicatorPrefix) ? string.Empty : dataSource.IndicatorPrefix.Trim();

            List<AssetIndicator> existingForDs = await _db.AssetIndicators
                .Where(a => a.DataSourceId == dataSourceId)
                .ToListAsync()
                .ConfigureAwait(false);

            var existingByKey = existingForDs.ToDictionary(a => a.AssetKey, StringComparer.OrdinalIgnoreCase);

            List<string> allIndicators = await _db.AssetIndicators
                .Select(a => a.Indicator)
                .ToListAsync()
                .ConfigureAwait(false);

            var usedIndicators = new HashSet<string>(allIndicators, StringComparer.OrdinalIgnoreCase);
            int affectedCount = 0;

            foreach (PivotedAssetRowDto asset in assets)
            {
                if (string.IsNullOrWhiteSpace(asset.AssetKey))
                {
                    continue;
                }

                string cachedJson = JsonConvert.SerializeObject(asset.BaseValues);
                AssetIndicator existing;

                if (existingByKey.TryGetValue(asset.AssetKey, out existing))
                {
                    // Preserve existing Indicator to keep previously printed QR labels valid
                    existing.CachedBaseFieldsJson = cachedJson;
                    existing.QrPayloadUrl = BuildPayloadUrl(normalizedBaseUrl, existing.Indicator);
                    affectedCount++;
                    continue;
                }

                string candidate = BuildCandidateIndicator(asset, indicatorCols, prefix, separator);
                string uniqueIndicator = EnsureUniqueIndicator(candidate, separator, usedIndicators);
                usedIndicators.Add(uniqueIndicator);

                var entity = new AssetIndicator
                {
                    DataSourceId = dataSourceId,
                    AssetKey = asset.AssetKey,
                    Indicator = uniqueIndicator,
                    QrPayloadUrl = BuildPayloadUrl(normalizedBaseUrl, uniqueIndicator),
                    CachedBaseFieldsJson = cachedJson,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.AssetIndicators.Add(entity);
                affectedCount++;
            }

            await _db.SaveChangesAsync().ConfigureAwait(false);
            return affectedCount;
        }

        public string GenerateQrPngBase64(string payloadUrl, int pixelsPerModule = 10)
        {
            if (string.IsNullOrWhiteSpace(payloadUrl))
            {
                return string.Empty;
            }

            using (var qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(payloadUrl, QRCodeGenerator.ECCLevel.Q))
            using (var pngCode = new PngByteQRCode(qrCodeData))
            {
                byte[] bytes = pngCode.GetGraphic(pixelsPerModule);
                return Convert.ToBase64String(bytes);
            }
        }

        public string GenerateQrSvg(string payloadUrl, int pixelsPerModule = 5)
        {
            if (string.IsNullOrWhiteSpace(payloadUrl))
            {
                return string.Empty;
            }

            using (var qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(payloadUrl, QRCodeGenerator.ECCLevel.Q))
            using (var svgCode = new SvgQRCode(qrCodeData))
            {
                return svgCode.GetGraphic(pixelsPerModule);
            }
        }

        private static string BuildCandidateIndicator(
            PivotedAssetRowDto asset,
            List<string> indicatorCols,
            string prefix,
            string separator)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(prefix))
            {
                parts.Add(SanitizeSegment(prefix));
            }

            foreach (string col in indicatorCols)
            {
                AssetFieldValueDto fieldVal;
                string raw = null;
                if (asset.BaseValues != null && asset.BaseValues.TryGetValue(col, out fieldVal) && fieldVal != null)
                {
                    raw = !string.IsNullOrWhiteSpace(fieldVal.DisplayValue) ? fieldVal.DisplayValue : fieldVal.RawValue;
                }

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    parts.Add(SanitizeSegment(raw));
                }
            }

            if (parts.Count == 0 || (parts.Count == 1 && !string.IsNullOrEmpty(prefix)))
            {
                parts.Add(SanitizeSegment(asset.AssetKey));
            }

            string combined = string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
            if (combined.Length > 230)
            {
                combined = combined.Substring(0, 230);
            }

            return combined;
        }

        private static string EnsureUniqueIndicator(string candidate, string separator, HashSet<string> usedIndicators)
        {
            if (!usedIndicators.Contains(candidate))
            {
                return candidate;
            }

            int suffix = 2;
            while (true)
            {
                string next = candidate + separator + suffix.ToString(CultureInfo.InvariantCulture);
                if (next.Length > 250)
                {
                    next = candidate.Substring(0, Math.Max(1, 250 - separator.Length - 6)) + separator + suffix.ToString(CultureInfo.InvariantCulture);
                }

                if (!usedIndicators.Contains(next))
                {
                    return next;
                }

                suffix++;
            }
        }

        private static string BuildPayloadUrl(string normalizedBaseUrl, string indicator)
        {
            string encoded = Uri.EscapeDataString(indicator);
            return string.IsNullOrEmpty(normalizedBaseUrl)
                ? "/scan/" + encoded
                : normalizedBaseUrl + "/scan/" + encoded;
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (char ch in value.Trim())
            {
                if (char.IsWhiteSpace(ch) || ch == '/' || ch == '\\' || ch == '?' || ch == '#' || ch == '&' || ch == '%' || ch == '+')
                {
                    sb.Append('-');
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Trim('-');
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
