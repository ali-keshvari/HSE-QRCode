using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Security;
using System.Threading.Tasks;
using System.Web.Http;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Domain.Entities;
using HSEQRCode.Domain.Enums;
using HSEQRCode.Web.Infrastructure;

namespace HSEQRCode.Web.Controllers.Api
{
    [Authorize(Roles = "Admin")]
    [RoutePrefix("api/meta")]
    public class MetaApiController : ApiController
    {
        private ServiceFactory _services;

        private ServiceFactory Services => _services ?? (_services = new ServiceFactory());

        [HttpGet]
        [Route("views")]
        public async Task<IHttpActionResult> GetViews()
        {
            List<ViewMetadataDto> views = await Services.SqlViewInspectorService.GetAvailableViewsAsync();
            return Ok(views);
        }

        [HttpGet]
        [Route("view-columns")]
        public async Task<IHttpActionResult> GetViewColumns(string viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName))
            {
                return BadRequest("viewName is required.");
            }

            try
            {
                ViewMetadataDto metadata = await Services.SqlViewInspectorService.InspectViewMetadataAsync(viewName);
                return Ok(metadata.Columns);
            }
            catch (SecurityException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("eav-fields")]
        public async Task<IHttpActionResult> GetEavFields(int dataSourceId)
        {
            DataSource ds = await Services.Db.DataSources.FirstOrDefaultAsync(d => d.Id == dataSourceId);
            if (ds == null)
            {
                return NotFound();
            }

            List<EavFieldDefinitionDto> fields = await Services.SqlViewInspectorService.DiscoverEavFieldsAsync(ds);
            return Ok(fields);
        }

        [HttpPost]
        [Route("preview-options")]
        public async Task<IHttpActionResult> PreviewOptions(PreviewOptionsRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            var tempField = new CustomField
            {
                OptionsSourceType = request.OptionsSourceType,
                StaticOptionsJson = request.StaticOptionsJson,
                OptionsViewName = request.OptionsViewName,
                OptionsValueColumn = request.OptionsValueColumn,
                OptionsDisplayColumn = request.OptionsDisplayColumn
            };

            try
            {
                List<SelectOptionDto> options = await Services.SqlViewInspectorService.LoadSelectOptionsAsync(tempField);
                return Ok(options);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _services?.Dispose();
                _services = null;
            }

            base.Dispose(disposing);
        }
    }

    public class PreviewOptionsRequest
    {
        public OptionsSourceType OptionsSourceType { get; set; }

        public string StaticOptionsJson { get; set; }

        public string OptionsViewName { get; set; }

        public string OptionsValueColumn { get; set; }

        public string OptionsDisplayColumn { get; set; }
    }
}
