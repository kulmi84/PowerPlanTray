using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PowerPlanTray;

internal static class Program
{
    private const string AppVersion = "1.4.2";
    private const string SettingsDirectoryName = "PowerPlanTray";
    private const string SettingsFileName = "settings.json";
    private const string StartupValueName = "PowerPlanTray";
    private const string WindowsHighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string WindowsHighPerformanceAlias = "SCHEME_MIN";

    [STAThread]
    private static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Icon _boltIcon;
        private readonly Icon _windowsBoltIcon;
        private readonly Icon _hpIcon;
        private readonly Icon _quietIcon;
        private readonly Icon _genericIcon;
        private AppSettings _settings;
        private List<PowerPlan> _plans = new();

        public TrayApplicationContext()
        {
            _settings = LoadSettings();
            _plans = GetPowerPlans();
            EnsureToggleDefaults();

            _boltIcon = CreateBoltIcon(Color.White);
            _windowsBoltIcon = CreateBoltIcon(Color.Red);
            _hpIcon = CreateCrossedBoltIcon();
            _quietIcon = CreateQuietIcon();
            _genericIcon = CreateGenericIcon();

            _menu = new ContextMenuStrip();
            _notifyIcon = new NotifyIcon
            {
                Icon = _genericIcon,
                Text = $"PowerPlanTray v{AppVersion}",
                ContextMenuStrip = _menu,
                Visible = true
            };

            _notifyIcon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ToggleSelectedPlans();
            };

            _timer = new System.Windows.Forms.Timer { Interval = 5000 };
            _timer.Tick += (_, _) => RefreshState();
            _timer.Start();
            RefreshState();
        }

        protected override void ExitThreadCore()
        {
            _timer.Stop();
            _timer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _boltIcon.Dispose();
            _windowsBoltIcon.Dispose();
            _hpIcon.Dispose();
            _quietIcon.Dispose();
            _genericIcon.Dispose();
            base.ExitThreadCore();
        }

        private void RefreshState()
        {
            _plans = GetPowerPlans();
            EnsureToggleDefaults();
            RebuildMenu();

            var current = GetActivePlan();
            var activePlan = _plans.FirstOrDefault(p => string.Equals(p.Guid, current, StringComparison.OrdinalIgnoreCase));
            _notifyIcon.Icon = GetIconForPlan(activePlan?.Name);
            _notifyIcon.Text = activePlan is null ? $"PowerPlanTray v{AppVersion}" : TrimNotifyText($"PowerPlanTray - {activePlan.Name}");
        }

        private void RebuildMenu()
        {
            _menu.Items.Clear();
            var current = GetActivePlan();
            foreach (var plan in _plans)
            {
                var capturedPlan = plan;
                _menu.Items.Add(new ToolStripMenuItem(plan.Name, null, (_, _) => SetPlan(capturedPlan))
                {
                    Checked = string.Equals(current, plan.Guid, StringComparison.OrdinalIgnoreCase)
                });
            }
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("Einstellungen...", null, (_, _) => ShowSettings()));
            _menu.Items.Add(new ToolStripMenuItem($"PowerPlanTray v{AppVersion}") { Enabled = false });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("Beenden", null, (_, _) => ExitThread()));
        }

        private void ToggleSelectedPlans()
        {
            var planA = FindPlanByGuid(_settings.TogglePlanA);
            var planB = FindPlanByGuid(_settings.TogglePlanB);
            if (planA is null || planB is null) return;
            var current = GetActivePlan();
            SetPlan(string.Equals(current, planA.Guid, StringComparison.OrdinalIgnoreCase) ? planB : planA);
        }

        private void SetPlan(PowerPlan plan)
        {
            RunPowerCfg($"/setactive {plan.ActivateTarget}");
            RefreshState();
        }

        private void ShowSettings()
        {
            _plans = GetPowerPlans();
            EnsureToggleDefaults();
            using var form = new Form
            {
                Text = $"PowerPlanTray v{AppVersion} - Einstellungen",
                Icon = _boltIcon, ShowIcon = true, FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen, MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, ClientSize = new Size(430, 230)
            };
            var labelA = new Label { Text = "Linksklick Plan A:", AutoSize = true, Location = new Point(18, 22) };
            var comboA = CreatePlanComboBox(new Point(155, 18));
            var labelB = new Label { Text = "Linksklick Plan B:", AutoSize = true, Location = new Point(18, 62) };
            var comboB = CreatePlanComboBox(new Point(155, 58));
            var startup = new CheckBox { Text = "Mit Windows starten", AutoSize = true, Location = new Point(18, 108), Checked = IsStartupEnabled() };
            var version = new Label { Text = $"Version {AppVersion}", AutoSize = true, Location = new Point(18, 150) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(252, 184), Size = new Size(75, 28) };
            var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Location = new Point(335, 184), Size = new Size(75, 28) };
            comboA.SelectedItem = _plans.FirstOrDefault(p => string.Equals(p.Guid, _settings.TogglePlanA, StringComparison.OrdinalIgnoreCase));
            comboB.SelectedItem = _plans.FirstOrDefault(p => string.Equals(p.Guid, _settings.TogglePlanB, StringComparison.OrdinalIgnoreCase));
            form.Controls.AddRange(new Control[] { labelA, comboA, labelB, comboB, startup, version, ok, cancel });
            form.AcceptButton = ok; form.CancelButton = cancel;
            if (form.ShowDialog() != DialogResult.OK) return;
            if (comboA.SelectedItem is PowerPlan selectedA) _settings.TogglePlanA = selectedA.Guid;
            if (comboB.SelectedItem is PowerPlan selectedB) _settings.TogglePlanB = selectedB.Guid;
            SaveSettings(_settings); SetStartupEnabled(startup.Checked); RefreshState();
        }

        private ComboBox CreatePlanComboBox(Point location)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = location, Size = new Size(255, 28) };
            foreach (var plan in _plans) combo.Items.Add(plan);
            return combo;
        }

        private void EnsureToggleDefaults()
        {
            if (_plans.Count == 0) return;
            if (FindPlanByGuid(_settings.TogglePlanA) is null)
                _settings.TogglePlanA = _plans.FirstOrDefault(p => p.Name.Equals("Höchstleistung HP", StringComparison.OrdinalIgnoreCase))?.Guid ?? _plans[0].Guid;
            if (FindPlanByGuid(_settings.TogglePlanB) is null)
                _settings.TogglePlanB = _plans.FirstOrDefault(p => p.Name.StartsWith("HP Optimized", StringComparison.OrdinalIgnoreCase))?.Guid
                    ?? _plans.FirstOrDefault(p => !string.Equals(p.Guid, _settings.TogglePlanA, StringComparison.OrdinalIgnoreCase))?.Guid ?? _plans[0].Guid;
            SaveSettings(_settings);
        }

        private PowerPlan? FindPlanByGuid(string? guid) => string.IsNullOrWhiteSpace(guid) ? null : _plans.FirstOrDefault(p => string.Equals(p.Guid, guid, StringComparison.OrdinalIgnoreCase));

        private Icon GetIconForPlan(string? planName)
        {
            if (planName?.Equals("Windows Höchstleistung", StringComparison.OrdinalIgnoreCase) == true) return _windowsBoltIcon;
            if (planName?.Equals("Höchstleistung HP", StringComparison.OrdinalIgnoreCase) == true) return _boltIcon;
            if (planName?.StartsWith("HP Optimized", StringComparison.OrdinalIgnoreCase) == true) return _hpIcon;
            if (planName?.Equals("Leise / Remote", StringComparison.OrdinalIgnoreCase) == true) return _quietIcon;
            return _genericIcon;
        }

        private static List<PowerPlan> GetPowerPlans()
        {
            var result = new List<PowerPlan>();
            var output = RunPowerCfg("/list");
            if (!string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var guidMatch = Regex.Match(line, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                    var nameMatch = Regex.Match(line, @"\((?<name>[^)]*)\)");
                    if (!guidMatch.Success || !nameMatch.Success) continue;
                    var guid = guidMatch.Value;
                    if (guid.Equals(WindowsHighPerformanceGuid, StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(new PowerPlan(guid, nameMatch.Groups["name"].Value.Trim(), guid));
                }
            }
            result.Add(new PowerPlan(WindowsHighPerformanceGuid, "Windows Höchstleistung", WindowsHighPerformanceAlias));
            return result;
        }

        private static AppSettings LoadSettings()
        {
            try { var path = GetSettingsPath(); return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings() : new AppSettings(); }
            catch { return new AppSettings(); }
        }
        private static void SaveSettings(AppSettings settings)
        {
            try { var path = GetSettingsPath(); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); }
            catch { }
        }
        private static string GetSettingsPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SettingsDirectoryName, SettingsFileName);
        private static bool IsStartupEnabled()
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false); return key?.GetValue(StartupValueName) is string; }
            catch { return false; }
        }
        private static void SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key is null) return;
                if (enabled) { var exe = Environment.ProcessPath; if (!string.IsNullOrWhiteSpace(exe)) key.SetValue(StartupValueName, $"\"{exe}\""); }
                else key.DeleteValue(StartupValueName, false);
            }
            catch { }
        }
        private static string? GetActivePlan()
        {
            var output = RunPowerCfg("/getactivescheme");
            var match = Regex.Match(output, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return match.Success ? match.Value : null;
        }
        private static string RunPowerCfg(string arguments)
        {
            try
            {
                var oemEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                using var process = Process.Start(new ProcessStartInfo { FileName = "powercfg.exe", Arguments = arguments, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = oemEncoding, StandardErrorEncoding = oemEncoding, CreateNoWindow = true });
                if (process is null) return string.Empty;
                var output = process.StandardOutput.ReadToEnd(); process.WaitForExit(3000); return output;
            }
            catch { return string.Empty; }
        }
        private static string TrimNotifyText(string value) => value.Length <= 63 ? value : value[..63];

        private static Icon CreateBoltIcon(Color circleColor)
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
                using var circle = new SolidBrush(circleColor); using var black = new SolidBrush(Color.Black);
                g.FillEllipse(circle, 0.25f, 0.25f, 31.5f, 31.5f);
                PointF[] bolt = { new(18.8f, 3.0f), new(7.0f, 17.2f), new(13.8f, 17.2f), new(11.5f, 29.0f), new(25.4f, 12.6f), new(18.0f, 12.6f) };
                g.FillPolygon(black, bolt);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateCrossedBoltIcon()
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
                using var white = new SolidBrush(Color.White); using var black = new SolidBrush(Color.Black);
                using var slashPen = new Pen(Color.Black, 3.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.FillEllipse(white, 0.25f, 0.25f, 31.5f, 31.5f);
                PointF[] bolt = { new(18.8f, 3.0f), new(7.0f, 17.2f), new(13.8f, 17.2f), new(11.5f, 29.0f), new(25.4f, 12.6f), new(18.0f, 12.6f) };
                g.FillPolygon(black, bolt); g.DrawLine(slashPen, 6.3f, 6.3f, 25.7f, 25.7f);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateQuietIcon()
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
                using var large = new Pen(Color.White, 5.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                using var medium = new Pen(Color.White, 4.0f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                using var small = new Pen(Color.White, 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(large, new[] { new PointF(14.5f, 3.2f), new PointF(29.0f, 3.2f), new PointF(14.0f, 18.0f), new PointF(28.8f, 18.0f) });
                g.DrawLines(medium, new[] { new PointF(8.0f, 14.0f), new PointF(19.0f, 14.0f), new PointF(8.0f, 24.0f), new PointF(19.2f, 24.0f) });
                g.DrawLines(small, new[] { new PointF(2.5f, 22.0f), new PointF(10.2f, 22.0f), new PointF(2.8f, 29.0f), new PointF(10.5f, 29.0f) });
            }
            return ToIcon(bitmap);
        }
        private static Icon CreateGenericIcon()
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                using var white = new SolidBrush(Color.White); using var black = new SolidBrush(Color.Black);
                g.FillEllipse(white, 0.25f, 0.25f, 31.5f, 31.5f); g.FillEllipse(black, 9f, 9f, 14f, 14f);
            }
            return ToIcon(bitmap);
        }
        private static Icon ToIcon(Bitmap bitmap)
        {
            var hIcon = bitmap.GetHicon(); try { return (Icon)Icon.FromHandle(hIcon).Clone(); } finally { DestroyIcon(hIcon); }
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr hIcon);
    }

    private sealed class AppSettings { public string? TogglePlanA { get; set; } public string? TogglePlanB { get; set; } }
    private sealed record PowerPlan(string Guid, string Name, string ActivateTarget) { public override string ToString() => Name; }
}
