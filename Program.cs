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
            SetPlan(string.Equals(current, HighPerformance, StringComparison.OrdinalIgnoreCase) ? HpOptimized : HighPerformance);
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
            else
            {
                _notifyIcon.Icon = _hpIcon;
                _notifyIcon.Text = string.Equals(current, HpOptimized, StringComparison.OrdinalIgnoreCase)
                    ? "PowerPlanTray - HP Optimized"
                    : string.Equals(current, Balanced, StringComparison.OrdinalIgnoreCase)
                        ? "PowerPlanTray - Ausbalanciert"
                        : "PowerPlanTray";
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
                    new(19.0f, 1.5f), new(4.5f, 18.0f), new(13.8f, 18.0f),
                    new(10.7f, 30.5f), new(27.5f, 11.5f), new(18.2f, 11.5f)
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
                using var white = new SolidBrush(Color.White);
                using var cutout = new SolidBrush(Color.Transparent);

                // Bold round HP mark: solid white disc with dark/transparent HP cutouts.
                // The disc fills almost the complete canvas so Windows tray scaling keeps it prominent.
                g.FillEllipse(white, 1.5f, 1.5f, 29f, 29f);

                // Cut the stylized lowercase hp out of the white disc.
                g.CompositingMode = CompositingMode.SourceCopy;
                using var clear = new SolidBrush(Color.Transparent);
                using var font = new Font("Arial", 18.5f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("hp", font, clear, new RectangleF(-0.5f, 0.5f, 33f, 31f), format);
                g.CompositingMode = CompositingMode.SourceOver;
            }
            return ToIcon(bitmap);
        }

        private static Bitmap NewCanvas() => new(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        private static void SetupGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
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
                    FileName = "powercfg.exe", Arguments = arguments, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                });
                if (process is null) return string.Empty;
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);
                return output;
            }
            catch { return string.Empty; }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
