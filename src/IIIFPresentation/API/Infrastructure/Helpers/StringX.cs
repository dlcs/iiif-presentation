namespace API.Infrastructure.Helpers;

public static class StringX
{
    public static List<T> Append<T>(this List<T> list, params T[] values)
    {
        list.AddRange(values);
        return list;
    }

    public static List<T> AppendIf<T>(this List<T> list, bool condition, params T[] values)
    {
        return condition ? list.Append(values) : list;
    }

    public static string GetLastPathElement(this string path)
    {
        return path.Split('/').Last();
    }
} 