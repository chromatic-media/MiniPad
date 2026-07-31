// The Appearance screen: light / dark / system, a list of common themes, custom
// background and text colours, and zoom. Every change applies immediately and is
// saved, so there is nothing to confirm.

using System;
using System.Drawing;
using System.Windows.Forms;

public class AppearanceForm : Form
{
    private readonly RadioButton light;
    private readonly RadioButton dark;
    private readonly RadioButton system;
    private readonly ComboBox presetBox;
    private readonly TrackBar zoomBar;
    private readonly Label zoomLabel;
    private readonly Label backSwatch;
    private readonly Label textSwatch;
    private readonly Panel sample;
    private readonly Label sampleHeading;
    private readonly Label sampleBody;
    private readonly Label sampleCode;

    private bool loading = true;

    public event EventHandler ThemeChanged;

    public AppearanceForm()
    {
        Text = "Appearance";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 560);
        Font = new Font("Segoe UI", 9f);

        Label heading = new Label();
        heading.Text = "Appearance";
        heading.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
        heading.AutoSize = true;
        heading.Location = new Point(18, 14);

        Label blurb = new Label();
        blurb.Text = "Applies to the editor, the Markdown preview and every dialog. Saved automatically.";
        blurb.AutoSize = true;
        blurb.Location = new Point(20, 44);

        // ---- mode ----
        GroupBox modeGroup = new GroupBox();
        modeGroup.Text = "Mode";
        modeGroup.Location = new Point(20, 72);
        modeGroup.Size = new Size(250, 108);

        light = Radio("&Light", 24);
        dark = Radio("&Dark", 50);
        system = Radio("&System  (follow Windows)", 76);
        modeGroup.Controls.Add(light);
        modeGroup.Controls.Add(dark);
        modeGroup.Controls.Add(system);

        // ---- theme presets ----
        GroupBox themeGroup = new GroupBox();
        themeGroup.Text = "Theme";
        themeGroup.Location = new Point(286, 72);
        themeGroup.Size = new Size(254, 108);

        presetBox = new ComboBox();
        presetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        presetBox.Location = new Point(14, 28);
        presetBox.Width = 224;
        foreach (string name in Theme.PresetNames) presetBox.Items.Add(name);
        presetBox.SelectedIndexChanged += new EventHandler(OnPresetChanged);

        Label themeNote = new Label();
        themeNote.Text = "A named theme overrides the mode above.";
        themeNote.AutoSize = true;
        themeNote.Location = new Point(16, 62);

        themeGroup.Controls.Add(presetBox);
        themeGroup.Controls.Add(themeNote);

        // ---- custom colours ----
        GroupBox colourGroup = new GroupBox();
        colourGroup.Text = "Custom colours";
        colourGroup.Location = new Point(20, 190);
        colourGroup.Size = new Size(520, 96);

        Button backButton = new Button();
        backButton.Text = "&Background...";
        backButton.Size = new Size(120, 28);
        backButton.Location = new Point(14, 28);
        backButton.Click += delegate { PickColour(true); };

        backSwatch = Swatch(142, 30);

        Button textButton = new Button();
        textButton.Text = "&Text...";
        textButton.Size = new Size(120, 28);
        textButton.Location = new Point(200, 28);
        textButton.Click += delegate { PickColour(false); };

        textSwatch = Swatch(328, 30);

        Button reset = new Button();
        reset.Text = "&Reset to theme";
        reset.Size = new Size(130, 28);
        reset.Location = new Point(380, 28);
        reset.Click += delegate
        {
            Theme.ClearCustomColours();
            Refresh(true);
        };

        Label colourNote = new Label();
        colourNote.Text = "Overrides sit on top of the chosen theme. Reset clears them.";
        colourNote.AutoSize = true;
        colourNote.Location = new Point(16, 64);

        colourGroup.Controls.Add(backButton);
        colourGroup.Controls.Add(backSwatch);
        colourGroup.Controls.Add(textButton);
        colourGroup.Controls.Add(textSwatch);
        colourGroup.Controls.Add(reset);
        colourGroup.Controls.Add(colourNote);

        // ---- zoom ----
        GroupBox zoomGroup = new GroupBox();
        zoomGroup.Text = "Zoom";
        zoomGroup.Location = new Point(20, 296);
        zoomGroup.Size = new Size(520, 92);

        zoomBar = new TrackBar();
        zoomBar.Minimum = Theme.MinZoom;
        zoomBar.Maximum = Theme.MaxZoom;
        zoomBar.TickFrequency = 25;
        zoomBar.SmallChange = 10;
        zoomBar.LargeChange = 25;
        zoomBar.Location = new Point(14, 26);
        zoomBar.Width = 380;
        zoomBar.Scroll += new EventHandler(OnZoomScroll);

        zoomLabel = new Label();
        zoomLabel.AutoSize = true;
        zoomLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        zoomLabel.Location = new Point(404, 34);

        Button zoomReset = new Button();
        zoomReset.Text = "100%";
        zoomReset.Size = new Size(70, 26);
        zoomReset.Location = new Point(440, 30);
        zoomReset.Click += delegate
        {
            Theme.SetZoom(100);
            Refresh(true);
        };

        Label zoomNote = new Label();
        zoomNote.Text = "Ctrl+Plus, Ctrl+Minus and Ctrl+0 also work in the editor.";
        zoomNote.AutoSize = true;
        zoomNote.Location = new Point(16, 64);

        zoomGroup.Controls.Add(zoomBar);
        zoomGroup.Controls.Add(zoomLabel);
        zoomGroup.Controls.Add(zoomReset);
        zoomGroup.Controls.Add(zoomNote);

        // ---- live sample ----
        sample = new Panel();
        sample.Location = new Point(20, 398);
        sample.Size = new Size(520, 106);
        sample.BorderStyle = BorderStyle.FixedSingle;

        sampleHeading = new Label();
        sampleHeading.AutoSize = true;
        sampleHeading.Location = new Point(12, 10);

        sampleBody = new Label();
        sampleBody.AutoSize = true;
        sampleBody.Location = new Point(12, 44);

        sampleCode = new Label();
        sampleCode.AutoSize = true;
        sampleCode.Location = new Point(12, 72);

        sample.Controls.Add(sampleHeading);
        sample.Controls.Add(sampleBody);
        sample.Controls.Add(sampleCode);

        Button ok = new Button();
        ok.Text = "OK";
        ok.Size = new Size(90, 28);
        ok.Location = new Point(ClientSize.Width - 110, ClientSize.Height - 42);
        ok.DialogResult = DialogResult.OK;

        Controls.Add(heading);
        Controls.Add(blurb);
        Controls.Add(modeGroup);
        Controls.Add(themeGroup);
        Controls.Add(colourGroup);
        Controls.Add(zoomGroup);
        Controls.Add(sample);
        Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = ok;

        AppInfo.ApplyIcon(this);
        Refresh(false);
        loading = false;
    }

    private RadioButton Radio(string text, int top)
    {
        RadioButton button = new RadioButton();
        button.Text = text;
        button.AutoSize = true;
        button.Location = new Point(16, top);
        button.CheckedChanged += new EventHandler(OnModeChanged);
        return button;
    }

    private Label Swatch(int left, int top)
    {
        Label swatch = new Label();
        swatch.BorderStyle = BorderStyle.FixedSingle;
        swatch.Size = new Size(46, 24);
        swatch.Location = new Point(left, top);
        return swatch;
    }

    // ---- reacting to changes ----

    private void OnModeChanged(object sender, EventArgs e)
    {
        if (loading) return;
        RadioButton button = (RadioButton)sender;
        if (!button.Checked) return;   // only react to the one being turned on

        Theme.SetMode(button == light ? ThemeMode.Light
                    : button == dark ? ThemeMode.Dark
                    : ThemeMode.System);

        // A named theme would mask the mode, so choosing a mode clears it.
        if (Theme.PresetName != Theme.NoPreset) Theme.SetPreset(Theme.NoPreset);

        Refresh(true);
    }

    private void OnPresetChanged(object sender, EventArgs e)
    {
        if (loading) return;
        Theme.SetPreset((string)presetBox.SelectedItem);
        Refresh(true);
    }

    private void OnZoomScroll(object sender, EventArgs e)
    {
        if (loading) return;
        Theme.SetZoom(zoomBar.Value);
        Refresh(true);
    }

    private void PickColour(bool background)
    {
        ColorDialog picker = new ColorDialog();
        picker.FullOpen = true;
        picker.Color = background ? Theme.EditorBack : Theme.EditorText;
        if (picker.ShowDialog(this) != DialogResult.OK) return;

        if (background) Theme.SetCustomBack(picker.Color);
        else Theme.SetCustomText(picker.Color);

        Refresh(true);
    }

    // Pull every control back in line with Theme, and optionally tell the owner.
    private void Refresh(bool notify)
    {
        bool wasLoading = loading;
        loading = true;

        if (Theme.Mode == ThemeMode.Light) light.Checked = true;
        else if (Theme.Mode == ThemeMode.Dark) dark.Checked = true;
        else system.Checked = true;

        presetBox.SelectedItem = Theme.PresetName;
        zoomBar.Value = Theme.ClampZoom(Theme.Zoom);
        zoomLabel.Text = Theme.Zoom + "%";
        backSwatch.BackColor = Theme.EditorBack;
        textSwatch.BackColor = Theme.EditorText;

        Theme.Apply(this);
        UpdateSample();

        loading = wasLoading;

        if (notify && ThemeChanged != null) ThemeChanged(this, EventArgs.Empty);
    }

    private void UpdateSample()
    {
        sample.BackColor = Theme.EditorBack;

        sampleHeading.Text = "Heading";
        sampleHeading.ForeColor = Theme.EditorText;
        sampleHeading.Font = new Font("Segoe UI", Theme.ScaleFont(13f), FontStyle.Bold);

        sampleBody.Text = "Body text with a link and " + Theme.Base.Name.ToLowerInvariant() + " colours.";
        sampleBody.ForeColor = Theme.EditorText;
        sampleBody.Font = new Font("Segoe UI", Theme.ScaleFont(9f));

        sampleCode.Text = "code span";
        sampleCode.ForeColor = Theme.Accent;
        sampleCode.Font = new Font("Consolas", Theme.ScaleFont(9f));
    }
}
