using System;
using System.Collections.Generic;
using System.Reflection;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Domain.Enums;
using HSEQRCode.Services.Implementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSEQRCode.Tests
{
    [TestClass]
    public class EavPivotingTests
    {
        private SqlViewInspectorService _service;
        private MethodInfo _pivotMethod;
        private MethodInfo _mapFlatMethod;

        [TestInitialize]
        public void Setup()
        {
            _service = new SqlViewInspectorService("Server=.;Database=Fake;Integrated Security=true;", new PersianDateService());
            _pivotMethod = typeof(SqlViewInspectorService).GetMethod("PivotEavRows", BindingFlags.NonPublic | BindingFlags.Instance);
            _mapFlatMethod = typeof(SqlViewInspectorService).GetMethod("MapFlatRows", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_pivotMethod);
            Assert.IsNotNull(_mapFlatMethod);
        }

        [TestMethod]
        public void PivotEavRows_GroupsByManagedAssetId_AndMapsAllFiveBaseFieldTypes()
        {
            var dataSource = new DataSource
            {
                Id = 1,
                ViewName = "gen.vw_FireExtinguishers_1001",
                IsEav = true,
                UniqueKeyColumn = "AssetKey",
                EavAssetIdColumn = "ManagedAssetId",
                EavFieldIdColumn = "CategoryFieldId",
                EavFieldNameColumn = "FieldName",
                EavFieldTypeColumn = "FieldType",
                EavDisplayOrderColumn = "DisplayOrder"
            };

            var metadata = new ViewMetadataDto
            {
                SchemaName = "gen",
                ViewName = "vw_FireExtinguishers_1001",
                FullViewName = "gen.vw_FireExtinguishers_1001",
                Columns = new List<ViewColumnDto>
                {
                    new ViewColumnDto { ColumnName = "ManagedAssetId", DataType = "int" },
                    new ViewColumnDto { ColumnName = "AssetKey", DataType = "nvarchar" },
                    new ViewColumnDto { ColumnName = "DisplayName", DataType = "nvarchar" },
                    new ViewColumnDto { ColumnName = "CategoryFieldId", DataType = "int" },
                    new ViewColumnDto { ColumnName = "FieldName", DataType = "nvarchar" },
                    new ViewColumnDto { ColumnName = "FieldType", DataType = "int" },
                    new ViewColumnDto { ColumnName = "DisplayOrder", DataType = "int" },
                    new ViewColumnDto { ColumnName = "TextValue", DataType = "nvarchar" },
                    new ViewColumnDto { ColumnName = "DateValue", DataType = "datetime2" },
                    new ViewColumnDto { ColumnName = "IsGregorian", DataType = "bit" },
                    new ViewColumnDto { ColumnName = "NumberValue", DataType = "decimal" },
                    new ViewColumnDto { ColumnName = "BooleanValue", DataType = "bit" },
                    new ViewColumnDto { ColumnName = "StoredFileName", DataType = "nvarchar" },
                    new ViewColumnDto { ColumnName = "OriginalFileName", DataType = "nvarchar" }
                }
            };

            var validColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in metadata.Columns)
            {
                validColumns.Add(col.ColumnName);
            }

            var gregorianDate = new DateTime(2025, 3, 21); // 1404/01/01 Jalali
            var rows = new List<Dictionary<string, object>>
            {
                // Asset 101 - Text
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManagedAssetId"] = 101,
                    ["AssetKey"] = "FE-101",
                    ["DisplayName"] = "کپسول آتش‌نشانی ۱",
                    ["FieldName"] = "Location",
                    ["FieldType"] = (int)BaseFieldType.Text,
                    ["TextValue"] = "سالن تولید A"
                },
                // Asset 101 - Date (Jalali)
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManagedAssetId"] = 101,
                    ["AssetKey"] = "FE-101",
                    ["DisplayName"] = "کپسول آتش‌نشانی ۱",
                    ["FieldName"] = "InstallDateJalali",
                    ["FieldType"] = (int)BaseFieldType.Date,
                    ["DateValue"] = gregorianDate,
                    ["IsGregorian"] = false
                },
                // Asset 101 - Date (Gregorian)
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManagedAssetId"] = 101,
                    ["AssetKey"] = "FE-101",
                    ["DisplayName"] = "کپسول آتش‌نشانی ۱",
                    ["FieldName"] = "ManufactureDateGreg",
                    ["FieldType"] = (int)BaseFieldType.Date,
                    ["DateValue"] = gregorianDate,
                    ["IsGregorian"] = true
                },
                // Asset 101 - Number
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManagedAssetId"] = 101,
                    ["AssetKey"] = "FE-101",
                    ["DisplayName"] = "کپسول آتش‌نشانی ۱",
                    ["FieldName"] = "CapacityKg",
                    ["FieldType"] = (int)BaseFieldType.Number,
                    ["NumberValue"] = 6.5000m
                },
                // Asset 101 - Boolean
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManagedAssetId"] = 101,
                    ["AssetKey"] = "FE-101",
                    ["DisplayName"] = "کپسول آتش‌نشانی ۱",
                    ["FieldName"] = "IsPressurized",
                    ["FieldType"] = (int)BaseFieldType.Boolean,
                    ["BooleanValue"] = true
                },
                // Asset 101 - Attachment
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManagedAssetId"] = 101,
                    ["AssetKey"] = "FE-101",
                    ["DisplayName"] = "کپسول آتش‌نشانی ۱",
                    ["FieldName"] = "CertificateFile",
                    ["FieldType"] = (int)BaseFieldType.Attachment,
                    ["StoredFileName"] = "cert-101.pdf",
                    ["OriginalFileName"] = "HydroTest.pdf"
                },
                // Asset 102 - Text only
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ManagedAssetId"] = 102,
                    ["AssetKey"] = "FE-102",
                    ["DisplayName"] = "کپسول آتش‌نشانی ۲",
                    ["FieldName"] = "Location",
                    ["FieldType"] = (int)BaseFieldType.Text,
                    ["TextValue"] = "انبار مرکزی"
                }
            };

            var pivoted = (List<PivotedAssetRowDto>)_pivotMethod.Invoke(_service, new object[] { dataSource, metadata, validColumns, rows });

            Assert.AreEqual(2, pivoted.Count);

            PivotedAssetRowDto first = pivoted[0];
            Assert.AreEqual("FE-101", first.AssetKey);
            Assert.AreEqual("کپسول آتش‌نشانی ۱", first.BaseValues["DisplayName"].DisplayValue);
            Assert.AreEqual("سالن تولید A", first.BaseValues["Location"].DisplayValue);
            Assert.AreEqual("1404/01/01", first.BaseValues["InstallDateJalali"].DisplayValue);
            Assert.IsFalse(first.BaseValues["InstallDateJalali"].IsGregorian);
            Assert.AreEqual("2025-03-21", first.BaseValues["ManufactureDateGreg"].DisplayValue);
            Assert.IsTrue(first.BaseValues["ManufactureDateGreg"].IsGregorian);
            Assert.AreEqual("6.5", first.BaseValues["CapacityKg"].DisplayValue);
            Assert.AreEqual("بله", first.BaseValues["IsPressurized"].DisplayValue);
            Assert.AreEqual("HydroTest.pdf", first.BaseValues["CertificateFile"].DisplayValue);
            Assert.AreEqual("cert-101.pdf", first.BaseValues["CertificateFile"].RawValue);

            PivotedAssetRowDto second = pivoted[1];
            Assert.AreEqual("FE-102", second.AssetKey);
            Assert.AreEqual("انبار مرکزی", second.BaseValues["Location"].DisplayValue);
        }

        [TestMethod]
        public void MapFlatRows_MapsByUniqueKeyColumn_AndSkipsEmptyKeys()
        {
            var dataSource = new DataSource
            {
                Id = 2,
                ViewName = "dbo.vw_FlatAssets",
                IsEav = false,
                UniqueKeyColumn = "Code"
            };

            var metadata = new ViewMetadataDto
            {
                SchemaName = "dbo",
                ViewName = "vw_FlatAssets",
                FullViewName = "dbo.vw_FlatAssets",
                Columns = new List<ViewColumnDto>
                {
                    new ViewColumnDto { ColumnName = "Code", DataType = "nvarchar" },
                    new ViewColumnDto { ColumnName = "Title", DataType = "nvarchar" },
                    new ViewColumnDto { ColumnName = "IsActive", DataType = "bit" }
                }
            };

            var validColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Code", "Title", "IsActive" };
            var rows = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Code"] = "A-01",
                    ["Title"] = "پمپ آب",
                    ["IsActive"] = true
                },
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Code"] = "  ",
                    ["Title"] = "ردیف نامعتبر",
                    ["IsActive"] = false
                }
            };

            var mapped = (List<PivotedAssetRowDto>)_mapFlatMethod.Invoke(_service, new object[] { dataSource, metadata, validColumns, rows });

            Assert.AreEqual(1, mapped.Count);
            Assert.AreEqual("A-01", mapped[0].AssetKey);
            Assert.AreEqual("پمپ آب", mapped[0].BaseValues["Title"].DisplayValue);
            Assert.AreEqual("بله", mapped[0].BaseValues["IsActive"].DisplayValue);
        }
    }
}
