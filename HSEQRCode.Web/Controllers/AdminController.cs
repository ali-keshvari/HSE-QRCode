using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Web.ViewModels;
using Newtonsoft.Json;

namespace HSEQRCode.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseMvcController
    {
        [HttpGet]
        public async Task<ActionResult> Index()
        {
            List<DataSource> dataSources = await Services.Db.DataSources
                .OrderByDescending(d => d.Id)
                .ToListAsync();

            var indicatorCounts = await Services.Db.AssetIndicators
                .GroupBy(a => a.DataSourceId)
                .Select(g => new { DataSourceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.DataSourceId, x => x.Count);

            var fieldCounts = await Services.Db.CustomFields
                .Where(f => !f.IsDeleted)
                .GroupBy(f => f.DataSourceId)
                .Select(g => new { DataSourceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.DataSourceId, x => x.Count);

            var personnelCounts = await Services.Db.FormPersonnel
                .Where(p => p.IsActive)
                .GroupBy(p => p.DataSourceId)
                .Select(g => new { DataSourceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.DataSourceId, x => x.Count);

            var vm = new AdminDataSourceListViewModel();
            foreach (DataSource ds in dataSources)
            {
                vm.DataSources.Add(new DataSourceSummaryItem
                {
                    Id = ds.Id,
                    Name = ds.Name,
                    ViewName = ds.ViewName,
                    IsEav = ds.IsEav,
                    IsActive = ds.IsActive,
                    AssetIndicatorCount = indicatorCounts.ContainsKey(ds.Id) ? indicatorCounts[ds.Id] : 0,
                    CustomFieldCount = fieldCounts.ContainsKey(ds.Id) ? fieldCounts[ds.Id] : 0,
                    PersonnelCount = personnelCounts.ContainsKey(ds.Id) ? personnelCounts[ds.Id] : 0,
                    CreatedAtJalali = Services.PersianDateService.ToJalaliString(ds.CreatedAtUtc.ToLocalTime(), includeTime: true)
                });
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<ActionResult> CreateDataSource()
        {
            var vm = new AdminDataSourceCreateViewModel
            {
                AvailableViews = await Services.SqlViewInspectorService.GetAvailableViewsAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateDataSource(AdminDataSourceCreateViewModel model)
        {
            if (model == null)
            {
                model = new AdminDataSourceCreateViewModel();
            }

            if (!ModelState.IsValid)
            {
                model.AvailableViews = await Services.SqlViewInspectorService.GetAvailableViewsAsync();
                return View(model);
            }

            try
            {
                ViewMetadataDto metadata = await Services.SqlViewInspectorService.InspectViewMetadataAsync(model.ViewName);
                var cols = new HashSet<string>(metadata.Columns.Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);

                if (!cols.Contains(model.UniqueKeyColumn))
                {
                    ModelState.AddModelError("UniqueKeyColumn", "ستون کلید یکتا در نمای انتخاب‌شده وجود ندارد.");
                    model.AvailableViews = await Services.SqlViewInspectorService.GetAvailableViewsAsync();
                    return View(model);
                }

                if (model.IsEav)
                {
                    if (string.IsNullOrWhiteSpace(model.EavFieldNameColumn) || !cols.Contains(model.EavFieldNameColumn) ||
                        string.IsNullOrWhiteSpace(model.EavFieldTypeColumn) || !cols.Contains(model.EavFieldTypeColumn))
                    {
                        ModelState.AddModelError(string.Empty, "ستون‌های EAV (نام فیلد و نوع فیلد) باید در نمای انتخاب‌شده وجود داشته باشند.");
                        model.AvailableViews = await Services.SqlViewInspectorService.GetAvailableViewsAsync();
                        return View(model);
                    }
                }

                bool duplicateView = await Services.Db.DataSources.AnyAsync(d => d.ViewName == metadata.FullViewName);
                if (duplicateView)
                {
                    ModelState.AddModelError("ViewName", "این نما قبلاً ثبت شده است.");
                    model.AvailableViews = await Services.SqlViewInspectorService.GetAvailableViewsAsync();
                    return View(model);
                }

                var entity = new DataSource
                {
                    Name = model.Name.Trim(),
                    ViewName = metadata.FullViewName,
                    UniqueKeyColumn = model.UniqueKeyColumn.Trim(),
                    IsEav = model.IsEav,
                    EavAssetIdColumn = string.IsNullOrWhiteSpace(model.EavAssetIdColumn) ? null : model.EavAssetIdColumn.Trim(),
                    EavFieldIdColumn = string.IsNullOrWhiteSpace(model.EavFieldIdColumn) ? null : model.EavFieldIdColumn.Trim(),
                    EavFieldNameColumn = string.IsNullOrWhiteSpace(model.EavFieldNameColumn) ? null : model.EavFieldNameColumn.Trim(),
                    EavFieldTypeColumn = string.IsNullOrWhiteSpace(model.EavFieldTypeColumn) ? null : model.EavFieldTypeColumn.Trim(),
                    EavDisplayOrderColumn = string.IsNullOrWhiteSpace(model.EavDisplayOrderColumn) ? null : model.EavDisplayOrderColumn.Trim(),
                    IndicatorColumnsJson = JsonConvert.SerializeObject(new[] { model.UniqueKeyColumn.Trim() }),
                    IndicatorSeparator = "-",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                Services.Db.DataSources.Add(entity);
                await Services.Db.SaveChangesAsync();

                return RedirectToAction("ConfigureBaseFields", new { id = entity.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                model.AvailableViews = await Services.SqlViewInspectorService.GetAvailableViewsAsync();
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleDataSourceActive(int id)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
            if (ds == null)
            {
                return HttpNotFound();
            }

            ds.IsActive = !ds.IsActive;
            await Services.Db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<ActionResult> ConfigureBaseFields(int id)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
            if (ds == null)
            {
                return HttpNotFound();
            }

            List<string> availableFields = await GetAvailableBaseFieldNamesAsync(ds);
            var vm = new AdminDataSourceConfigViewModel
            {
                DataSourceId = ds.Id,
                DataSourceName = ds.Name,
                ViewName = ds.ViewName,
                IsEav = ds.IsEav,
                IsActive = ds.IsActive,
                AvailableBaseFields = availableFields,
                SelectedPublicBaseFields = ParseStringList(ds.PublicBaseFieldsJson),
                SelectedLabelCaptionFields = ParseStringList(ds.LabelCaptionFieldsJson)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ConfigureBaseFields(AdminDataSourceConfigViewModel model)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == model.DataSourceId);
            if (ds == null)
            {
                return HttpNotFound();
            }

            List<string> pubFields = (model.SelectedPublicBaseFields ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            List<string> captionFields = (model.SelectedLabelCaptionFields ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            ds.PublicBaseFieldsJson = JsonConvert.SerializeObject(pubFields);
            ds.LabelCaptionFieldsJson = JsonConvert.SerializeObject(captionFields);
            await Services.Db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تنظیمات فیلدهای پایه با موفقیت ذخیره شد.";
            return RedirectToAction("ConfigureBaseFields", new { id = ds.Id });
        }

        [HttpGet]
        public async Task<ActionResult> CustomFields(int id)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
            if (ds == null)
            {
                return HttpNotFound();
            }

            List<CustomField> fields = await Services.Db.CustomFields
                .Where(f => f.DataSourceId == id && !f.IsDeleted)
                .OrderBy(f => f.DisplayOrder)
                .ThenBy(f => f.Id)
                .ToListAsync();

            List<ViewMetadataDto> views = await Services.SqlViewInspectorService.GetAvailableViewsAsync();

            var vm = new AdminCustomFieldsViewModel
            {
                DataSourceId = ds.Id,
                DataSourceName = ds.Name,
                Fields = fields.Select(f => new CustomFieldItemViewModel
                {
                    Id = f.Id,
                    Name = f.Name,
                    Label = f.Label,
                    FieldType = f.FieldType,
                    IsRequired = f.IsRequired,
                    DisplayOrder = f.DisplayOrder,
                    IsPublicVisible = f.IsPublicVisible,
                    OptionsSourceType = f.OptionsSourceType,
                    IsGregorianDate = f.IsGregorianDate
                }).ToList(),
                NewField = new CustomFieldFormViewModel
                {
                    DataSourceId = ds.Id,
                    DisplayOrder = fields.Count + 1,
                    AvailableViews = views
                }
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveCustomField(CustomFieldFormViewModel model)
        {
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == model.DataSourceId);
            if (ds == null)
            {
                return HttpNotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "لطفاً اطلاعات فیلد سفارشی را به درستی تکمیل کنید.";
                return RedirectToAction("CustomFields", new { id = model.DataSourceId });
            }

            CustomField entity;
            if (model.Id > 0)
            {
                entity = await Services.Db.CustomFields
                    .FirstOrDefaultAsync(f => f.Id == model.Id && f.DataSourceId == model.DataSourceId && !f.IsDeleted);
                if (entity == null)
                {
                    return HttpNotFound();
                }
            }
            else
            {
                entity = new CustomField
                {
                    DataSourceId = model.DataSourceId,
                    IsDeleted = false
                };
                Services.Db.CustomFields.Add(entity);
            }

            entity.Name = model.Name.Trim();
            entity.Label = model.Label.Trim();
            entity.FieldType = model.FieldType;
            entity.IsRequired = model.IsRequired;
            entity.DisplayOrder = model.DisplayOrder;
            entity.IsPublicVisible = model.IsPublicVisible;
            entity.BooleanTrueLabel = string.IsNullOrWhiteSpace(model.BooleanTrueLabel) ? null : model.BooleanTrueLabel.Trim();
            entity.BooleanFalseLabel = string.IsNullOrWhiteSpace(model.BooleanFalseLabel) ? null : model.BooleanFalseLabel.Trim();
            entity.OptionsSourceType = model.OptionsSourceType;
            entity.StaticOptionsJson = string.IsNullOrWhiteSpace(model.StaticOptionsJson) ? null : model.StaticOptionsJson.Trim();
            entity.OptionsViewName = string.IsNullOrWhiteSpace(model.OptionsViewName) ? null : model.OptionsViewName.Trim();
            entity.OptionsValueColumn = string.IsNullOrWhiteSpace(model.OptionsValueColumn) ? null : model.OptionsValueColumn.Trim();
            entity.OptionsDisplayColumn = string.IsNullOrWhiteSpace(model.OptionsDisplayColumn) ? null : model.OptionsDisplayColumn.Trim();
            entity.IsGregorianDate = model.IsGregorianDate;

            await Services.Db.SaveChangesAsync();
            TempData["SuccessMessage"] = "فیلد سفارشی با موفقیت ذخیره شد.";
            return RedirectToAction("CustomFields", new { id = model.DataSourceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteCustomField(int id, int dataSourceId)
        {
            CustomField field = await Services.Db.CustomFields
                .FirstOrDefaultAsync(f => f.Id == id && f.DataSourceId == dataSourceId);
            if (field == null)
            {
                return HttpNotFound();
            }

            field.IsDeleted = true;
            await Services.Db.SaveChangesAsync();
            TempData["SuccessMessage"] = "فیلد حذف شد.";
            return RedirectToAction("CustomFields", new { id = dataSourceId });
        }

        [HttpGet]
        public async Task<ActionResult> Indicators(int id)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
            if (ds == null)
            {
                return HttpNotFound();
            }

            List<string> availableCols = await GetAvailableBaseFieldNamesAsync(ds);
            List<AssetIndicator> indicators = await Services.Db.AssetIndicators
                .Where(a => a.DataSourceId == id)
                .OrderBy(a => a.Id)
                .ToListAsync();

            var vm = new AdminIndicatorConfigViewModel
            {
                DataSourceId = ds.Id,
                DataSourceName = ds.Name,
                AvailableColumns = availableCols,
                SelectedIndicatorColumns = ParseStringList(ds.IndicatorColumnsJson),
                IndicatorSeparator = string.IsNullOrEmpty(ds.IndicatorSeparator) ? "-" : ds.IndicatorSeparator,
                IndicatorPrefix = ds.IndicatorPrefix,
                GeneratedCount = indicators.Count,
                Indicators = indicators.Select(a => new AssetIndicatorItemViewModel
                {
                    Id = a.Id,
                    AssetKey = a.AssetKey,
                    Indicator = a.Indicator,
                    QrPayloadUrl = a.QrPayloadUrl,
                    QrPngBase64 = Services.IndicatorQrService.GenerateQrPngBase64(a.QrPayloadUrl, pixelsPerModule: 6)
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveIndicatorConfig(AdminIndicatorConfigViewModel model)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == model.DataSourceId);
            if (ds == null)
            {
                return HttpNotFound();
            }

            List<string> selectedCols = (model.SelectedIndicatorColumns ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            ds.IndicatorColumnsJson = JsonConvert.SerializeObject(selectedCols);
            ds.IndicatorSeparator = string.IsNullOrEmpty(model.IndicatorSeparator) ? "-" : model.IndicatorSeparator;
            ds.IndicatorPrefix = string.IsNullOrWhiteSpace(model.IndicatorPrefix) ? null : model.IndicatorPrefix.Trim();

            await Services.Db.SaveChangesAsync();
            TempData["SuccessMessage"] = "الگوی تولید شناسه یکتا ذخیره شد.";
            return RedirectToAction("Indicators", new { id = ds.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateIndicators(int id)
        {
            string baseUrl = Request.Url != null
                ? Request.Url.GetLeftPart(UriPartial.Authority) + Request.ApplicationPath?.TrimEnd('/')
                : string.Empty;

            int count = await Services.IndicatorQrService.GenerateIndicatorsForDataSourceAsync(id, baseUrl);
            TempData["SuccessMessage"] = count + " شناسه و کد QR با موفقیت پردازش شد.";
            return RedirectToAction("Indicators", new { id });
        }

        [HttpGet]
        public async Task<ActionResult> Personnel(int id)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
            if (ds == null)
            {
                return HttpNotFound();
            }

            List<FormPersonnel> assigned = await Services.Db.FormPersonnel
                .Where(p => p.DataSourceId == id)
                .OrderBy(p => p.FullName)
                .ToListAsync();

            List<AppUser> users = await Services.Db.AppUsers
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var vm = new AdminPersonnelConfigViewModel
            {
                DataSourceId = ds.Id,
                DataSourceName = ds.Name,
                AssignedPersonnel = assigned.Select(p => new FormPersonnelItemViewModel
                {
                    Id = p.Id,
                    Username = p.Username,
                    FullName = p.FullName,
                    IsActive = p.IsActive,
                    AssignedAtJalali = Services.PersianDateService.ToJalaliString(p.AssignedAtUtc.ToLocalTime(), includeTime: true)
                }).ToList(),
                AvailableUsers = users.Select(u => new AppUserOptionViewModel
                {
                    Username = u.Username,
                    FullName = u.FullName,
                    IsAdmin = u.IsAdmin
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AssignPersonnel(int dataSourceId, string selectedUsername)
        {
            if (string.IsNullOrWhiteSpace(selectedUsername))
            {
                TempData["ErrorMessage"] = "لطفاً یک کاربر انتخاب کنید.";
                return RedirectToAction("Personnel", new { id = dataSourceId });
            }

            string trimmed = selectedUsername.Trim();
            AppUser user = await Services.Db.AppUsers.FirstOrDefaultAsync(u => u.Username == trimmed && u.IsActive);
            if (user == null)
            {
                TempData["ErrorMessage"] = "کاربر انتخاب‌شده یافت نشد.";
                return RedirectToAction("Personnel", new { id = dataSourceId });
            }

            FormPersonnel existing = await Services.Db.FormPersonnel
                .FirstOrDefaultAsync(p => p.DataSourceId == dataSourceId && p.Username == user.Username);

            if (existing != null)
            {
                existing.IsActive = true;
                existing.FullName = user.FullName;
            }
            else
            {
                Services.Db.FormPersonnel.Add(new FormPersonnel
                {
                    DataSourceId = dataSourceId,
                    Username = user.Username,
                    FullName = user.FullName,
                    IsActive = true,
                    AssignedAtUtc = DateTime.UtcNow
                });
            }

            await Services.Db.SaveChangesAsync();
            TempData["SuccessMessage"] = "کاربر به لیست پرسنل مجاز اضافه شد.";
            return RedirectToAction("Personnel", new { id = dataSourceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemovePersonnel(int id, int dataSourceId)
        {
            FormPersonnel item = await Services.Db.FormPersonnel
                .FirstOrDefaultAsync(p => p.Id == id && p.DataSourceId == dataSourceId);
            if (item != null)
            {
                Services.Db.FormPersonnel.Remove(item);
                await Services.Db.SaveChangesAsync();
            }

            return RedirectToAction("Personnel", new { id = dataSourceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateUser(AdminPersonnelConfigViewModel model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.NewUsername) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.NewFullName))
            {
                TempData["ErrorMessage"] = "نام کاربری، رمز عبور و نام کامل برای ایجاد کاربر الزامی است.";
                return RedirectToAction("Personnel", new { id = model?.DataSourceId ?? 0 });
            }

            string username = model.NewUsername.Trim();
            bool exists = await Services.Db.AppUsers.AnyAsync(u => u.Username == username);
            if (exists)
            {
                TempData["ErrorMessage"] = "نام کاربری تکراری است.";
                return RedirectToAction("Personnel", new { id = model.DataSourceId });
            }

            var newUser = new AppUser
            {
                Username = username,
                PasswordHash = Services.AuthService.HashPassword(model.NewPassword),
                FullName = model.NewFullName.Trim(),
                IsAdmin = model.NewUserIsAdmin,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            Services.Db.AppUsers.Add(newUser);
            await Services.Db.SaveChangesAsync();

            TempData["SuccessMessage"] = "کاربر جدید ایجاد شد.";
            return RedirectToAction("Personnel", new { id = model.DataSourceId });
        }

        [HttpGet]
        public async Task<ActionResult> PrintQrSheet(int id, string mode = "A4")
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == id);
            if (ds == null)
            {
                return HttpNotFound();
            }

            List<string> captionFields = ParseStringList(ds.LabelCaptionFieldsJson);
            List<AssetIndicator> indicators = await Services.Db.AssetIndicators
                .Where(a => a.DataSourceId == id)
                .OrderBy(a => a.Id)
                .ToListAsync();

            var vm = new PrintQrSheetViewModel
            {
                DataSourceId = ds.Id,
                DataSourceName = ds.Name,
                LayoutMode = string.Equals(mode, "Thermal", StringComparison.OrdinalIgnoreCase) ? "Thermal" : "A4",
                LabelCaptionFields = captionFields
            };

            foreach (AssetIndicator item in indicators)
            {
                var qrItem = new PrintQrItemViewModel
                {
                    Indicator = item.Indicator,
                    AssetKey = item.AssetKey,
                    QrPayloadUrl = item.QrPayloadUrl,
                    QrPngBase64 = Services.IndicatorQrService.GenerateQrPngBase64(
                        item.QrPayloadUrl,
                        pixelsPerModule: vm.LayoutMode == "Thermal" ? 8 : 6)
                };

                if (!string.IsNullOrWhiteSpace(item.CachedBaseFieldsJson) && captionFields.Count > 0)
                {
                    try
                    {
                        var baseDict = JsonConvert.DeserializeObject<Dictionary<string, AssetFieldValueDto>>(item.CachedBaseFieldsJson);
                        if (baseDict != null)
                        {
                            var ciDict = new Dictionary<string, AssetFieldValueDto>(baseDict, StringComparer.OrdinalIgnoreCase);
                            foreach (string cap in captionFields)
                            {
                                AssetFieldValueDto val;
                                if (ciDict.TryGetValue(cap, out val) && val != null)
                                {
                                    qrItem.Captions[cap] = val.DisplayValue ?? string.Empty;
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore malformed JSON cache
                    }
                }

                vm.Items.Add(qrItem);
            }

            return View(vm);
        }

        private async Task<List<string>> GetAvailableBaseFieldNamesAsync(DataSource ds)
        {
            if (ds.IsEav)
            {
                List<EavFieldDefinitionDto> eavFields = await Services.SqlViewInspectorService.DiscoverEavFieldsAsync(ds);
                var list = new List<string>();
                if (!string.IsNullOrWhiteSpace(ds.UniqueKeyColumn))
                {
                    list.Add(ds.UniqueKeyColumn);
                }

                foreach (EavFieldDefinitionDto f in eavFields)
                {
                    if (!list.Contains(f.FieldName, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(f.FieldName);
                    }
                }

                return list;
            }

            ViewMetadataDto metadata = await Services.SqlViewInspectorService.InspectViewMetadataAsync(ds.ViewName);
            return metadata.Columns.Select(c => c.ColumnName).ToList();
        }

        private static List<string> ParseStringList(string json)
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
