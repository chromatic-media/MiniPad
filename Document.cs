// One open document. Each one owns its own editor control rather than sharing a
// single box between tabs: a TextBox keeps its undo history, caret and scroll
// position internally, and none of that can be extracted and put back, so
// swapping text in and out of one shared box would quietly lose all three every
// time you changed tab.

using System;
using System.Text;
using System.Windows.Forms;

public class Document
{
    public readonly TextBox Editor;

    public string Path;                 // null until it has been saved somewhere
    public bool Dirty;
    public Encoding Encoding = new UTF8Encoding(false);
    public bool UnixLineEndings;
    public int UntitledNumber;          // only used while Path is null

    public Document(int untitledNumber)
    {
        UntitledNumber = untitledNumber;

        Editor = new TextBox();
        Editor.Multiline = true;
        Editor.AcceptsTab = true;
        Editor.WordWrap = true;
        Editor.ScrollBars = ScrollBars.Vertical;
        Editor.Dock = DockStyle.Fill;
        Editor.BorderStyle = BorderStyle.None;
        Editor.MaxLength = 0;           // no practical limit
        Editor.Tag = this;
    }

    // What the tab and the title bar call this document.
    public string Name
    {
        get
        {
            if (Path != null) return System.IO.Path.GetFileName(Path);
            return UntitledNumber <= 1 ? "Untitled" : "Untitled " + UntitledNumber;
        }
    }

    public string TabText { get { return (Dirty ? "*" : "") + Name; } }

    // True for a document nobody has touched: opening a file replaces one of
    // these rather than leaving an empty tab behind, which is what Notepad does.
    public bool IsUnused
    {
        get { return Path == null && !Dirty && Editor.TextLength == 0; }
    }

    public bool IsSameFile(string path)
    {
        if (Path == null || path == null) return false;
        try
        {
            return string.Equals(System.IO.Path.GetFullPath(Path),
                                 System.IO.Path.GetFullPath(path),
                                 StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(Path, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
