namespace Raven.Cli;

/// <summary>
/// Console output helpers for consistent RAVEN CLI formatting.
/// </summary>
internal static class ConsoleHelper
{
    public static void WriteBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("""

         ██████╗  █████╗ ██╗   ██╗███████╗███╗   ██╗
         ██╔══██╗██╔══██╗██║   ██║██╔════╝████╗  ██║
         ██████╔╝███████║██║   ██║█████╗  ██╔██╗ ██║
         ██╔══██╗██╔══██║╚██╗ ██╔╝██╔══╝  ██║╚██╗██║
         ██║  ██║██║  ██║ ╚████╔╝ ███████╗██║ ╚████║
         ╚═╝  ╚═╝╚═╝  ╚═╝  ╚═══╝  ╚══════╝╚═╝  ╚═══╝

         Rally-Aware AI Virtual Engineering Navigator
        """);
        Console.ResetColor();
    }

    public static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✓ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  ✗ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  ⚠ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  ℹ ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteAgent(string agentName, string message)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"  [{agentName.ToUpperInvariant()}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void WriteHeader(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  {title}");
        Console.WriteLine($"  {new string('─', title.Length)}");
        Console.ResetColor();
    }

    public static void WriteTable(string[] headers, List<string[]> rows)
    {
        if (rows.Count == 0)
        {
            WriteInfo("No data to display.");
            return;
        }

        var widths = new int[headers.Length];
        for (var i = 0; i < headers.Length; i++)
        {
            widths[i] = headers[i].Length;
            foreach (var row in rows)
            {
                if (i < row.Length && row[i].Length > widths[i])
                    widths[i] = row[i].Length;
            }
            widths[i] = Math.Min(widths[i], 60); // cap column width
        }

        Console.WriteLine();

        // Header
        Console.ForegroundColor = ConsoleColor.White;
        for (var i = 0; i < headers.Length; i++)
        {
            Console.Write($"  {headers[i].PadRight(widths[i])}  ");
        }
        Console.WriteLine();

        // Separator
        Console.ForegroundColor = ConsoleColor.DarkGray;
        for (var i = 0; i < headers.Length; i++)
        {
            Console.Write($"  {new string('─', widths[i])}  ");
        }
        Console.WriteLine();
        Console.ResetColor();

        // Rows
        foreach (var row in rows)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var val = i < row.Length ? row[i] : "";
                if (val.Length > widths[i])
                    val = val[..(widths[i] - 1)] + "…";
                Console.Write($"  {val.PadRight(widths[i])}  ");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    public static string Prompt(string message, string? defaultValue = null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  ? {message}");
        if (defaultValue is not null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" [{defaultValue}]");
        }
        Console.Write(": ");
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? (defaultValue ?? "") : input;
    }

    public static string PromptSecret(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  ? {message}: ");
        Console.ResetColor();

        var input = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && input.Length > 0)
            {
                input.Remove(input.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input.Append(key.KeyChar);
                Console.Write('*');
            }
        }

        return input.ToString();
    }
}
