using System;

namespace HSEQRCode.Services.Interfaces
{
    /// <summary>
    /// Converts between Gregorian <see cref="DateTime"/> and Jalali (Persian) calendar strings.
    /// </summary>
    public interface IPersianDateService
    {
        string ToJalaliString(DateTime date, bool includeTime = false);

        string ToJalaliString(DateTime? date, bool includeTime = false);

        DateTime ParseJalali(string jalaliDate);

        bool TryParseJalali(string jalaliDate, out DateTime result);

        string FormatDateForDisplay(DateTime? date, bool isGregorian, bool includeTime = false);

        string NormalizeDigits(string input);
    }
}
