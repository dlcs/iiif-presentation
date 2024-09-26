namespace Core.Helpers;

public static class DateTimeX
{
    public static class Precision
    {
        public static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    }

    public static DateTime Floor(this DateTime dateTime, TimeSpan interval) =>
        dateTime.AddTicks(-(dateTime.Ticks % interval.Ticks));
}