using System;
using HSEQRCode.Data;
using HSEQRCode.Services.Implementations;
using HSEQRCode.Services.Interfaces;

namespace HSEQRCode.Web.Infrastructure
{
    /// <summary>
    /// Lightweight per-request service factory for MVC 5 and Web API 2 controllers.
    /// </summary>
    public sealed class ServiceFactory : IDisposable
    {
        public ServiceFactory()
        {
            Db = new HseDbContext();
            PersianDateService = new PersianDateService();
            SqlViewInspectorService = new SqlViewInspectorService(PersianDateService);
            AuthService = new AuthService(Db);
            IndicatorQrService = new IndicatorQrService(Db, SqlViewInspectorService);
            InspectionService = new InspectionService(Db, SqlViewInspectorService, PersianDateService, AuthService);
        }

        public HseDbContext Db { get; }

        public IPersianDateService PersianDateService { get; }

        public ISqlViewInspectorService SqlViewInspectorService { get; }

        public IAuthService AuthService { get; }

        public IIndicatorQrService IndicatorQrService { get; }

        public IInspectionService InspectionService { get; }

        public void Dispose()
        {
            Db?.Dispose();
        }
    }
}
