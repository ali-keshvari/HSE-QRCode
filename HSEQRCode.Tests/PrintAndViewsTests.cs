using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSEQRCode.Tests
{
    [TestClass]
    public class PrintAndViewsTests
    {
        private string _solutionRoot;

        [TestInitialize]
        public void Setup()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HSEQRCode.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate HSEQRCode.sln from test base directory.");
            _solutionRoot = dir.FullName;
        }

        [TestMethod]
        public void PrintQrCss_ContainsMediaPrint_A4AndThermalPageRules_AndNoPrintHiding()
        {
            string cssPath = Path.Combine(_solutionRoot, "HSEQRCode.Web", "Content", "print-qr.css");
            Assert.IsTrue(File.Exists(cssPath), "print-qr.css not found at " + cssPath);

            string css = File.ReadAllText(cssPath);
            Assert.IsTrue(css.Contains("@media print"), "Missing @media print rule.");
            Assert.IsTrue(css.Contains("size: A4 portrait"), "Missing A4 @page rule.");
            Assert.IsTrue(css.Contains("@page thermal-50x30"), "Missing thermal-50x30 @page rule.");
            Assert.IsTrue(css.Contains("@page thermal-60x40"), "Missing thermal-60x40 @page rule.");
            Assert.IsTrue(css.Contains(".qr-label-grid"), "Missing .qr-label-grid class.");
            Assert.IsTrue(css.Contains(".no-print"), "Missing .no-print class.");
            Assert.IsTrue(css.Contains("display: none !important"), "Missing display: none !important in print rules.");
        }

        [TestMethod]
        public void AllRequiredRazorViews_ExistInWebProject()
        {
            string[] requiredViews =
            {
                "_ViewStart.cshtml",
                @"Shared\_Layout.cshtml",
                @"Shared\_AdminLayout.cshtml",
                @"Account\Login.cshtml",
                @"Admin\Index.cshtml",
                @"Admin\CreateDataSource.cshtml",
                @"Admin\ConfigureBaseFields.cshtml",
                @"Admin\CustomFields.cshtml",
                @"Admin\Indicators.cshtml",
                @"Admin\Personnel.cshtml",
                @"Admin\PrintQrSheet.cshtml",
                @"Scan\Index.cshtml",
                @"Scan\ScanByIndicator.cshtml",
                @"Scan\Personnel.cshtml",
                @"Scan\NotFound.cshtml"
            };

            string viewsRoot = Path.Combine(_solutionRoot, "HSEQRCode.Web", "Views");
            foreach (string relativePath in requiredViews)
            {
                string fullPath = Path.Combine(viewsRoot, relativePath);
                Assert.IsTrue(File.Exists(fullPath), "Missing required Razor view: " + fullPath);
            }
        }
    }
}
