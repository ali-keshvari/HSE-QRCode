using System;
using HSEQRCode.Services.Implementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HSEQRCode.Tests
{
    [TestClass]
    public class PersianDateServiceTests
    {
        private PersianDateService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new PersianDateService();
        }

        [TestMethod]
        public void ToJalaliString_ConvertsGregorianToJalali_WithAndWithoutTime()
        {
            // 2025-03-21 is 1404/01/01 (Nowruz)
            var nowruz = new DateTime(2025, 3, 21, 9, 5, 0);
            Assert.AreEqual("1404/01/01", _service.ToJalaliString(nowruz));
            Assert.AreEqual("1404/01/01 09:05", _service.ToJalaliString(nowruz, includeTime: true));

            // 2026-11-06 is 1405/08/15
            var aban15 = new DateTime(2026, 11, 6, 14, 30, 0);
            Assert.AreEqual("1405/08/15", _service.ToJalaliString(aban15));
            Assert.AreEqual("1405/08/15 14:30", _service.ToJalaliString(aban15, includeTime: true));

            DateTime? nullDate = null;
            Assert.AreEqual(string.Empty, _service.ToJalaliString(nullDate));
        }

        [TestMethod]
        public void ParseJalali_ValidFormatsAndPersianDigits_ReturnsExpectedGregorianDate()
        {
            var expected = new DateTime(2026, 11, 6);

            Assert.AreEqual(expected, _service.ParseJalali("1405/08/15"));
            Assert.AreEqual(expected, _service.ParseJalali("1405-08-15"));
            Assert.AreEqual(expected, _service.ParseJalali("۱۴۰۵/۰۸/۱۵"));
            Assert.AreEqual(expected, _service.ParseJalali("  ۱۴۰۵-۰۸-۱۵ 10:45  "));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("1405/13/01")]
        [DataRow("1405/01/32")]
        [DataRow("1405/12/30")] // 1405 is not a Persian leap year
        [DataRow("not-a-date")]
        public void TryParseJalali_And_ParseJalali_InvalidInputs_FailGracefully(string input)
        {
            DateTime parsed;
            Assert.IsFalse(_service.TryParseJalali(input, out parsed));
            Assert.ThrowsException<FormatException>(() => _service.ParseJalali(input));
        }

        [TestMethod]
        public void FormatDateForDisplay_RespectsIsGregorianFlag()
        {
            var dt = new DateTime(2026, 11, 6, 16, 45, 0);

            Assert.AreEqual("2026-11-06", _service.FormatDateForDisplay(dt, isGregorian: true));
            Assert.AreEqual("2026-11-06 16:45", _service.FormatDateForDisplay(dt, isGregorian: true, includeTime: true));
            Assert.AreEqual("1405/08/15", _service.FormatDateForDisplay(dt, isGregorian: false));
            Assert.AreEqual("1405/08/15 16:45", _service.FormatDateForDisplay(dt, isGregorian: false, includeTime: true));
            Assert.AreEqual(string.Empty, _service.FormatDateForDisplay(null, isGregorian: false));
        }
    }
}
