using System;

namespace FmsWeb.Helpers
{
    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = dt.DayOfWeek - startOfWeek;
            if (diff < 0)
            {
                diff += 7;
            }

            return dt.AddDays(-1 * diff).Date;
        }


        public static DateTime StartOfMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1);
        }

        public static DateTime EndOfMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1).AddMonths(1).AddDays(-1);
        }

        public static DateTime StartOfPreviousMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1).AddMonths(-1);
        }

        public static DateTime EndOfPreviousMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1).AddMonths(-1).AddDays(-1);
        }
    }
}