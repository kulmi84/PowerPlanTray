using System.Drawing;
using System.Globalization;
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
    private readonly DataGridView _list;
    public List<EnergyRule> Rules => _rules;
    public bool AutomationEnabled => _enabled.Checked;
    private readonly CheckBox _enabled = new DarkCheckBox() { Text = L.T("Automatische Regeln aktivieren"), AutoSize = true };

    public RulesForm(List<EnergyRule> rules, IReadOnlyList<RulePlan> plans, bool enabled)
    {
        _rules = JsonSerializer.Deserialize<List<EnergyRule>>(JsonSerializer.Serialize(rules))!;
        _plans = plans;
        Text = L.T("App- und Zeitregeln");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(840, 500);
        ClientSize = new Size(1050, 610);
        _enabled.Checked = enabled;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 6, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(AppTheme.Heading(L.T("App- und Zeitregeln")), 0, 0);
        layout.Controls.Add(_enabled, 0, 1);
        var hint = AppTheme.Note(L.T("Die oberste passende Regel gewinnt. Danach wird der vorherige Plan wiederhergestellt.\nManuelle Planwahl pausiert die Automatik, bis keine Regel mehr greift oder Sie sie im Tray fortsetzen."));
        hint.Margin = new Padding(0, 10, 0, 18);
        layout.Controls.Add(hint, 0, 2);
        layout.SizeChanged += (_, _) => hint.MaximumSize = new Size(Math.Max(100, layout.ClientSize.Width - layout.Padding.Horizontal), 0);
        _list = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        _list.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 70 });
        _list.Columns.Add(new DataGridViewTextBoxColumn { Name = L.T("Regel"), HeaderText = L.T("Regel"), Width = 190 });
        _list.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ausloeser", HeaderText = L.T("Auslöser"), MinimumWidth = 260, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _list.Columns.Add(new DataGridViewTextBoxColumn { Name = "Plan", HeaderText = L.T("Energieplan"), Width = 240 });
        foreach (DataGridViewColumn column in _list.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        _list.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Edit(); };
        layout.Controls.Add(_list, 0, 3);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        AddButton(actions, L.T("Hinzufügen…"), () => Edit(true));
        AddButton(actions, L.T("Bearbeiten…"), () => Edit());
        AddButton(actions, L.T("Ein / Aus"), () => { if (Selected is { } r) { r.Enabled = !r.Enabled; Reload(r); } });
        AddButton(actions, L.T("Entfernen"), () => { if (Selected is { } r) { _rules.Remove(r); Reload(); } });
        AddButton(actions, L.T("Nach oben"), () => MoveRule(-1));
        AddButton(actions, L.T("Nach unten"), () => MoveRule(1));
        layout.Controls.Add(actions, 0, 4);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = L.T("Abbrechen"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = L.T("Speichern"), AutoSize = true, DialogResult = DialogResult.OK };
        footer.Controls.Add(cancel); footer.Controls.Add(save);
        layout.Controls.Add(footer, 0, 5);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        Reload();
        AppTheme.Apply(this);
    }

    private EnergyRule? Selected => _list.CurrentRow?.Tag as EnergyRule;
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
        _list.Rows.Clear();
        foreach (var rule in _rules)
        {
            var trigger = rule.Kind == EnergyRuleKind.App
                ? L.F($"App: {rule.ProcessName}.exe · Start {rule.DelaySeconds}s · Nachlauf {rule.GraceSeconds}s")
                : rule.Kind == EnergyRuleKind.HyperV ? L.F($"Hyper-V: {rule.VmName} · Start {rule.DelaySeconds}s · Nachlauf {rule.GraceSeconds}s") : L.F($"Zeit: {FormatDays(rule.Days)} {rule.StartMinute / 60:00}:{rule.StartMinute % 60:00}–{rule.EndMinute / 60:00}:{rule.EndMinute % 60:00}");
            int index = _list.Rows.Add(rule.Enabled ? L.T("An") : L.T("Aus"), rule.Name, trigger,
                _plans.FirstOrDefault(p => p.Guid.Equals(rule.PlanGuid, StringComparison.OrdinalIgnoreCase))?.Name ?? L.T("Plan nicht verfügbar"));
            _list.Rows[index].Tag = rule;
            if (rule == selected) _list.CurrentCell = _list.Rows[index].Cells[0];
        }
    }
    private static string FormatDays(int days)
    {
        var names = new[] { L.T("So"), L.T("Mo"), L.T("Di"), L.T("Mi"), L.T("Do"), L.T("Fr"), L.T("Sa") };
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

    internal sealed class RuleEditor : BufferedForm
    {
        public RuleEditor(EnergyRule rule, IReadOnlyList<RulePlan> plans)
        {
            Text = L.T("Regel bearbeiten"); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(760, 640);
            var table = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24), AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var labels = new Dictionary<Control, Label>();
            void Row(string label, Control control)
            {
                var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var caption = new ThemeLabel { Text = label, AutoSize = true, Margin = new Padding(0, 8, 8, 8) };
                labels[control] = caption;
                table.Controls.Add(caption, 0, row);
                control.Margin = new Padding(0, 4, 0, 8); control.Dock = DockStyle.Top; table.Controls.Add(control, 1, row);
            }
            var name = new TextBox { Text = rule.Name, MaxLength = 100 }; Row("Name", name);
            var kind = new DarkComboBox { Name = "RuleTrigger", DropDownStyle = ComboBoxStyle.DropDownList };
            kind.Items.AddRange(new object[] { L.T("App läuft"), L.T("Zeitfenster"), L.T("Microsoft Hyper-V: VM läuft") }); kind.SelectedIndex = (int)rule.Kind; Row(L.T("Auslöser"), kind);
            var plan = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var p in plans) plan.Items.Add(p);
            plan.SelectedItem = plans.FirstOrDefault(p => p.Guid.Equals(rule.PlanGuid, StringComparison.OrdinalIgnoreCase));
            if (plan.SelectedIndex < 0 && plan.Items.Count > 0) plan.SelectedIndex = 0;
            Row(L.T("Energieplan"), plan);
            var appPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
            appPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); appPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var exe = new TextBox { Text = rule.ProcessName, Dock = DockStyle.Fill };
            var browse = new Button { Text = L.T("EXE wählen…"), AutoSize = true };
            browse.Click += (_, _) =>
            {
                using var picker = new OpenFileDialog { Filter = L.T("Programme (*.exe)|*.exe"), CheckFileExists = true };
                if (picker.ShowDialog(this) == DialogResult.OK) exe.Text = EnergyRule.NormalizeProcess(picker.FileName);
            };
            appPanel.Controls.Add(exe, 0, 0); appPanel.Controls.Add(browse, 1, 0); Row(L.T("App / EXE-Dateiname"), appPanel);
            var vmPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
            vmPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            vmPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var vmChoice = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            if (!string.IsNullOrEmpty(rule.VmId))
            {
                vmChoice.Items.Add(new HyperVMachine(rule.VmId, rule.VmName, null));
                vmChoice.SelectedIndex = 0;
            }
            var vmRefresh = new Button { Text = L.T("VMs laden"), AutoSize = true };
            vmPanel.Controls.Add(vmChoice, 0, 0); vmPanel.Controls.Add(vmRefresh, 1, 0);
            Row("Hyper-V-VM", vmPanel);
            var vmStatus = AppTheme.Note(L.T("VMs dieses Computers laden und die gewünschte VM auswählen."));
            vmStatus.MaximumSize = new Size(510, 0); Row("", vmStatus);
            vmRefresh.Click += async (_, _) =>
            {
                vmRefresh.Enabled = false; vmStatus.Text = L.T("Hyper-V-VMs werden geladen…");
                var selected = vmChoice.SelectedItem as HyperVMachine;
                var snapshot = await Task.Run(HyperVReader.Read);
                if (IsDisposed) return;
                vmRefresh.Enabled = true;
                if (snapshot.Machines is null) { vmStatus.Text = snapshot.Error; return; }
                vmChoice.Items.Clear();
                foreach (var vm in snapshot.Machines) vmChoice.Items.Add(vm);
                if (selected is not null)
                {
                    var match = snapshot.Machines.FirstOrDefault(v => v.Id.Equals(selected.Id, StringComparison.OrdinalIgnoreCase));
                    if (match is null) { vmChoice.Items.Add(selected); match = selected; }
                    vmChoice.SelectedItem = match;
                }
                vmStatus.Text = snapshot.Machines.Count == 0 ? L.T("Keine Hyper-V-VMs auf diesem Computer gefunden.") : L.T("VM auswählen. Die Zuordnung bleibt bei einer Umbenennung über die VM-ID erhalten.");
            };
            var delay = new NumericUpDown { Minimum = 0, Maximum = 86400, Value = Math.Clamp(rule.DelaySeconds, 0, 86400) }; Row(L.T("Startverzögerung (s)"), delay);
            var delayHelp = AppTheme.Note(L.T("0 = sofort umschalten. Bei z. B. 10 Sekunden muss die App erst 10 Sekunden durchgehend laufen."));
            delayHelp.MaximumSize = new Size(510, 0); Row("", delayHelp);
            var grace = new NumericUpDown { Minimum = 0, Maximum = 86400, Value = Math.Clamp(rule.GraceSeconds, 0, 86400) }; Row(L.T("Nachlaufzeit (s)"), grace);
            var graceHelp = AppTheme.Note(L.T("0 = kein Nachlauf. Bei z. B. 30 Sekunden bleibt diese Regel nach dem App-Ende noch 30 Sekunden aktiv."));
            graceHelp.MaximumSize = new Size(510, 0); Row("", graceHelp);
            var dayPanel = new FlowLayoutPanel { AutoSize = true };
            var days = new List<(CheckBox Box, int Day)>();
            var dayNames = new[] { L.T("So"), L.T("Mo"), L.T("Di"), L.T("Mi"), L.T("Do"), L.T("Fr"), L.T("Sa") };
            foreach (var day in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                var box = new DarkCheckBox { Text = dayNames[day], AutoSize = true, Checked = (rule.Days & (1 << day)) != 0 };
                days.Add((box, day)); dayPanel.Controls.Add(box);
            }
            Row(L.T("Wochentage"), dayPanel);
            var start = new MaskedTextBox("00:00") { Text = $"{Math.Clamp(rule.StartMinute, 0, 1439) / 60:00}:{Math.Clamp(rule.StartMinute, 0, 1439) % 60:00}" }; Row(L.T("Von · HH:mm"), start);
            var end = new MaskedTextBox("00:00") { Text = $"{Math.Clamp(rule.EndMinute, 0, 1439) / 60:00}:{Math.Clamp(rule.EndMinute, 0, 1439) % 60:00}" }; Row(L.T("Bis · HH:mm"), end);
            var help = new Label { Text = "EXE-Dateinamen gelten für alle gleichnamigen Prozesse. Bei VMs den Prozess der laufenden VM wählen.\nZeitfenster über Mitternacht zählen zum Starttag. Prüfung alle 5 Sekunden.", AutoSize = true };
            Row(L.T("Hinweise"), help);
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = L.T("Abbrechen"), DialogResult = DialogResult.Cancel, AutoSize = true };
            var save = new Button { Text = L.T("Übernehmen"), AutoSize = true }; buttons.Controls.Add(cancel); buttons.Controls.Add(save); Row("", buttons);
            void UpdateFields()
            {
                AppTheme.UpdateLayout(table, () =>
                {
                bool app = kind.SelectedIndex == 0;
                bool vm = kind.SelectedIndex == 2;
                foreach (var control in new Control[] { appPanel }) control.Visible = labels[control].Visible = app;
                foreach (var control in new Control[] { dayPanel, start, end }) control.Visible = labels[control].Visible = kind.SelectedIndex == 1;
                foreach (var control in new Control[] { delay, delayHelp, grace, graceHelp }) control.Visible = labels[control].Visible = app || vm;
                foreach (var control in new Control[] { vmPanel, vmStatus }) control.Visible = labels[control].Visible = vm;
                delayHelp.Text = vm ? L.T("0 = sofort. Die ausgewählte VM muss für diese Dauer durchgehend laufen.") : L.T("0 = sofort. Die App muss für diese Dauer durchgehend laufen.");
                graceHelp.Text = vm ? L.T("0 = kein Nachlauf. Nach dem Stoppen oder Pausieren der VM bleibt die Regel noch so lange aktiv.") : L.T("0 = kein Nachlauf. Nach dem App-Ende bleibt die Regel noch so lange aktiv.");
                help.Text = app
                    ? L.T("EXE-Dateinamen gelten für alle gleichnamigen Prozesse. Für einzelne Hyper-V-VMs den eigenen Hyper-V-Auslöser verwenden. Prüfung alle 5 Sekunden.")
                    : vm ? L.T("Nur die ausgewählte lokale VM im Zustand Läuft aktiviert diese Regel. Bei fehlendem Hyper-V-Zugriff bleibt der Energieplan unverändert. Prüfung alle 5 Sekunden.") : L.T("Uhrzeiten gelten in der lokalen Windows-Zeit. Zeitfenster über Mitternacht zählen zum Starttag. Prüfung alle 5 Sekunden.");
                });
            }
            kind.SelectedIndexChanged += (_, _) => UpdateFields(); UpdateFields();
            save.Click += (_, _) =>
            {
                var process = EnergyRule.NormalizeProcess(exe.Text);
                bool validStart = TimeSpan.TryParseExact(start.Text, @"hh\:mm", CultureInfo.InvariantCulture, out var startTime);
                bool validEnd = TimeSpan.TryParseExact(end.Text, @"hh\:mm", CultureInfo.InvariantCulture, out var endTime);
                int startMinute = (int)startTime.TotalMinutes, endMinute = (int)endTime.TotalMinutes;
                int dayMask = days.Where(d => d.Box.Checked).Aggregate(0, (mask, d) => mask | (1 << d.Day));
                if (string.IsNullOrWhiteSpace(name.Text) || plan.SelectedItem is not RulePlan)
                { MessageBox.Show(this, L.T("Bitte Name und Energieplan angeben.")); return; }
                if (kind.SelectedIndex == 0 && (process.Length == 0 || process.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                { MessageBox.Show(this, L.T("Bitte einen gültigen EXE-Dateinamen wählen.")); return; }
                if (kind.SelectedIndex == 1 && (!validStart || !validEnd || dayMask == 0 || startMinute == endMinute))
                { MessageBox.Show(this, L.T("Bitte Wochentage und gültige, unterschiedliche Uhrzeiten im Format HH:mm wählen.")); return; }
                if (kind.SelectedIndex == 2 && vmChoice.SelectedItem is not HyperVMachine)
                { MessageBox.Show(this, L.T("Bitte die Hyper-V-VMs laden und eine VM auswählen.")); return; }
                if (vmChoice.SelectedItem is HyperVMachine chosenVm) { rule.VmId = chosenVm.Id; rule.VmName = chosenVm.Name; }
                rule.Name = name.Text.Trim(); rule.Kind = (EnergyRuleKind)kind.SelectedIndex;
                rule.PlanGuid = ((RulePlan)plan.SelectedItem!).Guid; rule.ProcessName = process;
                rule.DelaySeconds = (int)delay.Value; rule.GraceSeconds = (int)grace.Value;
                rule.Days = dayMask; rule.StartMinute = startMinute; rule.EndMinute = endMinute;
                DialogResult = DialogResult.OK;
            };
            Controls.Add(table); AcceptButton = save; CancelButton = cancel;
            AppTheme.Apply(this);
        }
    }
}
