namespace PowerPlanTray;

internal sealed class AppSettings
{
    public string? TogglePlanA { get; set; }
    public string? TogglePlanB { get; set; }
    public string? StartupPlan { get; set; }
    public bool UseLastPlan { get; set; } = true;
    // Opt-in only, including fresh Store installs and settings from older versions.
    public bool CodexStandbyEnabled { get; set; } = false;
    public int CodexGraceMinutes { get; set; } = 15;
    public bool RulesEnabled { get; set; } = true;
    public List<EnergyRule> Rules { get; set; } = new();
}
