using System.Text.Json;

namespace PowerPlanTray;

// Retain the byte offset even after a session becomes stale. Otherwise old logs
// are read from the beginning again on every timer tick.
internal sealed class CodexSessionLog
{
    internal static readonly TimeSpan ActivityTimeout = TimeSpan.FromMinutes(5);
    private const int MaxLineBytes = 2 * 1024 * 1024;
    private readonly MemoryStream _line = new();
    private bool _oversizedLine;
    internal long Position { get; private set; }
    internal bool HasMore { get; private set; }
    internal bool Active { get; private set; }
    internal DateTimeOffset LastActivity { get; private set; } = DateTimeOffset.MinValue;
    internal DateTimeOffset? GraceStart { get; private set; }

    // A start without a matching completion can survive a disconnected or
    // abandoned chat. Require recent work, and never trust a partially replayed log.
    internal bool IsActive(DateTimeOffset now) =>
        !HasMore && Active && now - LastActivity <= ActivityTimeout;

    internal long Read(string path, byte[] buffer)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var length = stream.Length;
        if (length < Position)
        {
            Position = 0;
            _line.SetLength(0);
            _oversizedLine = false;
            Active = false;
            GraceStart = null;
            LastActivity = DateTimeOffset.MinValue;
        }
        stream.Seek(Position, SeekOrigin.Begin);
        var start = Position;
        // Bound each pass, including the first read of a large session. Continue
        // from this position next tick; never chase a file that keeps growing.
        var end = Math.Min(length, Position + 256 * 1024);
        while (stream.Position < end)
        {
            var count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, end - stream.Position));
            if (count == 0) break;
            var offset = 0;
            while (offset < count)
            {
                var newline = Array.IndexOf(buffer, (byte)'\n', offset, count - offset);
                var stop = newline < 0 ? count : newline;
                var size = stop - offset;
                if (!_oversizedLine && _line.Length + size <= MaxLineBytes)
                    _line.Write(buffer, offset, size);
                else
                {
                    _oversizedLine = true;
                    _line.SetLength(0);
                }
                if (newline < 0) break;
                if (!_oversizedLine && _line.Length > 0)
                    ReadEvent(_line.GetBuffer().AsMemory(0, (int)_line.Length));
                _line.SetLength(0);
                if (_line.Capacity > 64 * 1024) _line.Capacity = 64 * 1024;
                _oversizedLine = false;
                offset = newline + 1;
            }
        }
        Position = stream.Position;
        HasMore = Position < length;
        return Position - start;
    }

    private void ReadEvent(ReadOnlyMemory<byte> line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var kind)) return;
            var observedAt = DateTimeOffset.UtcNow;
            var eventTime = observedAt;
            if (root.TryGetProperty("timestamp", out var timestamp) &&
                timestamp.ValueKind == JsonValueKind.String && timestamp.TryGetDateTimeOffset(out var parsed))
                eventTime = parsed > observedAt ? observedAt : parsed;
            if (kind.ValueKind != JsonValueKind.String || kind.GetString() != "event_msg" ||
                !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("type", out var payloadKind) || payloadKind.ValueKind != JsonValueKind.String)
            {
                if (Active && kind.ValueKind == JsonValueKind.String && kind.GetString() == "response_item" &&
                    root.TryGetProperty("payload", out var response) && response.ValueKind == JsonValueKind.Object &&
                    response.TryGetProperty("type", out var responseType) && responseType.ValueKind == JsonValueKind.String &&
                    (responseType.GetString() is "function_call" or "function_call_output" or "reasoning" ||
                     responseType.GetString() == "message" && response.TryGetProperty("role", out var role) &&
                     role.ValueKind == JsonValueKind.String && role.GetString() == "assistant"))
                    LastActivity = eventTime;
                return;
            }
            switch (payloadKind.GetString())
            {
                case "task_started":
                    Active = true;
                    GraceStart = null;
                    LastActivity = eventTime;
                    break;
                case "task_complete":
                case "turn_aborted":
                    Active = false;
                    GraceStart = eventTime;
                    LastActivity = eventTime;
                    break;
                case "token_count":
                case "token_usage_record":
                case "item_completed":
                case "agent_message":
                case "agent_reasoning":
                    if (Active) LastActivity = eventTime;
                    break;
            }
        }
        catch (JsonException) { /* Ignore malformed records, never log content. */ }
    }
}
