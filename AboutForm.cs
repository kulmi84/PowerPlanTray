using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PowerPlanTray;

internal sealed class AboutForm : BufferedForm
{
    public AboutForm(string version)
    {
        Text = "Über PowerPlanTray";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(560, 380);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 8 };
        for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(AppTheme.Heading("PowerPlanTray", 17), 0, 0);
        layout.Controls.Add(AppTheme.Note($"Version {version}"), 0, 1);
        layout.Controls.Add(AppTheme.Note("Entwickelt von Marcin Kulmaczewski"), 0, 2);
        void Link(string text, string url, int row)
        {
            var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 10, 0, 6), LinkColor = Color.FromArgb(110, 200, 255), ActiveLinkColor = Color.White, VisitedLinkColor = Color.FromArgb(110, 200, 255) };
            link.LinkClicked += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                { MessageBox.Show(this, "Der Browser konnte nicht geöffnet werden.\n" + url, "Link öffnen", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            layout.Controls.Add(link, 0, row);
        }
        const string project = "https://github.com/kulmi84/PowerPlanTray";
        Link("Projekt auf GitHub", project, 3);
        Link("Fehler melden / Verbesserung vorschlagen", project + "/issues/new", 4);
        Link("Anleitung öffnen", project + "/blob/main/README.md", 5);
        Link("Komponentenhinweise", project + "/blob/main/docs/COMPONENTS.md", 6);
        var close = new Button { Text = "Schließen", AutoSize = true, DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
        layout.Controls.Add(close, 0, 7);
        Controls.Add(layout);
        AcceptButton = CancelButton = close;
        AppTheme.Apply(this);
    }
}
