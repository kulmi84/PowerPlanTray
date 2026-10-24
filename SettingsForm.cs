using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace PowerPlanTray;

internal sealed class SettingsForm : Form
{
    public AppSettings Settings { get; }
    public bool StartupEnabled => _startup.Checked;
    public bool RulesEdited { get; private set; }
    public event Action<bool>? RuleEditorOpenChanged;
    private readonly CheckBox _startup;

    public SettingsForm(AppSettings settings, IReadOnlyList<RulePlan> plans, string version, bool startupEnabled,
        bool startupEditable, bool codexAvailable, string codexStatus)
    {
        Settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        Text = "PowerPlanTray – Einstellungen"; StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560); ClientSize = new Size(860, 740);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(24, 20, 24, 8), ColumnCount = 1 };
        AppTheme.AddRow(header, AppTheme.Heading("PowerPlanTray"));
        AppTheme.AddRow(header, AppTheme.Note("Energiepläne und Automatik"));
        layout.Controls.Add(header, 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 6, 24, 0) };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        scroll.Controls.Add(content); layout.Controls.Add(scroll, 0, 1);

        ComboBox PlanBox(string? guid)
        {
            var box = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var plan in plans) box.Items.Add(plan);
            box.SelectedItem = plans.FirstOrDefault(p => p.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));
            return box;
        }
        var toggleCard = AppTheme.Card("Energiepläne wechseln");
        AppTheme.AddRow(toggleCard, AppTheme.Note("Ein Linksklick auf das Tray-Symbol wechselt zwischen diesen beiden Plänen."));
        var toggleFields = AppTheme.Fields(); var planA = PlanBox(Settings.TogglePlanA); var planB = PlanBox(Settings.TogglePlanB);
        planA.Name = "TogglePlanA"; planB.Name = "TogglePlanB";
        AppTheme.FieldRow(toggleFields, "Linksklick · Plan A", planA); AppTheme.FieldRow(toggleFields, "Linksklick · Plan B", planB);
        AppTheme.AddRow(toggleCard, toggleFields); AppTheme.AddRow(content, toggleCard);

        var startupCard = AppTheme.Card("Mit Windows starten");
        _startup = new DarkCheckBox { Text = startupEditable ? "PowerPlanTray beim Anmelden starten" : "Autostart im installierten MSIX testen", AutoSize = true, Checked = startupEnabled, Enabled = startupEditable };
        AppTheme.AddRow(startupCard, _startup);
        var lastPlan = new DarkCheckBox { Text = "Zuletzt aktiven Energieplan beibehalten", AutoSize = true, Checked = Settings.UseLastPlan };
        lastPlan.Name = "UseLastPlan";
        AppTheme.AddRow(startupCard, lastPlan);
        var startupFields = AppTheme.Fields(); var startPlan = PlanBox(Settings.StartupPlan);
        startPlan.Name = "StartupPlan";
        AppTheme.FieldRow(startupFields, "Fester Plan beim Autostart", startPlan); AppTheme.AddRow(startupCard, startupFields);
        void UpdateStartup() => startupFields.Enabled = !lastPlan.Checked;
        lastPlan.CheckedChanged += (_, _) => UpdateStartup(); UpdateStartup();
        AppTheme.AddRow(content, startupCard);

        var rulesCard = AppTheme.Card("App- und Zeitregeln");
        var rulesSummary = AppTheme.Note("");
        void UpdateSummary() => rulesSummary.Text = $"{Settings.Rules.Count} Regeln · Automatik {(Settings.RulesEnabled ? "aktiviert" : "deaktiviert")}. Manuelle Planwahl hat Vorrang.";
        UpdateSummary(); AppTheme.AddRow(rulesCard, rulesSummary);
        var rulesButton = new Button { Text = "Regeln verwalten…", AutoSize = true, Dock = DockStyle.Left };
        var rulesActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top }; rulesActions.Controls.Add(rulesButton); AppTheme.AddRow(rulesCard, rulesActions);
        rulesButton.Click += (_, _) =>
        {
            RuleEditorOpenChanged?.Invoke(true);
            try
            {
                using var form = new RulesForm(Settings.Rules, plans, Settings.RulesEnabled) { Icon = Icon };
                if (form.ShowDialog(this) != DialogResult.OK) return;
                Settings.Rules = form.Rules; Settings.RulesEnabled = form.AutomationEnabled;
                RulesEdited = true; UpdateSummary();
            }
            finally { RuleEditorOpenChanged?.Invoke(false); }
        };
        AppTheme.AddRow(content, rulesCard);

        var codex = new DarkCheckBox { Text = "Codex-Erweiterung aktivieren", AutoSize = true, Checked = Settings.CodexStandbyEnabled };
        codex.Name = "CodexEnabled";
        var grace = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        grace.Name = "CodexGrace";
        foreach (var minutes in new[] { 5, 10, 15, 30, 60 }) grace.Items.Add(minutes);
        grace.SelectedItem = grace.Items.Contains(Settings.CodexGraceMinutes) ? Settings.CodexGraceMinutes : 15;
        if (codexAvailable)
        {
            var codexCard = AppTheme.Card("Codex · optionaler Standby-Schutz");
            AppTheme.AddRow(codexCard, codex);
            AppTheme.AddRow(codexCard, AppTheme.Note("Standardmäßig aus. Verhindert automatischen Standby während erkannter Codex-Aufgaben; die Auswertung bleibt lokal."));
            var codexFields = AppTheme.Fields(); AppTheme.FieldRow(codexFields, "Nachlaufzeit (Minuten)", grace);
            AppTheme.AddRow(codexCard, codexFields);
            var status = AppTheme.Note("");
            void UpdateCodex() { codexFields.Enabled = codex.Checked; status.Text = codex.Checked ? $"Status bei Öffnen: {codexStatus}. Änderungen gelten nach Speichern." : "Standby-Schutz ausgeschaltet"; }
            codex.CheckedChanged += (_, _) => UpdateCodex(); UpdateCodex();
            AppTheme.AddRow(codexCard, status); AppTheme.AddRow(content, codexCard);
        }
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Padding = new Padding(24, 12, 24, 16) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var versionLabel = AppTheme.Note($"Version {version}");
        versionLabel.Name = "SettingsVersion";
        versionLabel.Anchor = AnchorStyles.Left;
        versionLabel.Margin = new Padding(0, 0, 12, 0);
        var identity = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty, Anchor = AnchorStyles.Left };
        identity.Controls.Add(versionLabel, 0, 0);
        var credit = AppTheme.Note("Entwickelt von Marcin Kulmaczewski");
        credit.Margin = new Padding(0, 2, 12, 0);
        identity.Controls.Add(credit, 0, 1);
        var aboutButton = new Button { Text = "Über…", AutoSize = true, Anchor = AnchorStyles.Left };
        aboutButton.Click += (_, _) => { using var about = new AboutForm(version) { Icon = Icon }; about.ShowDialog(this); };
        identity.Controls.Add(aboutButton, 1, 0);
        identity.SetRowSpan(aboutButton, 2);
        footer.Controls.Add(identity, 0, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty };
        var cancel = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Speichern", AutoSize = true };
        save.Click += (_, _) =>
        {
            if (planA.SelectedItem is RulePlan a) Settings.TogglePlanA = a.Guid;
            if (planB.SelectedItem is RulePlan b) Settings.TogglePlanB = b.Guid;
            if (startPlan.SelectedItem is RulePlan start) Settings.StartupPlan = start.Guid;
            Settings.UseLastPlan = lastPlan.Checked;
            if (codexAvailable) { Settings.CodexStandbyEnabled = codex.Checked; Settings.CodexGraceMinutes = (int)grace.SelectedItem!; }
            DialogResult = DialogResult.OK;
        };
        actions.Controls.Add(cancel); actions.Controls.Add(save); footer.Controls.Add(actions, 1, 0); layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        AppTheme.Apply(this);
    }
}
