namespace Common;

public static class ConsoleUi
{
    public static void WriteUserPrompt() => WritePrefix("Me > ", ConsoleColor.Cyan);

    public static void WriteAgentPrompt() => WritePrefix("Agent > ", ConsoleColor.Green);

    public static void WriteAgentChunk(object? chunk) =>
        WriteColored(chunk?.ToString() ?? string.Empty, ConsoleColor.Yellow);

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
