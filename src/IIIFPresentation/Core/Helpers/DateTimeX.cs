namespace Core.Helpers;

public static class DateTimeX
{
    private static class Precision
    {
        public static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    }

    public static DateTime Floor(this DateTime dateTime, TimeSpan interval) =>
        dateTime.AddTicks(-(dateTime.Ticks % interval.Ticks));

    /// <summary>
    /// Truncates DateTime to whole seconds.
    /// </summary>
    public static DateTime ToSecondPrecision(this DateTime dateTime) => dateTime.Floor(Precision.Second);
}
