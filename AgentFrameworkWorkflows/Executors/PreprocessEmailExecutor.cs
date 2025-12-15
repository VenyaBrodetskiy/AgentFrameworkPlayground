using System.Text.RegularExpressions;
using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Deterministic: cleans inbound emails and masks obvious PII.
/// </summary>
internal sealed partial class PreprocessEmailExecutor(string id) : Executor<string, EmailDocument>(id)
{
    public override async ValueTask<EmailDocument> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var original = message ?? string.Empty;
        var lines = SplitLines(original);

        string? from = TryExtractHeaderValue(lines, "From:");
        string? subject = TryExtractHeaderValue(lines, "Subject:");

        var body = RemoveHeaders(lines);
        body = StripQuotedReplies(body);
        body = NormalizeWhitespace(body);

        var detectedEmails = EmailRegex().Matches(body).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var detectedPhones = PhoneRegex()
            .Matches(body)
            .Select(m => m.Value)
            .Where(IsLikelyPhoneNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var detectedOrderIds = OrderIdRegex().Matches(body).Select(m => m.Groups["id"].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var modelSafe = MaskPii(body);
        var containsPii = detectedEmails.Count > 0 || detectedPhones.Count > 0;

        var email = new EmailDocument
        {
            OriginalText = original,
            CleanText = body,
            ModelSafeText = modelSafe,
            From = from,
            Subject = subject,
            ContainsPii = containsPii,
            DetectedEmails = detectedEmails,
            DetectedPhones = detectedPhones,
            DetectedOrderIds = detectedOrderIds,
        };

        await context.AddEventAsync(new EmailPreprocessedEvent(email), cancellationToken);
        await context.QueueStateUpdateAsync(SupportRunState.KeyEmail, email, scopeName: SupportRunState.ScopeName);
        return email;
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Split('\n');

    private static string? TryExtractHeaderValue(string[] lines, string headerPrefix)
    {
        foreach (var line in lines.Take(15))
        {
            if (line.StartsWith(headerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[headerPrefix.Length..].Trim();
            }
        }

        return null;
    }

    private static string RemoveHeaders(string[] lines)
    {
        // If the email looks like it has headers, drop everything until the first blank line.
        var blankIdx = Array.FindIndex(lines, l => string.IsNullOrWhiteSpace(l));
        if (blankIdx >= 0 && blankIdx < lines.Length - 1)
        {
            return string.Join("\n", lines[(blankIdx + 1)..]).Trim();
        }

        return string.Join("\n", lines).Trim();
    }

    private static string StripQuotedReplies(string text)
    {
        // Remove common quote prefixes and "Original Message" blocks (best effort).
        var lines = SplitLines(text);
        var filtered = lines
            .TakeWhile(l =>
                !l.StartsWith("-----Original Message-----", StringComparison.OrdinalIgnoreCase) &&
                !l.StartsWith("From:", StringComparison.OrdinalIgnoreCase) &&
                !l.TrimStart().StartsWith(">", StringComparison.Ordinal));

        return string.Join("\n", filtered).Trim();
    }

    private static string NormalizeWhitespace(string text) =>
        Regex.Replace(text, @"[ \t]+\n", "\n").Trim();

    private static string MaskPii(string text)
    {
        var masked = EmailRegex().Replace(text, "[REDACTED_EMAIL]");
        masked = PhoneRegex().Replace(masked, m => IsLikelyPhoneNumber(m.Value) ? "[REDACTED_PHONE]" : m.Value);
        return masked;
    }

    private static bool IsLikelyPhoneNumber(string candidate)
    {
        // Filter out false-positives like dates (e.g., 2025-12-14) by requiring >= 9 digits.
        var digitCount = candidate.Count(char.IsDigit);
        return digitCount is >= 9 and <= 15;
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\+?\d[\d\s().-]{7,}\d", RegexOptions.IgnoreCase)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(?:order|ticket|case)\s*#?\s*(?<id>[A-Z0-9-]{5,})\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrderIdRegex();
}

