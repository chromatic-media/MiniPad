// Appearance for MiniPad: light / dark / system, named palettes, custom colours,
// and zoom. Everything resolves through Theme, which also generates the preview
// stylesheet so the rendered Markdown always matches the editor.
//
// "System" follows the Windows personalisation setting, read from the registry
// because .NET Framework 4 has no API for it.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

public enum ThemeMode { Light, Dark, System }

public class Palette
{
    public string Name;
    public bool Dark;
    public Color Back;       // editor background
    public Color Text;       // editor text
    public Color Chrome;     // menus, status bar, dialog surfaces
    public Color Accent;     // links and highlights
    public Color CodeBack;   // inline code and code blocks
    public Color Border;
    public Color Dim;        // secondary text

    public Palette(string name, bool dark, string back, string text, string chrome,
                   string accent, string codeBack, string border, string dim)
    {
        Name = name;
        Dark = dark;
        Back = Parse(back);
        Text = Parse(text);
        Chrome = Parse(chrome);
        Accent = Parse(accent);
        CodeBack = Parse(codeBack);
        Border = Parse(border);
        Dim = Parse(dim);
    }

    public static Color Parse(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(
            int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
            int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
            int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
    }

    public static string ToHex(Color colour)
    {
        return string.Format("#{0:x2}{1:x2}{2:x2}", colour.R, colour.G, colour.B);
    }
}

public static class Theme
{
    public const string NoPreset = "(follow light / dark choice)";
    public const int MinZoom = 50;
    public const int MaxZoom = 300;

    public static ThemeMode Mode { get; private set; }
    public static string PresetName { get; private set; }   // NoPreset, or a palette name
    public static int Zoom { get; private set; }            // percent
    public static Color? CustomBack { get; private set; }
    public static Color? CustomText { get; private set; }

    // Not a colour, but this class owns settings.txt and one bool does not
    // justify a second settings file. On for a first run, so the preview is
    // found rather than hunted for; remembered after that.
    public static bool PreviewOn { get; private set; }

    // How the program is used, rather than how it looks. These used to reset on
    // every launch, which made the editor feel like it was forgetting you: the
    // font in particular, since choosing one and losing it each morning reads as
    // a fault rather than a default.
    public static string FontFamily { get; private set; }   // "" = the built-in default
    public static float FontSize { get; private set; }      // 0  = the built-in default
    public static int PreviewMode { get; private set; }     // 0 read-only, 1 click-to-jump, 2 editable
    public static bool WordWrap { get; private set; }
    public static Rectangle WindowBounds { get; private set; }  // Empty = centre at the default size
    public static bool WindowMaximised { get; private set; }

    // ---- the palettes ----

    private static readonly List<Palette> All = new List<Palette>
    {
        new Palette("Light",           false, "ffffff", "181a1f", "f0f0f0", "0969da", "eff1f3", "d0d7de", "5a606a"),
        new Palette("Dark",            true,  "181a1c", "e2e5e9", "2b2d31", "6cb6ff", "2b2d31", "3c3f44", "969ca5"),
        new Palette("Solarized Light", false, "fdf6e3", "586e75", "eee8d5", "268bd2", "eee8d5", "d5cdb6", "93a1a1"),
        new Palette("Solarized Dark",  true,  "002b36", "93a1a1", "073642", "268bd2", "073642", "10505f", "657b83"),
        new Palette("Monokai",         true,  "272822", "f8f8f2", "3e3d32", "a6e22e", "3e3d32", "55554a", "a9a9a0"),
        new Palette("Nord",            true,  "2e3440", "d8dee9", "3b4252", "88c0d0", "3b4252", "4c566a", "8f9bb0"),
        new Palette("Gruvbox Dark",    true,  "282828", "ebdbb2", "3c3836", "fabd2f", "3c3836", "504945", "a89984"),
        new Palette("Sepia",           false, "f4ecd8", "4b3f32", "e8dcc0", "8b5a2b", "e8dcc0", "cbbb9a", "7d6b57"),
        new Palette("High Contrast",   true,  "000000", "ffffff", "000000", "ffff00", "1a1a1a", "ffffff", "d0d0d0"),
    };

    public static IEnumerable<Palette> Palettes { get { return All; } }

    public static List<string> PresetNames
    {
        get
        {
            List<string> names = new List<string> { NoPreset };
            foreach (Palette palette in All) names.Add(palette.Name);
            return names;
        }
    }

    private static Palette Find(string name)
    {
        foreach (Palette palette in All)
            if (palette.Name == name) return palette;
        return null;
    }

    // The palette in force, before custom colour overrides.
    public static Palette Base
    {
        get
        {
            Palette preset = PresetName == NoPreset ? null : Find(PresetName);
            if (preset != null) return preset;

            bool dark = Mode == ThemeMode.Dark
                        || (Mode == ThemeMode.System && SystemPrefersDark());
            return Find(dark ? "Dark" : "Light");
        }
    }

    public static bool IsDark { get { return Base.Dark; } }

    // ---- resolved colours ----

    public static Color EditorBack { get { return CustomBack ?? Base.Back; } }
    public static Color EditorText { get { return CustomText ?? Base.Text; } }
    public static Color WindowBack { get { return Base.Chrome; } }
    public static Color MenuBack { get { return Base.Chrome; } }
    public static Color MenuText { get { return Base.Text; } }
    public static Color MenuHighlight { get { return Blend(Base.Chrome, Base.Accent, 0.35); } }
    public static Color Border { get { return Base.Border; } }
    public static Color Dim { get { return Base.Dim; } }
    public static Color Accent { get { return Base.Accent; } }

    // Secondary text for the window chrome, guaranteed readable.
    //
    // Dim is the palette's own muted tone and is used verbatim in the preview
    // stylesheet, where matching the palette is the point. Solarized, Nord,
    // Gruvbox and Sepia keep that tone deliberately low-contrast, which suits a
    // blockquote but leaves the status bar hard to read - Solarized Dark manages
    // only 2.9:1. Chrome text is lifted towards the body colour until it clears
    // 4.5:1, stopping there: secondary text should never be heavier than the
    // text beside it. Solarized Light cannot reach 4.5:1 even at its body
    // colour (4.39:1), so it ends up exactly as readable as the rest of itself.
    public static Color UiDim
    {
        get
        {
            Palette palette = Base;
            for (int step = 0; step <= 20; step++)
            {
                Color candidate = Blend(palette.Dim, palette.Text, step / 20.0);
                if (ContrastRatio(candidate, palette.Chrome) >= 4.5) return candidate;
            }
            return palette.Text;
        }
    }

    // The accent, lifted the same way, for accent-coloured text on the chrome -
    // the zoom indicator. Solarized Light's raw accent manages only 3.0:1 there.
    public static Color UiAccent
    {
        get
        {
            Palette palette = Base;
            for (int step = 0; step <= 20; step++)
            {
                Color candidate = Blend(palette.Accent, palette.Text, step / 20.0);
                if (ContrastRatio(candidate, palette.Chrome) >= 4.5) return candidate;
            }
            return palette.Text;
        }
    }

    // Ink dark enough to read against the fixed yellow search highlights.
    public static readonly Color HighlightInk = Color.FromArgb(26, 26, 26);

    private static double Luminance(Color colour)
    {
        double[] channels = new double[3];
        int[] raw = { colour.R, colour.G, colour.B };
        for (int n = 0; n < 3; n++)
        {
            double value = raw[n] / 255.0;
            channels[n] = value <= 0.03928 ? value / 12.92
                                           : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
    }

    // The WCAG contrast ratio between two colours, from 1:1 to 21:1.
    public static double ContrastRatio(Color first, Color second)
    {
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static Color Blend(Color a, Color b, double amount)
    {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * amount),
            (int)(a.G + (b.G - a.G) * amount),
            (int)(a.B + (b.B - a.B) * amount));
    }

    public static float ScaleFont(float points)
    {
        float scaled = points * Zoom / 100f;
        return Math.Max(5f, Math.Min(96f, scaled));
    }

    // ---- the preview stylesheet, generated from the palette ----

    public static string PreviewCss()
    {
        Palette palette = Base;
        string back = Palette.ToHex(EditorBack);
        string text = Palette.ToHex(EditorText);
        string code = Palette.ToHex(palette.CodeBack);
        string border = Palette.ToHex(palette.Border);
        string dim = Palette.ToHex(palette.Dim);
        string link = Palette.ToHex(palette.Accent);
        // AwayFromZero, or .NET's banker's rounding turns 150% into 22px instead of 23.
        int size = (int)Math.Round(15.0 * Zoom / 100.0, MidpointRounding.AwayFromZero);

        return "body{font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:" + size + "px;"
             + "line-height:1.6;color:" + text + ";background:" + back + ";margin:0;padding:24px 28px;}"
             + "h1,h2,h3,h4,h5,h6{margin:24px 0 12px;line-height:1.25;font-weight:600;}"
             + "h1{font-size:1.9em;border-bottom:1px solid " + border + ";padding-bottom:8px;}"
             + "h2{font-size:1.45em;border-bottom:1px solid " + border + ";padding-bottom:6px;}"
             + "h3{font-size:1.2em;} h4{font-size:1em;} h5,h6{font-size:.9em;color:" + dim + ";}"
             + "p{margin:0 0 14px;} ul,ol{margin:0 0 14px;padding-left:26px;} li{margin:3px 0;}"
             + "code{font-family:Consolas,monospace;font-size:.9em;background:" + code + ";"
             + "padding:2px 5px;border-radius:4px;}"
             + "pre{background:" + code + ";border:1px solid " + border + ";border-radius:6px;"
             + "padding:12px 14px;overflow:auto;}"
             + "pre code{background:none;padding:0;font-size:.88em;}"
             + "blockquote{margin:0 0 14px;padding:2px 14px;color:" + dim + ";"
             + "border-left:4px solid " + border + ";}"
             + "table{border-collapse:collapse;margin:0 0 16px;}"
             + "th,td{border:1px solid " + border + ";padding:6px 12px;}"
             + "th{background:" + code + ";font-weight:600;}"
             + "hr{border:0;border-top:1px solid " + border + ";margin:22px 0;}"
             + "a{color:" + link + ";} img{max-width:100%;} del{color:" + dim + ";}";
    }

    // ---- persistence ----

    // ---- where settings live: portable, or the user profile ----
    //
    // Portable mode is switched on by the mere PRESENCE of settings.txt beside the
    // executable. There is deliberately no prompt and no first-run banner: the
    // feature matters to people running MiniPad from a USB stick or a locked-down
    // machine, and everyone else should never have to think about it. The menu item
    // under Format is the whole of its advertising.
    //
    // Presence, not a flag inside the file, is the switch - so a user can enable it
    // with nothing but a text editor, and disable it by deleting one file.

    public static string PortableSettingsPath
    {
        get
        {
            try { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt"); }
            catch { return null; }
        }
    }

    public static string ProfileSettingsPath
    {
        get
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniPad");
            return Path.Combine(folder, "settings.txt");
        }
    }

    public static bool IsPortable
    {
        get
        {
            try
            {
                string portable = PortableSettingsPath;
                return portable != null && File.Exists(portable);
            }
            catch { return false; }
        }
    }

    // Which file is actually in use. Shown in the About box so the mode is never
    // a mystery - "why did it forget my theme" is otherwise unanswerable.
    public static string SettingsPath
    {
        get { return IsPortable ? PortableSettingsPath : ProfileSettingsPath; }
    }

    // Returns null on success, or a human-readable reason it failed. Writing beside
    // the executable is not always allowed - Program Files and write-protected
    // sticks both refuse - and that must be reported, not swallowed.
    public static string MakePortable()
    {
        string portable = PortableSettingsPath;
        if (portable == null) return "The program's own folder could not be determined.";
        if (File.Exists(portable)) return null;      // already portable, nothing to do
        try
        {
            WriteTo(portable);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // The caller is responsible for confirming with the user first: this deletes a
    // file. Settings are copied back to the profile before it goes, so switching
    // off never loses the user's choices.
    public static string MakeNotPortable()
    {
        string portable = PortableSettingsPath;
        if (portable == null || !File.Exists(portable)) return null;
        try
        {
            string folder = Path.GetDirectoryName(ProfileSettingsPath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            WriteTo(ProfileSettingsPath);
            File.Delete(portable);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static void Load()
    {
        Mode = ThemeMode.System;
        PresetName = NoPreset;
        Zoom = 100;
        CustomBack = null;
        CustomText = null;
        PreviewOn = true;
        FontFamily = "";
        FontSize = 0;
        PreviewMode = 0;
        WordWrap = true;
        WindowBounds = Rectangle.Empty;
        WindowMaximised = false;

        try
        {
            if (!File.Exists(SettingsPath)) return;
            foreach (string line in File.ReadAllLines(SettingsPath))
            {
                string[] parts = line.Split(new char[] { '=' }, 2);
                if (parts.Length != 2) continue;
                string key = parts[0].Trim().ToLowerInvariant();
                string value = parts[1].Trim();

                if (key == "theme")
                {
                    string mode = value.ToLowerInvariant();
                    Mode = mode == "light" ? ThemeMode.Light
                         : mode == "dark" ? ThemeMode.Dark
                         : ThemeMode.System;
                }
                else if (key == "preset")
                {
                    PresetName = Find(value) != null ? value : NoPreset;
                }
                else if (key == "zoom")
                {
                    int zoom;
                    if (int.TryParse(value, out zoom)) Zoom = ClampZoom(zoom);
                }
                else if (key == "background")
                {
                    CustomBack = ParseOrNull(value);
                }
                else if (key == "text")
                {
                    CustomText = ParseOrNull(value);
                }
                else if (key == "preview")
                {
                    // Anything that is not "off" leaves it on, so a damaged
                    // value errs towards showing the pane rather than hiding it.
                    PreviewOn = !value.Equals("off", StringComparison.OrdinalIgnoreCase);
                }
                else if (key == "fontfamily")
                {
                    // Not validated here. The font may exist on the machine that
                    // wrote the file and not on this one - especially with portable
                    // settings on a USB stick - so the editor checks it at the point
                    // of use and falls back rather than failing here.
                    FontFamily = value.Equals("none", StringComparison.OrdinalIgnoreCase) ? "" : value;
                }
                else if (key == "fontsize")
                {
                    float size;
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out size)
                        && size >= 5 && size <= 72)
                        FontSize = size;
                }
                else if (key == "previewmode")
                {
                    int mode;
                    if (int.TryParse(value, out mode) && mode >= 0 && mode <= 2) PreviewMode = mode;
                }
                else if (key == "wordwrap")
                {
                    WordWrap = !value.Equals("off", StringComparison.OrdinalIgnoreCase);
                }
                else if (key == "window")
                {
                    // "x,y,width,height". Restored only if it lands on a screen that
                    // still exists - see MainForm - so unplugging a second monitor
                    // cannot strand the window off the visible desktop.
                    string[] bits = value.Split(',');
                    int x, y, w, h;
                    if (bits.Length == 4
                        && int.TryParse(bits[0], out x) && int.TryParse(bits[1], out y)
                        && int.TryParse(bits[2], out w) && int.TryParse(bits[3], out h)
                        && w > 200 && h > 150)
                        WindowBounds = new Rectangle(x, y, w, h);
                }
                else if (key == "maximised")
                {
                    WindowMaximised = value.Equals("on", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            Mode = ThemeMode.System;
            PresetName = NoPreset;
            Zoom = 100;
        }
    }

    private static Color? ParseOrNull(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;
        try { return Palette.Parse(value); }
        catch { return null; }
    }

    public static int ClampZoom(int zoom)
    {
        return Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
    }

    public static void Save()
    {
        try
        {
            string target = SettingsPath;
            string folder = Path.GetDirectoryName(target);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            WriteTo(target);
        }
        catch
        {
            // A read-only profile must not stop the app working. If the portable
            // file has become unwritable - a stick flipped to read-only mid-session
            // - fall back to the profile rather than silently losing the settings.
            try
            {
                if (IsPortable)
                {
                    string folder = Path.GetDirectoryName(ProfileSettingsPath);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    WriteTo(ProfileSettingsPath);
                }
            }
            catch { }
        }
    }

    private static void WriteTo(string path)
    {
        File.WriteAllText(path,
            "theme=" + Mode.ToString().ToLowerInvariant() + "\r\n"
          + "preset=" + (PresetName == NoPreset ? "none" : PresetName) + "\r\n"
          + "zoom=" + Zoom + "\r\n"
          + "background=" + (CustomBack.HasValue ? Palette.ToHex(CustomBack.Value) : "none") + "\r\n"
          + "text=" + (CustomText.HasValue ? Palette.ToHex(CustomText.Value) : "none") + "\r\n"
          + "preview=" + (PreviewOn ? "on" : "off") + "\r\n"
          + "fontfamily=" + (string.IsNullOrEmpty(FontFamily) ? "none" : FontFamily) + "\r\n"
          + "fontsize=" + FontSize.ToString(CultureInfo.InvariantCulture) + "\r\n"
          + "previewmode=" + PreviewMode + "\r\n"
          + "wordwrap=" + (WordWrap ? "on" : "off") + "\r\n"
          + "window=" + (WindowBounds.IsEmpty ? "none"
                : string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
                    WindowBounds.X, WindowBounds.Y, WindowBounds.Width, WindowBounds.Height)) + "\r\n"
          + "maximised=" + (WindowMaximised ? "on" : "off") + "\r\n");
    }

    public static void SetFont(string family, float size)
    {
        FontFamily = family == null ? "" : family;
        FontSize = size;
        Save();
    }

    public static void SetPreviewMode(int mode)
    {
        if (mode < 0 || mode > 2 || PreviewMode == mode) return;
        PreviewMode = mode;
        Save();
    }

    public static void SetWordWrap(bool wrap)
    {
        if (WordWrap == wrap) return;
        WordWrap = wrap;
        Save();
    }

    // Called as the window closes, so a session's final geometry is what returns.
    public static void SetWindow(Rectangle bounds, bool maximised)
    {
        WindowBounds = bounds;
        WindowMaximised = maximised;
        Save();
    }

    public static void SetMode(ThemeMode mode) { Mode = mode; Save(); }

    public static void SetPreset(string name)
    {
        PresetName = (name == NoPreset || Find(name) == null) ? NoPreset : name;
        Save();
    }

    public static void SetZoom(int zoom) { Zoom = ClampZoom(zoom); Save(); }

    public static void SetPreviewOn(bool on)
    {
        if (PreviewOn == on) return;    // no need to rewrite the file to say the same thing
        PreviewOn = on;
        Save();
    }

    public static void SetCustomBack(Color? colour) { CustomBack = colour; Save(); }

    public static void SetCustomText(Color? colour) { CustomText = colour; Save(); }

    public static void ClearCustomColours()
    {
        CustomBack = null;
        CustomText = null;
        Save();
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key == null) return false;
                object value = key.GetValue("AppsUseLightTheme");
                if (value == null) return false;
                return Convert.ToInt32(value) == 0;
            }
        }
        catch
        {
            return false;   // if in doubt, stay light
        }
    }

    // ---- applying to a form ----

    public static void Apply(Control root)
    {
        root.BackColor = WindowBack;
        root.ForeColor = MenuText;
        ApplyToChildren(root);
    }

    private static void ApplyToChildren(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TextBoxBase)
            {
                control.BackColor = EditorBack;
                control.ForeColor = EditorText;
            }
            else if (control is MenuStrip)
            {
                MenuStrip menu = (MenuStrip)control;
                menu.BackColor = MenuBack;
                menu.ForeColor = MenuText;
                menu.Renderer = new ToolStripProfessionalRenderer(new ThemeColours());
                foreach (ToolStripItem item in menu.Items) ApplyToMenuItem(item);
            }
            else if (control is StatusStrip)
            {
                control.BackColor = MenuBack;
                control.ForeColor = Dim;
            }
            else if (control is TabControl || control is TabPage || control is Panel
                     || control is SplitContainer || control is FlowLayoutPanel
                     || control is GroupBox)
            {
                control.BackColor = WindowBack;
                control.ForeColor = MenuText;
            }
            else if (control is Button)
            {
                control.BackColor = Blend(WindowBack, MenuText, 0.08);
                control.ForeColor = MenuText;
            }
            else if (control is ComboBox || control is ListBox || control is TrackBar)
            {
                control.BackColor = EditorBack;
                control.ForeColor = EditorText;
            }
            else if (control is Label || control is RadioButton || control is CheckBox)
            {
                control.ForeColor = MenuText;
            }

            if (control.Controls.Count > 0) ApplyToChildren(control);
        }
    }

    private static void ApplyToMenuItem(ToolStripItem item)
    {
        item.BackColor = MenuBack;
        item.ForeColor = MenuText;
        ToolStripMenuItem menuItem = item as ToolStripMenuItem;
        if (menuItem == null) return;
        foreach (ToolStripItem child in menuItem.DropDownItems) ApplyToMenuItem(child);
    }

    // Colours for menus and their drop-downs.
    private class ThemeColours : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return MenuHighlight; } }
        public override Color MenuItemSelectedGradientBegin { get { return MenuHighlight; } }
        public override Color MenuItemSelectedGradientEnd { get { return MenuHighlight; } }
        public override Color MenuItemPressedGradientBegin { get { return MenuBack; } }
        public override Color MenuItemPressedGradientEnd { get { return MenuBack; } }
        public override Color MenuStripGradientBegin { get { return MenuBack; } }
        public override Color MenuStripGradientEnd { get { return MenuBack; } }
        public override Color ToolStripDropDownBackground { get { return MenuBack; } }
        public override Color ImageMarginGradientBegin { get { return MenuBack; } }
        public override Color ImageMarginGradientEnd { get { return MenuBack; } }
        public override Color ImageMarginGradientMiddle { get { return MenuBack; } }
        public override Color MenuBorder { get { return Border; } }
        public override Color MenuItemBorder { get { return MenuHighlight; } }
        public override Color SeparatorDark { get { return Border; } }
        public override Color SeparatorLight { get { return Border; } }
    }
}
