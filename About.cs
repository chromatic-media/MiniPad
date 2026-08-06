// Product identity and the About dialog for MiniPad.

using System;
using System.Reflection;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

public static class AppInfo
{
    public const string Name = "MiniPad";
    public const string Version = "1.10";
    public const string ReleaseDate = "8/6/26";
    public const string Designer = "Tim Broussard";
    public const string Author = "Legion";

    // "Built in conversation" rather than "written by": nothing here came from
    // either side alone. Spelling out what Legion is matters more than the
    // billing - without it a reader reasonably assumes a second person.
    public const string AuthorNote = "Claude, Anthropic";
    public const string Credit = "Designed by " + Designer + "\r\n"
                               + "Built in conversation with " + Author
                               + " (" + AuthorNote + ")";

    private static Icon icon;
    private static bool iconTried;

    // The notepad icon compiled into the exe by /win32icon. Read from the running
    // file so no .ico has to ship alongside it.
    public static Icon AppIcon
    {
        get
        {
            if (iconTried) return icon;
            iconTried = true;
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    icon = Icon.ExtractAssociatedIcon(path);
            }
            catch
            {
                icon = null;   // a missing icon must never stop the app opening
            }
            return icon;
        }
    }

    public static void ApplyIcon(Form form)
    {
        Icon shared = AppIcon;
        if (shared != null) form.Icon = shared;
    }
}

// The assisted-living provider MiniPad's designer supports. Their words below are
// quoted from faleofaz.org, with the owners' permission - asked for and given
// before any of this was written.
//
// Everything factual lives here as a named constant rather than inline in the
// page, so a detail that changes on their end is a one-line edit in two files
// (this one and minipad.py) instead of a hunt through layout code.
public static class FaleOfaz
{
    public const string Name = "FaleOfaz";
    public const string Tagline = "Empowering Independence, One Life at a Time";
    public const string Site = "https://faleofaz.org/";
    public const string SiteShort = "faleofaz.org";
    public const string Venmo = "https://www.venmo.com/u/faleofaz";
    public const string Phone = "801-317-5454";
    public const string Email = "faleofaz@utfaleofaz.org";
    public const string Instagram = "@faleofaz_llc";
    public const string Where = "West Valley City, Utah - serving Salt Lake County";

    // Kept on its own line, and worded flatly, because a claim about tax
    // treatment inside software that other people redistribute has to be
    // exactly right. Do not embellish it. If their status is ever stated
    // differently, this is the single string to change.
    public const string Status = "FaleOfaz is a registered 501(c)(3) nonprofit.";

    // Venmo issues giving records only for verified Charity profiles, not for
    // ordinary business profiles - so promising a receipt here would be wrong.
    public const string Receipts =
        "A Venmo business profile does not issue a giving receipt automatically.\r\n"
      + "If you need one for tax purposes, ask FaleOfaz directly.";

    public const string Mission =
        "\"At FaleOfaz, our mission has always been rooted in one thing -\r\n"
      + "genuine care for the individuals and families we serve.\"";

    public const string Story =
        "FaleOfaz was founded by three brothers - Jose Fesili, Legrand Z Wolfgramm,\r\n"
      + "and Manisela Wolfgramm - who grew up in West Valley City, Utah. Rooted in\r\n"
      + "faith, family, and community, they built FaleOfaz to be a place where Salt\r\n"
      + "Lake County residents with disabilities are truly seen, celebrated, and\r\n"
      + "supported.\r\n"
      + "\r\n"
      + "They believe every person - regardless of ability - is created with purpose\r\n"
      + "and deserves dignity, love, and meaningful experiences every single day:\r\n"
      + "\r\n"
      + "        \"They are not less than - they are exactly who they\r\n"
      + "         were meant to be.\"\r\n"
      + "\r\n"
      + "Certified by Utah's Office of Licensing, FaleOfaz holds both Group Home and\r\n"
      + "Licensed Home certification, caring for up to five residents in a real home\r\n"
      + "with 24/7 support. Alongside that they provide individual and group day\r\n"
      + "supports, transportation, extended living supports, and personal budget\r\n"
      + "assistance - for people with disabilities, autism, and brain injuries.\r\n"
      + "\r\n"
      + "From Disney trips to Special Olympics swim meets, the point is not only\r\n"
      + "care. It is a life with things in it worth remembering.";

    public const string Why =
        "MiniPad is free and always will be. Nothing here is asked of you - this\r\n"
      + "tab is an invitation, not a request.";

    // ROOM AT THE BOTTOM - deliberately empty.
    //
    // Put a sentence here and it appears at the foot of the Donate tab, styled
    // and set apart, with no other change needed anywhere. Leave it as "" and
    // nothing is drawn at all - no gap, no empty heading.
    //
    // The Linux build carries its own copy of this string. Keep the two in step.
    public const string PersonalNote = "";
}

public class AboutBox : Form
{
    private readonly TabControl tabs;
    private readonly TabPage bunniesPage;
    private readonly TabPage helpPage;
    private readonly TabPage licensePage;
    private readonly TabPage donatePage;
    private readonly HelpPage help;

    public AboutBox() : this(false) { }

    public AboutBox(bool startOnBunnies)
    {
        Text = "About " + AppInfo.Name;
        // Sizable, because the build report is wide and someone will want it bigger.
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 620);
        MinimumSize = new Size(560, 460);
        Font = new Font("Segoe UI", 9f);

        // Everything is docked. Sizing from page.Width/page.Height in the constructor
        // does not work: the pages have not been laid out yet and report tiny defaults.
        FlowLayoutPanel footer = new FlowLayoutPanel();
        footer.Dock = DockStyle.Bottom;
        footer.FlowDirection = FlowDirection.RightToLeft;
        footer.Height = 44;
        footer.Padding = new Padding(0, 8, 10, 0);

        Button ok = new Button();
        ok.Text = "OK";
        ok.Size = new Size(90, 28);
        ok.DialogResult = DialogResult.OK;
        footer.Controls.Add(ok);

        tabs = new TabControl();
        tabs.Dock = DockStyle.Fill;
        tabs.Padding = new Point(12, 4);

        TabPage aboutPage = new TabPage("About");
        helpPage = new TabPage("Help");
        licensePage = new TabPage("License");
        // TabPage.Text is literal: "&&" would show as two ampersands.
        TabPage buildPage = new TabPage("Build & Libraries");
        bunniesPage = new TabPage("Bunnies");
        donatePage = new TabPage("Donate");
        tabs.TabPages.Add(aboutPage);
        tabs.TabPages.Add(helpPage);
        tabs.TabPages.Add(licensePage);
        tabs.TabPages.Add(buildPage);
        tabs.TabPages.Add(bunniesPage);
        tabs.TabPages.Add(donatePage);

        BuildAboutPage(aboutPage);
        help = new HelpPage();
        helpPage.Controls.Add(help);
        BuildLicensePage(licensePage);
        BuildDetailsPage(buildPage);
        BuildBunniesPage(bunniesPage);
        BuildDonatePage(donatePage);

        Padding = new Padding(10, 10, 10, 0);
        Controls.Add(tabs);     // added first so the footer claims the bottom edge
        Controls.Add(footer);

        AcceptButton = ok;
        CancelButton = ok;

        tabs.SelectedIndex = 0;
        if (startOnBunnies) tabs.SelectedTab = bunniesPage;

        AppInfo.ApplyIcon(this);
        Theme.Apply(this);
    }

    public void ShowBunnies()
    {
        tabs.SelectedTab = bunniesPage;
    }

    // Used by F1: open on Help with the cursor already in the search box.
    public void ShowHelp()
    {
        tabs.SelectedTab = helpPage;
        help.FocusSearch();
    }

    private void BuildAboutPage(TabPage page)
    {
        Label title = new Label();
        title.Text = AppInfo.Name + " " + AppInfo.Version;
        title.Font = new Font("Segoe UI", 20f, FontStyle.Bold);
        title.AutoSize = true;
        title.Location = new Point(20, 16);

        Label tagline = new Label();
        tagline.Text = "A small text editor with live Markdown preview";
        tagline.ForeColor = Color.FromArgb(90, 96, 106);
        tagline.AutoSize = true;
        tagline.Location = new Point(23, 58);

        Label credits = new Label();
        credits.Text = AppInfo.Credit + "\r\n"
                     + "Date: " + AppInfo.ReleaseDate;
        credits.Font = new Font("Segoe UI", 10.5f);
        credits.AutoSize = true;
        credits.Location = new Point(23, 88);

        Label rule = new Label();
        rule.BorderStyle = BorderStyle.Fixed3D;
        rule.Height = 2;
        // Width comes from the form, which is known here; page.Width is not yet.
        rule.Width = Math.Max(300, ClientSize.Width - 66);
        rule.Location = new Point(23, 158);
        page.Resize += delegate
        {
            rule.Width = Math.Max(200, page.ClientSize.Width - 46);
        };

        Label blurb = new Label();
        blurb.Text = "Two builds ship together: MiniPad.exe for Windows and minipad.py\r\n"
                   + "for Linux. Both read and write plain text, and both render Markdown.\r\n\r\n"
                   + "Copyright (c) 2026 " + AppInfo.Designer + ". All rights reserved.\r\n"
                   + "Free to use. The source code may not be copied or reused - the\r\n"
                   + "full terms are on the License tab.\r\n\r\n"
                   + "The full owner's manual is under Help, where it is searchable. F1\r\n"
                   + "opens it from anywhere. There is something under Bunnies, too.";
        blurb.AutoSize = true;
        blurb.Location = new Point(23, 176);
        blurb.ForeColor = Color.FromArgb(70, 76, 86);

        page.Controls.Add(title);
        page.Controls.Add(tagline);
        page.Controls.Add(credits);
        page.Controls.Add(rule);
        page.Controls.Add(blurb);
    }

    // The license, read from the copy compiled into the executable, so the
    // terms travel with the program and LICENSE.txt never has to be fetched.
    private void BuildLicensePage(TabPage page)
    {
        page.Padding = new Padding(12);

        TextBox licence = new TextBox();
        licence.Multiline = true;
        licence.ReadOnly = true;
        licence.ScrollBars = ScrollBars.Vertical;
        licence.WordWrap = true;
        licence.Font = new Font("Consolas", 9f);
        licence.Dock = DockStyle.Fill;
        licence.Text = HelpPage.ReadLicense().Replace("\r\n", "\n").Replace("\n", "\r\n");
        licence.Select(0, 0);

        FlowLayoutPanel actions = new FlowLayoutPanel();
        actions.Dock = DockStyle.Bottom;
        actions.Height = 38;
        actions.Padding = new Padding(0, 8, 0, 0);

        Button copy = new Button();
        copy.Text = "&Copy license";
        copy.Size = new Size(110, 28);
        copy.Click += delegate
        {
            try { Clipboard.SetText(licence.Text); }
            catch { }
        };
        actions.Controls.Add(copy);

        Button save = new Button();
        save.Text = "&Save a copy...";
        save.Size = new Size(120, 28);
        save.Click += delegate { SaveLicense(licence.Text); };
        actions.Controls.Add(save);

        page.Controls.Add(licence);    // added first so the buttons take the bottom
        page.Controls.Add(actions);
    }

    private void SaveLicense(string text)
    {
        SaveFileDialog dialog = new SaveFileDialog();
        dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
        dialog.DefaultExt = "txt";
        dialog.FileName = "LICENSE.txt";
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
        }
        catch (Exception error)
        {
            MessageBox.Show(this, "Could not save the license:\r\n\r\n" + error.Message,
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Open the dialog on the License tab.</summary>
    public void ShowLicense()
    {
        tabs.SelectedTab = licensePage;
    }

    private void BuildDetailsPage(TabPage page)
    {
        page.Padding = new Padding(12);

        TextBox details = new TextBox();
        details.Multiline = true;
        details.ReadOnly = true;
        // Both scrollbars: the report is deliberately wider than the window.
        details.ScrollBars = ScrollBars.Both;
        details.WordWrap = false;
        details.Font = new Font("Consolas", 9f);
        details.BackColor = Color.White;
        details.Dock = DockStyle.Fill;
        details.Text = BuildReport();
        details.Select(0, 0);

        FlowLayoutPanel actions = new FlowLayoutPanel();
        actions.Dock = DockStyle.Bottom;
        actions.Height = 38;
        actions.Padding = new Padding(0, 8, 0, 0);

        Button copy = new Button();
        copy.Text = "&Copy details";
        copy.Size = new Size(110, 28);
        copy.Click += delegate
        {
            try { Clipboard.SetText(details.Text); }
            catch { }
        };
        actions.Controls.Add(copy);

        page.Controls.Add(details);   // added first so the buttons take the bottom
        page.Controls.Add(actions);
    }

    // The easter eggs, and the commands that reach them.
    private void BuildBunniesPage(TabPage page)
    {
        Label heading = new Label();
        heading.Text = "Bunnies";
        heading.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        heading.AutoSize = true;
        heading.Location = new Point(18, 14);

        Label intro = new Label();
        intro.Text = "Games hiding inside a text editor. Nothing here touches your document.";
        intro.ForeColor = Color.FromArgb(90, 96, 106);
        intro.AutoSize = true;
        intro.Location = new Point(20, 48);

        Label commandTitle = new Label();
        commandTitle.Text = "Commands";
        commandTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        commandTitle.AutoSize = true;
        commandTitle.Location = new Point(20, 82);

        TextBox commands = new TextBox();
        commands.Multiline = true;
        commands.ReadOnly = true;
        commands.BackColor = Color.White;
        commands.Font = new Font("Consolas", 9f);
        commands.Location = new Point(20, 106);
        commands.Size = new Size(Math.Max(300, ClientSize.Width - 66), 96);
        // Anchoring cannot help here: the page has no real size yet, so the anchor
        // offsets get computed from a placeholder. Resize with the page instead.
        page.Resize += delegate
        {
            commands.Width = Math.Max(200, page.ClientSize.Width - 40);
        };
        commands.Text =
            "  !bunnies            type it in the editor to open this tab\r\n"
          + "  Ctrl+Shift+B        jump straight to this tab from anywhere\r\n"
          + "  About > Bunnies     the tab you are reading\r\n"
          + "\r\n"
          + "  The typed command is left in your text - delete it yourself.";
        commands.Select(0, 0);

        Label gamesTitle = new Label();
        gamesTitle.Text = "Games";
        gamesTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        gamesTitle.AutoSize = true;
        gamesTitle.Location = new Point(20, 214);

        Button minesweeper = new Button();
        minesweeper.Text = "&Minesweeper";
        minesweeper.Size = new Size(130, 30);
        minesweeper.Location = new Point(20, 240);
        minesweeper.Click += delegate
        {
            using (MinesweeperForm game = new MinesweeperForm())
                game.ShowDialog(this);
        };

        Label mineNote = new Label();
        mineNote.Text = "Beginner 9x9/10, Intermediate 16x16/40, Expert 30x16/99.\r\n"
                      + "Left click reveals, right click flags. The first click is always safe.";
        mineNote.AutoSize = true;
        mineNote.Location = new Point(160, 242);
        mineNote.ForeColor = Color.FromArgb(70, 76, 86);

        Button tictactoe = new Button();
        tictactoe.Text = "&Tic-tac-toe";
        tictactoe.Size = new Size(130, 30);
        tictactoe.Location = new Point(20, 296);
        tictactoe.Click += delegate
        {
            using (TicTacToeForm game = new TicTacToeForm())
                game.ShowDialog(this);
        };

        Label ticNote = new Label();
        ticNote.Text = "Easy plays at random, Medium blocks and takes wins,\r\n"
                     + "Hard is full minimax - it cannot be beaten, only drawn.";
        ticNote.AutoSize = true;
        ticNote.Location = new Point(160, 298);
        ticNote.ForeColor = Color.FromArgb(70, 76, 86);

        Label signature = new Label();
        signature.Text = "Designed by " + AppInfo.Designer
                       + ". Built in conversation with " + AppInfo.Author + ".";
        signature.AutoSize = true;
        signature.Location = new Point(20, 352);
        signature.ForeColor = Color.FromArgb(140, 146, 156);

        page.Controls.Add(heading);
        page.Controls.Add(intro);
        page.Controls.Add(commandTitle);
        page.Controls.Add(commands);
        page.Controls.Add(gamesTitle);
        page.Controls.Add(minesweeper);
        page.Controls.Add(mineNote);
        page.Controls.Add(tictactoe);
        page.Controls.Add(ticNote);
        page.Controls.Add(signature);
    }

    // The Donate tab. Content-heavy, so it lives in a scrolling panel rather
    // than being positioned against the page directly - the text is taller than
    // the dialog and would otherwise be silently cut off at the bottom.
    private void BuildDonatePage(TabPage page)
    {
        Panel scroller = new Panel();
        scroller.Dock = DockStyle.Fill;
        scroller.AutoScroll = true;
        scroller.Padding = new Padding(20, 14, 20, 18);
        page.Controls.Add(scroller);

        // An explicit body font, assigned to every label on this page. An AutoSize
        // label measures itself with whatever font it has AT THAT MOMENT, and a label
        // not yet added to its parent has the WinForms default, not this dialog's
        // Segoe UI 9. It then grows when it inherits - after y was already computed
        // from the smaller size. Over a 15-line block that drift is enough to drop the
        // buttons on top of the text. Setting the font up front removes the timing
        // dependency entirely.
        Font body = new Font("Segoe UI", 9f);

        int y = 14;

        // Their logo is black line art on white. A PictureBox is used as the
        // white card on purpose: Theme.Apply() repaints every Panel with the
        // window colour, so a Panel here would be erased in the dark themes and
        // the black artwork would vanish into it. PictureBox is not in that
        // list, so the white it is given survives every theme.
        Image logo = LoadLogo();
        PictureBox card = new PictureBox();
        card.BackColor = Color.White;
        card.SizeMode = PictureBoxSizeMode.CenterImage;
        card.Size = new Size(380, 224);
        card.Location = new Point(20, y);
        card.BorderStyle = BorderStyle.FixedSingle;
        card.Cursor = Cursors.Hand;
        if (logo != null) card.Image = logo;
        card.Click += delegate { OpenLink(FaleOfaz.Site); };
        scroller.Controls.Add(card);
        y = card.Bottom + 18;

        // Every following position is taken from the previous control's real
        // Bottom, never from a guessed step or from Label.PreferredHeight.
        //
        // PreferredHeight is the height of a SINGLE line of text. Using it to
        // advance past a multi-line label under-measures badly - the story block
        // here is 324px tall and PreferredHeight reports about 250 - and the next
        // control is then positioned on top of the text. It compiles, it throws
        // nothing, and it is invisible until someone opens the tab.

        Label heading = new Label();
        heading.Text = FaleOfaz.Name;
        heading.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        heading.AutoSize = true;
        heading.Location = new Point(20, y);
        scroller.Controls.Add(heading);
        y = heading.Bottom + 2;

        Label tagline = new Label();
        tagline.Text = FaleOfaz.Tagline;
        tagline.Font = new Font("Segoe UI", 9.75f, FontStyle.Italic);
        tagline.ForeColor = Color.FromArgb(90, 96, 106);
        tagline.AutoSize = true;
        tagline.Location = new Point(22, y);
        scroller.Controls.Add(tagline);
        y = tagline.Bottom + 4;

        Label where = new Label();
        where.Text = "DSPD-certified residential care - " + FaleOfaz.Where;
        where.Font = body;
        where.ForeColor = Color.FromArgb(140, 146, 156);
        where.AutoSize = true;
        where.Location = new Point(22, y);
        scroller.Controls.Add(where);
        y = where.Bottom + 12;

        Label mission = new Label();
        mission.Text = FaleOfaz.Mission;
        mission.Font = new Font("Segoe UI", 10.5f, FontStyle.Italic);
        mission.AutoSize = true;
        mission.Location = new Point(22, y);
        scroller.Controls.Add(mission);
        y = mission.Bottom + 16;

        Label story = new Label();
        story.Text = FaleOfaz.Story;
        story.Font = body;
        story.AutoSize = true;
        story.Location = new Point(22, y);
        story.ForeColor = Color.FromArgb(70, 76, 86);
        scroller.Controls.Add(story);
        y = story.Bottom + 22;

        Button visit = new Button();
        visit.Text = "&Visit " + FaleOfaz.SiteShort;
        visit.Size = new Size(170, 30);
        visit.Location = new Point(22, y);
        visit.Click += delegate { OpenLink(FaleOfaz.Site); };
        scroller.Controls.Add(visit);

        Button donate = new Button();
        donate.Text = "&Donate with Venmo";
        donate.Size = new Size(170, 30);
        donate.Location = new Point(202, y);
        donate.Click += delegate { OpenLink(FaleOfaz.Venmo); };
        scroller.Controls.Add(donate);
        y = donate.Bottom + 10;

        Label opens = new Label();
        // Kept identical to the Linux build's wording. The fuller point - that
        // MiniPad never connects to anything itself - is made in the manual.
        opens.Text = "Both open in your own browser.";
        opens.Font = body;
        opens.ForeColor = Color.FromArgb(140, 146, 156);
        opens.AutoSize = true;
        opens.Location = new Point(22, y);
        scroller.Controls.Add(opens);
        y = opens.Bottom + 14;

        Label status = new Label();
        status.Text = FaleOfaz.Status + "\r\n" + FaleOfaz.Receipts;
        status.Font = body;
        status.AutoSize = true;
        status.Location = new Point(22, y);
        status.ForeColor = Color.FromArgb(70, 76, 86);
        scroller.Controls.Add(status);
        y = status.Bottom + 18;

        Label contact = new Label();
        contact.Text = "Phone      " + FaleOfaz.Phone + "\r\n"
                     + "Email      " + FaleOfaz.Email + "\r\n"
                     + "Instagram  " + FaleOfaz.Instagram;
        contact.Font = new Font("Consolas", 9f);
        contact.AutoSize = true;
        contact.Location = new Point(22, y);
        contact.ForeColor = Color.FromArgb(70, 76, 86);
        scroller.Controls.Add(contact);
        y = contact.Bottom + 18;

        // Only drawn when there is something to draw. An empty personal note
        // must leave no trace at all - no heading, no gap.
        if (FaleOfaz.PersonalNote.Length > 0)
        {
            Label rule = new Label();
            rule.BorderStyle = BorderStyle.Fixed3D;
            rule.Height = 2;
            rule.Width = 520;
            rule.Location = new Point(22, y);
            scroller.Controls.Add(rule);
            y += 16;

            Label note = new Label();
            note.Text = FaleOfaz.PersonalNote;
            note.Font = new Font("Segoe UI", 10f, FontStyle.Italic);
            note.AutoSize = true;
            note.Location = new Point(22, y);
            scroller.Controls.Add(note);
            y = note.Bottom + 6;

            Label from = new Label();
            from.Text = "- " + AppInfo.Designer;
            from.Font = body;
            from.ForeColor = Color.FromArgb(140, 146, 156);
            from.AutoSize = true;
            from.Location = new Point(22, y);
            scroller.Controls.Add(from);
            y = from.Bottom + 16;
        }

        Label why = new Label();
        why.Text = FaleOfaz.Why;
        why.Font = body;
        why.ForeColor = Color.FromArgb(140, 146, 156);
        why.AutoSize = true;
        why.Location = new Point(22, y);
        scroller.Controls.Add(why);
    }

    // The logo, read from the copy compiled into the executable, so it travels
    // with the program exactly like the manual and the licence do.
    private static Image LoadLogo()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith("faleofaz-logo.png", StringComparison.OrdinalIgnoreCase)) continue;
                using (Stream stream = assembly.GetManifestResourceStream(name))
                {
                    // Image.FromStream keeps using the stream for the life of the
                    // image, and a manifest resource stream closes when this
                    // using block ends. Copy to memory first, and deliberately do
                    // NOT dispose that copy.
                    MemoryStream copy = new MemoryStream();
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        copy.Write(buffer, 0, read);
                    copy.Position = 0;
                    return Image.FromStream(copy);
                }
            }
        }
        catch { }

        // Missing artwork must not take the tab down with it - the words matter
        // more than the picture, and a blank white card still reads as deliberate.
        return null;
    }

    private void OpenLink(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not open:\r\n" + url + "\r\n\r\n" + ex.Message,
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string BuildReport()
    {
        StringBuilder report = new StringBuilder();
        Assembly self = Assembly.GetExecutingAssembly();

        report.AppendLine("BUILD");
        report.AppendLine("  Version          " + AppInfo.Version);
        report.AppendLine("  Release date     " + AppInfo.ReleaseDate);
        report.AppendLine("  Executable       " + SafeLocation(self));
        report.AppendLine("  Built            " + BuildTimestamp(self));
        report.AppendLine("  Size             " + FileSize(self));
        report.AppendLine("  Compiler         csc.exe (.NET Framework 4, Framework64 v4.0.30319)");
        report.AppendLine("  Language         C# 5");
        report.AppendLine("  Sources          MiniPad.cs, Markdown.cs, HtmlToMarkdown.cs,");
        report.AppendLine("                   About.cs, Help.cs, Games.cs, Theme.cs,");
        report.AppendLine("                   Appearance.cs, Document.cs, TabStrip.cs");
        report.AppendLine();

        report.AppendLine("SETTINGS");
        report.AppendLine("  Mode             " + (Theme.IsPortable
                                                ? "Portable - kept beside the program"
                                                : "Normal - kept in your user profile"));
        report.AppendLine("  In use           " + Theme.SettingsPath);
        if (!Theme.IsPortable)
            report.AppendLine("  Portable file    " + Theme.PortableSettingsPath + "  (not present)");
        report.AppendLine("  Switch with      Format > Portable Settings...");
        report.AppendLine();

        report.AppendLine("RUNTIME");
        report.AppendLine("  CLR version      " + Environment.Version);
        report.AppendLine("  Process          " + (IntPtr.Size == 8 ? "64-bit" : "32-bit"));
        report.AppendLine("  Operating system " + OperatingSystemName());
        report.AppendLine("  CLR reports      " + Environment.OSVersion.VersionString
                                                + "  (apps without a manifest see Windows 8)");
        report.AppendLine("  Machine          " + Environment.MachineName);
        report.AppendLine("  Processors       " + Environment.ProcessorCount);
        report.AppendLine();

        report.AppendLine("WINDOWS LIBRARIES USED");
        report.AppendLine("  System.Windows.Forms.dll   window, menus, text box, split pane,");
        report.AppendLine("                             file/font dialogs, clipboard, drag-drop");
        report.AppendLine("  System.Drawing.dll         GDI+ fonts, colours, layout metrics");
        report.AppendLine("  System.dll                 Text.RegularExpressions (Markdown parser),");
        report.AppendLine("                             Diagnostics.Process (opens preview links)");
        report.AppendLine("  mscorlib.dll               core types, file and encoding I/O");
        report.AppendLine();
        report.AppendLine("  WebBrowser control hosts MSHTML (Trident, the Internet Explorer");
        report.AppendLine("  engine built into Windows) to render the Markdown preview. No");
        report.AppendLine("  network access and no third-party packages are used.");
        report.AppendLine();
        report.AppendLine("  Reached indirectly through WinForms: user32.dll (windowing and");
        report.AppendLine("  input), gdi32.dll (drawing), comdlg32.dll (open/save/font dialogs),");
        report.AppendLine("  shell32.dll (launching the default browser), ole32.dll (clipboard");
        report.AppendLine("  and control hosting).");
        report.AppendLine();

        report.AppendLine("LOADED ASSEMBLIES");
        Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in loaded)
        {
            try
            {
                AssemblyName name = assembly.GetName();
                report.AppendLine("  " + name.Name.PadRight(26) + name.Version.ToString());
            }
            catch { }
        }
        report.AppendLine();

        report.AppendLine("FEATURES");
        report.AppendLine("  Text       open, save, save as, drag-and-drop, command-line path,");
        report.AppendLine("             encoding and line-ending preserved, word wrap, font");
        report.AppendLine("  Markdown   live preview (F9), headings, bold, italic, strikethrough,");
        report.AppendLine("             inline and fenced code, quotes, nested lists, pipe");
        report.AppendLine("             tables, links, images, rules, export as HTML");
        report.AppendLine("  Preview    three selectable modes under Markdown > Preview Mode:");
        report.AppendLine("             read-only, click-to-jump-to-source, and editable");
        report.AppendLine("             (typing on the rendered page syncs back to Markdown)");
        report.AppendLine();
        report.AppendLine("  A Linux build (minipad.py, Python 3 + Tkinter) ships alongside this");
        report.AppendLine("  executable and runs on Ubuntu with the same feature set.");

        return report.ToString();
    }

    // Environment.OSVersion reports 6.2 for unmanifested apps, so read the real
    // version out of the registry instead.
    private static string OperatingSystemName()
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                if (key == null) return Environment.OSVersion.VersionString;

                string product = key.GetValue("ProductName") as string;
                string display = key.GetValue("DisplayVersion") as string;
                string build = Convert.ToString(key.GetValue("CurrentBuildNumber"));
                string revision = Convert.ToString(key.GetValue("UBR"));

                int buildNumber;
                if (product != null && int.TryParse(build, out buildNumber)
                    && buildNumber >= 22000 && product.Contains("Windows 10"))
                {
                    product = product.Replace("Windows 10", "Windows 11");
                }

                StringBuilder name = new StringBuilder(product ?? "Windows");
                if (!string.IsNullOrEmpty(display)) name.Append(' ').Append(display);
                if (!string.IsNullOrEmpty(build))
                {
                    name.Append(" (build ").Append(build);
                    if (!string.IsNullOrEmpty(revision)) name.Append('.').Append(revision);
                    name.Append(')');
                }
                return name.ToString();
            }
        }
        catch
        {
            return Environment.OSVersion.VersionString;
        }
    }

    private static string SafeLocation(Assembly assembly)
    {
        try { return assembly.Location; }
        catch { return "(unknown)"; }
    }

    private static string BuildTimestamp(Assembly assembly)
    {
        try { return File.GetLastWriteTime(assembly.Location).ToString("yyyy-MM-dd HH:mm:ss"); }
        catch { return "(unknown)"; }
    }

    private static string FileSize(Assembly assembly)
    {
        try
        {
            long bytes = new FileInfo(assembly.Location).Length;
            return string.Format("{0:n0} bytes ({1:n1} KB)", bytes, bytes / 1024.0);
        }
        catch { return "(unknown)"; }
    }
}
