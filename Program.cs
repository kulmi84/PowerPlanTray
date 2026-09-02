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
            _hpIcon = CreateCrossedBoltIcon();
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
            // Keep the existing Höchstleistung bolt exactly as before.
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); using var brush = new SolidBrush(Color.White);
                PointF[] bolt = { new(19f,1.5f), new(4.5f,18f), new(13.8f,18f), new(10.7f,30.5f), new(27.5f,11.5f), new(18.2f,11.5f) }; g.FillPolygon(brush, bolt);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateCrossedBoltIcon()
        {
            // HP Optimized: same visual language as the performance bolt,
            // surrounded by a smooth white prohibition ring and diagonal slash.
            // Render oversized and downsample once for clean 16px tray edges.
            const int scale = 4;
            using var large = new Bitmap(32 * scale, 32 * scale, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(large))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.ScaleTransform(scale, scale);

                using var white = new SolidBrush(Color.White);
                using var ringPen = new Pen(Color.White, 2.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var slashPen = new Pen(Color.White, 3.0f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

                // Slightly smaller bolt so the ring has breathing room.
                PointF[] bolt = { new(18.2f,5.2f), new(8.6f,16.2f), new(14.7f,16.2f), new(12.6f,26.0f), new(23.5f,13.3f), new(17.4f,13.3f) };
                g.FillPolygon(white, bolt);
                g.DrawEllipse(ringPen, 3.4f, 3.4f, 25.2f, 25.2f);
                g.DrawLine(slashPen, 6.9f, 7.0f, 25.0f, 25.1f);
            }

            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(large, new Rectangle(0, 0, 32, 32));
            }
            return ToIcon(bitmap);
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
