using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace PowerPlanTray;

/// <summary>
/// Observes Codex JSONL status events and holds a Windows SYSTEM power request
/// while a turn is active or its user-configured grace period has not elapsed.
/// Never reads or displays prompt or response contents.
/// </summary>
#if CODEX_EDITION
internal sealed class CodexStandbyGuard : IDisposable
{
    private static readonly TimeSpan StaleTaskTimeout = TimeSpan.FromMinutes(90);
    private static readonly int[] SupportedGraceMinutes = { 5, 10, 15, 30, 60 };
    private readonly object _gate = new();
    private readonly Dictionary<string, SessionTail> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Timer _timer;
    private readonly string _sessionsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");

    private bool _enabled;
    private bool _disposed;
    private bool _polling;
    private int _graceMinutes = 15;
    private IntPtr _requestHandle = IntPtr.Zero;
    private bool _requestActive;
    private string _status = L.T("Deaktiviert");

    public CodexStandbyGuard()
    {
        _timer = new System.Threading.Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Status
    {
        get { lock (_gate) return _status; }
    }

    public void Configure(bool enabled, int graceMinutes)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _enabled = enabled;
            _graceMinutes = SupportedGraceMinutes.Contains(graceMinutes) ? graceMinutes : 15;

            if (!enabled)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                ReleaseRequest();
                _status = L.T("Deaktiviert");
            }
            else
            {
                _timer.Change(0, 2000);
            }
        }
    }

    private void Poll()
    {
        lock (_gate)
        {
            if (_disposed || !_enabled || _polling) return;
            _polling = true;
            try
            {
                if (Directory.Exists(_sessionsRoot))
                {
                    // Old sessions can still be resumed, so retain already watched paths.
                    var paths = Directory.EnumerateFiles(_sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                        .Select(path => new FileInfo(path))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .Take(250)
                        .Select(f => f.FullName)
                        .Concat(_sessions.Keys)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    foreach (var path in paths)
                        ReadAppendedEvents(path);
                }

                var now = DateTimeOffset.UtcNow;
                var activeCount = _sessions.Values.Count(session =>
                    session.Active && now - session.LastActivity <= StaleTaskTimeout);
                var graceUntil = _sessions.Values
                    .Where(session => !session.Active && session.GraceStart.HasValue)
                    .Select(session => session.GraceStart!.Value.AddMinutes(_graceMinutes))
                    .DefaultIfEmpty(DateTimeOffset.MinValue)
                    .Max();

                var shouldBlock = activeCount > 0 || now < graceUntil;
                if (shouldBlock)
                {
                    if (!AcquireRequest())
                    {
                        _status = L.T("Fehler: Windows-Energieanforderung fehlgeschlagen");
                        return;
                    }
                    _status = activeCount > 0
                        ? L.F($"Codex arbeitet ({activeCount} Sitzung(en)) - Standby gesperrt")
                        : L.F($"Nachlauf bis {graceUntil.ToLocalTime():HH:mm:ss} - Standby gesperrt");
                }
                else
                {
                    ReleaseRequest();
                    _status = L.T("Bereit - Windows-Standby normal");
                }

                // Drop obsolete tails; retained empty/old sessions do not prevent sleep.
                foreach (var path in _sessions
                    .Where(p => !p.Value.Active && now - p.Value.LastActivity > TimeSpan.FromDays(2))
                    .Select(p => p.Key).ToArray())
                    _sessions.Remove(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Fail safe: do not keep an old request on an unreadable log.
                ReleaseRequest();
                _status = L.T("Codex-Protokolle nicht lesbar - Standby normal");
            }
            finally
            {
                _polling = false;
            }
        }
    }

    private void ReadAppendedEvents(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (!_sessions.TryGetValue(path, out var session))
            {
                session = new SessionTail();
                _sessions[path] = session;
            }

            if (stream.Length < session.Position)
            {
                session.Position = 0;
                session.Line.SetLength(0);
                session.Active = false;
                session.GraceStart = null;
            }
            if (stream.Length == session.Position) return;

            stream.Seek(session.Position, SeekOrigin.Begin);
            var buffer = new byte[64 * 1024];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        if (session.Line.Length > 0)
                            ReadEvent(session, Encoding.UTF8.GetString(session.Line.ToArray()).TrimEnd('\r'));
                        session.Line.SetLength(0);
                    }
                    else if (session.Line.Length < 2 * 1024 * 1024)
                    {
                        // Bound memory when a response_item contains a very large payload.
                        session.Line.WriteByte(buffer[i]);
                    }
                }
            }
            session.Position = stream.Position;
        }
        catch (IOException) { /* A file being rotated will be checked again next tick. */ }
        catch (UnauthorizedAccessException) { /* Other Codex sessions remain monitored. */ }
    }

    private static void ReadEvent(SessionTail session, string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var kind)) return;

            var observedAt = DateTimeOffset.UtcNow;
            var eventTime = observedAt;
            if (root.TryGetProperty("timestamp", out var timestamp) &&
                timestamp.ValueKind == JsonValueKind.String &&
                timestamp.TryGetDateTimeOffset(out var parsed))
                eventTime = parsed > observedAt ? observedAt : parsed;

            // All new JSONL records indicate activity in this session.
            session.LastActivity = eventTime;
            if (kind.GetString() != "event_msg" ||
                !root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("type", out var payloadKind))
                return;

            switch (payloadKind.GetString())
            {
                case "task_started":
                    session.Active = true;
                    session.GraceStart = null;
                    break;
                case "task_complete":
                case "turn_aborted":
                    session.Active = false;
                    session.GraceStart = eventTime;
                    break;
            }
        }
        catch (JsonException) { /* Skip partial/unsupported messages, never log content. */ }
    }

    private bool AcquireRequest()
    {
        if (_requestActive) return true;
        if (_requestHandle == IntPtr.Zero)
        {
            var context = new PowerRequestContext
            {
                Version = 0,
                Flags = 1, // POWER_REQUEST_CONTEXT_SIMPLE_STRING
                Reason = L.T("Codex arbeitet oder Codex-Nachlaufzeit")
            };
            _requestHandle = PowerCreateRequest(ref context);
            if (_requestHandle == IntPtr.Zero || _requestHandle == new IntPtr(-1))
            {
                _requestHandle = IntPtr.Zero;
                return false;
            }
        }

        // PowerRequestSystemRequired = 1; never set PowerRequestDisplayRequired.
        _requestActive = PowerSetRequest(_requestHandle, 1);
        return _requestActive;
    }

    private void ReleaseRequest()
    {
        if (_requestActive)
        {
            PowerClearRequest(_requestHandle, 1);
            _requestActive = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timer.Dispose();
            ReleaseRequest();
            if (_requestHandle != IntPtr.Zero)
            {
                CloseHandle(_requestHandle);
                _requestHandle = IntPtr.Zero;
            }
        }
    }

    private sealed class SessionTail
    {
        public long Position;
        public MemoryStream Line { get; } = new();
        public bool Active;
        public DateTimeOffset LastActivity = DateTimeOffset.MinValue;
        public DateTimeOffset? GraceStart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PowerRequestContext
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Reason;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref PowerRequestContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr powerRequest, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr powerRequest, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
#else
// The standard edition contains no session watcher or standby power request.
internal sealed class CodexStandbyGuard : IDisposable
{
    public string Status => L.T("Nicht enthalten");
    public void Configure(bool enabled, int graceMinutes) { }
    public void Dispose() { }
}
#endif
