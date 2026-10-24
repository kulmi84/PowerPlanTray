using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace PowerPlanTray;

internal sealed record RulePlan(string Guid, string Name)
{
    public override string ToString() => Name;
}

internal sealed class RulesForm : Form
{
    private readonly List<EnergyRule> _rules;
    private readonly IReadOnlyList<RulePlan> _plans;
    private readonly ListView _list;
    public List<EnergyRule> Rules => _rules;
    public bool AutomationEnabled => _enabled.Checked;
    private readonly CheckBox _enabled = new() { Text = "Automatische Regeln aktivieren", AutoSize = true };

    public RulesForm(List<EnergyRule> rules, IReadOnlyList<RulePlan> plans, bool enabled)
    {
        _rules = JsonSerializer.Deserialize<List<EnergyRule>>(JsonSerializer.Serialize(rules))!;
        _plans = plans;
        Text = "App- und Zeitregeln";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(780, 450);
        ClientSize = new Size(900, 520);
        _enabled.Checked = enabled;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 5, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_enabled, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "Die oberste passende Regel gewinnt. Danach wird der vorherige Plan wiederhergestellt.\nManuelle Planwahl pausiert die Automatik, bis keine Regel mehr greift oder Sie sie im Tray fortsetzen.",
            AutoSize = true, Margin = new Padding(0, 10, 0, 12)
        }, 0, 1);
        _list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
        _list.Columns.Add("Status", 65);
        _list.Columns.Add("Regel", 170);
        _list.Columns.Add("Auslöser", 340);
        _list.Columns.Add("Energieplan", 250);
        _list.DoubleClick += (_, _) => Edit();
        layout.Controls.Add(_list, 0, 2);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        AddButton(actions, "Hinzufügen…", () => Edit(true));
        AddButton(actions, "Bearbeiten…", () => Edit());
        AddButton(actions, "Ein / Aus", () => { if (Selected is { } r) { r.Enabled = !r.Enabled; Reload(r); } });
        AddButton(actions, "Entfernen", () => { if (Selected is { } r) { _rules.Remove(r); Reload(); } });
        AddButton(actions, "Nach oben", () => MoveRule(-1));
        AddButton(actions, "Nach unten", () => MoveRule(1));
        layout.Controls.Add(actions, 0, 3);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Speichern", AutoSize = true, DialogResult = DialogResult.OK };
        footer.Controls.Add(cancel); footer.Controls.Add(save);
        layout.Controls.Add(footer, 0, 4);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        Reload();
    }

    private EnergyRule? Selected => _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as EnergyRule : null;
    private static void AddButton(FlowLayoutPanel panel, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action(); panel.Controls.Add(button);
    }
    private void MoveRule(int delta)
    {
        if (Selected is not { } rule) return;
        var index = _rules.IndexOf(rule); var next = index + delta;
        if (next < 0 || next >= _rules.Count) return;
        _rules.RemoveAt(index); _rules.Insert(next, rule); Reload(rule);
    }
    private void Reload(EnergyRule? selected = null)
    {
        _list.Items.Clear();
        foreach (var rule in _rules)
        {
            var trigger = rule.Kind == EnergyRuleKind.App
                ? $"App: {rule.ProcessName}.exe · Start {rule.DelaySeconds}s · Nachlauf {rule.GraceSeconds}s"
                : $"Zeit: {FormatDays(rule.Days)} {rule.StartMinute / 60:00}:{rule.StartMinute % 60:00}–{rule.EndMinute / 60:00}:{rule.EndMinute % 60:00}";
            var item = new ListViewItem(new[] { rule.Enabled ? "An" : "Aus", rule.Name, trigger,
                _plans.FirstOrDefault(p => p.Guid.Equals(rule.PlanGuid, StringComparison.OrdinalIgnoreCase))?.Name ?? "Plan nicht verfügbar" }) { Tag = rule };
            _list.Items.Add(item); if (rule == selected) item.Selected = true;
        }
    }
    private static string FormatDays(int days)
    {
        var names = new[] { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
        return string.Join(",", Enumerable.Range(1, 7).Select(i => i % 7).Where(d => (days & (1 << d)) != 0).Select(d => names[d]));
    }
    private void Edit(bool add = false)
    {
        var rule = add ? new EnergyRule() : Selected;
        if (rule is null) return;
        using var editor = new RuleEditor(rule, _plans);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        if (add) _rules.Add(rule);
        Reload(rule);
    }

    private sealed class RuleEditor : Form
    {
        public RuleEditor(EnergyRule rule, IReadOnlyList<RulePlan> plans)
        {
            Text = "Regel bearbeiten"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(650, 485);
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            void Row(string label, Control control)
            {
                var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 8, 8) }, 0, row);
                control.Margin = new Padding(0, 4, 0, 8); control.Dock = DockStyle.Top; table.Controls.Add(control, 1, row);
            }
            var name = new TextBox { Text = rule.Name, MaxLength = 100 }; Row("Name", name);
            var kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            kind.Items.AddRange(new object[] { "App läuft", "Zeitfenster" }); kind.SelectedIndex = (int)rule.Kind; Row("Auslöser", kind);
            var plan = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var p in plans) plan.Items.Add(p);
            plan.SelectedItem = plans.FirstOrDefault(p => p.Guid.Equals(rule.PlanGuid, StringComparison.OrdinalIgnoreCase));
            if (plan.SelectedIndex < 0 && plan.Items.Count > 0) plan.SelectedIndex = 0;
            Row("Energieplan", plan);
            var appPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
            appPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); appPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var exe = new TextBox { Text = rule.ProcessName, Dock = DockStyle.Fill };
            var browse = new Button { Text = "EXE wählen…", AutoSize = true };
            browse.Click += (_, _) =>
            {
                using var picker = new OpenFileDialog { Filter = "Programme (*.exe)|*.exe", CheckFileExists = true };
                if (picker.ShowDialog(this) == DialogResult.OK) exe.Text = EnergyRule.NormalizeProcess(picker.FileName);
            };
            appPanel.Controls.Add(exe, 0, 0); appPanel.Controls.Add(browse, 1, 0); Row("App / EXE-Dateiname", appPanel);
            var delay = new NumericUpDown { Minimum = 0, Maximum = 86400, Value = Math.Clamp(rule.DelaySeconds, 0, 86400) }; Row("Startverzögerung (s)", delay);
            var grace = new NumericUpDown { Minimum = 0, Maximum = 86400, Value = Math.Clamp(rule.GraceSeconds, 0, 86400) }; Row("Nachlaufzeit (s)", grace);
            var dayPanel = new FlowLayoutPanel { AutoSize = true };
            var days = new List<(CheckBox Box, int Day)>();
            var dayNames = new[] { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
            foreach (var day in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                var box = new CheckBox { Text = dayNames[day], AutoSize = true, Checked = (rule.Days & (1 << day)) != 0 };
                days.Add((box, day)); dayPanel.Controls.Add(box);
            }
            Row("Wochentage", dayPanel);
            var start = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true,
                Value = DateTime.Today.AddMinutes(Math.Clamp(rule.StartMinute, 0, 1439)) }; Row("Von (Ortszeit)", start);
            var end = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true,
                Value = DateTime.Today.AddMinutes(Math.Clamp(rule.EndMinute, 0, 1439)) }; Row("Bis (Ortszeit)", end);
            var help = new Label { Text = "EXE-Dateinamen gelten für alle gleichnamigen Prozesse. Bei VMs den Prozess der laufenden VM wählen.\nZeitfenster über Mitternacht zählen zum Starttag. Prüfung alle 5 Sekunden.", AutoSize = true };
            Row("Hinweise", help);
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
            var save = new Button { Text = "Übernehmen", AutoSize = true }; buttons.Controls.Add(cancel); buttons.Controls.Add(save); Row("", buttons);
            void UpdateFields()
            {
                bool app = kind.SelectedIndex == 0;
                appPanel.Enabled = delay.Enabled = grace.Enabled = app;
                dayPanel.Enabled = start.Enabled = end.Enabled = !app;
            }
            kind.SelectedIndexChanged += (_, _) => UpdateFields(); UpdateFields();
            save.Click += (_, _) =>
            {
                var process = EnergyRule.NormalizeProcess(exe.Text);
                int startMinute = start.Value.Hour * 60 + start.Value.Minute, endMinute = end.Value.Hour * 60 + end.Value.Minute;
                int dayMask = days.Where(d => d.Box.Checked).Aggregate(0, (mask, d) => mask | (1 << d.Day));
                if (string.IsNullOrWhiteSpace(name.Text) || plan.SelectedItem is not RulePlan)
                { MessageBox.Show(this, "Bitte Name und Energieplan angeben."); return; }
                if (kind.SelectedIndex == 0 && (process.Length == 0 || process.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                { MessageBox.Show(this, "Bitte einen gültigen EXE-Dateinamen wählen."); return; }
                if (kind.SelectedIndex == 1 && (dayMask == 0 || startMinute == endMinute))
                { MessageBox.Show(this, "Bitte Wochentage und unterschiedliche Start-/Endzeiten wählen."); return; }
                rule.Name = name.Text.Trim(); rule.Kind = (EnergyRuleKind)kind.SelectedIndex;
                rule.PlanGuid = ((RulePlan)plan.SelectedItem!).Guid; rule.ProcessName = process;
                rule.DelaySeconds = (int)delay.Value; rule.GraceSeconds = (int)grace.Value;
                rule.Days = dayMask; rule.StartMinute = startMinute; rule.EndMinute = endMinute;
                DialogResult = DialogResult.OK;
            };
            Controls.Add(table); AcceptButton = save; CancelButton = cancel;
        }
    }
}
