using System;
using System.Globalization;
using System.Text;
using HSEQRCode.Services.Interfaces;

namespace HSEQRCode.Services.Implementations
{
    public class PersianDateService : IPersianDateService
    {
        private static readonly PersianCalendar Calendar = new PersianCalendar();

        public string ToJalaliString(DateTime date, bool includeTime = false)
        {
            int year = Calendar.GetYear(date);
            int month = Calendar.GetMonth(date);
            int day = Calendar.GetDayOfMonth(date);

            if (includeTime)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0000}/{1:00}/{2:00} {3:00}:{4:00}",
                    year, month, day, date.Hour, date.Minute);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0000}/{1:00}/{2:00}", year, month, day);
        }

        public string ToJalaliString(DateTime? date, bool includeTime = false)
        {
            return date.HasValue ? ToJalaliString(date.Value, includeTime) : string.Empty;
        }

        public DateTime ParseJalali(string jalaliDate)
        {
            DateTime result;
            if (!TryParseJalali(jalaliDate, out result))
            {
                throw new FormatException("Invalid Jalali date format. Expected yyyy/MM/dd or yyyy-MM-dd.");
            }

            return result;
        }

        public bool TryParseJalali(string jalaliDate, out DateTime result)
        {
            result = default(DateTime);
            if (string.IsNullOrWhiteSpace(jalaliDate))
            {
                return false;
            }

            string normalized = NormalizeDigits(jalaliDate).Trim();

            // Strip optional time part if present (e.g. "1404/01/15 10:30")
            int spaceIdx = normalized.IndexOf(' ');
            string datePart = spaceIdx > 0 ? normalized.Substring(0, spaceIdx) : normalized;

            string[] parts = datePart.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            int year;
            int month;
            int day;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out year) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out month) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out day))
            {
                return false;
            }

            if (year < 1 || year > 9378 || month < 1 || month > 12 || day < 1 || day > 31)
            {
                return false;
            }

            try
            {
                int daysInMonth = Calendar.GetDaysInMonth(year, month);
                if (day > daysInMonth)
                {
                    return false;
                }

                result = Calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public string FormatDateForDisplay(DateTime? date, bool isGregorian, bool includeTime = false)
        {
            if (!date.HasValue)
            {
                return string.Empty;
            }

            if (isGregorian)
            {
                return includeTime
                    ? date.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return ToJalaliString(date.Value, includeTime);
        }

        public string NormalizeDigits(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var sb = new StringBuilder(input.Length);
            foreach (char ch in input)
            {
                // Persian digits: U+06F0..U+06F9
                if (ch >= '\u06F0' && ch <= '\u06F9')
                {
                    sb.Append((char)('0' + (ch - '\u06F0')));
                }
                // Arabic-Indic digits: U+0660..U+0669
                else if (ch >= '\u0660' && ch <= '\u0669')
                {
                    sb.Append((char)('0' + (ch - '\u0660')));
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }
    }
}
