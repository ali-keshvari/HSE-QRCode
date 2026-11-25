using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using HSEQRCode.Data;
using HSEQRCode.Domain.DTOs;
using HSEQRCode.Services.Implementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSEQRCode.Tests
{
    [TestClass]
    public class SqlViewSecurityTests
    {
        [TestMethod]
        public void QuoteIdentifier_ValidNames_QuotesAndEscapesBrackets()
        {
            Assert.AreEqual("[AssetKey]", SqlViewMetadataQueries.QuoteIdentifier("AssetKey"));
            Assert.AreEqual("[AssetKey]", SqlViewMetadataQueries.QuoteIdentifier("[AssetKey]"));
            Assert.AreEqual("[Bad]]Name]", SqlViewMetadataQueries.QuoteIdentifier("Bad]Name"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("[]")]
        public void QuoteIdentifier_NullOrEmpty_ThrowsArgumentException(string identifier)
        {
            Assert.ThrowsException<ArgumentException>(() => SqlViewMetadataQueries.QuoteIdentifier(identifier));
        }

        [TestMethod]
        public void QuoteIdentifier_TooLongOrControlChars_ThrowsArgumentException()
        {
            string over128 = new string('A', 129);
            Assert.ThrowsException<ArgumentException>(() => SqlViewMetadataQueries.QuoteIdentifier(over128));
            Assert.ThrowsException<ArgumentException>(() => SqlViewMetadataQueries.QuoteIdentifier("Col\0Name"));
            Assert.ThrowsException<ArgumentException>(() => SqlViewMetadataQueries.QuoteIdentifier("Col\nName"));
        }

        [TestMethod]
        public void ParseViewName_ValidTwoPartAndSinglePart_ParsesCorrectly()
        {
            string schema;
            string view;

            SqlViewMetadataQueries.ParseViewName("gen.vw_FireExtinguishers_1001", out schema, out view);
            Assert.AreEqual("gen", schema);
            Assert.AreEqual("vw_FireExtinguishers_1001", view);

            SqlViewMetadataQueries.ParseViewName("vw_Simple", out schema, out view);
            Assert.AreEqual("dbo", schema);
            Assert.AreEqual("vw_Simple", view);

            SqlViewMetadataQueries.ParseViewName("[gen].[vw_FireExtinguishers_1001]", out schema, out view);
            Assert.AreEqual("gen", schema);
            Assert.AreEqual("vw_FireExtinguishers_1001", view);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("db.dbo.vw_ThreePart")]
        [DataRow("dbo.")]
        [DataRow(".vw_MissingSchema")]
        public void ParseViewName_InvalidOrThreePart_ThrowsArgumentException(string viewName)
        {
            string schema;
            string view;
            Assert.ThrowsException<ArgumentException>(() => SqlViewMetadataQueries.ParseViewName(viewName, out schema, out view));
        }

        [TestMethod]
        public void QuoteTwoPartName_InjectionPayload_SafelyQuotedAsIdentifier()
        {
            string payload = "dbo.Test; DROP TABLE AppUsers--";
            string quoted = SqlViewMetadataQueries.QuoteTwoPartName(payload);
            Assert.AreEqual("[dbo].[Test; DROP TABLE AppUsers--]", quoted);
        }

        [TestMethod]
        public void BuildSelectSql_ValidAndInvalidColumns_BehavesSafely()
        {
            string sql = SqlViewMetadataQueries.BuildSelectSql("gen.vw_Assets", new[] { "AssetKey", "Tag]No" });
            Assert.AreEqual("SELECT [AssetKey], [Tag]]No] FROM [gen].[vw_Assets]", sql);

            Assert.ThrowsException<ArgumentNullException>(() => SqlViewMetadataQueries.BuildSelectSql("gen.vw_Assets", null));
            Assert.ThrowsException<ArgumentException>(() => SqlViewMetadataQueries.BuildSelectSql("gen.vw_Assets", new string[0]));
        }

        [TestMethod]
        public void SqlViewInspectorService_ConstructorValidation_ThrowsOnMissingArgs()
        {
            var dateService = new PersianDateService();
            Assert.ThrowsException<InvalidOperationException>(() => new SqlViewInspectorService(string.Empty, dateService));
            Assert.ThrowsException<ArgumentNullException>(() => new SqlViewInspectorService("Server=.;Database=Test;", null));
        }

        [TestMethod]
        public async Task EnsureViewAndGetMetadataAsync_SystemSchemasAndMalformedNames_ThrowsSecurityExceptionBeforeSql()
        {
            var service = new SqlViewInspectorService("Server=.;Database=Fake;Integrated Security=true;", new PersianDateService());
            MethodInfo method = typeof(SqlViewInspectorService).GetMethod(
                "EnsureViewAndGetMetadataAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);

            using (var conn = new SqlConnection())
            {
                await AssertThrowsSecurityExceptionAsync(() => (Task)method.Invoke(service, new object[] { conn, "sys.views" }));
                await AssertThrowsSecurityExceptionAsync(() => (Task)method.Invoke(service, new object[] { conn, "INFORMATION_SCHEMA.TABLES" }));
                await AssertThrowsSecurityExceptionAsync(() => (Task)method.Invoke(service, new object[] { conn, "db.dbo.vw_Injected" }));
                await AssertThrowsSecurityExceptionAsync(() => (Task)method.Invoke(service, new object[] { conn, "" }));
            }
        }

        [TestMethod]
        public void RequireWhitelistedColumn_UnwhitelistedOrMissingColumn_ThrowsSecurityOrInvalidOp()
        {
            MethodInfo method = typeof(SqlViewInspectorService).GetMethod(
                "RequireWhitelistedColumn",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            var validColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AssetKey",
                "ManagedAssetId",
                "FieldName"
            };

            string valid = (string)method.Invoke(null, new object[] { validColumns, "assetkey", "UniqueKeyColumn" });
            Assert.AreEqual("assetkey", valid);

            TargetInvocationException secEx = Assert.ThrowsException<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { validColumns, "AssetKey; DROP TABLE AppUsers--", "UniqueKeyColumn" }));
            Assert.IsInstanceOfType(secEx.InnerException, typeof(SecurityException));

            TargetInvocationException invEx = Assert.ThrowsException<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { validColumns, "   ", "UniqueKeyColumn" }));
            Assert.IsInstanceOfType(invEx.InnerException, typeof(InvalidOperationException));
        }

        private static async Task AssertThrowsSecurityExceptionAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                Assert.Fail("Expected SecurityException was not thrown.");
            }
            catch (SecurityException)
            {
                // Expected
            }
        }
    }
}
