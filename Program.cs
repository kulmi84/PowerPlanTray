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
        private readonly Icon _hpIcon;

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

            // Geometry mirrors assets/bolt-white.svg and assets/hp-white.svg.
            _boltIcon = CreateBoltIcon();
            _hpIcon = CreateHpIcon();

            _notifyIcon = new NotifyIcon
            {
                Icon = _boltIcon,
                Text = "PowerPlanTray",
                ContextMenuStrip = menu,
                Visible = true
            };

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
            _hpIcon.Dispose();
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
                _notifyIcon.Icon = _hpIcon;
                _notifyIcon.Text = "PowerPlanTray - HP Optimized";
            }
            else if (string.Equals(current, Balanced, StringComparison.OrdinalIgnoreCase))
            {
                _notifyIcon.Icon = _hpIcon;
                _notifyIcon.Text = "PowerPlanTray - Ausbalanciert";
            }
            else
            {
                _notifyIcon.Icon = _hpIcon;
                _notifyIcon.Text = "PowerPlanTray";
            }
        }

        private static Icon CreateBoltIcon()
        {
            using var bitmap = NewCanvas();
            using (var g = Graphics.FromImage(bitmap))
            {
                SetupGraphics(g);
                using var brush = new SolidBrush(Color.White);
                PointF[] bolt =
                {
                    new(18f, 4f),
                    new(8.5f, 17f),
                    new(14.5f, 17f),
                    new(12.5f, 28f),
                    new(24f, 13f),
                    new(18f, 13f)
                };
                g.FillPolygon(brush, bolt);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateHpIcon()
        {
            using var bitmap = NewCanvas();
            using (var g = Graphics.FromImage(bitmap))
            {
                SetupGraphics(g);
                using var pen = new Pen(Color.White, 2f);
                using var brush = new SolidBrush(Color.White);

                // Round HP mark, intentionally compact for the Windows 11 tray.
                g.DrawEllipse(pen, 4f, 4f, 24f, 24f);

                var state = g.Save();
                using var skew = new Matrix();
                skew.Shear(-0.21f, 0f);
                g.Transform = skew;

                g.FillRectangle(brush, 11.2f, 9f, 2.8f, 14f);
                g.FillRectangle(brush, 13.3f, 13f, 5.0f, 2.4f);
                g.FillRectangle(brush, 16.0f, 13f, 2.6f, 10f);
                g.FillRectangle(brush, 19.4f, 13f, 2.7f, 10f);

                using var p = new GraphicsPath();
                p.AddPolygon(new PointF[]
                {
                    new(21.5f,13f), new(25.2f,13f), new(26.5f,14.1f),
                    new(26.1f,17.2f), new(25.1f,20.0f), new(22.2f,20.0f),
                    new(21.7f,23f), new(19.1f,23f), new(20.9f,13f)
                });
                g.FillPath(brush, p);
                g.Restore(state);
            }
            return ToIcon(bitmap);
        }

        private static Bitmap NewCanvas() =>
            new(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        private static void SetupGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
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
