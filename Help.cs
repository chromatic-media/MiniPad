// The Help tab: the owner's manual, embedded in the executable and searchable.
//
// The manual is compiled in as a resource (see build.ps1), so the exe stays
// self-contained. If that resource is ever missing it falls back to MANUAL.md
// sitting next to the program.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

public class HelpPage : Panel
{
    private readonly RichTextBox viewer;
    private readonly TextBox searchBox;
    private readonly Label resultLabel;
    private readonly Button previousButton;
    private readonly Button nextButton;

    private readonly List<int> matches = new List<int>();
    private string plainText = "";
    private string lastQuery = "";
    private int current = -1;

    // ---- what keeps searching fast ----
    //
    // Every highlight is a native EM_SETCHARFORMAT round trip, and the manual is
    // 56 KB: a single letter matches ~5,000 times. The old code did that work
    // THREE times per keystroke - once to paint every match, once more because
    // ShowCurrent repainted all of them, and a third time when the next keystroke
    // cleared them - with no debounce. Typing "the" froze the dialog for seconds.
    //
    // Three things fix it: wait until typing pauses, never repaint a match that
    // does not need it, and stop painting thousands of backgrounds nobody asked
    // for. The MATCH COUNT is always exact - only the painting is capped.
    private readonly Timer searchTimer;
    private const int HighlightCap = 400;
    private bool highlightedAll;      // did every match get a background?
    private int currentPainted = -1;  // which match currently wears the orange

    private static readonly Color HighlightAll = Color.FromArgb(255, 244, 158);
    private static readonly Color HighlightCurrent = Color.FromArgb(255, 176, 62);

    public HelpPage()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(12);

        // ---- search strip ----
        Panel bar = new Panel();
        bar.Dock = DockStyle.Top;
        bar.Height = 40;

        Label searchLabel = new Label();
        searchLabel.Text = "Search";
        searchLabel.AutoSize = true;
        searchLabel.Location = new Point(0, 9);

        searchTimer = new Timer();
        searchTimer.Interval = 200;   // long enough to swallow a burst of typing
        searchTimer.Tick += delegate { searchTimer.Stop(); Search(searchBox.Text, true); };

        searchBox = new TextBox();
        searchBox.Location = new Point(52, 6);
        searchBox.Width = 260;
        // Debounced, not immediate: searching on every keystroke means searching
        // for "t", then "th", then "the", and the first of those is the expensive one.
        searchBox.TextChanged += delegate { searchTimer.Stop(); searchTimer.Start(); };
        searchBox.KeyDown += new KeyEventHandler(OnSearchKeyDown);

        previousButton = new Button();
        previousButton.Text = "Previous";
        previousButton.Size = new Size(80, 25);
        previousButton.Location = new Point(320, 5);
        previousButton.Click += delegate { Step(-1); };

        nextButton = new Button();
        nextButton.Text = "Next";
        nextButton.Size = new Size(70, 25);
        nextButton.Location = new Point(406, 5);
        nextButton.Click += delegate { Step(1); };

        resultLabel = new Label();
        resultLabel.AutoSize = true;
        resultLabel.Location = new Point(486, 9);

        bar.Controls.Add(searchLabel);
        bar.Controls.Add(searchBox);
        bar.Controls.Add(previousButton);
        bar.Controls.Add(nextButton);
        bar.Controls.Add(resultLabel);

        // ---- the manual ----
        viewer = new RichTextBox();
        viewer.Dock = DockStyle.Fill;
        viewer.ReadOnly = true;
        viewer.BorderStyle = BorderStyle.FixedSingle;
        viewer.WordWrap = true;
        viewer.HideSelection = false;    // keep matches visible when focus moves away
        viewer.Font = new Font("Segoe UI", 9.75f);
        viewer.DetectUrls = false;

        Controls.Add(viewer);            // added first so the strip claims the top
        Controls.Add(bar);

        LoadManual();
        UpdateResultLabel();
    }

    public void FocusSearch()
    {
        searchBox.Focus();
        searchBox.SelectAll();
    }

    // ---- loading ----

    public static string ReadManual()
    {
        return ReadBundled("MANUAL.md",
            "The manual could not be loaded.\r\n\r\n"
          + "It is normally compiled into " + AppInfo.Name + ".exe. If this build was made "
          + "without it, put MANUAL.md in the same folder as the program.");
    }

    public static string ReadLicense()
    {
        return ReadBundled("LICENSE.txt",
            "The license could not be loaded.\r\n\r\n"
          + "It is normally compiled into " + AppInfo.Name + ".exe. If this build was made "
          + "without it, put LICENSE.txt in the same folder as the program.");
    }

    /// <summary>A file compiled into the executable, or failing that one beside it.</summary>
    public static string ReadBundled(string fileName, string fallback)
    {
        // 1. the copy compiled into the executable
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)) continue;
                using (Stream stream = assembly.GetManifestResourceStream(name))
                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false), true))
                    return reader.ReadToEnd();
            }
        }
        catch { }

        // 2. a copy next to the program
        try
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(folder ?? ".", fileName);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        catch { }

        return fallback;
    }

    private void LoadManual()
    {
        string markdown = ReadManual().Replace("\r\n", "\n");

        // Lay the manual out for reading: headings get weight, code and tables get a
        // monospaced font, and heading markers are dropped since they add nothing here.
        viewer.SuspendLayout();
        viewer.Clear();

        Font body = new Font("Segoe UI", 9.75f);
        Font mono = new Font("Consolas", 9f);
        Font h1 = new Font("Segoe UI", 15f, FontStyle.Bold);
        Font h2 = new Font("Segoe UI", 12f, FontStyle.Bold);
        Font h3 = new Font("Segoe UI", 10.5f, FontStyle.Bold);

        bool inFence = false;
        foreach (string raw in markdown.Split('\n'))
        {
            string line = raw;
            Font font = body;

            if (line.TrimStart().StartsWith("```"))
            {
                inFence = !inFence;
                continue;                       // the fence markers themselves add nothing
            }

            if (inFence)
            {
                font = mono;
            }
            else if (line.StartsWith("### "))
            {
                font = h3;
                line = line.Substring(4);
            }
            else if (line.StartsWith("## "))
            {
                font = h2;
                line = line.Substring(3);
            }
            else if (line.StartsWith("# "))
            {
                font = h1;
                line = line.Substring(2);
            }
            else if (line.StartsWith("|"))
            {
                font = mono;
            }

            viewer.SelectionFont = font;
            viewer.AppendText(line + "\n");
        }

        viewer.SelectionStart = 0;
        viewer.SelectionLength = 0;
        viewer.ResumeLayout();

        plainText = viewer.Text;
    }

    // ---- searching ----

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            Step(e.Shift ? -1 : 1);
            e.Handled = true;
            e.SuppressKeyPress = true;      // no ding from the text box
        }
        else if (e.KeyCode == Keys.Escape)
        {
            searchBox.Clear();
            e.Handled = true;
        }
    }

    private void Search(string query, bool jumpToFirst)
    {
        // ⚠ ClearHighlights runs FIRST, while lastQuery still names the OLD query -
        // it needs that length to know how wide the old highlights were.
        ClearHighlights();
        matches.Clear();
        current = -1;
        currentPainted = -1;
        lastQuery = query;

        // Finding the matches is pure string work and costs almost nothing. It is
        // painting them that is expensive, so the two are now separate steps.
        if (query.Length > 0)
        {
            int index = plainText.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                matches.Add(index);
                int next = index + Math.Max(1, query.Length);
                if (next >= plainText.Length) break;
                index = plainText.IndexOf(query, next, StringComparison.OrdinalIgnoreCase);
            }
        }

        highlightedAll = matches.Count > 0 && matches.Count <= HighlightCap;
        if (highlightedAll)
            foreach (int start in matches) Highlight(start, HighlightAll, Theme.HighlightInk);

        if (jumpToFirst && matches.Count > 0)
        {
            // Prefer the first match at or after where the reader already is.
            int caret = viewer.SelectionStart;
            current = 0;
            for (int n = 0; n < matches.Count; n++)
            {
                if (matches[n] >= caret) { current = n; break; }
            }
            ShowCurrent();
        }
        else
        {
            viewer.SelectionStart = 0;
            viewer.SelectionLength = 0;
        }

        UpdateResultLabel();
    }

    private void Step(int direction)
    {
        if (searchBox.Text != lastQuery)
        {
            // Enter should not wait out the debounce.
            searchTimer.Stop();
            Search(searchBox.Text, true);
            return;
        }
        if (matches.Count == 0) return;

        // Wrap around at either end.
        if (current < 0) current = direction > 0 ? 0 : matches.Count - 1;
        else current = (current + direction + matches.Count) % matches.Count;

        ShowCurrent();
        UpdateResultLabel();
    }

    // One highlight. Every painting path goes through here.
    // Named Highlight, not Paint: Control already has a Paint event, and shadowing
    // it compiles with a warning and confuses everyone who reads it afterwards.
    private void Highlight(int start, Color back, Color ink)
    {
        viewer.SelectionStart = start;
        viewer.SelectionLength = lastQuery.Length;
        viewer.SelectionBackColor = back;
        viewer.SelectionColor = ink;
    }

    private void ShowCurrent()
    {
        if (current < 0 || current >= matches.Count) return;

        // Put back ONLY the match that was current. Repainting every match here
        // was a second full pass over thousands of them, on top of the one Search
        // had just done - and all it needed to undo was one highlight.
        if (currentPainted >= 0 && currentPainted < matches.Count && currentPainted != current)
        {
            if (highlightedAll) Highlight(matches[currentPainted], HighlightAll, Theme.HighlightInk);
            else Highlight(matches[currentPainted], viewer.BackColor, viewer.ForeColor);
        }

        Highlight(matches[current], HighlightCurrent, Theme.HighlightInk);
        currentPainted = current;

        // Move the caret without leaving a selection on top of the orange.
        viewer.SelectionStart = matches[current];
        viewer.SelectionLength = 0;
        viewer.ScrollToCaret();
    }

    private void ClearHighlights()
    {
        if (matches.Count == 0 || lastQuery.Length == 0) return;

        if (highlightedAll)
        {
            foreach (int start in matches)
                Highlight(start, viewer.BackColor, viewer.ForeColor);
        }
        else if (currentPainted >= 0 && currentPainted < matches.Count)
        {
            // Over the cap only one match was ever painted, so only one needs clearing.
            Highlight(matches[currentPainted], viewer.BackColor, viewer.ForeColor);
        }
        currentPainted = -1;
    }

    // The debounce timer outlives nothing: it must go when the page does, or it
    // ticks at a disposed RichTextBox.
    protected override void Dispose(bool disposing)
    {
        if (disposing && searchTimer != null)
        {
            searchTimer.Stop();
            searchTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void UpdateResultLabel()
    {
        previousButton.Enabled = matches.Count > 0;
        nextButton.Enabled = matches.Count > 0;

        if (searchBox.Text.Length == 0)
        {
            resultLabel.Text = "Enter to jump, Shift+Enter to go back";
            resultLabel.ForeColor = Theme.UiDim;
        }
        else if (matches.Count == 0)
        {
            resultLabel.Text = "no matches";
            resultLabel.ForeColor = Color.FromArgb(190, 60, 60);
        }
        else
        {
            // Say when the highlighting was capped, rather than letting it look
            // like most matches were missed. The count itself is always exact.
            //
            // ⚠ Kept SHORT on purpose. The first wording ran off the right edge of
            // the dialog at its minimum width - the same trap as the Donate tab's
            // caption, and equally invisible until someone screenshots it.
            resultLabel.Text = string.Format("{0} of {1} match{2}{3}",
                current + 1, matches.Count, matches.Count == 1 ? "" : "es",
                highlightedAll ? "" : " (current only)");
            resultLabel.ForeColor = Theme.UiDim;
        }
    }

    // Exposed for testing: how many matches a query finds.
    public int MatchCount(string query)
    {
        Search(query, false);
        return matches.Count;
    }
}
