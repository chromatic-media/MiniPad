// A small, dependency-free Markdown to HTML converter for MiniPad.
// Supports: ATX headings, fenced code, blockquotes, pipe tables, ordered and
// unordered lists (one level of nesting), horizontal rules, paragraphs, and the
// usual inline spans (bold, italic, strike, code, links, images).

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public static class Markdown
{
    // The fallback stylesheet, used only when a caller passes no stylesheet of its
    // own. Nothing in MiniPad does - the editor and the HTML export both hand over
    // Theme.PreviewCss(), generated from the palette in force - so this exists to
    // keep ToHtml usable on its own rather than to style anything you will see.
    //
    // ⚠ A DARK TWIN OF THIS AND TWO ToHtml OVERLOADS WERE REMOVED. They were
    // unreachable: the only live path is ToHtml(markdown, css). The dark copy had
    // drifted to carry zebra-striped table rows that the real generated stylesheet
    // does not, so the dead code described a program that no longer existed - which
    // is worse than no code, because it reads like documentation.
    private const string Css =
        "body{font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:15px;line-height:1.6;" +
        "color:#24292f;background:#ffffff;margin:0;padding:24px 28px;}" +
        "h1,h2,h3,h4,h5,h6{margin:24px 0 12px;line-height:1.25;font-weight:600;}" +
        "h1{font-size:1.9em;border-bottom:1px solid #d8dee4;padding-bottom:8px;}" +
        "h2{font-size:1.45em;border-bottom:1px solid #d8dee4;padding-bottom:6px;}" +
        "h3{font-size:1.2em;} h4{font-size:1em;} h5,h6{font-size:.9em;color:#57606a;}" +
        "p{margin:0 0 14px;} ul,ol{margin:0 0 14px;padding-left:26px;} li{margin:3px 0;}" +
        "code{font-family:Consolas,monospace;font-size:.9em;background:#eff1f3;" +
        "padding:2px 5px;border-radius:4px;}" +
        "pre{background:#f6f8fa;border:1px solid #d8dee4;border-radius:6px;padding:12px 14px;" +
        "overflow:auto;} pre code{background:none;padding:0;font-size:.88em;}" +
        "blockquote{margin:0 0 14px;padding:2px 14px;color:#57606a;border-left:4px solid #d0d7de;}" +
        "table{border-collapse:collapse;margin:0 0 16px;}" +
        "th,td{border:1px solid #d0d7de;padding:6px 12px;}" +
        "th{background:#f6f8fa;font-weight:600;}" +
        "hr{border:0;border-top:1px solid #d8dee4;margin:22px 0;}" +
        "a{color:#0969da;} img{max-width:100%;} del{color:#82878d;}";

    // Used by the editor, which hands over a stylesheet generated from the palette.
    public static string ToHtml(string markdown, string css)
    {
        return Render(markdown, string.IsNullOrEmpty(css) ? Css : css);
    }

    private static string Render(string markdown, string css)
    {
        return "<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\" />" +
               "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />" +
               "<style>" + css + "</style></head><body>\n" +
               Blocks(markdown, 0) +
               "</body></html>";
    }

    // Every block carries the source line it came from, so the preview can map a
    // click (or an edit) back to the right place in the editor.
    private static string Anchor(int line)
    {
        return " data-line=\"" + line + "\"";
    }

    // The opening fence, and the ONE definition of it.
    //
    // Everything after the language is an "info string" - `title="a.js"`,
    // `{.python}`, `:line-numbers` - and it must be allowed, not just tolerated.
    // Requiring end-of-line straight after the language is what made
    // ```js title="a.js" match IsBlockStart but not this pattern, so the
    // paragraph loop consumed nothing, the "never stall" i++ threw the line away,
    // and the code inside was rendered as prose with its markdown interpreted.
    //
    // ⚠ IsBlockStart MUST test with this same regex. It used to carry its own
    // prefix-only copy, and that disagreement was the entire bug.
    // The trailing `[^`]*` is CommonMark's actual rule: the info string is
    // anything after the fence that contains no backtick. That covers
    // `js title="a.js"`, `{.python}`, `python {highlight}` and `js:line-numbers`
    // alike. The LANGUAGE is just the leading run of name characters, which may be
    // empty - `{.python}` names a language MiniPad has no highlighting for anyway,
    // so recognising the block matters and naming it does not.
    private static readonly Regex FenceStart =
        new Regex(@"^\s{0,3}(?:```+|~~~+)\s*([A-Za-z0-9_+#-]*)[^`]*$");

    private static readonly Regex FenceEnd =
        new Regex(@"^\s{0,3}(?:```+|~~~+)\s*$");

    // ---- block level ----

    private static string Blocks(string text, int lineOffset)
    {
        if (text == null) text = "";
        // A stray BOM would stop the first line matching any block pattern.
        if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        StringBuilder html = new StringBuilder();
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i];
            string anchor = Anchor(lineOffset + i);

            if (line.Trim().Length == 0) { i++; continue; }

            Match fence = FenceStart.Match(line);
            if (fence.Success)
            {
                string language = fence.Groups[1].Value;
                i++;
                StringBuilder code = new StringBuilder();
                while (i < lines.Length && !FenceEnd.IsMatch(lines[i]))
                {
                    code.Append(lines[i]).Append('\n');
                    i++;
                }
                if (i < lines.Length) i++; // closing fence
                html.Append("<pre").Append(anchor).Append("><code");
                if (language.Length > 0) html.Append(" class=\"lang-").Append(Escape(language)).Append('"');
                html.Append('>').Append(Escape(code.ToString())).Append("</code></pre>\n");
                continue;
            }

            Match heading = Regex.Match(line, @"^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$");
            if (heading.Success)
            {
                string level = heading.Groups[1].Value.Length.ToString();
                html.Append("<h").Append(level).Append(anchor).Append('>')
                    .Append(Inline(heading.Groups[2].Value))
                    .Append("</h").Append(level).Append(">\n");
                i++;
                continue;
            }

            if (IsHorizontalRule(line))
            {
                html.Append("<hr").Append(anchor).Append(" />\n");
                i++;
                continue;
            }

            if (Regex.IsMatch(line, @"^\s{0,3}>"))
            {
                int quoteStart = i;
                StringBuilder quoted = new StringBuilder();
                while (i < lines.Length && Regex.IsMatch(lines[i], @"^\s{0,3}>"))
                {
                    quoted.Append(Regex.Replace(lines[i], @"^\s{0,3}>\s?", "")).Append('\n');
                    i++;
                }
                html.Append("<blockquote").Append(anchor).Append(">\n")
                    .Append(Blocks(quoted.ToString(), lineOffset + quoteStart))
                    .Append("</blockquote>\n");
                continue;
            }

            if (line.IndexOf('|') >= 0 && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                html.Append(Table(lines, ref i, lineOffset));
                continue;
            }

            if (IsListItem(line))
            {
                html.Append(List(lines, ref i, Indent(line), lineOffset));
                continue;
            }

            StringBuilder paragraph = new StringBuilder();
            while (i < lines.Length && lines[i].Trim().Length > 0 && !IsBlockStart(lines, i))
            {
                if (paragraph.Length > 0) paragraph.Append('\n');
                paragraph.Append(lines[i].Trim());
                i++;
            }
            if (paragraph.Length > 0)
            {
                string body = Inline(paragraph.ToString()).Replace("\n", "<br />\n");
                html.Append("<p").Append(anchor).Append('>').Append(body).Append("</p>\n");
            }
            else
            {
                // Never stall - and never silently DISCARD the line either, which
                // is what a bare i++ did here. Reaching this point means
                // IsBlockStart claimed a block starts on this line but no block
                // parser above recognised it, so the two disagree. Emitting the
                // text is always better than losing it: a wrong-looking paragraph
                // is visible and reportable, a vanished line is neither.
                html.Append("<p").Append(anchor).Append('>')
                    .Append(Inline(line.Trim())).Append("</p>\n");
                i++;
            }
        }

        return html.ToString();
    }

    private static bool IsBlockStart(string[] lines, int i)
    {
        string line = lines[i];
        // The SAME test the fence parser uses, not a prefix-only lookalike.
        if (FenceStart.IsMatch(line) || FenceEnd.IsMatch(line)) return true;
        if (Regex.IsMatch(line, @"^\s{0,3}#{1,6}\s")) return true;
        if (Regex.IsMatch(line, @"^\s{0,3}>")) return true;
        if (IsHorizontalRule(line)) return true;
        if (IsListItem(line)) return true;
        if (line.IndexOf('|') >= 0 && i + 1 < lines.Length && IsTableSeparator(lines[i + 1])) return true;
        return false;
    }

    private static bool IsHorizontalRule(string line)
    {
        return Regex.IsMatch(line, @"^\s{0,3}(?:\*\s*){3,}$")
            || Regex.IsMatch(line, @"^\s{0,3}(?:-\s*){3,}$")
            || Regex.IsMatch(line, @"^\s{0,3}(?:_\s*){3,}$");
    }

    private static bool IsListItem(string line)
    {
        return Regex.IsMatch(line, @"^\s*(?:[-*+]|\d+[.)])\s+\S");
    }

    private static bool IsOrderedItem(string line)
    {
        return Regex.IsMatch(line.TrimStart(), @"^\d+[.)]\s");
    }

    // A tab counts as four columns, otherwise tab-indented nested lists never nest.
    private static int Indent(string line)
    {
        int columns = 0;
        for (int n = 0; n < line.Length; n++)
        {
            if (line[n] == ' ') columns++;
            else if (line[n] == '\t') columns += 4;
            else break;
        }
        return columns;
    }

    // ---- lists ----

    private static string List(string[] lines, ref int i, int baseIndent, int lineOffset)
    {
        bool ordered = IsOrderedItem(lines[i]);
        StringBuilder html = new StringBuilder();
        html.Append(ordered ? "<ol" : "<ul").Append(Anchor(lineOffset + i)).Append(">\n");

        while (i < lines.Length)
        {
            string line = lines[i];
            if (line.Trim().Length == 0)
            {
                // A blank line ends the list unless the next line continues the same kind of list.
                if (i + 1 < lines.Length && IsListItem(lines[i + 1])
                    && Indent(lines[i + 1]) >= baseIndent
                    && IsOrderedItem(lines[i + 1]) == ordered)
                {
                    i++;
                    continue;
                }
                break;
            }

            // Switching between bulleted and numbered markers starts a new list.
            if (!IsListItem(line) || Indent(line) < baseIndent || IsOrderedItem(line) != ordered) break;

            Match marker = Regex.Match(line, @"^\s*(?:[-*+]|\d+[.)])\s+(.*)$");
            StringBuilder item = new StringBuilder(marker.Groups[1].Value);
            int itemIndent = Indent(line);
            string itemAnchor = Anchor(lineOffset + i);
            i++;

            string nested = "";
            while (i < lines.Length && lines[i].Trim().Length > 0)
            {
                if (IsListItem(lines[i]))
                {
                    if (Indent(lines[i]) >= itemIndent + 2)
                        nested += List(lines, ref i, Indent(lines[i]), lineOffset);
                    else
                        break;
                }
                else if (Indent(lines[i]) >= itemIndent + 2)
                {
                    item.Append('\n').Append(lines[i].Trim()); // lazy continuation
                    i++;
                }
                else
                {
                    break;
                }
            }

            html.Append("<li").Append(itemAnchor).Append('>')
                .Append(Inline(item.ToString()).Replace("\n", "<br />\n"));
            if (nested.Length > 0) html.Append('\n').Append(nested);
            html.Append("</li>\n");
        }

        html.Append(ordered ? "</ol>\n" : "</ul>\n");
        return html.ToString();
    }

    // ---- tables ----

    private static bool IsTableSeparator(string line)
    {
        return Regex.IsMatch(line, @"^\s*\|?\s*:?-{1,}:?\s*(?:\|\s*:?-{1,}:?\s*)*\|?\s*$")
            && line.IndexOf('-') >= 0;
    }

    private static string Table(string[] lines, ref int i, int lineOffset)
    {
        List<string> header = SplitRow(lines[i]);
        List<string> alignSpec = SplitRow(lines[i + 1]);
        string tableAnchor = Anchor(lineOffset + i);
        i += 2;

        string[] align = new string[alignSpec.Count];
        for (int c = 0; c < alignSpec.Count; c++)
        {
            string spec = alignSpec[c].Trim();
            bool left = spec.StartsWith(":");
            bool right = spec.EndsWith(":");
            if (left && right) align[c] = "center";
            else if (right) align[c] = "right";
            else if (left) align[c] = "left";
            else align[c] = null;
        }

        StringBuilder html = new StringBuilder("<table" + tableAnchor + ">\n<thead><tr>");
        for (int c = 0; c < header.Count; c++)
            html.Append("<th").Append(Style(align, c)).Append('>')
                .Append(Inline(header[c])).Append("</th>");
        html.Append("</tr></thead>\n<tbody>\n");

        while (i < lines.Length && lines[i].IndexOf('|') >= 0 && lines[i].Trim().Length > 0)
        {
            List<string> cells = SplitRow(lines[i]);
            html.Append("<tr").Append(Anchor(lineOffset + i)).Append('>');
            for (int c = 0; c < header.Count; c++)
            {
                string value = c < cells.Count ? cells[c] : "";
                html.Append("<td").Append(Style(align, c)).Append('>')
                    .Append(Inline(value)).Append("</td>");
            }
            html.Append("</tr>\n");
            i++;
        }

        html.Append("</tbody></table>\n");
        return html.ToString();
    }

    private static string Style(string[] align, int column)
    {
        if (column >= align.Length || align[column] == null) return "";
        return " style=\"text-align:" + align[column] + "\"";
    }

    private static List<string> SplitRow(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith("|") && !trimmed.EndsWith("\\|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
        List<string> cells = new List<string>();
        foreach (string cell in trimmed.Split('|'))
            cells.Add(cell.Trim());
        return cells;
    }

    // ---- inline level ----

    private static string Inline(string text)
    {
        List<string> spans = new List<string>();

        // Code spans and backslash escapes are resolved together in one left-to-right
        // pass. Handling code first would let an escaped backtick open a span, and
        // handling escapes first would strip backslashes that are literal inside code.
        text = ExtractLiterals(text, spans);

        text = Escape(text);

        text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)\s]+)(?:\s+&quot;([^&]*)&quot;)?\)",
            delegate(Match m)
            {
                return "<img src=\"" + SafeUrl(m.Groups[2].Value) + "\" alt=\"" + m.Groups[1].Value
                     + "\" title=\"" + m.Groups[3].Value + "\" />";
            });
        text = Regex.Replace(text, @"\[([^\]]*)\]\(([^)\s]+)(?:\s+&quot;([^&]*)&quot;)?\)", delegate(Match m)
        {
            return "<a href=\"" + SafeUrl(m.Groups[2].Value) + "\">" + m.Groups[1].Value + "</a>";
        });

        text = Regex.Replace(text, @"~~(?=\S)(.+?)(?<=\S)~~", "<del>$1</del>", RegexOptions.Singleline);
        text = Regex.Replace(text, @"\*\*(?=\S)(.+?)(?<=\S)\*\*", "<strong>$1</strong>", RegexOptions.Singleline);
        text = Regex.Replace(text, @"__(?=\S)(.+?)(?<=\S)__", "<strong>$1</strong>", RegexOptions.Singleline);
        text = Regex.Replace(text, @"\*(?=\S)(.+?)(?<=\S)\*", "<em>$1</em>", RegexOptions.Singleline);
        text = Regex.Replace(text, @"(?<![A-Za-z0-9_])_(?=\S)(.+?)(?<=\S)_(?![A-Za-z0-9_])", "<em>$1</em>", RegexOptions.Singleline);

        for (int n = 0; n < spans.Count; n++)
            text = text.Replace("\u0001" + n + "\u0001", spans[n]);

        return text;
    }

    // The placeholder marker used to park literals while emphasis is processed.
    private const char Sentinel = (char)1;
    private const string Escapable = "\\`*_[]()#+-.!|~>{}";

    // Walks the text once, lifting out code spans and backslash-escaped characters as
    // placeholders so neither can be reinterpreted by the emphasis passes that follow.
    private static string ExtractLiterals(string text, List<string> spans)
    {
        StringBuilder output = new StringBuilder(text.Length);
        int n = 0;

        while (n < text.Length)
        {
            char current = text[n];

            if (current == '\\' && n + 1 < text.Length && Escapable.IndexOf(text[n + 1]) >= 0)
            {
                spans.Add(Escape(text[n + 1].ToString()));
                output.Append(Sentinel).Append(spans.Count - 1).Append(Sentinel);
                n += 2;
                continue;
            }

            if (current == '`')
            {
                int run = 0;
                while (n + run < text.Length && text[n + run] == '`') run++;
                string delimiter = new string('`', run);
                int close = text.IndexOf(delimiter, n + run);

                if (close > n + run)
                {
                    string content = text.Substring(n + run, close - n - run);
                    spans.Add("<code>" + Escape(content) + "</code>");
                    output.Append(Sentinel).Append(spans.Count - 1).Append(Sentinel);
                    n = close + run;
                    continue;
                }

                output.Append(delimiter);   // unmatched run, so it is literal text
                n += run;
                continue;
            }

            output.Append(current);
            n++;
        }

        return output.ToString();
    }

    private static string SafeUrl(string url)
    {
        string trimmed = url.Trim();
        if (Regex.IsMatch(trimmed, @"^\s*(?:javascript|vbscript|data)\s*:", RegexOptions.IgnoreCase))
            return "#";
        return trimmed;
    }

    private static string Escape(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
