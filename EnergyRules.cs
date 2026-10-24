using System.Diagnostics;

namespace PowerPlanTray;

internal enum EnergyRuleKind { App, Schedule, HyperV }

internal sealed class EnergyRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = L.T("Neue Regel");
    public bool Enabled { get; set; } = true;
    public EnergyRuleKind Kind { get; set; }
    public string PlanGuid { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string VmId { get; set; } = "";
    public string VmName { get; set; } = "";
    public int DelaySeconds { get; set; }
    public int GraceSeconds { get; set; }
    // Bit 0 = Sunday, matching DayOfWeek. Selected days refer to the start day.
    public int Days { get; set; } = 62;
    public int StartMinute { get; set; } = 8 * 60;
    public int EndMinute { get; set; } = 17 * 60;

    public static string NormalizeProcess(string value) =>
        Path.GetFileNameWithoutExtension(value.Trim().Trim('"'));

    public bool IsScheduled(DateTime localNow)
    {
        if (StartMinute is < 0 or >= 1440 || EndMinute is < 0 or >= 1440 || StartMinute == EndMinute)
            return false;
        var minute = localNow.Hour * 60 + localNow.Minute;
        var day = localNow.DayOfWeek;
        if (StartMinute < EndMinute)
            return (Days & (1 << (int)day)) != 0 && minute >= StartMinute && minute < EndMinute;
        // An overnight window belongs to the day on which it started.
        if (minute < EndMinute)
            day = localNow.AddDays(-1).DayOfWeek;
        else if (minute < StartMinute)
            return false;
        return (Days & (1 << (int)day)) != 0;
    }
}

internal sealed class EnergyRuleEngine
{
    private sealed class AppState
    {
        public DateTimeOffset? SeenSince;
        public DateTimeOffset? MissingSince;
        public bool Qualified;
    }

    private readonly Dictionary<string, AppState> _apps = new();
    public void Reset() => _apps.Clear();

    // UTC is used for durations, local wall time for schedules. Process sampling is
    // injected so evaluation can be tested without changing the machine's plan.
    public EnergyRule? Evaluate(IEnumerable<EnergyRule> rules, ISet<string> processes,
        ISet<string> availablePlans, DateTimeOffset utcNow, DateTime localNow, ISet<string>? runningVms = null)
    {
        EnergyRule? winner = null;
        var liveIds = new HashSet<string>();
        foreach (var rule in rules)
        {
            if (!rule.Enabled || !availablePlans.Contains(rule.PlanGuid)) continue;
            bool active;
            if (rule.Kind == EnergyRuleKind.Schedule)
                active = rule.IsScheduled(localNow);
            else
            {
                liveIds.Add(rule.Id);
                if (!_apps.TryGetValue(rule.Id, out var state))
                    _apps[rule.Id] = state = new AppState();
                var processName = EnergyRule.NormalizeProcess(rule.ProcessName);
                bool present = rule.Kind == EnergyRuleKind.HyperV
                    ? rule.VmId.Length > 0 && runningVms is not null && runningVms.Contains(rule.VmId)
                    : processName.Length > 0 && processes.Contains(processName);
                if (present)
                {
                    state.MissingSince = null;
                    state.SeenSince ??= utcNow;
                    if ((utcNow - state.SeenSince.Value).TotalSeconds >= Math.Max(0, rule.DelaySeconds))
                        state.Qualified = true;
                    active = state.Qualified;
                }
                else
                {
                    state.SeenSince = null;
                    state.MissingSince ??= utcNow;
                    active = state.Qualified &&
                        (utcNow - state.MissingSince.Value).TotalSeconds < Math.Max(0, rule.GraceSeconds);
                    if (!active) state.Qualified = false;
                }
            }
            if (active && winner is null) winner = rule;
        }
        foreach (var id in _apps.Keys.Where(id => !liveIds.Contains(id)).ToArray()) _apps.Remove(id);
        return winner;
    }

    public static HashSet<string>? ReadProcesses()
    {
        try
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try { names.Add(process.ProcessName); }
                    catch (InvalidOperationException) { } // Process exited during sampling.
                    catch (System.ComponentModel.Win32Exception) { }
                }
            }
            return names;
        }
        catch { return null; } // An unavailable snapshot must not look like all apps exited.
    }
}

internal sealed class EnergyPlanAutomation
{
    private readonly Func<string, bool> _activate;
    private string? _previous;
    private string? _applied;
    public bool Paused { get; private set; }
    public string Status { get; private set; } = L.T("Keine Regel aktiv");

    public EnergyPlanAutomation(Func<string, bool> activate) => _activate = activate;

    public void Pause()
    {
        Paused = true;
        // A user's manual choice replaces the old baseline; do not restore over it.
        _previous = _applied = null;
        Status = L.T("Pausiert (manuelle Auswahl)");
    }

    public void Resume() => Paused = false;
    public void ReportUnavailable(string message) => Status = message;

    public void Update(EnergyRule? rule, string? current)
    {
        if (current is null) { Status = L.T("Aktiver Energieplan nicht lesbar"); return; }
        if (_applied is not null && !Same(current, _applied)) Pause();
        if (Paused)
        {
            if (rule is not null) return;
            Paused = false;
        }
        if (rule is null)
        {
            if (_previous is not null && _applied is not null && Same(current, _applied) && !Same(current, _previous))
            {
                if (!_activate(_previous)) { Status = L.T("Vorheriger Plan konnte nicht wiederhergestellt werden"); return; }
            }
            _previous = _applied = null;
            Status = L.T("Keine Regel aktiv");
            return;
        }
        if (!Same(current, rule.PlanGuid))
        {
            var baseline = _previous ?? current;
            if (!_activate(rule.PlanGuid)) { Status = L.F($"Planwechsel fehlgeschlagen: {rule.Name}"); return; }
            _previous = baseline;
            _applied = rule.PlanGuid;
        }
        else if (_previous is not null) _applied = rule.PlanGuid;
        Status = L.F($"Aktiv: {rule.Name}");
    }

    public void Restore(string? current) => Update(null, current);
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
