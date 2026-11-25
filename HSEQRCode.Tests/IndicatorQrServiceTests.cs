using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using HSEQRCode.Data;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Domain.Enums;
using HSEQRCode.Services.Implementations;
using HSEQRCode.Services.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

namespace HSEQRCode.Tests
{
    [TestClass]
    public class IndicatorQrServiceTests
    {
        private DbConnection _connection;
        private HseDbContext _db;

        [TestInitialize]
        public void Setup()
        {
            _connection = Effort.DbConnectionFactory.CreateTransient();
            _db = new HseDbContext(_connection, contextOwnsConnection: true);
            _db.Database.CreateIfNotExists();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
            _connection?.Dispose();
        }

        [TestMethod]
        public async Task GenerateIndicatorsForDataSourceAsync_GeneratesUniqueIndicators_AndPreservesExistingOnRerun()
        {
            var ds = new DataSource
            {
                Name = "Fire Extinguishers",
                ViewName = "gen.vw_FireExtinguishers_1001",
                UniqueKeyColumn = "AssetKey",
                IndicatorColumnsJson = JsonConvert.SerializeObject(new[] { "CategoryCode", "SerialNo" }),
                IndicatorSeparator = "-",
                IndicatorPrefix = "HSE",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.DataSources.Add(ds);
            await _db.SaveChangesAsync();

            // Pre-existing indicator from another data source or asset to force collision
            var dsOther = new DataSource
            {
                Name = "Other",
                ViewName = "dbo.vw_Other",
                UniqueKeyColumn = "Code",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.DataSources.Add(dsOther);
            await _db.SaveChangesAsync();

            _db.AssetIndicators.Add(new AssetIndicator
            {
                DataSourceId = dsOther.Id,
                AssetKey = "EXT-000",
                Indicator = "HSE-FE-1001",
                QrPayloadUrl = "https://hse.local/Scan/HSE-FE-1001",
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            var mockedAssets = new List<PivotedAssetRowDto>
            {
                new PivotedAssetRowDto
                {
                    AssetKey = "A-1",
                    BaseValues = new Dictionary<string, AssetFieldValueDto>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CategoryCode"] = new AssetFieldValueDto { FieldName = "CategoryCode", FieldType = BaseFieldType.Text, DisplayValue = "FE" },
                        ["SerialNo"] = new AssetFieldValueDto { FieldName = "SerialNo", FieldType = BaseFieldType.Text, DisplayValue = "1001" }
                    }
                },
                new PivotedAssetRowDto
                {
                    AssetKey = "A-2",
                    BaseValues = new Dictionary<string, AssetFieldValueDto>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CategoryCode"] = new AssetFieldValueDto { FieldName = "CategoryCode", FieldType = BaseFieldType.Text, DisplayValue = "FE" },
                        ["SerialNo"] = new AssetFieldValueDto { FieldName = "SerialNo", FieldType = BaseFieldType.Text, DisplayValue = "1002" }
                    }
                }
            };

            var mockInspector = new Mock<ISqlViewInspectorService>();
            mockInspector.Setup(s => s.QueryAssetsAsync(It.Is<DataSource>(d => d.Id == ds.Id)))
                .ReturnsAsync(mockedAssets);

            var service = new IndicatorQrService(_db, mockInspector.Object);
            int count = await service.GenerateIndicatorsForDataSourceAsync(ds.Id, "https://hse.local/");

            Assert.AreEqual(2, count);

            List<AssetIndicator> generated = _db.AssetIndicators
                .Where(a => a.DataSourceId == ds.Id)
                .OrderBy(a => a.AssetKey)
                .ToList();

            Assert.AreEqual(2, generated.Count);
            // A-1 collided with "HSE-FE-1001", so uniqueness enforcement must suffix "-2"
            Assert.AreEqual("HSE-FE-1001-2", generated[0].Indicator);
            Assert.AreEqual("https://hse.local/scan/HSE-FE-1001-2", generated[0].QrPayloadUrl);
            Assert.AreEqual("HSE-FE-1002", generated[1].Indicator);
            Assert.AreEqual("https://hse.local/scan/HSE-FE-1002", generated[1].QrPayloadUrl);

            // Re-run with updated base field: indicator must be preserved, URL & cache updated
            mockedAssets[0].BaseValues["SerialNo"].DisplayValue = "9999";
            int rerunCount = await service.GenerateIndicatorsForDataSourceAsync(ds.Id, "https://new-host.local");
            Assert.AreEqual(2, rerunCount);

            AssetIndicator refreshed = _db.AssetIndicators.First(a => a.DataSourceId == ds.Id && a.AssetKey == "A-1");
            Assert.AreEqual("HSE-FE-1001-2", refreshed.Indicator);
            Assert.AreEqual("https://new-host.local/scan/HSE-FE-1001-2", refreshed.QrPayloadUrl);
            Assert.IsTrue(refreshed.CachedBaseFieldsJson.Contains("9999"));
        }

        [TestMethod]
        public void GenerateQrPngBase64_And_GenerateQrSvg_ReturnValidOutputs()
        {
            var mockInspector = new Mock<ISqlViewInspectorService>();
            var service = new IndicatorQrService(_db, mockInspector.Object);

            Assert.AreEqual(string.Empty, service.GenerateQrPngBase64(null));
            Assert.AreEqual(string.Empty, service.GenerateQrSvg("   "));

            string payload = "https://hse.local/Scan/HSE-FE-1001";
            string pngBase64 = service.GenerateQrPngBase64(payload, 8);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pngBase64));

            byte[] pngBytes = Convert.FromBase64String(pngBase64);
            Assert.IsTrue(pngBytes.Length > 8);
            // PNG magic header: 0x89 0x50 0x4E 0x47
            Assert.AreEqual(0x89, pngBytes[0]);
            Assert.AreEqual(0x50, pngBytes[1]);
            Assert.AreEqual(0x4E, pngBytes[2]);
            Assert.AreEqual(0x47, pngBytes[3]);

            string svg = service.GenerateQrSvg(payload, 5);
            Assert.IsFalse(string.IsNullOrWhiteSpace(svg));
            Assert.IsTrue(svg.Contains("<svg"));
            Assert.IsTrue(svg.Contains("</svg>"));
        }
    }
}
