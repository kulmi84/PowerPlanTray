using System.Text.Json;

namespace PowerPlanTray;

// Claude Code transcripts have no explicit start event. Every prompt, tool call
// and tool result marks work; an assistant message that ends the turn starts grace.
// Subagent records (isSidechain) never end the main conversation's turn.
internal sealed class ClaudeSessionLog : AgentSessionLog
{
    // Claude Code writes nothing while a long tool call runs (up to ten minutes
    // for a foreground shell command), so allow a longer silence than Codex.
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);
    internal override TimeSpan ActivityTimeout => Timeout;
    private bool _sawMainThread;

    private protected override void Reset() => _sawMainThread = false;

    private protected override void ReadEvent(JsonElement root, string kind, DateTimeOffset eventTime)
    {
        if (kind is not ("user" or "assistant")) return;
        var sidechain = root.TryGetProperty("isSidechain", out var side) && side.ValueKind == JsonValueKind.True;
        if (!sidechain) _sawMainThread = true;
        root.TryGetProperty("message", out var message);
        if (kind == "assistant" && StringProperty(message, "stop_reason") is "end_turn" or "stop_sequence" or "refusal")
        {
            // A dedicated subagent log ends with its own final message; inside a
            // main transcript that message only hands control back to the main turn.
            if (!sidechain || !_sawMainThread) Complete(eventTime);
            else if (Active) LastActivity = eventTime;
            return;
        }
        Start(eventTime);
    }
}
