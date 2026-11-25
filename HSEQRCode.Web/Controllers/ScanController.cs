using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Domain.Enums;
using HSEQRCode.Services.Models;
using HSEQRCode.Web.ViewModels;

namespace HSEQRCode.Web.Controllers
{
    public class ScanController : BaseMvcController
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> ScanByIndicator(string indicator)
        {
            if (string.IsNullOrWhiteSpace(indicator))
            {
                return RedirectToAction("Index");
            }

            string decoded = Uri.UnescapeDataString(indicator).Trim();
            PublicScanResult result = await Services.InspectionService.GetPublicScanDataAsync(decoded);
            if (result == null)
            {
                ViewBag.NotFoundIndicator = decoded;
                return View("NotFound");
            }

            bool isAuth = User.Identity != null && User.Identity.IsAuthenticated;
            bool canInspect = isAuth && await Services.AuthService.IsUserAuthorizedForDataSourceAsync(result.DataSourceId, User.Identity.Name);

            var vm = new PublicScanViewModel
            {
                AssetIndicatorId = result.AssetIndicatorId,
                DataSourceId = result.DataSourceId,
                DataSourceName = result.DataSourceName,
                Indicator = result.Indicator,
                AssetKey = result.AssetKey,
                QrPayloadUrl = result.QrPayloadUrl,
                PublicBaseFields = result.PublicBaseFields,
                LatestInspection = result.LatestInspection,
                IsUserAuthenticated = isAuth,
                CanInspect = canInspect
            };

            return View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> Lookup(string indicator)
        {
            if (string.IsNullOrWhiteSpace(indicator))
            {
                return RedirectToAction("Index");
            }

            string trimmed = indicator.Trim();
            PublicScanResult result = await Services.InspectionService.GetPublicScanDataAsync(trimmed);
            if (result == null)
            {
                TempData["LookupError"] = "کد تجهیز یا شناسه '" + trimmed + "' یافت نشد.";
                return RedirectToAction("Index");
            }

            return RedirectToAction("ScanByIndicator", new { indicator = result.Indicator });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> LookupJson(string indicator)
        {
            if (string.IsNullOrWhiteSpace(indicator))
            {
                return Json(new { found = false, message = "شناسه وارد نشده است." }, JsonRequestBehavior.AllowGet);
            }

            string trimmed = indicator.Trim();
            PublicScanResult result = await Services.InspectionService.GetPublicScanDataAsync(trimmed);
            if (result == null)
            {
                return Json(new { found = false, message = "تجهیزی با این شناسه یافت نشد." }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                found = true,
                indicator = result.Indicator,
                assetKey = result.AssetKey,
                dataSourceName = result.DataSourceName,
                redirectUrl = Url.Action("ScanByIndicator", "Scan", new { indicator = result.Indicator }),
                data = result
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> Personnel(string indicator)
        {
            if (string.IsNullOrWhiteSpace(indicator))
            {
                return RedirectToAction("Index");
            }

            string decoded = Uri.UnescapeDataString(indicator).Trim();
            PersonnelInspectionResult result;
            try
            {
                result = await Services.InspectionService.GetPersonnelInspectionDataAsync(decoded, User.Identity.Name);
            }
            catch (UnauthorizedAccessException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "شما مجوز بازرسی این دسته تجهیزات را ندارید.");
            }

            if (result == null)
            {
                ViewBag.NotFoundIndicator = decoded;
                return View("NotFound");
            }

            var vm = MapPersonnelViewModel(result);
            vm.SuccessMessage = TempData["SuccessMessage"] as string;
            vm.ErrorMessage = TempData["ErrorMessage"] as string;
            return View(vm);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SubmitInspection(string indicator, string notes)
        {
            if (string.IsNullOrWhiteSpace(indicator))
            {
                return RedirectToAction("Index");
            }

            string decoded = Uri.UnescapeDataString(indicator).Trim();
            PersonnelInspectionResult currentData;
            try
            {
                currentData = await Services.InspectionService.GetPersonnelInspectionDataAsync(decoded, User.Identity.Name);
            }
            catch (UnauthorizedAccessException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "شما مجوز ثبت بازرسی برای این دسته تجهیزات را ندارید.");
            }

            if (currentData == null)
            {
                ViewBag.NotFoundIndicator = decoded;
                return View("NotFound");
            }

            var request = new InspectionSubmissionRequest
            {
                Indicator = currentData.Indicator,
                Username = User.Identity.Name,
                Notes = notes,
                UploadRootPath = ResolveUploadRootPath()
            };

            foreach (CustomFieldInputDefinition field in currentData.CustomFields)
            {
                string formKey = "field_" + field.CustomFieldId;

                if (field.FieldType == CustomFieldType.MultiSelect)
                {
                    string[] vals = Request.Form.GetValues(formKey);
                    request.MultiSelectValues[field.CustomFieldId] = vals != null ? vals.ToList() : new List<string>();
                }
                else if (field.FieldType == CustomFieldType.Attachment)
                {
                    HttpPostedFileBase postedFile = Request.Files["file_" + field.CustomFieldId];
                    if (postedFile != null && postedFile.ContentLength > 0)
                    {
                        request.Files[field.CustomFieldId] = new UploadedFileInput
                        {
                            FileName = postedFile.FileName,
                            ContentType = postedFile.ContentType,
                            InputStream = postedFile.InputStream
                        };
                    }
                }
                else
                {
                    request.ScalarValues[field.CustomFieldId] = Request.Form[formKey];
                }
            }

            try
            {
                await Services.InspectionService.SubmitInspectionAsync(request);
                TempData["SuccessMessage"] = "بازرسی با موفقیت ثبت شد.";
                return RedirectToAction("Personnel", new { indicator = currentData.Indicator });
            }
            catch (Exception ex)
            {
                var vm = MapPersonnelViewModel(currentData);
                vm.Notes = notes;
                vm.ErrorMessage = ex.Message;
                return View("Personnel", vm);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> DownloadAttachment(int id)
        {
            string username = User.Identity != null && User.Identity.IsAuthenticated ? User.Identity.Name : null;
            InspectionRecordValue val;
            try
            {
                val = await Services.InspectionService.GetAttachmentForDownloadAsync(id, username);
            }
            catch (UnauthorizedAccessException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            if (val == null || string.IsNullOrWhiteSpace(val.StoredFileName))
            {
                return HttpNotFound();
            }

            string uploadRoot = ResolveUploadRootPath();
            string safeStoredName = Path.GetFileName(val.StoredFileName);
            string fullPath = Path.Combine(uploadRoot, safeStoredName);
            if (!System.IO.File.Exists(fullPath))
            {
                return HttpNotFound();
            }

            string downloadName = string.IsNullOrWhiteSpace(val.OriginalFileName) ? safeStoredName : val.OriginalFileName;
            string contentType = string.IsNullOrWhiteSpace(val.ContentType) ? "application/octet-stream" : val.ContentType;
            return File(fullPath, contentType, downloadName);
        }

        private string ResolveUploadRootPath()
        {
            string configured = ConfigurationManager.AppSettings["UploadRootPath"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = "~/App_Data/Uploads";
            }

            return configured.StartsWith("~") ? Server.MapPath(configured) : configured;
        }

        private static PersonnelInspectionViewModel MapPersonnelViewModel(PersonnelInspectionResult result)
        {
            return new PersonnelInspectionViewModel
            {
                AssetIndicatorId = result.AssetIndicatorId,
                DataSourceId = result.DataSourceId,
                DataSourceName = result.DataSourceName,
                Indicator = result.Indicator,
                AssetKey = result.AssetKey,
                QrPayloadUrl = result.QrPayloadUrl,
                BaseFields = result.BaseFields,
                CustomFields = result.CustomFields,
                InspectionHistory = result.InspectionHistory
            };
        }
    }
}
