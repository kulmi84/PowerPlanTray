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
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _balancedItem;
        private readonly ToolStripMenuItem _highItem;
        private readonly ToolStripMenuItem _hpItem;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Icon _boltIcon;
        private readonly Icon _batteryIcon;

        public TrayApplicationContext()
        {
            _balancedItem = new ToolStripMenuItem("Ausbalanciert", null, (_, _) => SetPlan(Balanced));
            _highItem = new ToolStripMenuItem("Höchstleistung", null, (_, _) => SetPlan(HighPerformance));
            _hpItem = new ToolStripMenuItem("HP Optimized", null, (_, _) => SetPlan(HpOptimized));

            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                _highItem,
                _hpItem,
                _balancedItem,
                new ToolStripSeparator(),
                new ToolStripMenuItem("Beenden", null, (_, _) => ExitThread())
            });

            _boltIcon = CreateBoltIcon();
            _batteryIcon = CreateBatteryIcon();

            _notifyIcon = new NotifyIcon
            {
                Icon = _boltIcon,
                Text = "PowerPlanTray",
                ContextMenuStrip = menu,
                Visible = true
            };

            // One left click toggles directly between High Performance and HP Optimized.
            _notifyIcon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ToggleHighHp();
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
            _boltIcon.Dispose();
            _batteryIcon.Dispose();
            base.ExitThreadCore();
        }

        private void ToggleHighHp()
        {
            var current = GetActivePlan();
            SetPlan(string.Equals(current, HighPerformance, StringComparison.OrdinalIgnoreCase)
                ? HpOptimized
                : HighPerformance);
        }

        private void SetPlan(string guid)
        {
            RunPowerCfg($"/setactive {guid}");
            RefreshState();
        }

        private void RefreshState()
        {
            var current = GetActivePlan();

            _balancedItem.Checked = string.Equals(current, Balanced, StringComparison.OrdinalIgnoreCase);
            _highItem.Checked = string.Equals(current, HighPerformance, StringComparison.OrdinalIgnoreCase);
            _hpItem.Checked = string.Equals(current, HpOptimized, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(current, HighPerformance, StringComparison.OrdinalIgnoreCase))
            {
                _notifyIcon.Icon = _boltIcon;
                _notifyIcon.Text = "PowerPlanTray - Höchstleistung";
            }
            else if (string.Equals(current, HpOptimized, StringComparison.OrdinalIgnoreCase))
            {
                _notifyIcon.Icon = _batteryIcon;
                _notifyIcon.Text = "PowerPlanTray - HP Optimized";
            }
            else if (string.Equals(current, Balanced, StringComparison.OrdinalIgnoreCase))
            {
                _notifyIcon.Icon = _batteryIcon;
                _notifyIcon.Text = "PowerPlanTray - Ausbalanciert";
            }
            else
            {
                _notifyIcon.Icon = _batteryIcon;
                _notifyIcon.Text = "PowerPlanTray";
            }
        }

        private static Icon CreateBoltIcon()
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(Color.White);

                PointF[] bolt =
                {
                    new(18.2f, 1.5f),
                    new(6.0f, 17.2f),
                    new(13.7f, 17.2f),
                    new(10.7f, 30.3f),
                    new(26.2f, 12.2f),
                    new(18.0f, 12.2f)
                };
                g.FillPolygon(brush, bolt);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateBatteryIcon()
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using var pen = new Pen(Color.White, 3.0f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                using var brush = new SolidBrush(Color.White);

                // Large monochrome Windows-style battery outline, sized for the tray.
                using var path = RoundedRect(new RectangleF(3.0f, 8.0f, 23.0f, 16.0f), 3.0f);
                g.DrawPath(pen, path);
                g.FillRectangle(brush, 27.0f, 12.0f, 3.0f, 8.0f);

                // Small fill bar so the icon stays readable at 16x16.
                g.FillRectangle(brush, 7.0f, 12.0f, 10.0f, 8.0f);
            }
            return ToIcon(bitmap);
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Icon ToIcon(Bitmap bitmap)
        {
            var hIcon = bitmap.GetHicon();
            try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
            finally { DestroyIcon(hIcon); }
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
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

                if (process is null) return string.Empty;
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}