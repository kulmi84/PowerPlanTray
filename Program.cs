using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PowerPlanTray;

internal static class Program
{
    private const string HighPerformance = "4d922812-1350-43fd-9a02-bd8cb10e3619";
    private const string HpOptimized = "fb5220ff-7e1a-47aa-9a42-50ffbf45c673";
    private const string QuietRemote = "708c8ab9-7ca4-4f43-9652-2809432ef837";

    [STAThread]
    private static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new TrayApplicationContext()); }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _highItem, _hpItem, _quietItem;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Icon _boltIcon, _hpIcon, _quietIcon;

        public TrayApplicationContext()
        {
            _highItem = new ToolStripMenuItem("Höchstleistung", null, (_, _) => SetPlan(HighPerformance));
            _hpItem = new ToolStripMenuItem("HP Optimized", null, (_, _) => SetPlan(HpOptimized));
            _quietItem = new ToolStripMenuItem("Leise / Remote", null, (_, _) => SetPlan(QuietRemote));

            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                _highItem,
                _hpItem,
                _quietItem,
                new ToolStripSeparator(),
                new ToolStripMenuItem("Beenden", null, (_, _) => ExitThread())
            });

            _boltIcon = CreateBoltIcon();
            _hpIcon = CreateCrossedBoltIcon();
            _quietIcon = CreateQuietIcon();

            _notifyIcon = new NotifyIcon
            {
                Icon = _boltIcon,
                Text = "PowerPlanTray",
                ContextMenuStrip = menu,
                Visible = true
            };

            // Linksklick bleibt bewusst nur der schnelle Wechsel
            // zwischen Höchstleistung und HP Optimized.
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
            _quietIcon.Dispose();
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

            _highItem.Checked = string.Equals(current, HighPerformance, StringComparison.OrdinalIgnoreCase);
            _hpItem.Checked = string.Equals(current, HpOptimized, StringComparison.OrdinalIgnoreCase);
            _quietItem.Checked = string.Equals(current, QuietRemote, StringComparison.OrdinalIgnoreCase);

            if (_highItem.Checked)
            {
                _notifyIcon.Icon = _boltIcon;
                _notifyIcon.Text = "PowerPlanTray - Höchstleistung";
            }
            else if (_quietItem.Checked)
            {
                _notifyIcon.Icon = _quietIcon;
                _notifyIcon.Text = "PowerPlanTray - Leise / Remote";
            }
            else
            {
                _notifyIcon.Icon = _hpIcon;
                _notifyIcon.Text = _hpItem.Checked
                    ? "PowerPlanTray - HP Optimized"
                    : "PowerPlanTray";
            }
        }

        private static Icon CreateBoltIcon()
        {
            // Höchstleistung: großer weißer Kreis mit schwarzem Blitz.
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                using var white = new SolidBrush(Color.White);
                using var black = new SolidBrush(Color.Black);

                g.FillEllipse(white, 0.25f, 0.25f, 31.5f, 31.5f);

                PointF[] bolt =
                {
                    new(18.8f, 3.0f),
                    new(7.0f, 17.2f),
                    new(13.8f, 17.2f),
                    new(11.5f, 29.0f),
                    new(25.4f, 12.6f),
                    new(18.0f, 12.6f)
                };
                g.FillPolygon(black, bolt);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateCrossedBoltIcon()
        {
            // HP Optimized: gleicher großer Kreis, Blitz mit diagonalem Strich.
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                using var white = new SolidBrush(Color.White);
                using var black = new SolidBrush(Color.Black);
                using var slashPen = new Pen(Color.Black, 3.8f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                g.FillEllipse(white, 0.25f, 0.25f, 31.5f, 31.5f);

                PointF[] bolt =
                {
                    new(18.8f, 3.0f),
                    new(7.0f, 17.2f),
                    new(13.8f, 17.2f),
                    new(11.5f, 29.0f),
                    new(25.4f, 12.6f),
                    new(18.0f, 12.6f)
                };
                g.FillPolygon(black, bolt);
                g.DrawLine(slashPen, 6.3f, 6.3f, 25.7f, 25.7f);
            }
            return ToIcon(bitmap);
        }

        private static Icon CreateQuietIcon()
        {
            // Leise / Remote: drei weiße Z ohne Kreis oder Rand.
            // Die Zeichen nutzen fast die komplette Tray-Fläche und werden
            // als feste Geometrie gezeichnet, damit sie auch bei 16 px klar bleiben.
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                using var large = new Pen(Color.White, 5.2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                using var medium = new Pen(Color.White, 4.0f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                using var small = new Pen(Color.White, 3.2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };

                // Großes Z oben/rechts – dominant und möglichst groß.
                g.DrawLines(large, new[]
                {
                    new PointF(14.5f, 3.2f),
                    new PointF(29.0f, 3.2f),
                    new PointF(14.0f, 18.0f),
                    new PointF(28.8f, 18.0f)
                });

                // Mittleres z in der Mitte.
                g.DrawLines(medium, new[]
                {
                    new PointF(8.0f, 14.0f),
                    new PointF(19.0f, 14.0f),
                    new PointF(8.0f, 24.0f),
                    new PointF(19.2f, 24.0f)
                });

                // Kleines z unten/links.
                g.DrawLines(small, new[]
                {
                    new PointF(2.5f, 22.0f),
                    new PointF(10.2f, 22.0f),
                    new PointF(2.8f, 29.0f),
                    new PointF(10.5f, 29.0f)
                });
            }
            return ToIcon(bitmap);
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
