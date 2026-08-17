// MiniPad - a small Notepad-like text editor with live Markdown preview.
// Build: see build.ps1 (compiles with the .NET Framework 4 csc.exe, no SDK needed).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

public class MainForm : Form
{
    private const string FileFilter =
        "Text files (*.txt)|*.txt|Markdown (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*";

    // How the preview pane behaves. The user picks this from Markdown > Preview Mode.
    private enum PreviewMode
    {
        ReadOnly,    // render only; the pane never takes edits
        ClickToJump, // clicking rendered text moves the editor caret to that source line
        Editable,    // type onto the rendered page; edits convert back into Markdown
    }

    private readonly List<Document> documents = new List<Document>();
    private Document current;
    private TabStrip tabs;
    private Panel editorHost;
    private SplitContainer split;
    private WebBrowser preview;
    private Timer previewTimer;
    private Timer syncTimer;
    private StatusStrip statusBar;
    private ToolStripStatusLabel positionLabel;
    private ToolStripStatusLabel modeLabel;
    private ToolStripMenuItem wordWrapItem;
    private ToolStripMenuItem previewItem;
    private ToolStripMenuItem portableItem;
    private Label previewCaption;
    private Label editorCaption;

    // The two panes are kept in vertical step by construction rather than by
    // eye: identical caption heights, and an editor inset equal to the preview
    // stylesheet's body padding-top. Change either and change the CSS to match.
    private const int CaptionHeight = 22;
    private const int PreviewTopPadding = 24;
    private ToolStripMenuItem[] modeItems;

    private ToolStripStatusLabel zoomLabel;
    private PreviewMode previewMode = PreviewMode.ReadOnly;
    private bool syncingFromPreview = false;

    // The unzoomed font; zoom scales this rather than compounding on itself.
    private string baseFontFamily = "Consolas";
    private float baseFontSize = 11f;

    private int previewScroll = 0;
    private bool returnFocusToEditor = false;
    private bool previewNavigating = false;   // a render is in flight
    private bool previewNeedsRender = false;  // and another was asked for meanwhile
    private Document previewDocument = null;  // whose document the pane is showing

    // Everything below reads and writes whichever document is in front. Keeping
    // these as properties means the rest of the editor never had to learn that
    // there is now more than one document.
    private TextBox editor { get { return current.Editor; } }

    private string currentPath
    {
        get { return current.Path; }
        set { current.Path = value; }
    }

    private bool dirty
    {
        get { return current.Dirty; }
        set { current.Dirty = value; }
    }

    private bool unixLineEndings
    {
        get { return current.UnixLineEndings; }
        set { current.UnixLineEndings = value; }
    }

    private Encoding currentEncoding
    {
        get { return current.Encoding; }
        set { current.Encoding = value; }
    }

    // One constructor only. An overload taking a single path read nicely but made
    // `new MainForm(null)` ambiguous, and the compiler is right to complain.
    public MainForm(string[] startupPaths)
    {
        // Settings are loaded FIRST, before any control is built. The font, word
        // wrap, preview mode and window geometry all have to be known before the
        // things that use them exist, and a control built with a default and
        // corrected afterwards flickers visibly on a slow machine.
        Theme.Load();

        Text = "MiniPad";
        Width = 1100;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        // A remembered font may have come from another machine - portable settings
        // travel on a USB stick - so it is validated here rather than trusted. An
        // absent family silently substitutes in WinForms, which would leave the user
        // with a font they did not choose and no idea why.
        if (!string.IsNullOrEmpty(Theme.FontFamily) && FontFamilyExists(Theme.FontFamily))
            baseFontFamily = Theme.FontFamily;
        if (Theme.FontSize > 0)
            baseFontSize = Theme.FontSize;

        RestoreWindowPosition();

        tabs = new TabStrip();
        tabs.TabSelected += new TabIndexHandler(OnTabSelected);
        tabs.TabCloseRequested += new TabIndexHandler(OnTabCloseRequested);
        tabs.NewTabRequested += delegate { NewFile(); };

        editorHost = new Panel();
        editorHost.Dock = DockStyle.Fill;
        // Matches the preview's body padding-top, so line 1 of the source sits at
        // the same height as line 1 of the rendering. A WinForms TextBox has no
        // usable padding of its own, so the inset comes from its host panel.
        editorHost.Padding = new Padding(0, PreviewTopPadding, 0, 0);

        // Both panes carry a caption of the same height. Beyond naming them, the
        // matching height is what keeps the two first lines level - a caption over
        // only one pane offsets everything below it by its own height.
        editorCaption = new Label();
        editorCaption.Dock = DockStyle.Top;
        editorCaption.AutoSize = false;
        editorCaption.Height = CaptionHeight;
        editorCaption.TextAlign = ContentAlignment.MiddleLeft;
        editorCaption.Padding = new Padding(8, 0, 0, 0);
        editorCaption.Text = "Markdown source";

        preview = new WebBrowser();
        preview.Dock = DockStyle.Fill;
        preview.AllowWebBrowserDrop = false;
        preview.ScriptErrorsSuppressed = true;
        preview.WebBrowserShortcutsEnabled = false;
        preview.Navigating += new WebBrowserNavigatingEventHandler(OnPreviewNavigating);
        preview.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(OnPreviewCompleted);

        // A caption over the preview. Without it the second pane is unexplained:
        // a new user types on the left, sees the same words appear on the right,
        // and has no way to know why. The caption answers that in two words, and
        // naming the mode explains why the pane behaves as it does.
        previewCaption = new Label();
        previewCaption.Dock = DockStyle.Top;
        previewCaption.AutoSize = false;
        previewCaption.Height = CaptionHeight;
        previewCaption.TextAlign = ContentAlignment.MiddleLeft;
        previewCaption.Padding = new Padding(8, 0, 0, 0);
        previewCaption.Text = "Live Preview";

        split = new SplitContainer();
        split.Dock = DockStyle.Fill;
        split.Orientation = Orientation.Vertical;
        split.Panel1.Controls.Add(editorHost);
        split.Panel1.Controls.Add(editorCaption);
        // The filled control goes in first, so the caption can claim the top edge
        // from what is left rather than being painted over.
        split.Panel2.Controls.Add(preview);
        split.Panel2.Controls.Add(previewCaption);
        split.Panel2Collapsed = true;

        previewTimer = new Timer();
        previewTimer.Interval = 450; // debounce so typing stays smooth
        previewTimer.Tick += delegate { previewTimer.Stop(); RenderPreview(); };

        syncTimer = new Timer();
        syncTimer.Interval = 600; // debounce edits made in the preview pane
        syncTimer.Tick += delegate { syncTimer.Stop(); PullFromPreview(); };

        statusBar = new StatusStrip();
        positionLabel = new ToolStripStatusLabel("Ln 1, Col 1");
        modeLabel = new ToolStripStatusLabel("Preview: off");
        modeLabel.Alignment = ToolStripItemAlignment.Right;
        zoomLabel = new ToolStripStatusLabel("100%");
        zoomLabel.Alignment = ToolStripItemAlignment.Right;
        statusBar.Items.Add(positionLabel);
        statusBar.Items.Add(modeLabel);
        statusBar.Items.Add(zoomLabel);

        // Docking is resolved last-added-outermost, so this order puts the menu
        // at the very top, the tab strip directly below it spanning the whole
        // window, the status bar at the bottom, and the split in what is left.
        // The strip sits above the preview rather than inside the editor pane so
        // the tabs get the full window width instead of half of it.
        Controls.Add(split);
        Controls.Add(statusBar);
        Controls.Add(tabs);
        MainMenuStrip = BuildMenu();
        Controls.Add(MainMenuStrip);

        DragEnter += new DragEventHandler(OnDragEnter);
        DragDrop += new DragEventHandler(OnDragDrop);
        FormClosing += new FormClosingEventHandler(OnFormClosing);
        FormClosed += delegate
        {
            previewTimer.Stop();
            previewTimer.Dispose();
            syncTimer.Stop();
            syncTimer.Dispose();
        };
        AppInfo.ApplyIcon(this);

        // There is always at least one document, so `current` is never null.
        AddDocument(NewDocument());

        // ⚠ THE REMEMBERED MODE IS RESTORED FIRST, AND THE ORDER IS THE POINT.
        //
        // EnablePreview ends by calling UpdateModeLabel, which reads previewMode.
        // Restoring the mode afterwards left the status bar and the pane caption
        // reporting "read-only" while the pane was in fact editable - the
        // behaviour was right and only the label lied, which is the hardest kind
        // of wrong to notice. Nothing called UpdateModeLabel again.
        //
        // Set without going through SetPreviewMode, which would turn the pane on
        // as a side effect - wrong when the user last left it hidden - and would
        // re-save the value we just read.
        int remembered = Theme.PreviewMode;
        if (remembered < 0 || remembered >= modeItems.Length) remembered = 0;
        previewMode = (PreviewMode)remembered;
        for (int index = 0; index < modeItems.Length; index++)
            modeItems[index].Checked = index == remembered;

        // The preview starts showing unless it was last turned off, so it is
        // found rather than hunted for. The splitter has no width until Shown,
        // which is where the first render happens.
        EnablePreview(Theme.PreviewOn, false);
        UpdatePortableItem();
        ApplyTheme();

        Shown += delegate
        {
            split.SplitterDistance = split.Width / 2;
            if (previewItem.Checked) RenderPreview();
        };

        bool openedAny = false;
        if (startupPaths != null)
        {
            foreach (string path in startupPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                LoadFile(path);
                openedAny = true;
            }
        }
        if (!openedAny) UpdateTitle();
    }

    // ---- documents and tabs ----

    private Document NewDocument()
    {
        Document document = new Document(NextUntitledNumber());
        document.Editor.TextChanged += new EventHandler(OnTextChanged);
        // The bunny command is checked on key-up, not on TextChanged: loading a file or
        // syncing from the preview also changes the text, and a document that happens to
        // end with the command would otherwise pop the dialog open by itself.
        document.Editor.KeyUp += delegate
        {
            UpdatePosition();
            CheckForBunnyCommand();
        };
        document.Editor.MouseUp += delegate { UpdatePosition(); };
        document.Editor.MouseWheel += new MouseEventHandler(OnEditorMouseWheel);
        document.Editor.WordWrap = wordWrapItem == null || wordWrapItem.Checked;
        document.Editor.ScrollBars = document.Editor.WordWrap ? ScrollBars.Vertical : ScrollBars.Both;
        // A tab opened after startup has missed every theme change so far, so it
        // is dressed on the way in rather than waiting for the next one.
        StyleEditor(document.Editor);
        return document;
    }

    private void StyleEditor(TextBox box)
    {
        box.BackColor = Theme.EditorBack;
        box.ForeColor = Theme.EditorText;
        ReplaceFont(box, new Font(baseFontFamily, Theme.ScaleFont(baseFontSize)));
    }

    // Puts `replacement` on the control and frees whichever Font ends up unused.
    //
    // 🔴 WINFORMS IGNORES A VALUE-EQUAL FONT ASSIGNMENT. Control.Font compares by
    // VALUE (family, size, style, unit), not by reference, so assigning a Font that
    // merely equals the current one leaves the control holding its ORIGINAL instance
    // and silently drops the new one. Disposing the old instance then leaves the
    // control painting with freed memory, which surfaces as
    // ArgumentException("Parameter is not valid") from Font.ToHfont() - reported far
    // from the actual mistake, and usually as a crash before the window ever appears.
    //
    // ⚠ THIS IS WHY THE TEST BELOW IS ReferenceEquals AND NOT ==. Font overloads
    // neither == nor Equals to reference semantics, so a value comparison here would
    // always say "unchanged" and reintroduce the bug. 1.11 shipped exactly that.
    private static void ReplaceFont(Control box, Font replacement)
    {
        Font previous = box.Font;
        box.Font = replacement;
        if (ReferenceEquals(box.Font, replacement))
            DisposeIfOwned(previous);   // the swap took: the old one is ours to free
        else
            replacement.Dispose();      // the swap was ignored: free the spare instead
    }

    // The lowest number not already taken, so closing "Untitled 2" frees the name.
    private int NextUntitledNumber()
    {
        for (int candidate = 1; ; candidate++)
        {
            bool taken = false;
            foreach (Document document in documents)
                if (document.Path == null && document.UntitledNumber == candidate) taken = true;
            if (!taken) return candidate;
        }
    }

    private void AddDocument(Document document)
    {
        documents.Add(document);
        editorHost.Controls.Add(document.Editor);
        SelectDocument(documents.Count - 1);
    }

    private void SelectDocument(int index)
    {
        if (index < 0 || index >= documents.Count) return;
        current = documents[index];

        foreach (Document document in documents)
            document.Editor.Visible = document == current;
        current.Editor.BringToFront();

        UpdateTitle();      // which also refreshes the strip
        UpdatePosition();
        RenderPreview();       // the preview follows whichever document is in front
        if (IsHandleCreated) current.Editor.Focus();
    }

    private void RefreshTabs()
    {
        List<string> labels = new List<string>();
        foreach (Document document in documents) labels.Add(document.TabText);
        tabs.SetTabs(labels, documents.IndexOf(current));
    }

    private void OnTabSelected(int index)
    {
        if (index >= 0 && index < documents.Count && documents[index] != current)
            SelectDocument(index);
    }

    private void OnTabCloseRequested(int index)
    {
        CloseDocument(index);
    }

    /// <summary>Close one tab. Returns false if the user cancelled.</summary>
    private bool CloseDocument(int index)
    {
        if (index < 0 || index >= documents.Count) return true;

        // Closing the last tab closes the window, as Notepad does. The asking is
        // left to the form's own closing handler, or it would be asked twice.
        if (documents.Count == 1)
        {
            Close();
            return true;
        }

        Document document = documents[index];
        if (!ConfirmDiscard(document)) return false;

        documents.RemoveAt(index);
        editorHost.Controls.Remove(document.Editor);
        document.Editor.Dispose();
        // Never leave the preview pointing at something that no longer exists.
        if (previewDocument == document) previewDocument = null;

        if (current == document)
            SelectDocument(Math.Min(index, documents.Count - 1));
        else
            RefreshTabs();
        return true;
    }

    private Document DocumentFor(TextBox box)
    {
        if (box == null) return null;
        foreach (Document document in documents)
            if (document.Editor == box) return document;
        return null;
    }

    private void SelectRelativeTab(int step)
    {
        if (documents.Count < 2) return;
        int index = documents.IndexOf(current) + step;
        if (index < 0) index = documents.Count - 1;
        if (index >= documents.Count) index = 0;
        SelectDocument(index);
    }

    private MenuStrip BuildMenu()
    {
        MenuStrip menu = new MenuStrip();

        ToolStripMenuItem file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(Item("&New Tab", Keys.Control | Keys.N, delegate { NewFile(); }));
        file.DropDownItems.Add(Item("&Open...", Keys.Control | Keys.O, delegate { OpenFile(); }));
        file.DropDownItems.Add(Item("&Save", Keys.Control | Keys.S, delegate { Save(); }));
        file.DropDownItems.Add(Item("Save &As...", Keys.Control | Keys.Shift | Keys.S, delegate { SaveAs(); }));
        file.DropDownItems.Add(Item("Save A&ll", Keys.Control | Keys.Alt | Keys.S, delegate { SaveAll(); }));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("&Close Tab", Keys.Control | Keys.W,
            delegate { CloseDocument(documents.IndexOf(current)); }));
        file.DropDownItems.Add(Item("Close &Other Tabs", Keys.None, delegate { CloseOtherTabs(); }));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("&Export as HTML...", Keys.None, delegate { ExportHtml(); }));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("E&xit", Keys.None, delegate { Close(); }));

        ToolStripMenuItem edit = new ToolStripMenuItem("&Edit");
        edit.DropDownItems.Add(Item("&Undo", Keys.Control | Keys.Z, delegate { editor.Undo(); }));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Item("Cu&t", Keys.Control | Keys.X, delegate { editor.Cut(); }));
        edit.DropDownItems.Add(Item("&Copy", Keys.Control | Keys.C, delegate { editor.Copy(); }));
        edit.DropDownItems.Add(Item("&Paste", Keys.Control | Keys.V, delegate { editor.Paste(); }));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Item("Select &All", Keys.Control | Keys.A, delegate { editor.SelectAll(); }));
        edit.DropDownItems.Add(Item("Time/&Date", Keys.F5,
            delegate { editor.SelectedText = DateTime.Now.ToString("g"); }));

        ToolStripMenuItem markdown = new ToolStripMenuItem("&Markdown");
        previewItem = Item("Live &Preview", Keys.F9, delegate { EnablePreview(!previewItem.Checked); });
        markdown.DropDownItems.Add(previewItem);

        ToolStripMenuItem modeMenu = new ToolStripMenuItem("Preview &Mode");
        ToolStripMenuItem readOnlyMode = Item("&Rendered (read-only)", Keys.None,
            delegate { SetPreviewMode(PreviewMode.ReadOnly); });
        ToolStripMenuItem jumpMode = Item("Rendered, &click to jump to source", Keys.None,
            delegate { SetPreviewMode(PreviewMode.ClickToJump); });
        ToolStripMenuItem editMode = Item("Rendered, &editable (syncs back to Markdown)", Keys.None,
            delegate { SetPreviewMode(PreviewMode.Editable); });
        modeItems = new ToolStripMenuItem[] { readOnlyMode, jumpMode, editMode };
        readOnlyMode.Checked = true;
        modeMenu.DropDownItems.Add(readOnlyMode);
        modeMenu.DropDownItems.Add(jumpMode);
        modeMenu.DropDownItems.Add(editMode);
        markdown.DropDownItems.Add(modeMenu);

        markdown.DropDownItems.Add(new ToolStripSeparator());
        markdown.DropDownItems.Add(Item("&Bold", Keys.Control | Keys.B, delegate { Wrap("**", "**"); }));
        markdown.DropDownItems.Add(Item("&Italic", Keys.Control | Keys.I, delegate { Wrap("*", "*"); }));
        markdown.DropDownItems.Add(Item("Strike&through", Keys.None, delegate { Wrap("~~", "~~"); }));
        markdown.DropDownItems.Add(Item("Inline &Code", Keys.Control | Keys.OemQuestion, delegate { Wrap("`", "`"); }));
        markdown.DropDownItems.Add(Item("Code &Block", Keys.None, delegate { Wrap("```\r\n", "\r\n```"); }));
        markdown.DropDownItems.Add(new ToolStripSeparator());
        markdown.DropDownItems.Add(Item("Heading &1", Keys.Control | Keys.D1, delegate { PrefixLines("# ", false); }));
        markdown.DropDownItems.Add(Item("Heading &2", Keys.Control | Keys.D2, delegate { PrefixLines("## ", false); }));
        markdown.DropDownItems.Add(Item("Heading &3", Keys.Control | Keys.D3, delegate { PrefixLines("### ", false); }));
        markdown.DropDownItems.Add(new ToolStripSeparator());
        markdown.DropDownItems.Add(Item("Bulle&ted List", Keys.None, delegate { PrefixLines("- ", false); }));
        markdown.DropDownItems.Add(Item("&Numbered List", Keys.None, delegate { PrefixLines(null, true); }));
        markdown.DropDownItems.Add(Item("Bloc&kquote", Keys.None, delegate { PrefixLines("> ", false); }));
        markdown.DropDownItems.Add(new ToolStripSeparator());
        markdown.DropDownItems.Add(Item("&Link...", Keys.Control | Keys.K, delegate { InsertLink(); }));
        markdown.DropDownItems.Add(Item("&Table", Keys.None, delegate
        {
            editor.SelectedText = "| Column A | Column B |\r\n| --- | --- |\r\n| value | value |\r\n";
        }));
        markdown.DropDownItems.Add(Item("&Horizontal Rule", Keys.None,
            delegate { editor.SelectedText = "\r\n---\r\n"; }));

        ToolStripMenuItem format = new ToolStripMenuItem("F&ormat");
        wordWrapItem = Item("&Word Wrap", Keys.None, delegate { ToggleWordWrap(); });
        wordWrapItem.Checked = Theme.WordWrap;
        format.DropDownItems.Add(wordWrapItem);
        format.DropDownItems.Add(Item("&Font...", Keys.None, delegate { ChooseFont(); }));
        format.DropDownItems.Add(new ToolStripSeparator());
        format.DropDownItems.Add(Item("&Appearance...", Keys.None, delegate { ShowAppearance(); }));
        format.DropDownItems.Add(new ToolStripSeparator());
        portableItem = Item("Portable &Settings...", Keys.None, delegate { TogglePortable(); });
        format.DropDownItems.Add(portableItem);
        format.DropDownItems.Add(new ToolStripSeparator());
        format.DropDownItems.Add(Item("Zoom &In", Keys.Control | Keys.Oemplus,
            delegate { ChangeZoom(10); }));
        format.DropDownItems.Add(Item("Zoom &Out", Keys.Control | Keys.OemMinus,
            delegate { ChangeZoom(-10); }));
        format.DropDownItems.Add(Item("&Reset Zoom", Keys.Control | Keys.D0,
            delegate { Theme.SetZoom(100); ApplyTheme(); }));

        // No drop-down: clicking opens the dialog, and Bunnies lives inside it as a tab.
        ToolStripMenuItem about = new ToolStripMenuItem("&About");
        about.Click += delegate { ShowAbout(false); };

        menu.Items.Add(file);
        menu.Items.Add(edit);
        menu.Items.Add(markdown);
        menu.Items.Add(format);
        menu.Items.Add(about);
        return menu;
    }

    private ToolStripMenuItem Item(string text, Keys shortcut, EventHandler handler)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(text);
        if (shortcut != Keys.None)
            item.ShortcutKeys = shortcut;
        item.Click += handler;
        return item;
    }

    // ---- file operations ----

    // New opens a tab rather than replacing what is in front of you, which is
    // the whole point of having tabs.
    private void NewFile()
    {
        AddDocument(NewDocument());
    }

    // Every common dialog here is in a `using`. They are IDisposable components
    // holding native resources, and none of them used to be disposed.
    private void OpenFile()
    {
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Filter = FileFilter;
            dialog.Multiselect = true;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            foreach (string path in dialog.FileNames)
                LoadFile(path);
        }
    }

    private void LoadFile(string path)
    {
        // Already open? Show that tab instead of opening a second view of one
        // file, which would let two copies drift apart and overwrite each other.
        for (int index = 0; index < documents.Count; index++)
        {
            if (documents[index].IsSameFile(path))
            {
                SelectDocument(index);
                return;
            }
        }

        string content;
        Encoding detected;
        try
        {
            // ⚠ THE ENCODING MUST NOT BE GUESSED AS UTF-8 AND FORGIVEN.
            //
            // This used to read through a NON-throwing UTF8Encoding, which turns
            // every byte it cannot understand into U+FFFD instead of failing. Open
            // a Windows-1252 file holding a smart quote or a pound sign, save it,
            // and the replacement character is what got written - the original
            // byte was gone, with no warning and no way back. Silent data loss in
            // a text editor is the worst bug it can have.
            //
            // Strict UTF-8 turns that guess into a question the file can answer.
            using (StreamReader reader = new StreamReader(path, new UTF8Encoding(false, true), true))
            {
                content = reader.ReadToEnd();
                detected = reader.CurrentEncoding;
            }
        }
        catch (DecoderFallbackException)
        {
            // Not UTF-8, so treat it as the system's ANSI code page: every byte
            // maps to a character and back, so saving reproduces the original
            // bytes rather than a lossy approximation of them. This is also what
            // Notepad does with a file that carries no BOM and is not valid UTF-8.
            try
            {
                detected = Encoding.Default;
                content = File.ReadAllText(path, detected);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the file:\r\n\r\n" + ex.Message,
                    "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open the file:\r\n\r\n" + ex.Message,
                "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // An untouched empty tab is reused rather than left behind as clutter.
        Document target = current != null && current.IsUnused ? current : null;
        if (target == null)
        {
            target = NewDocument();
            AddDocument(target);
        }

        // A WinForms TextBox only renders CRLF reliably, so normalise for display
        // and remember the original convention so saving does not rewrite every line.
        target.UnixLineEndings = content.IndexOf("\r\n") < 0 && content.IndexOf('\n') >= 0;
        content = content.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");

        target.Editor.Text = content;
        target.Editor.SelectionStart = 0;
        target.Editor.SelectionLength = 0;
        target.Path = path;
        target.Encoding = detected;
        target.Dirty = false;

        // Turning the preview on before selecting, so the document is rendered
        // once by SelectDocument rather than twice from here.
        if (IsMarkdownPath(path) && !previewItem.Checked)
            EnablePreview(true);

        SelectDocument(documents.IndexOf(target));
    }

    private bool Save()
    {
        if (currentPath == null)
            return SaveAs();
        return WriteFile(currentPath);
    }

    private bool SaveAs()
    {
        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Filter = FileFilter;
            dialog.DefaultExt = "txt";
            if (currentPath != null)
            {
                dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                dialog.FileName = Path.GetFileName(currentPath);
                if (IsMarkdownPath(currentPath)) dialog.FilterIndex = 2;
            }
            else
            {
                dialog.FileName = "Untitled.txt";
            }
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;
            return WriteFile(dialog.FileName);
        }
    }

    private void SaveAll()
    {
        Document wasCurrent = current;
        foreach (Document document in new List<Document>(documents))
        {
            if (!document.Dirty) continue;
            int index = documents.IndexOf(document);
            if (index >= 0) SelectDocument(index);
            if (!Save()) break;      // the user cancelled a Save As; stop asking
        }
        int back = documents.IndexOf(wasCurrent);
        if (back >= 0) SelectDocument(back);
    }

    private void CloseOtherTabs()
    {
        foreach (Document document in new List<Document>(documents))
        {
            if (document == current) continue;
            int index = documents.IndexOf(document);
            if (index >= 0 && !CloseDocument(index)) return;   // cancelled
        }
    }

    // The tab other than this one that already has this file open, if any.
    //
    // Two tabs claiming one file is the same hazard that opening guards
    // against: each holds a different version, and whichever saves last wins
    // silently. Opening is guarded by switching to the existing tab, but Save
    // As can arrive at the same place from the other direction.
    private Document OtherTabHolding(string path)
    {
        foreach (Document document in documents)
            if (document != current && document.IsSameFile(path)) return document;
        return null;
    }

    private bool WriteFile(string path)
    {
        Document clash = OtherTabHolding(path);
        if (clash != null)
        {
            MessageBox.Show(this,
                Path.GetFileName(path) + " is already open in another tab.\r\n\r\n"
                + "Close that tab first, or save this one under a different name.",
                "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            string content = editor.Text;
            if (unixLineEndings)
                content = content.Replace("\r\n", "\n");
            File.WriteAllText(path, content, currentEncoding);
            currentPath = path;
            dirty = false;
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save the file:\r\n\r\n" + ex.Message,
                "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void ExportHtml()
    {
        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*";
            dialog.DefaultExt = "html";
            dialog.FileName = (currentPath == null ? "Untitled" : Path.GetFileNameWithoutExtension(currentPath)) + ".html";
            if (currentPath != null)
                dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                File.WriteAllText(dialog.FileName, Markdown.ToHtml(editor.Text, Theme.PreviewCss()), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not export:\r\n\r\n" + ex.Message,
                    "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // Returns false when the user cancels out of an action that would lose changes.
    private bool ConfirmDiscard(Document document)
    {
        if (!document.Dirty) return true;

        // Show the document being asked about, so "Save changes to X?" is not a
        // question about something you cannot see.
        if (document != current)
        {
            int index = documents.IndexOf(document);
            if (index >= 0) SelectDocument(index);
        }

        DialogResult answer = MessageBox.Show(this,
            "Save changes to " + document.Name + "?", "MiniPad",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel) return false;
        if (answer == DialogResult.No) return true;
        return Save();
    }

    private static bool IsMarkdownPath(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".md" || ext == ".markdown" || ext == ".mdown" || ext == ".mkd";
    }

    // ---- markdown editing helpers ----

    private void Wrap(string prefix, string suffix)
    {
        int start = editor.SelectionStart;
        int length = editor.SelectionLength;
        editor.SelectedText = prefix + editor.SelectedText + suffix;
        editor.SelectionStart = start + prefix.Length;
        editor.SelectionLength = length;
        editor.Focus();
    }

    // Applies a line marker to every line touched by the selection.
    private void PrefixLines(string marker, bool numbered)
    {
        int start = editor.SelectionStart;
        int length = editor.SelectionLength;
        int firstLine = LogicalLineFromCharIndex(start);
        int lastLine = LogicalLineFromCharIndex(start + (length > 0 ? length - 1 : 0));

        string text = editor.Text;
        int blockStart = CharIndexFromLogicalLine(firstLine);
        int blockEnd = CharIndexFromLogicalLine(lastLine + 1);

        // Drop the newline that ends the last line so it is not part of the selection.
        while (blockEnd > blockStart && (text[blockEnd - 1] == '\n' || text[blockEnd - 1] == '\r'))
            blockEnd--;

        editor.SelectionStart = blockStart;
        editor.SelectionLength = blockEnd - blockStart;

        string[] lines = editor.SelectedText.Replace("\r\n", "\n").Split('\n');
        StringBuilder result = new StringBuilder();
        for (int n = 0; n < lines.Length; n++)
        {
            if (n > 0) result.Append("\r\n");
            result.Append(numbered ? (n + 1) + ". " : marker).Append(lines[n]);
        }

        editor.SelectedText = result.ToString();
        editor.SelectionStart = blockStart;
        editor.SelectionLength = result.Length;
        editor.Focus();
    }

    private void InsertLink()
    {
        string selection = editor.SelectedText;
        int start = editor.SelectionStart;

        if (selection.Length == 0)
        {
            editor.SelectedText = "[text](url)";
            editor.SelectionStart = start + 1;
            editor.SelectionLength = 4;
        }
        else if (selection.StartsWith("http://") || selection.StartsWith("https://") || selection.StartsWith("www."))
        {
            editor.SelectedText = "[](" + selection + ")";
            editor.SelectionStart = start + 1;
            editor.SelectionLength = 0;
        }
        else
        {
            editor.SelectedText = "[" + selection + "](url)";
            editor.SelectionStart = start + selection.Length + 3;
            editor.SelectionLength = 3;
        }
        editor.Focus();
    }

    // ---- preview ----

    private void EnablePreview(bool on)
    {
        EnablePreview(on, true);
    }

    // `remember` is false only when restoring at startup: writing the setting
    // back on the way in would rewrite settings.txt every launch to say what it
    // already said.
    private void EnablePreview(bool on, bool remember)
    {
        previewItem.Checked = on;
        split.Panel2Collapsed = !on;
        if (on)
        {
            if (split.Width > 0) split.SplitterDistance = split.Width / 2;
            RenderPreview();
        }
        if (remember) Theme.SetPreviewOn(on);
        UpdateModeLabel();
    }

    private void RenderPreview()
    {
        // A debounced render is now redundant, whatever asked for this one.
        previewTimer.Stop();

        if (!previewItem.Checked || !IsHandleCreated) return;

        // Setting DocumentText starts a navigation that only finishes when the
        // message loop runs. Starting a second one before the first has
        // finished loses both, and the pane stays blank until something else
        // renders it - which is easy to reach, since selecting a tab renders
        // and several operations select more than one tab in a row. So a render
        // arriving mid-flight is remembered and run once the current one lands.
        if (previewNavigating)
        {
            previewNeedsRender = true;
            return;
        }

        try
        {
            if (preview.Document != null && preview.Document.Body != null)
                previewScroll = preview.Document.Body.ScrollTop;
        }
        catch { }
        returnFocusToEditor = editor.Focused;
        try
        {
            previewNavigating = true;
            // Remember whose document this is, so edits made in the pane go back
            // to the right one even if the tab changes before they are applied.
            previewDocument = current;
            preview.DocumentText = Markdown.ToHtml(editor.Text, Theme.PreviewCss());
        }
        catch
        {
            // Never leave the flag set on a failure, or the pane would never
            // render again for the rest of the session.
            previewNavigating = false;
        }
    }

    private void OnPreviewCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
    {
        previewNavigating = false;
        try
        {
            if (preview.Document != null && preview.Document.Body != null)
                preview.Document.Body.ScrollTop = previewScroll;
        }
        catch { }
        ApplyPreviewMode();
        if (returnFocusToEditor)
        {
            returnFocusToEditor = false;
            editor.Focus();
        }

        if (previewNeedsRender)
        {
            previewNeedsRender = false;
            RenderPreview();
        }
    }

    // ---- preview modes ----

    private void SetPreviewMode(PreviewMode mode)
    {
        previewMode = mode;
        Theme.SetPreviewMode((int)mode);
        foreach (ToolStripMenuItem item in modeItems)
            item.Checked = false;
        modeItems[(int)mode].Checked = true;

        // Choosing a mode implies you want to see the pane.
        if (!previewItem.Checked)
            EnablePreview(true);
        else
            RenderPreview();

        UpdateModeLabel();
    }

    private void UpdateModeLabel()
    {
        if (!previewItem.Checked)
        {
            modeLabel.Text = "Preview: off";
            return;
        }
        if (previewMode == PreviewMode.ClickToJump)
            modeLabel.Text = "Preview: click to jump";
        else if (previewMode == PreviewMode.Editable)
            modeLabel.Text = "Preview: editable";
        else
            modeLabel.Text = "Preview: read-only";
        UpdatePreviewCaption();
    }

    // The caption says what the pane IS, and then what it is currently DOING.
    // The second half matters: in editable mode the pane accepts typing, and a
    // user who does not know that will be surprised by it either way.
    private void UpdatePreviewCaption()
    {
        if (previewCaption == null) return;
        if (previewMode == PreviewMode.ClickToJump)
            previewCaption.Text = "Live Preview  -  click any block to jump to its source";
        else if (previewMode == PreviewMode.Editable)
            previewCaption.Text = "Live Preview  -  editable, your changes go back to the text";
        else
            previewCaption.Text = "Live Preview  -  updates as you type on the left";
    }

    // Re-applied after every render, because each render builds a new document.
    private void ApplyPreviewMode()
    {
        try
        {
            HtmlDocument document = preview.Document;
            if (document == null || document.Body == null) return;

            bool editable = previewMode == PreviewMode.Editable;
            document.Body.SetAttribute("contentEditable", editable ? "true" : "false");

            if (editable)
            {
                document.Body.AttachEventHandler("onkeyup", OnPreviewEdited);
                document.Body.AttachEventHandler("onpaste", OnPreviewEdited);
                document.Body.AttachEventHandler("oncut", OnPreviewEdited);
                document.Body.AttachEventHandler("ondragend", OnPreviewEdited);
            }

            document.Click -= OnPreviewClick;
            document.Click += OnPreviewClick;
        }
        catch { }
    }

    private void OnPreviewEdited(object sender, EventArgs e)
    {
        if (previewMode != PreviewMode.Editable) return;
        syncTimer.Stop();
        syncTimer.Start();
    }

    // Click-to-jump: find the nearest element carrying a source line number.
    private void OnPreviewClick(object sender, HtmlElementEventArgs e)
    {
        if (previewMode != PreviewMode.ClickToJump) return;
        try
        {
            HtmlElement element = preview.Document.GetElementFromPoint(e.ClientMousePosition);
            while (element != null)
            {
                string value = element.GetAttribute("data-line");
                int line;
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out line))
                {
                    JumpToLine(line);
                    return;
                }
                element = element.Parent;
            }
        }
        catch { }
    }

    private void JumpToLine(int line)
    {
        if (line < 0) return;

        // Markdown gives us a logical source line, so map it without wrapping.
        int index = CharIndexFromLogicalLine(line);
        if (index < 0 || index > editor.TextLength) return;

        editor.SelectionStart = index;
        editor.SelectionLength = 0;
        editor.ScrollToCaret();
        editor.Focus();
        UpdatePosition();
    }

    // Editable mode: take the edited HTML back to Markdown and update the editor.
    private void PullFromPreview()
    {
        if (previewMode != PreviewMode.Editable) return;

        // The pane is shared between tabs, and rendering it is asynchronous, so
        // by the time this runs the tab in front may not be the one the pane is
        // showing. The edit belongs to the document it was rendered from.
        Document target = previewDocument;
        if (target == null || !documents.Contains(target)) return;

        string html;
        try
        {
            if (preview.Document == null || preview.Document.Body == null) return;
            html = preview.Document.Body.InnerHtml;
        }
        catch { return; }

        if (html == null) return;

        // The converter is a tolerant hand-written parser fed markup that MSHTML
        // rewrote as the user typed. Everything else risky in this file is
        // guarded; this was not, and an exception here would take the whole editor
        // down mid-sentence. Abandoning one sync is harmless - the pane still
        // holds the edit, and the next keystroke tries again.
        string markdown;
        try
        {
            markdown = HtmlToMarkdown.Convert(html).Replace("\r\n", "\n");
        }
        catch
        {
            return;
        }
        string existing = target.Editor.Text.Replace("\r\n", "\n");
        if (markdown.TrimEnd() == existing.TrimEnd()) return;

        int caret = target.Editor.SelectionStart;
        syncingFromPreview = true;
        try
        {
            target.Editor.Text = markdown.Replace("\n", "\r\n");
            target.Editor.SelectionStart = Math.Min(caret, target.Editor.TextLength);
            target.Editor.SelectionLength = 0;
        }
        finally
        {
            syncingFromPreview = false;
        }

        target.Dirty = true;
        UpdateTitle();
    }

    // Links in the preview open outside rather than replacing the pane. Only the
    // initial about:blank is allowed through: the document is loaded from a string,
    // so a relative link resolves to about:<something> and navigating there would
    // wipe the preview.
    private void OnPreviewNavigating(object sender, WebBrowserNavigatingEventArgs e)
    {
        string url = e.Url == null ? "" : e.Url.ToString();
        if (url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            // A relative link. Resolve it next to the file being edited.
            string relative = url.Substring("about:".Length).TrimStart('/');
            if (relative.Length == 0 || currentPath == null) return;
            try
            {
                string target = Path.Combine(Path.GetDirectoryName(currentPath),
                                             Uri.UnescapeDataString(relative));
                if (File.Exists(target)) Process.Start(target);
            }
            catch { }
            return;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return; // never hand an unknown scheme to the shell

        try { Process.Start(url); }
        catch { }
    }

    // ---- view helpers ----

    // Word wrap, the font and the theme are settings of the editor, not of one
    // document, so they apply to every tab at once.
    private void ToggleWordWrap()
    {
        bool wrap = !wordWrapItem.Checked;
        wordWrapItem.Checked = wrap;
        foreach (Document document in documents)
        {
            document.Editor.WordWrap = wrap;
            document.Editor.ScrollBars = wrap ? ScrollBars.Vertical : ScrollBars.Both;
        }
        Theme.SetWordWrap(wrap);
    }

    // Portable settings: on or off, with the consequences spelled out before
    // anything happens. Switching off deletes a file, so it asks first - and it
    // copies the settings back to the profile before deleting, so no choice is lost.
    private void TogglePortable()
    {
        if (!Theme.IsPortable)
        {
            string question =
                "Keep MiniPad's settings in a file next to the program itself, rather "
              + "than in your user profile?\r\n\r\n"
              + "This is for running MiniPad from a USB stick, or on a machine where "
              + "you would rather not leave anything behind.\r\n\r\n"
              + "The file would be:\r\n" + Theme.PortableSettingsPath;
            if (MessageBox.Show(this, question, "MiniPad - Portable Settings",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string failure = Theme.MakePortable();
            if (failure != null)
            {
                MessageBox.Show(this,
                    "Portable settings could not be turned on.\r\n\r\n" + failure
                  + "\r\n\r\nThis usually means the program is in a folder you cannot "
                  + "write to, such as Program Files. Your settings are unchanged and "
                  + "are still being saved to your user profile.",
                    "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show(this,
                "Portable settings are on.\r\n\r\nSettings are now saved to:\r\n"
              + Theme.SettingsPath + "\r\n\r\nMove the program and that file together and "
              + "your preferences travel with it. Deleting the file returns MiniPad to "
              + "using your user profile.",
                "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            if (MessageBox.Show(this,
                    "Turn portable settings off?\r\n\r\nYour current settings will be copied "
                  + "back to your user profile first, and then this file will be deleted:\r\n"
                  + Theme.PortableSettingsPath,
                    "MiniPad - Portable Settings",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string failure = Theme.MakeNotPortable();
            if (failure != null)
            {
                MessageBox.Show(this, "The file could not be removed.\r\n\r\n" + failure,
                    "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show(this,
                "Portable settings are off. Settings are saved to:\r\n" + Theme.SettingsPath,
                "MiniPad", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        UpdatePortableItem();
    }

    private void UpdatePortableItem()
    {
        if (portableItem == null) return;
        portableItem.Checked = Theme.IsPortable;
    }

    // ---- appearance ----

    private void ShowAppearance()
    {
        using (AppearanceForm dialog = new AppearanceForm())
        {
            dialog.ThemeChanged += delegate { ApplyTheme(); };
            Theme.Apply(dialog);
            dialog.ShowDialog(this);
        }
    }

    private void ApplyTheme()
    {
        Theme.Apply(this);

        // The editors and the status bar need saying explicitly: the generic walk
        // cannot know which TextBox is a document.
        foreach (Document document in documents)
            StyleEditor(document.Editor);
        editorHost.BackColor = Theme.EditorBack;
        split.BackColor = Theme.WindowBack;
        tabs.Height = tabs.PreferredHeight;
        tabs.Invalidate();          // the strip paints itself straight from Theme
        positionLabel.ForeColor = Theme.UiDim;
        modeLabel.ForeColor = Theme.UiDim;
        previewCaption.BackColor = Theme.WindowBack;
        previewCaption.ForeColor = Theme.UiDim;
        editorCaption.BackColor = Theme.WindowBack;
        editorCaption.ForeColor = Theme.UiDim;
        UpdateZoomLabel();

        RenderPreview();   // the preview stylesheet changes with the theme
    }

    private void UpdateZoomLabel()
    {
        zoomLabel.Text = Theme.Zoom == 100 ? "100%" : Theme.Zoom + "%";
        zoomLabel.ForeColor = Theme.Zoom == 100 ? Theme.UiDim : Theme.UiAccent;
    }

    private void OnEditorMouseWheel(object sender, MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) != Keys.Control) return;

        // Mark it handled, or the text box scrolls as well as zooming.
        HandledMouseEventArgs handled = e as HandledMouseEventArgs;
        if (handled != null) handled.Handled = true;

        ChangeZoom(e.Delta > 0 ? 10 : -10);
    }

    private void ChangeZoom(int delta)
    {
        Theme.SetZoom(Theme.Zoom + delta);
        ApplyTheme();
    }

    private void ChooseFont()
    {
        using (FontDialog dialog = new FontDialog())
        {
            using (Font seed = new Font(baseFontFamily, baseFontSize))
                dialog.Font = seed;
            dialog.ShowEffects = false;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            // Store it unzoomed so zooming stays a multiplier rather than compounding.
            baseFontFamily = dialog.Font.FontFamily.Name;
            baseFontSize = dialog.Font.Size;
        }
        ApplyEditorFont();
        Theme.SetFont(baseFontFamily, baseFontSize);
    }

    // Gives every open editor the current base font at the current zoom.
    //
    // ⚠ EACH EDITOR GETS ITS OWN Font INSTANCE, and that is the whole point of this
    // method. Handing one shared instance to every editor looks harmless and is not:
    // StyleEditor disposes whatever font it finds on a control, so the first editor
    // restyled would free a Font the others still reference. They then throw
    // ArgumentException("Parameter is not valid") from Font.ToHfont() on the next
    // repaint - far from the actual mistake, and only ever with two or more tabs open.
    //
    // Split out from ChooseFont so it can be tested without driving a modal FontDialog.
    private void ApplyEditorFont()
    {
        float scaled = Theme.ScaleFont(baseFontSize);
        foreach (Document document in documents)
            ReplaceFont(document.Editor, new Font(baseFontFamily, scaled));
    }

    // A Font handed to a control is not disposed by that control, so replacing one
    // leaks the old handle. StyleEditor runs on every theme change and every zoom
    // step, so this was a Font per document per keystroke of Ctrl+Plus.
    //
    // ⚠ Never dispose the ambient/default font - it is shared and still in use
    // elsewhere. Only fonts this class created are ours to free.
    private static void DisposeIfOwned(Font font)
    {
        if (font == null) return;
        // A control that has never had a Font set returns its parent's ambient font,
        // which is this shared static. Freeing it would break every control at once.
        //
        // ⚠ Do not add `|| font == SystemFonts.DefaultFont` here - it reads like a
        // second safety net and is not one. That property builds a NEW Font on every
        // access, so the reference comparison can never be true. It was in this guard
        // until 1.11.1 doing precisely nothing.
        if (font == DefaultFont) return;
        try { font.Dispose(); }
        catch { }
    }

    private static bool FontFamilyExists(string name)
    {
        try
        {
            using (FontFamily family = new FontFamily(name)) { return family != null; }
        }
        catch { return false; }     // FontFamily throws when the name is unknown
    }

    // Only restore a position that still lands on a screen that exists. Unplugging
    // a second monitor would otherwise reopen the window at coordinates nobody can
    // reach, which looks exactly like the program failing to start.
    private void RestoreWindowPosition()
    {
        Rectangle saved = Theme.WindowBounds;
        if (!saved.IsEmpty)
        {
            bool onScreen = false;
            foreach (Screen screen in Screen.AllScreens)
            {
                // Intersects, not Contains: a window nudged partly off the edge is
                // still perfectly usable and should not be thrown away.
                if (screen.WorkingArea.IntersectsWith(saved)) { onScreen = true; break; }
            }
            if (onScreen)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = saved;
            }
        }
        if (Theme.WindowMaximised) WindowState = FormWindowState.Maximized;
    }

    private void SaveWindowPosition()
    {
        // RestoreBounds rather than Bounds, so a maximised window remembers the size
        // it will return to instead of the size of the screen.
        Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (bounds.Width > 200 && bounds.Height > 150)
            Theme.SetWindow(bounds, WindowState == FormWindowState.Maximized);
    }

    private void UpdateTitle()
    {
        Text = current.TabText + " - MiniPad";
        RefreshTabs();      // the tab carries the same name and the same asterisk
    }

    // TextBox's own GetLineFromCharIndex / GetFirstCharIndexFromLine count WRAPPED
    // display rows, not lines of text, so with word wrap on they disagree with the
    // Lines array and with Markdown source line numbers. These work in logical lines.
    private int LogicalLineFromCharIndex(int index)
    {
        string text = editor.Text;
        if (index > text.Length) index = text.Length;
        int line = 0;
        for (int n = 0; n < index; n++)
            if (text[n] == '\n') line++;
        return line;
    }

    private int CharIndexFromLogicalLine(int line)
    {
        if (line <= 0) return 0;
        string text = editor.Text;
        int seen = 0;
        for (int n = 0; n < text.Length; n++)
        {
            if (text[n] != '\n') continue;
            if (++seen == line) return n + 1;
        }
        return text.Length;
    }

    private void UpdatePosition()
    {
        int line = LogicalLineFromCharIndex(editor.SelectionStart);
        int column = editor.SelectionStart - CharIndexFromLogicalLine(line);
        positionLabel.Text = string.Format("Ln {0}, Col {1}", line + 1, column + 1);
    }

    // ---- events ----

    // ---- bunnies ----

    private const string BunnyCommand = "!bunnies";

    private void ShowAbout(bool onBunnies)
    {
        using (AboutBox box = new AboutBox(onBunnies))
            box.ShowDialog(this);
    }

    private void ShowHelp()
    {
        using (AboutBox box = new AboutBox())
        {
            box.Shown += delegate { box.ShowHelp(); };
            box.ShowDialog(this);
        }
    }

    // Ctrl+Shift+B has no menu entry of its own - Bunnies is a tab, not a command.
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Shift | Keys.B))
        {
            ShowAbout(true);
            return true;
        }
        if (keyData == Keys.F1)
        {
            ShowHelp();
            return true;
        }

        // Moving between tabs. Ctrl+1..9 is not used for this because it already
        // means Heading 1/2/3 here, so the numbered jumps take Ctrl+Alt instead.
        if (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.PageDown))
        {
            SelectRelativeTab(1);
            return true;
        }
        if (keyData == (Keys.Control | Keys.Shift | Keys.Tab)
            || keyData == (Keys.Control | Keys.PageUp))
        {
            SelectRelativeTab(-1);
            return true;
        }
        for (int n = 1; n <= 9; n++)
        {
            if (keyData != (Keys.Control | Keys.Alt | (Keys.D0 + n))) continue;
            // Ctrl+Alt+9 goes to the last tab however many there are, as browsers do.
            SelectDocument(n == 9 ? documents.Count - 1 : Math.Min(n - 1, documents.Count - 1));
            return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    // Typing the command in the document opens the Bunnies tab. The text is left
    // exactly as typed - an easter egg should not edit someone's file.
    private void CheckForBunnyCommand()
    {
        int caret = editor.SelectionStart;
        if (caret < BunnyCommand.Length) return;

        string typed = editor.Text.Substring(caret - BunnyCommand.Length, BunnyCommand.Length);
        if (!typed.Equals(BunnyCommand, StringComparison.OrdinalIgnoreCase)) return;

        // Only fire when the command stands on its own, not inside a longer word.
        int before = caret - BunnyCommand.Length - 1;
        if (before >= 0)
        {
            char previous = editor.Text[before];
            if (char.IsLetterOrDigit(previous) || previous == '_') return;
        }

        ShowAbout(true);
    }

    private void OnTextChanged(object sender, EventArgs e)
    {
        // Which document changed comes from the sender, not from whichever tab
        // is in front: a preview edit can be applied to a tab that is not the
        // one on screen, and marking the wrong one unsaved would be a lie.
        Document changed = DocumentFor(sender as TextBox);
        if (changed == null) return;

        // Only redraw the title and the tab strip when the asterisk actually
        // appears, rather than repainting the whole strip on every keystroke.
        bool wasDirty = changed.Dirty;
        changed.Dirty = true;
        if (!wasDirty) UpdateTitle();
        if (changed != current) return;     // a background tab needs no caret work
        UpdatePosition();

        // The text came from the preview pane, so re-rendering would throw away
        // the caret the user is typing at.
        if (syncingFromPreview) return;


        if (previewItem.Checked)
        {
            previewTimer.Stop();
            previewTimer.Start();
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    // Every dropped file gets its own tab.
    private void OnDragDrop(object sender, DragEventArgs e)
    {
        string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (paths == null) return;
        foreach (string path in paths)
            LoadFile(path);
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        // Ask about each unsaved document in turn; cancelling any one of them
        // abandons the whole close, so nothing already answered for is lost.
        foreach (Document document in new List<Document>(documents))
        {
            if (ConfirmDiscard(document)) continue;
            e.Cancel = true;
            return;
        }

        // Only once the close is certain. Saving geometry before the unsaved-changes
        // prompt would record a position for a window the user then decided to keep.
        SaveWindowPosition();
    }

    [STAThread]
    public static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(args));
    }
}
