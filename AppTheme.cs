using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace PowerPlanTray;

// Palette shared visually with HomeCam Monitor's settings, without camera code.
internal static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(24, 24, 27);
    public static readonly Color Surface = Color.FromArgb(35, 35, 39);
    public static readonly Color Field = Color.FromArgb(43, 43, 48);
    public static readonly Color Border = Color.FromArgb(72, 72, 78);
    public static readonly Color Text = Color.FromArgb(242, 242, 244);
    public static readonly Color Muted = Color.FromArgb(164, 164, 170);

    public static void ApplyMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Background; menu.ForeColor = Text;
        menu.Font = new Font("Segoe UI", 10);
        menu.Renderer = new DarkMenuRenderer();
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        { e.TextColor = e.Item.Enabled ? Text : Muted; base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        { e.ArrowColor = e.Item?.Enabled == true ? Text : Muted; base.OnRenderArrow(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var state = e.Item.Enabled ? CheckBoxState.CheckedNormal : CheckBoxState.CheckedDisabled;
            var size = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
            var area = e.ImageRectangle;
            CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(area.X + (area.Width - size.Width) / 2,
                area.Y + (area.Height - size.Height) / 2), state);
        }
    }
    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color MenuItemSelected => Field;
        public override Color MenuItemBorder => Border;
        public override Color MenuBorder => Border;
        public override Color CheckBackground => Field;
        public override Color CheckSelectedBackground => Field;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }

    public static void Apply(Form form)
    {
        form.Font = new Font("Segoe UI", 10f);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        Style(form);
        form.HandleCreated += (_, _) =>
        {
            int dark = 1;
            DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
        };
        form.Shown += (_, _) =>
        {
            var area = Screen.FromControl(form.Owner ?? form).WorkingArea;
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height));
            form.Size = new Size(Math.Min(form.Width, area.Width), Math.Min(form.Height, area.Height));
            form.Location = new Point(Math.Clamp(form.Left, area.Left, area.Right - form.Width), Math.Clamp(form.Top, area.Top, area.Bottom - form.Height));
        };
    }

    private static void Style(Control control)
    {
        control.ForeColor = Equals(control.Tag, "muted") ? Muted : Text;
        control.BackColor = Equals(control.Tag, "surface") ? Surface : control.Parent?.BackColor ?? Background;
        switch (control)
        {
            case TextBoxBase box:
                box.BackColor = Field; box.BorderStyle = BorderStyle.FixedSingle;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = Field; numeric.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox combo:
                combo.BackColor = Field; combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = 26;
                combo.DrawItem += (_, e) =>
                {
                    using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(64, 64, 74) : Field);
                    e.Graphics.FillRectangle(brush, e.Bounds);
                    var value = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
                    TextRenderer.DrawText(e.Graphics, value, combo.Font, Rectangle.Inflate(e.Bounds, -5, 0),
                        combo.Enabled ? Text : Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                };
                break;
            case Button button:
                button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.FromArgb(48, 48, 54);
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 69);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(72, 72, 80);
                button.MinimumSize = new Size(96, 34); button.Padding = new Padding(8, 2, 8, 2);
                button.Cursor = Cursors.Hand;
                break;
            case CheckBox check:
                check.FlatStyle = FlatStyle.Flat; check.Padding = new Padding(0, 3, 0, 3);
                break;
            case DataGridView grid:
                grid.EnableHeadersVisualStyles = false; grid.BackgroundColor = Surface;
                grid.BorderStyle = BorderStyle.None; grid.GridColor = Border;
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(64, 64, 74); grid.DefaultCellStyle.SelectionForeColor = Text;
                grid.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 31, 35);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 31, 35);
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                grid.ColumnHeadersHeight = 40; grid.RowTemplate.Height = 44;
                foreach (DataGridViewRow row in grid.Rows) row.Height = 44;
                break;
        }
        if (control is TextBoxBase or NumericUpDown or ComboBox or DataGridView || control is Panel { AutoScroll: true })
            control.HandleCreated += (_, _) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        foreach (Control child in control.Controls) Style(child);
    }

    // Suppress child-window paints until all visibility and layout changes are
    // complete. SuspendLayout alone does not stop native controls repainting.
    public static void UpdateLayout(Control container, Action update)
    {
        bool freeze = container.IsHandleCreated && container.Visible;
        if (freeze) SendMessage(container.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW
        container.SuspendLayout();
        try { update(); }
        finally
        {
            container.ResumeLayout(true);
            if (freeze)
            {
                SendMessage(container.Handle, 0x000B, new IntPtr(1), IntPtr.Zero);
                RedrawWindow(container.Handle, IntPtr.Zero, IntPtr.Zero, 0x0085); // invalidate, erase, all children
            }
        }
    }
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr window, IntPtr updateRect, IntPtr region, uint flags);

    public static Label Heading(string text, float size = 15) => new ThemeLabel()
    {
        Text = text, AutoSize = true, Font = new Font("Segoe UI", size, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8)
    };

    public static Label Note(string text) => new ThemeLabel() { Text = text, AutoSize = true, Tag = "muted", Margin = new Padding(0, 4, 0, 8), MaximumSize = new Size(750, 0) };

    public static TableLayoutPanel Card(string title)
    {
        var card = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top,
            ColumnCount = 1, Padding = new Padding(18), Margin = new Padding(0, 0, 0, 12), Tag = "surface"
        };
        AddRow(card, Heading(title, 11));
        return card;
    }

    public static void AddRow(TableLayoutPanel table, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Dock = DockStyle.Top; table.Controls.Add(control, 0, row);
    }

    public static TableLayoutPanel Fields() => new()
    {
        AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2,
        ColumnStyles = { new ColumnStyle(SizeType.Absolute, 235), new ColumnStyle(SizeType.Percent, 100) }
    };

    public static void FieldRow(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new ThemeLabel { Text = label, AutoSize = true, Margin = new Padding(0, 9, 12, 8), MaximumSize = new Size(220, 0) }, 0, row);
        control.Dock = DockStyle.Top; control.Margin = new Padding(0, 4, 0, 8); table.Controls.Add(control, 1, row);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string theme, string? subId);
}

internal sealed class ThemeLabel : Label
{
    public ThemeLabel() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
        ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var background = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(background, ClientRectangle);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        OnPaintBackground(e);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : AppTheme.Muted, BackColor,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
            TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
    }
}

internal sealed class DarkCheckBox : CheckBox
{
    public DarkCheckBox()
    {
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(size.Width + 6 * DeviceDpi / 96, Math.Max(26 * DeviceDpi / 96, size.Height));
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var background = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(background, ClientRectangle);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        // Paint the entire opaque background and preserve the paint DC's origin
        // and clip. Otherwise GDI text can leak into sibling controls on repaint.
        OnPaintBackground(e);
        var state = (Enabled, Checked) switch
        {
            (true, true) => CheckBoxState.CheckedNormal,
            (true, false) => CheckBoxState.UncheckedNormal,
            (false, true) => CheckBoxState.CheckedDisabled,
            _ => CheckBoxState.UncheckedDisabled
        };
        var size = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
        CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(0, (Height - size.Height) / 2), state);
        int left = size.Width + 7 * DeviceDpi / 96;
        var text = new Rectangle(left, 0, Math.Max(0, Width - left), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? AppTheme.Text : AppTheme.Muted, BackColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, text, AppTheme.Text, BackColor);
    }
}

internal sealed class DarkComboBox : ComboBox
{
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is not (0x000F or 0x0317 or 0x0318)) return;
        using var graphics = message.Msg != 0x000F && message.WParam != IntPtr.Zero
            ? Graphics.FromHdc(message.WParam) : Graphics.FromHwnd(Handle);
        using var fill = new SolidBrush(AppTheme.Field);
        graphics.FillRectangle(fill, ClientRectangle);
        int arrowWidth = 26 * DeviceDpi / 96;
        TextRenderer.DrawText(graphics, SelectedIndex >= 0 ? GetItemText(SelectedItem) : Text, Font,
            new Rectangle(7, 1, Math.Max(0, Width - arrowWidth - 9), Math.Max(0, Height - 2)),
            Enabled ? AppTheme.Text : AppTheme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var border = new Pen(Focused ? Color.FromArgb(110, 110, 120) : AppTheme.Border);
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        int x = Width - arrowWidth / 2, y = Height / 2;
        using var arrow = new SolidBrush(Enabled ? AppTheme.Text : AppTheme.Muted);
        graphics.FillPolygon(arrow, new[] { new Point(x - 4, y - 2), new Point(x + 4, y - 2), new Point(x, y + 2) });
    }
}

// Compose child HWNDs off-screen too, including native edit and combo controls.
internal class BufferedForm : Form
{
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; parameters.ExStyle |= 0x02000000; return parameters; }
    }
}
internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel() => DoubleBuffered = true;
}
