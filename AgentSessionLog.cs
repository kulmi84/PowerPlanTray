using System.Text.Json;

namespace PowerPlanTray;

// Retain the byte offset even after a session becomes stale. Otherwise old logs
// are read from the beginning again on every timer tick.
internal abstract class AgentSessionLog
{
    private const int MaxLineBytes = 2 * 1024 * 1024;
    private readonly MemoryStream _line = new();
    private bool _oversizedLine;
    internal long Position { get; private set; }
    internal bool HasMore { get; private set; }
    internal bool Active { get; private protected set; }
    internal DateTimeOffset LastActivity { get; private protected set; } = DateTimeOffset.MinValue;
    internal DateTimeOffset? GraceStart { get; private protected set; }

    // How long an unfinished task may stay silent before it no longer blocks sleep.
    internal abstract TimeSpan ActivityTimeout { get; }

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
            Reset();
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
                    ParseEvent(_line.GetBuffer().AsMemory(0, (int)_line.Length));
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

    private void ParseEvent(ReadOnlyMemory<byte> line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var kind) ||
                kind.ValueKind != JsonValueKind.String) return;
            var observedAt = DateTimeOffset.UtcNow;
            var eventTime = observedAt;
            if (root.TryGetProperty("timestamp", out var timestamp) &&
                timestamp.ValueKind == JsonValueKind.String && timestamp.TryGetDateTimeOffset(out var parsed))
                eventTime = parsed > observedAt ? observedAt : parsed;
            ReadEvent(root, kind.GetString()!, eventTime);
        }
        catch (JsonException) { /* Ignore malformed records, never log content. */ }
    }

    // Called for every complete JSON object record with a string "type".
    // Implementations inspect only record structure, never prompt or response text.
    private protected abstract void ReadEvent(JsonElement root, string kind, DateTimeOffset eventTime);

    // Clears implementation state after the log was truncated.
    private protected virtual void Reset() { }

    private protected void Start(DateTimeOffset eventTime)
    {
        Active = true;
        GraceStart = null;
        LastActivity = eventTime;
    }

    private protected void Complete(DateTimeOffset eventTime)
    {
        Active = false;
        GraceStart = eventTime;
        LastActivity = eventTime;
    }

    private protected static string? StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
