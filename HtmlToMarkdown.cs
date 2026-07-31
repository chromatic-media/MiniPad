// Converts HTML back into Markdown. Used when the preview pane is editable: the
// browser control hands back its edited HTML and this rebuilds the source text.
//
// It is deliberately tolerant. MSHTML rewrites markup as the user types - tags come
// back upper-case, attributes get re-quoted, and pressing Enter inserts DIV or SPAN
// wrappers that were never in the original document.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public static class HtmlToMarkdown
{
    private class Node
    {
        public string Tag;
        public string Text;
        public bool IsText;
        public Dictionary<string, string> Attributes = new Dictionary<string, string>();
        public List<Node> Children = new List<Node>();
    }

    private static readonly string[] VoidTags = { "br", "hr", "img", "meta", "link", "input" };

    public static string Convert(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        Node root = Parse(html);
        StringBuilder markdown = new StringBuilder();
        Blocks(root, markdown);

        string result = markdown.ToString().Replace("\r\n", "\n");
        result = Regex.Replace(result, @"[ \t]+\n", "\n");
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim() + "\n";
    }

    // ---- parsing ----

    private static Node Parse(string html)
    {
        Node root = new Node();
        root.Tag = "root";
        Stack<Node> open = new Stack<Node>();
        open.Push(root);

        int position = 0;
        while (position < html.Length)
        {
            int start = html.IndexOf('<', position);
            if (start < 0)
            {
                AddText(open.Peek(), html.Substring(position));
                break;
            }
            if (start > position)
                AddText(open.Peek(), html.Substring(position, start - position));

            int end = html.IndexOf('>', start);
            if (end < 0)
            {
                AddText(open.Peek(), html.Substring(start));
                break;
            }

            string raw = html.Substring(start + 1, end - start - 1).Trim();
            position = end + 1;

            if (raw.StartsWith("!") || raw.StartsWith("?"))
                continue; // comment or declaration

            if (raw.StartsWith("/"))
            {
                string closing = raw.Substring(1).Trim().ToLowerInvariant();
                CloseTag(open, closing, root);
                continue;
            }

            bool selfClosing = raw.EndsWith("/");
            if (selfClosing) raw = raw.Substring(0, raw.Length - 1);

            Node node = new Node();
            node.Tag = TagName(raw);
            ReadAttributes(raw, node.Attributes);

            // Browsers do not require these to be closed explicitly.
            if (node.Tag == "p" || node.Tag == "li")
            {
                if (open.Peek().Tag == node.Tag)
                    open.Pop();
            }

            open.Peek().Children.Add(node);

            if (!selfClosing && Array.IndexOf(VoidTags, node.Tag) < 0)
                open.Push(node);
        }

        return root;
    }

    private static void CloseTag(Stack<Node> open, string tag, Node root)
    {
        // Tolerate mismatched markup: only unwind if the tag is actually open.
        bool found = false;
        foreach (Node candidate in open)
        {
            if (candidate.Tag == tag) { found = true; break; }
        }
        if (!found) return;

        while (open.Count > 1)
        {
            Node node = open.Pop();
            if (node.Tag == tag) break;
        }
    }

    private static string TagName(string raw)
    {
        int space = raw.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' });
        string name = space < 0 ? raw : raw.Substring(0, space);
        return name.Trim().ToLowerInvariant();
    }

    private static void ReadAttributes(string raw, Dictionary<string, string> into)
    {
        foreach (Match match in Regex.Matches(raw,
            @"([A-Za-z_:][-A-Za-z0-9_:.]*)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+))"))
        {
            string name = match.Groups[1].Value.ToLowerInvariant();
            string value = match.Groups[2].Success ? match.Groups[2].Value
                         : match.Groups[3].Success ? match.Groups[3].Value
                         : match.Groups[4].Value;
            into[name] = Decode(value);
        }
    }

    private static void AddText(Node parent, string text)
    {
        if (text.Length == 0) return;
        Node node = new Node();
        node.IsText = true;
        node.Text = text;
        parent.Children.Add(node);
    }

    // ---- block serialisation ----

    private static readonly string[] InlineTags =
    {
        "a", "strong", "b", "em", "i", "code", "del", "s", "strike", "span", "font",
        "u", "img", "br", "small", "big", "sub", "sup", "label", "abbr", "mark"
    };

    private static bool IsInline(string tag)
    {
        return Array.IndexOf(InlineTags, tag) >= 0;
    }

    // Text and inline elements sitting directly between blocks form their own
    // paragraph, so a bare <a> keeps its link syntax instead of collapsing to text.
    private static void FlushInline(List<Node> run, StringBuilder output)
    {
        if (run.Count == 0) return;

        Node wrapper = new Node();
        wrapper.Tag = "span";
        wrapper.Children.AddRange(run);
        run.Clear();

        string text = Inline(wrapper).Trim();
        if (text.Length > 0) output.Append(text).Append("\n\n");
    }

    private static void Blocks(Node parent, StringBuilder output)
    {
        List<Node> pending = new List<Node>();

        foreach (Node child in parent.Children)
        {
            if (child.IsText || IsInline(child.Tag))
            {
                pending.Add(child);
                continue;
            }

            FlushInline(pending, output);

            switch (child.Tag)
            {
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    output.Append(new string('#', child.Tag[1] - '0')).Append(' ')
                          .Append(Inline(child).Trim()).Append("\n\n");
                    break;

                case "p":
                case "div":
                    string paragraph = Inline(child).Trim();
                    if (paragraph.Length > 0) output.Append(paragraph).Append("\n\n");
                    break;

                case "pre":
                    output.Append(CodeBlock(child)).Append("\n\n");
                    break;

                case "blockquote":
                    StringBuilder quoted = new StringBuilder();
                    Blocks(child, quoted);
                    foreach (string line in quoted.ToString().TrimEnd().Split('\n'))
                        output.Append(line.Length > 0 ? "> " + line : ">").Append('\n');
                    output.Append('\n');
                    break;

                case "ul":
                case "ol":
                    output.Append(List(child, 0)).Append('\n');
                    break;

                case "hr":
                    output.Append("---\n\n");
                    break;

                case "table":
                    output.Append(Table(child)).Append('\n');
                    break;

                case "body":
                case "html":
                case "section":
                case "article":
                    Blocks(child, output); // wrappers the editor may have introduced
                    break;

                default:
                    string fallback = Inline(child).Trim();
                    if (fallback.Length > 0) output.Append(fallback).Append("\n\n");
                    break;
            }
        }

        FlushInline(pending, output);
    }

    private static string CodeBlock(Node pre)
    {
        Node code = null;
        foreach (Node child in pre.Children)
        {
            if (!child.IsText && child.Tag == "code") { code = child; break; }
        }
        Node source = code ?? pre;

        string language = "";
        if (code != null && code.Attributes.ContainsKey("class"))
        {
            Match match = Regex.Match(code.Attributes["class"], @"lang-([A-Za-z0-9_+#-]+)");
            if (match.Success) language = match.Groups[1].Value;
        }

        string text = RawText(source).Replace("\r\n", "\n").Trim('\n');
        return "```" + language + "\n" + text + "\n```";
    }

    private static string List(Node list, int depth)
    {
        bool ordered = list.Tag == "ol";
        StringBuilder output = new StringBuilder();
        string indent = new string(' ', depth * 2);
        int number = 1;

        foreach (Node child in list.Children)
        {
            if (child.IsText || child.Tag != "li") continue;

            string marker = ordered ? (number++) + ". " : "- ";
            output.Append(indent).Append(marker)
                  .Append(Inline(child).Trim().Replace("\n", "\n" + indent + "  "))
                  .Append('\n');

            foreach (Node nested in child.Children)
            {
                if (!nested.IsText && (nested.Tag == "ul" || nested.Tag == "ol"))
                    output.Append(List(nested, depth + 1));
            }
        }

        return output.ToString();
    }

    private static string Table(Node table)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> alignments = new List<string>();
        bool headerSeen = false;

        foreach (Node section in Flatten(table))
        {
            if (section.IsText || section.Tag != "tr") continue;

            List<string> cells = new List<string>();
            foreach (Node cell in section.Children)
            {
                if (cell.IsText) continue;
                if (cell.Tag != "td" && cell.Tag != "th") continue;

                cells.Add(Inline(cell).Trim().Replace("|", "\\|"));

                if (!headerSeen)
                {
                    string style = cell.Attributes.ContainsKey("style") ? cell.Attributes["style"] : "";
                    Match align = Regex.Match(style, @"text-align\s*:\s*(left|right|center)",
                                              RegexOptions.IgnoreCase);
                    alignments.Add(align.Success ? align.Groups[1].Value.ToLowerInvariant() : null);
                }
            }
            if (cells.Count > 0)
            {
                rows.Add(cells);
                headerSeen = true;
            }
        }

        if (rows.Count == 0) return "";

        StringBuilder output = new StringBuilder();
        output.Append("| ").Append(string.Join(" | ", rows[0].ToArray())).Append(" |\n");

        List<string> separators = new List<string>();
        for (int c = 0; c < rows[0].Count; c++)
        {
            string align = c < alignments.Count ? alignments[c] : null;
            if (align == "center") separators.Add(":---:");
            else if (align == "right") separators.Add("---:");
            else if (align == "left") separators.Add(":---");
            else separators.Add("---");
        }
        output.Append("| ").Append(string.Join(" | ", separators.ToArray())).Append(" |\n");

        for (int r = 1; r < rows.Count; r++)
            output.Append("| ").Append(string.Join(" | ", rows[r].ToArray())).Append(" |\n");

        return output.ToString();
    }

    // Rows may sit directly under the table or inside thead/tbody/tfoot.
    private static IEnumerable<Node> Flatten(Node table)
    {
        foreach (Node child in table.Children)
        {
            if (child.IsText) continue;
            if (child.Tag == "thead" || child.Tag == "tbody" || child.Tag == "tfoot")
            {
                foreach (Node row in child.Children)
                    yield return row;
            }
            else
            {
                yield return child;
            }
        }
    }

    // ---- inline serialisation ----

    private static string Inline(Node parent)
    {
        StringBuilder output = new StringBuilder();

        foreach (Node child in parent.Children)
        {
            if (child.IsText)
            {
                output.Append(Collapse(child.Text));
                continue;
            }

            switch (child.Tag)
            {
                case "strong":
                case "b":
                    output.Append(Emphasis(child, "**"));
                    break;
                case "em":
                case "i":
                    output.Append(Emphasis(child, "*"));
                    break;
                case "del":
                case "s":
                case "strike":
                    output.Append(Emphasis(child, "~~"));
                    break;
                case "code":
                    output.Append('`').Append(RawText(child).Trim()).Append('`');
                    break;
                case "a":
                    string href = child.Attributes.ContainsKey("href") ? child.Attributes["href"] : "";
                    string label = Inline(child).Trim();
                    output.Append('[').Append(label).Append("](").Append(href).Append(')');
                    break;
                case "img":
                    string src = child.Attributes.ContainsKey("src") ? child.Attributes["src"] : "";
                    string alt = child.Attributes.ContainsKey("alt") ? child.Attributes["alt"] : "";
                    output.Append("![").Append(alt).Append("](").Append(src).Append(')');
                    break;
                case "br":
                    output.Append('\n');
                    break;
                case "ul":
                case "ol":
                    break; // handled by the block serialiser
                default:
                    output.Append(Inline(child)); // span, font, u, and anything unknown
                    break;
            }
        }

        // A <br /> followed by a newline in the source must not become two spaces.
        return output.ToString().Replace(" \n", "\n").Replace("\n ", "\n");
    }

    // Skip the markers when the element is empty, so "**" never appears alone.
    private static string Emphasis(Node node, string marker)
    {
        string inner = Inline(node);
        if (inner.Trim().Length == 0) return inner;

        string leading = inner.Substring(0, inner.Length - inner.TrimStart().Length);
        string trailing = inner.Substring(inner.TrimEnd().Length);
        return leading + marker + inner.Trim() + marker + trailing;
    }

    private static string RawText(Node node)
    {
        StringBuilder output = new StringBuilder();
        if (node.IsText) return Decode(node.Text);
        foreach (Node child in node.Children)
            output.Append(RawText(child));
        return output.ToString();
    }

    private static string Collapse(string text)
    {
        return EscapeMarkdown(Regex.Replace(Decode(text), @"\s+", " "));
    }

    // Text the user typed literally must not turn into formatting when it is parsed
    // again - otherwise typing _snake_case_ in the preview comes back as italics.
    // Only the characters that would actually start markup are escaped, so Windows
    // paths like C:\Users\Tim are left alone.
    private static string EscapeMarkdown(string text)
    {
        StringBuilder output = new StringBuilder(text.Length);
        for (int n = 0; n < text.Length; n++)
        {
            char current = text[n];
            if (current == '\\' && n + 1 < text.Length && "\\`*_[".IndexOf(text[n + 1]) >= 0)
                output.Append('\\');
            else if (current == '`' || current == '*' || current == '_' || current == '[')
                output.Append('\\');
            output.Append(current);
        }
        return output.ToString();
    }

    private static string Decode(string text)
    {
        if (text.IndexOf('&') < 0) return text;

        text = text.Replace("&lt;", "<").Replace("&gt;", ">")
                   .Replace("&quot;", "\"").Replace("&apos;", "'")
                   .Replace("&nbsp;", " ").Replace("&#39;", "'");
        text = Regex.Replace(text, @"&#(\d{1,7});", delegate(Match match)
        {
            int code;
            if (int.TryParse(match.Groups[1].Value, out code) && code > 0 && code < 0x10FFFF)
                return char.ConvertFromUtf32(code);
            return match.Value;
        });
        return text.Replace("&amp;", "&"); // last, so &amp;lt; survives as &lt;
    }
}
