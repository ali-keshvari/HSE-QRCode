using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSEQRCode.Data;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Domain.Enums;
using HSEQRCode.Services.Implementations;
using HSEQRCode.Services.Interfaces;
using HSEQRCode.Services.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

namespace HSEQRCode.Tests
{
    [TestClass]
    public class InspectionAndRbacTests
    {
        private DbConnection _connection;
        private HseDbContext _db;
        private AuthService _authService;
        private PersianDateService _persianDateService;
        private Mock<ISqlViewInspectorService> _mockViewInspector;
        private InspectionService _inspectionService;
        private string _tempUploadDir;

        private DataSource _dataSource;
        private AssetIndicator _assetIndicator;
        private List<CustomField> _fields;

        [TestInitialize]
        public async Task Setup()
        {
            _connection = Effort.DbConnectionFactory.CreateTransient();
            _db = new HseDbContext(_connection, contextOwnsConnection: true);
            _db.Database.CreateIfNotExists();
            _authService = new AuthService(_db);
            _persianDateService = new PersianDateService();
            _mockViewInspector = new Mock<ISqlViewInspectorService>();
            _inspectionService = new InspectionService(_db, _mockViewInspector.Object, _persianDateService, _authService);

            _tempUploadDir = Path.Combine(Path.GetTempPath(), "HSEQRCode_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempUploadDir);

            // Seed users: admin, assigned inspector, unassigned user
            _db.AppUsers.Add(new AppUser
            {
                Username = "admin1",
                PasswordHash = _authService.HashPassword("Admin@123"),
                FullName = "مدیر سیستم",
                IsAdmin = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            _db.AppUsers.Add(new AppUser
            {
                Username = "inspector1",
                PasswordHash = _authService.HashPassword("Insp@123"),
                FullName = "بازرس ایمنی",
                IsAdmin = false,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            _db.AppUsers.Add(new AppUser
            {
                Username = "outsider1",
                PasswordHash = _authService.HashPassword("Out@123"),
                FullName = "کاربر غیرمجاز",
                IsAdmin = false,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            _dataSource = new DataSource
            {
                Name = "کپسول‌های آتش‌نشانی",
                ViewName = "gen.vw_FireExtinguishers_1001",
                UniqueKeyColumn = "AssetKey",
                PublicBaseFieldsJson = JsonConvert.SerializeObject(new[] { "DisplayName", "Location" }),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.DataSources.Add(_dataSource);
            await _db.SaveChangesAsync();

            _db.FormPersonnel.Add(new FormPersonnel
            {
                DataSourceId = _dataSource.Id,
                Username = "inspector1",
                FullName = "بازرس ایمنی",
                IsActive = true,
                AssignedAtUtc = DateTime.UtcNow
            });

            var cachedBaseFields = new Dictionary<string, AssetFieldValueDto>(StringComparer.OrdinalIgnoreCase)
            {
                ["DisplayName"] = new AssetFieldValueDto { FieldName = "DisplayName", FieldType = BaseFieldType.Text, DisplayValue = "کپسول پودری ۶ کیلویی" },
                ["Location"] = new AssetFieldValueDto { FieldName = "Location", FieldType = BaseFieldType.Text, DisplayValue = "ساختمان اداری - طبقه ۱" },
                ["InternalSerial"] = new AssetFieldValueDto { FieldName = "InternalSerial", FieldType = BaseFieldType.Text, DisplayValue = "SECRET-9988" }
            };

            _assetIndicator = new AssetIndicator
            {
                DataSourceId = _dataSource.Id,
                AssetKey = "FE-1001",
                Indicator = "HSE-FE-1001",
                QrPayloadUrl = "https://hse.local/Scan/HSE-FE-1001",
                CachedBaseFieldsJson = JsonConvert.SerializeObject(cachedBaseFields),
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.AssetIndicators.Add(_assetIndicator);

            // Seed all 7 CustomFieldTypes (mix of public and private visibility)
            _fields = new List<CustomField>
            {
                new CustomField { DataSourceId = _dataSource.Id, Name = "StatusText", Label = "وضعیت ظاهری", FieldType = CustomFieldType.Text, IsPublicVisible = true, DisplayOrder = 1 },
                new CustomField { DataSourceId = _dataSource.Id, Name = "PressureBar", Label = "فشار (بار)", FieldType = CustomFieldType.Number, IsPublicVisible = false, DisplayOrder = 2 },
                new CustomField { DataSourceId = _dataSource.Id, Name = "NextDue", Label = "سررسید بعدی", FieldType = CustomFieldType.Date, IsPublicVisible = true, IsGregorianDate = false, DisplayOrder = 3 },
                new CustomField { DataSourceId = _dataSource.Id, Name = "IsSealed", Label = "پلمپ سالم", FieldType = CustomFieldType.Boolean, IsPublicVisible = true, BooleanTrueLabel = "سالم", BooleanFalseLabel = "مخدوش", DisplayOrder = 4 },
                new CustomField { DataSourceId = _dataSource.Id, Name = "Photo", Label = "تصویر برچسب", FieldType = CustomFieldType.Attachment, IsPublicVisible = false, DisplayOrder = 5 },
                new CustomField { DataSourceId = _dataSource.Id, Name = "Condition", Label = "نتیجه کلی", FieldType = CustomFieldType.Select, IsPublicVisible = true, OptionsSourceType = OptionsSourceType.Static, StaticOptionsJson = "[{\"Value\":\"OK\",\"Text\":\"تایید شده\"},{\"Value\":\"FAIL\",\"Text\":\"نیاز به تعمیر\"}]", DisplayOrder = 6 },
                new CustomField { DataSourceId = _dataSource.Id, Name = "Defects", Label = "موارد نقص", FieldType = CustomFieldType.MultiSelect, IsPublicVisible = false, OptionsSourceType = OptionsSourceType.Static, StaticOptionsJson = "[{\"Value\":\"D1\",\"Text\":\"شیلنگ\"},{\"Value\":\"D2\",\"Text\":\"مانومتر\"}]", DisplayOrder = 7 }
            };
            _db.CustomFields.AddRange(_fields);
            await _db.SaveChangesAsync();

            _mockViewInspector.Setup(v => v.LoadSelectOptionsAsync(It.IsAny<CustomField>()))
                .ReturnsAsync((CustomField f) =>
                {
                    if (string.IsNullOrWhiteSpace(f.StaticOptionsJson))
                    {
                        return new List<SelectOptionDto>();
                    }
                    return JsonConvert.DeserializeObject<List<SelectOptionDto>>(f.StaticOptionsJson);
                });

            _mockViewInspector.Setup(v => v.QuerySingleAssetAsync(It.IsAny<DataSource>(), "FE-1001"))
                .ReturnsAsync(new PivotedAssetRowDto
                {
                    AssetKey = "FE-1001",
                    BaseValues = cachedBaseFields
                });

            // Seed two historical inspection records (older and newer)
            var older = new InspectionRecord
            {
                DataSourceId = _dataSource.Id,
                AssetIndicatorId = _assetIndicator.Id,
                AssetKey = "FE-1001",
                InspectorUsername = "inspector1",
                InspectorFullName = "بازرس ایمنی",
                InspectedAtUtc = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
                Notes = "Old inspection",
                Values = new List<InspectionRecordValue>
                {
                    new InspectionRecordValue { CustomFieldId = _fields[0].Id, TextValue = "قدیمی" },
                    new InspectionRecordValue { CustomFieldId = _fields[1].Id, NumberValue = 11.5m }
                }
            };
            var newer = new InspectionRecord
            {
                DataSourceId = _dataSource.Id,
                AssetIndicatorId = _assetIndicator.Id,
                AssetKey = "FE-1001",
                InspectorUsername = "inspector1",
                InspectorFullName = "بازرس ایمنی",
                InspectedAtUtc = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc),
                Notes = "Latest seeded inspection",
                Values = new List<InspectionRecordValue>
                {
                    new InspectionRecordValue { CustomFieldId = _fields[0].Id, TextValue = "جدید و سالم" },
                    new InspectionRecordValue { CustomFieldId = _fields[1].Id, NumberValue = 14.0m },
                    new InspectionRecordValue { CustomFieldId = _fields[2].Id, DateValue = new DateTime(2026, 11, 6) },
                    new InspectionRecordValue { CustomFieldId = _fields[3].Id, BooleanValue = true },
                    new InspectionRecordValue { CustomFieldId = _fields[5].Id, TextValue = "OK" }
                }
            };
            _db.InspectionRecords.Add(older);
            _db.InspectionRecords.Add(newer);
            await _db.SaveChangesAsync();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
            _connection?.Dispose();
            if (Directory.Exists(_tempUploadDir))
            {
                Directory.Delete(_tempUploadDir, recursive: true);
            }
        }

        [TestMethod]
        public async Task GetPublicScanDataAsync_FiltersBaseAndCustomFields_AndReturnsOnlyLatestRecord()
        {
            PublicScanResult pub = await _inspectionService.GetPublicScanDataAsync("HSE-FE-1001");
            Assert.IsNotNull(pub);

            // Only PublicBaseFieldsJson ("DisplayName", "Location") must be returned; "InternalSerial" must be excluded
            Assert.AreEqual(2, pub.PublicBaseFields.Count);
            Assert.IsTrue(pub.PublicBaseFields.Any(f => f.FieldName == "DisplayName"));
            Assert.IsTrue(pub.PublicBaseFields.Any(f => f.FieldName == "Location"));
            Assert.IsFalse(pub.PublicBaseFields.Any(f => f.FieldName == "InternalSerial"));

            // Latest record only, and ONLY IsPublicVisible == true custom fields
            Assert.IsNotNull(pub.LatestInspection);
            Assert.AreEqual("Latest seeded inspection", pub.LatestInspection.Notes);
            Assert.IsTrue(pub.LatestInspection.Values.Any(v => v.FieldName == "StatusText" && v.DisplayValue == "جدید و سالم"));
            Assert.IsTrue(pub.LatestInspection.Values.Any(v => v.FieldName == "NextDue" && v.DisplayValue == "1405/08/15"));
            Assert.IsTrue(pub.LatestInspection.Values.Any(v => v.FieldName == "IsSealed" && v.DisplayValue == "سالم"));
            Assert.IsTrue(pub.LatestInspection.Values.Any(v => v.FieldName == "Condition" && v.DisplayValue == "تایید شده"));

            // Private fields (PressureBar, Photo, Defects) must NEVER appear on public scan
            Assert.IsFalse(pub.LatestInspection.Values.Any(v => v.FieldName == "PressureBar"));
            Assert.IsFalse(pub.LatestInspection.Values.Any(v => v.FieldName == "Photo"));
            Assert.IsFalse(pub.LatestInspection.Values.Any(v => v.FieldName == "Defects"));
        }

        [TestMethod]
        public async Task RbacIsolation_DeniesUnauthorizedUser_AllowsPersonnelAndAdmin_WithNewestFirstHistory()
        {
            // Non-personnel user denied
            Assert.IsFalse(await _authService.IsUserAuthorizedForDataSourceAsync(_dataSource.Id, "outsider1"));
            await AssertThrowsUnauthorizedAsync(() => _inspectionService.GetPersonnelInspectionDataAsync("HSE-FE-1001", "outsider1"));

            // Assigned FormPersonnel allowed, sees all base fields & full history newest first
            Assert.IsTrue(await _authService.IsUserAuthorizedForDataSourceAsync(_dataSource.Id, "inspector1"));
            PersonnelInspectionResult personnelData = await _inspectionService.GetPersonnelInspectionDataAsync("HSE-FE-1001", "inspector1");
            Assert.IsNotNull(personnelData);
            Assert.AreEqual(3, personnelData.BaseFields.Count);
            Assert.IsTrue(personnelData.BaseFields.Any(f => f.FieldName == "InternalSerial"));
            Assert.AreEqual(2, personnelData.InspectionHistory.Count);
            Assert.IsTrue(personnelData.InspectionHistory[0].InspectedAtUtc > personnelData.InspectionHistory[1].InspectedAtUtc);
            Assert.AreEqual("Latest seeded inspection", personnelData.InspectionHistory[0].Notes);
            Assert.AreEqual("Old inspection", personnelData.InspectionHistory[1].Notes);

            // Admin allowed even without FormPersonnel row
            Assert.IsTrue(await _authService.IsUserAuthorizedForDataSourceAsync(_dataSource.Id, "admin1"));
            PersonnelInspectionResult adminData = await _inspectionService.GetPersonnelInspectionDataAsync("HSE-FE-1001", "admin1");
            Assert.IsNotNull(adminData);
            Assert.AreEqual(2, adminData.InspectionHistory.Count);
        }

        [TestMethod]
        public async Task SubmitInspectionAsync_PersistsAllSevenCustomFieldTypes_AndUpdatesPublicLatest()
        {
            byte[] fileBytes = Encoding.UTF8.GetBytes("fake-image-content");
            using (var ms = new MemoryStream(fileBytes))
            {
                var request = new InspectionSubmissionRequest
                {
                    Indicator = "HSE-FE-1001",
                    Username = "inspector1",
                    Notes = "بازرسی جدید کامل",
                    UploadRootPath = _tempUploadDir,
                    ScalarValues = new Dictionary<int, string>
                    {
                        [_fields[0].Id] = "عالی - شارژ شده",
                        [_fields[1].Id] = "۱۵.۵",
                        [_fields[2].Id] = "۱۴۰۶/۰۲/۲۰",
                        [_fields[3].Id] = "false",
                        [_fields[5].Id] = "FAIL"
                    },
                    MultiSelectValues = new Dictionary<int, List<string>>
                    {
                        [_fields[6].Id] = new List<string> { "D1", "D2" }
                    },
                    Files = new Dictionary<int, UploadedFileInput>
                    {
                        [_fields[4].Id] = new UploadedFileInput
                        {
                            FileName = "tag.jpg",
                            ContentType = "image/jpeg",
                            InputStream = ms
                        }
                    }
                };

                InspectionRecord saved = await _inspectionService.SubmitInspectionAsync(request);
                Assert.IsNotNull(saved);
                Assert.AreEqual(7, saved.Values.Count);

                // Verify all 7 persisted values
                Assert.AreEqual("عالی - شارژ شده", saved.Values.First(v => v.CustomFieldId == _fields[0].Id).TextValue);
                Assert.AreEqual(15.5m, saved.Values.First(v => v.CustomFieldId == _fields[1].Id).NumberValue);
                Assert.AreEqual(_persianDateService.ParseJalali("1406/02/20"), saved.Values.First(v => v.CustomFieldId == _fields[2].Id).DateValue);
                Assert.AreEqual(false, saved.Values.First(v => v.CustomFieldId == _fields[3].Id).BooleanValue);
                Assert.AreEqual("tag.jpg", saved.Values.First(v => v.CustomFieldId == _fields[4].Id).OriginalFileName);
                Assert.AreEqual("FAIL", saved.Values.First(v => v.CustomFieldId == _fields[5].Id).TextValue);
                Assert.IsTrue(saved.Values.First(v => v.CustomFieldId == _fields[6].Id).SelectedValuesJson.Contains("D1"));
            }

            // Immediately verify public scan reflects the new latest inspection
            PublicScanResult pubAfter = await _inspectionService.GetPublicScanDataAsync("HSE-FE-1001");
            Assert.IsNotNull(pubAfter.LatestInspection);
            Assert.AreEqual("بازرسی جدید کامل", pubAfter.LatestInspection.Notes);
            Assert.AreEqual("عالی - شارژ شده", pubAfter.LatestInspection.Values.First(v => v.FieldName == "StatusText").DisplayValue);
            Assert.AreEqual("مخدوش", pubAfter.LatestInspection.Values.First(v => v.FieldName == "IsSealed").DisplayValue);
            Assert.AreEqual("نیاز به تعمیر", pubAfter.LatestInspection.Values.First(v => v.FieldName == "Condition").DisplayValue);
        }

        [TestMethod]
        public async Task DamagedQrManualLookup_TrimmedCaseInsensitiveAndUnknownIndicator()
        {
            PublicScanResult exact = await _inspectionService.GetPublicScanDataAsync("HSE-FE-1001");
            PublicScanResult trimmedLower = await _inspectionService.GetPublicScanDataAsync("  hse-fe-1001  ");

            Assert.IsNotNull(exact);
            Assert.IsNotNull(trimmedLower);
            Assert.AreEqual(exact.Indicator, trimmedLower.Indicator);
            Assert.AreEqual(exact.AssetKey, trimmedLower.AssetKey);
            Assert.AreEqual(exact.PublicBaseFields.Count, trimmedLower.PublicBaseFields.Count);

            // Unknown indicator returns null (which maps to Found == false in ScanController.LookupJson)
            PublicScanResult unknown = await _inspectionService.GetPublicScanDataAsync("UNKNOWN-9999");
            Assert.IsNull(unknown);
        }

        private static async Task AssertThrowsUnauthorizedAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                Assert.Fail("Expected UnauthorizedAccessException was not thrown.");
            }
            catch (UnauthorizedAccessException)
            {
                // Expected
            }
        }
    }
}
