using System.Text;

namespace Demo
{
    internal static class StringBuilderX
    {
        public static void AppendAndWriteLine(this StringBuilder sb, string? s = null)
        {
            if(string.IsNullOrWhiteSpace(s))
            {
                sb.AppendLine();
                Console.WriteLine();
            }
            else
            {
                sb.AppendLine(s);
                Console.WriteLine(s);
            }
        }
    }
}
