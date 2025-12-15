namespace Common;

public static class ConsoleUi
{
    public static void WriteUserPrompt() => WritePrefix("Me > ", ConsoleColor.Cyan);

    public static void WriteAgentPrompt() => WritePrefix("Agent > ", ConsoleColor.Green);

    public static void WriteAgentChunk(object? chunk) =>
        WriteColored(chunk?.ToString() ?? string.Empty, ConsoleColor.Yellow);

    public static void WriteHistoricalUserPrompt() => WritePrefix("Me > ", ConsoleColor.DarkCyan);

    public static void WriteHistoricalAgentPrompt() => WritePrefix("Agent > ", ConsoleColor.DarkGreen);

    public static void WriteHistoricalMessage(string text) =>
        WriteColored(text, ConsoleColor.DarkGray);

    public static void WriteSectionTitle(string title, ConsoleColor color = ConsoleColor.Cyan)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"\n=== {title} ===");
        Console.ResetColor();
    }

    public static void WriteSection(string title, string content, ConsoleColor titleColor = ConsoleColor.Cyan)
    {
        WriteSectionTitle(title, titleColor);
        Console.WriteLine(content);
    }

    public static void WriteColoredLine(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static void WritePrefix(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void WriteColored(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }
}
