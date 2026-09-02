using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PowerPlanTray;

internal static class Program
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerformance = "4d922812-1350-43fd-9a02-bd8cb10e3619";
    private const string HpOptimized = "fb5220ff-7e1a-47aa-9a42-50ffbf45c673";

    [STAThread]
    private static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new TrayApplicationContext()); }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _balancedItem, _highItem, _hpItem;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Icon _boltIcon, _hpIcon;

        public TrayApplicationContext()
        {
            _balancedItem = new ToolStripMenuItem("Ausbalanciert", null, (_, _) => SetPlan(Balanced));
            _highItem = new ToolStripMenuItem("Höchstleistung", null, (_, _) => SetPlan(HighPerformance));
            _hpItem = new ToolStripMenuItem("HP Optimized", null, (_, _) => SetPlan(HpOptimized));
            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[] { _highItem, _hpItem, _balancedItem, new ToolStripSeparator(), new ToolStripMenuItem("Beenden", null, (_, _) => ExitThread()) });
            _boltIcon = CreateBoltIcon();
            _hpIcon = CreateHpIcon();
            _notifyIcon = new NotifyIcon { Icon = _boltIcon, Text = "PowerPlanTray", ContextMenuStrip = menu, Visible = true };
            _notifyIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleHighHp(); };
            _timer = new System.Windows.Forms.Timer { Interval = 5000 };
            _timer.Tick += (_, _) => RefreshState(); _timer.Start(); RefreshState();
        }

        protected override void ExitThreadCore() { _timer.Stop(); _timer.Dispose(); _notifyIcon.Visible = false; _notifyIcon.Dispose(); _boltIcon.Dispose(); _hpIcon.Dispose(); base.ExitThreadCore(); }
        private void ToggleHighHp() => SetPlan(string.Equals(GetActivePlan(), HighPerformance, StringComparison.OrdinalIgnoreCase) ? HpOptimized : HighPerformance);
        private void SetPlan(string guid) { RunPowerCfg($"/setactive {guid}"); RefreshState(); }

        private void RefreshState()
        {
            var current = GetActivePlan();
            _balancedItem.Checked = string.Equals(current, Balanced, StringComparison.OrdinalIgnoreCase);
            _highItem.Checked = string.Equals(current, HighPerformance, StringComparison.OrdinalIgnoreCase);
            _hpItem.Checked = string.Equals(current, HpOptimized, StringComparison.OrdinalIgnoreCase);
            if (_highItem.Checked) { _notifyIcon.Icon = _boltIcon; _notifyIcon.Text = "PowerPlanTray - Höchstleistung"; }
            else { _notifyIcon.Icon = _hpIcon; _notifyIcon.Text = _hpItem.Checked ? "PowerPlanTray - HP Optimized" : _balancedItem.Checked ? "PowerPlanTray - Ausbalanciert" : "PowerPlanTray"; }
        }

        private static Icon CreateBoltIcon()
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); using var brush = new SolidBrush(Color.White);
                PointF[] bolt = { new(19f,1.5f), new(4.5f,18f), new(13.8f,18f), new(10.7f,30.5f), new(27.5f,11.5f), new(18.2f,11.5f) }; g.FillPolygon(brush, bolt);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateHpIcon()
        {
            // Fixed ICO containing dedicated 16x16 and 32x32 raster layers.
            // White filled circle + bold black italic "hp", matching the chosen preview style.
            const string data = "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";
            var bytes = Convert.FromBase64String(data);
            var stream = new MemoryStream(bytes, writable: false);
            return new Icon(stream, 16, 16);
        }

        private static Icon ToIcon(Bitmap bitmap) { var hIcon = bitmap.GetHicon(); try { return (Icon)Icon.FromHandle(hIcon).Clone(); } finally { DestroyIcon(hIcon); } }
        private static string? GetActivePlan() { var output = RunPowerCfg("/getactivescheme"); var match = Regex.Match(output, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"); return match.Success ? match.Value : null; }
        private static string RunPowerCfg(string arguments)
        {
            try { using var process = Process.Start(new ProcessStartInfo { FileName = "powercfg.exe", Arguments = arguments, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }); if (process is null) return string.Empty; var output = process.StandardOutput.ReadToEnd(); process.WaitForExit(3000); return output; }
            catch { return string.Empty; }
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
