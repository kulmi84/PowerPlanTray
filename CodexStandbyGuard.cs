using System.Runtime.InteropServices;
using System.Collections.Concurrent;
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
    private readonly Dictionary<string, CodexSessionLog> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _changedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] _readBuffer = new byte[64 * 1024];
    private FileSystemWatcher? _watcher;
    private DateTimeOffset _nextDiscovery;
    private int _rescanRequested = 1;
    private readonly System.Threading.Timer _timer;
    private readonly string _sessionsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");

    private bool _enabled;
    private bool _disposed;
    private bool _polling;
    private int _graceMinutes = 15;
    private IntPtr _requestHandle = IntPtr.Zero;
    private bool _requestActive;
    private volatile string _status = L.T("Deaktiviert");

    public CodexStandbyGuard()
    {
        _timer = new System.Threading.Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Status
    {
        // Opening the tray menu must never wait for filesystem work in Poll.
        get { return _status; }
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
                _watcher?.Dispose();
                _watcher = null;
                _changedPaths.Clear();
                ReleaseRequest();
                _status = L.T("Deaktiviert");
            }
            else
            {
                Interlocked.Exchange(ref _rescanRequested, 1);
                _timer.Change(0, Timeout.Infinite);
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
                    EnsureWatcher();
                    var discoveryTime = DateTimeOffset.UtcNow;
                    if (Interlocked.Exchange(ref _rescanRequested, 0) != 0 || discoveryTime >= _nextDiscovery)
                    {
                        // Watcher events discover resumed old sessions immediately;
                        // this occasional scan also recovers missed watcher events.
                        foreach (var file in Directory.EnumerateFiles(_sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                            .Select(path => new FileInfo(path))
                            .Where(f => f.LastWriteTimeUtc >= discoveryTime.UtcDateTime.AddDays(-2))
                            .OrderByDescending(f => f.LastWriteTimeUtc).Take(250))
                        {
                            _sessions.TryAdd(file.FullName, new CodexSessionLog());
                            _pendingPaths.Add(file.FullName);
                        }
                        _nextDiscovery = discoveryTime.AddMinutes(1);
                    }
                    foreach (var path in _changedPaths.Keys)
                        if (_changedPaths.TryRemove(path, out _))
                        {
                            _sessions.TryAdd(path, new CodexSessionLog());
                            _pendingPaths.Add(path);
                        }

                    foreach (var path in _pendingPaths.ToArray())
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

                // Keep stale byte offsets: discarding them caused complete old logs
                // to be reparsed on every poll. Stale activity above cannot block sleep.
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
                // A one-shot timer prevents queued polls contending for the gate.
                // Catch up existing files in small passes, then return to idle polling.
                if (!_disposed && _enabled)
                    _timer.Change(_sessions.Values.Any(session => session.HasMore) ? 100 : 2000, Timeout.Infinite);
            }
        }
    }

    private void EnsureWatcher()
    {
        if (_watcher is not null) return;
        _watcher = new FileSystemWatcher(_sessionsRoot, "*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Created += (_, e) => _changedPaths[e.FullPath] = 0;
        _watcher.Changed += (_, e) => _changedPaths[e.FullPath] = 0;
        _watcher.Renamed += (_, e) => _changedPaths[e.FullPath] = 0;
        _watcher.Error += (_, _) => Interlocked.Exchange(ref _rescanRequested, 1);
        _watcher.EnableRaisingEvents = true;
    }

    private void ReadAppendedEvents(string path)
    {
        try
        {
            var session = _sessions[path];
            session.Read(path, _readBuffer);
            if (!session.HasMore) _pendingPaths.Remove(path);
        }
        catch (FileNotFoundException) { _sessions.Remove(path); _pendingPaths.Remove(path); }
        catch (DirectoryNotFoundException) { _sessions.Remove(path); _pendingPaths.Remove(path); }
        catch (IOException) { /* Retry rotated or temporarily unavailable logs. */ }
        catch (UnauthorizedAccessException) { /* Other sessions remain monitored. */ }
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
            _watcher?.Dispose();
            _watcher = null;
            ReleaseRequest();
            if (_requestHandle != IntPtr.Zero)
            {
                CloseHandle(_requestHandle);
                _requestHandle = IntPtr.Zero;
            }
        }
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
