# MiniPad Owner's Manual

**Version 1.11.1** — 8/16/26
Designed by Tim Broussard. Built in conversation with Legion (Claude, Anthropic).

Open this file in MiniPad and press **F9** to read it as rendered Markdown, or press
**F1** anywhere in the program to read this same manual inside it, searchable.

---

## Contents

1. What MiniPad is
2. System requirements
3. Getting started
4. The window
4a. Tabs: several documents at once
5. Working with files
6. Editing text
7. Writing Markdown
8. The Markdown menu
9. Live preview and its three modes
10. Exporting HTML
11. Appearance: themes, colours and zoom
12. Bunnies
12a. Help: the manual inside the program
13. Keyboard reference
14. Menu reference
15. Files and locations
16. Building from source
17. Windows and Linux differences
18. Troubleshooting
19. Known limitations, and why
20. How this was tested
21. License and terms of use
22. Supporting FaleOfaz
23. Version history

---

## 1. What MiniPad is

MiniPad is a small plain-text editor that also renders Markdown. It opens and saves
ordinary `.txt` and `.md` files, and it never changes anything in your file that you did
not change yourself.

It ships as two programs built from one design:

| Build | File | Needs |
|---|---|---|
| Windows | `MiniPad.exe` | Nothing — one self-contained 309 KB executable |
| Linux | `minipad.py` | Python 3 and `python3-tk` |

Both read and write the same files, render the same Markdown, and export HTML that looks
the same from either build. Neither one uses the network, and neither installs anything.

The Windows executable carries everything inside it: the program, its notepad icon at
eight sizes, this manual and the license. Nothing else has to sit beside it.

**Design rules it follows:**

- Your file's text encoding and line endings are preserved exactly as found.
- Nothing is written to disk unless you save it. Settings are the one exception.
- No third-party libraries, no package downloads, no telemetry.

---

## 2. System requirements

### Windows

`MiniPad.exe` requires **.NET Framework 4.0 or later**, which is built into Windows 8 and
everything newer.

| Windows version | Status |
|---|---|
| 11, 10 | Fully supported |
| 8, 8.1 | Fully supported |
| 7 SP1 | Supported — needs .NET 4 installed, usually already present |
| Vista SP1, XP SP3 | Runs, but the preview looks plain. See below |

Two details depend on your Windows version:

- **The preview needs Internet Explorer 9 or newer.** MiniPad renders Markdown using
  MSHTML, the HTML engine built into Windows. On Windows XP, which tops out at IE8, the
  preview still works but loses rounded corners and table shading.
- **Appearance mode "System" needs Windows 10 1607 or newer.** It reads the Windows
  light/dark setting, which older versions do not expose. On older Windows it quietly
  falls back to Light; the Light, Dark and named themes all still work.

The executable is AnyCPU, so it runs as 32-bit or 64-bit to match your Windows, and runs
on ARM64 Windows under emulation.

### Linux

| Requirement | Minimum | Notes |
|---|---|---|
| Python | 3.4 in principle, **3.6 recommended** | No f-strings, no walrus, no type hints used |
| Tk | 8.5 for the tabbed About dialog, 8.6 for the window icon | Every current distro ships 8.6 |
| Package | `python3-tk` | Not installed by default on most distros |
| Display | X11, or Wayland through XWayland | |

There are no glibc, systemd or distro-specific dependencies. Ubuntu 18.04 and newer work
comfortably. Because it is pure standard library, `minipad.py` also runs on Windows and
macOS if you have Python 3 with Tk.

---

## 3. Getting started

### Windows

Copy `MiniPad.exe` anywhere and run it. There is no installer and nothing to uninstall —
delete the file and the only trace left is a small settings file (see section 15).

```
MiniPad.exe                     open with an empty document
MiniPad.exe notes.txt           open a file
MiniPad.exe "C:\path\my doc.md" quote paths containing spaces
```

To make MiniPad your handler for a file type, right-click the file, choose *Open with*,
then *Choose another app*, and point it at `MiniPad.exe`.

### Linux

```bash
sudo apt install python3-tk        # once, on Debian and Ubuntu
python3 minipad.py                 # open with an empty document
python3 minipad.py notes.md        # open a file
```

To run it as a command, mark it executable — it already carries a `#!/usr/bin/env python3`
line:

```bash
chmod +x minipad.py
./minipad.py notes.md
```

On Fedora the package is `python3-tkinter`; on Arch it is `tk`.

---

## 4. The window

```
+--------------------------------------------------------------+
| File  Edit  Markdown  Format  About                          |  menu bar
+--------------------------------------------------------------+
| notes.md x | *todo.txt x | README.md x |                 | + |  tab strip
+----------------------------------+---------------------------+
|                                  |                           |
|   the editor                     |   the preview pane        |
|   plain text, your document      |   rendered Markdown       |
|                                  |   (F9 hides it)           |
+----------------------------------+---------------------------+
| Ln 12, Col 4        Preview: read-only              100%     |  status bar
+--------------------------------------------------------------+
```

The tab strip spans the **whole** window, above the preview rather than inside the editor's
half of it, so the tabs get the full width to spread out in.

**Title bar.** Shows the file name and `MiniPad`. A leading asterisk means unsaved
changes: `*notes.md - MiniPad`. Untitled documents show `Untitled`.

**Tab strip.** One tab per open document — see section 4a.

**Editor.** Your document as plain text, always. Markdown markers stay visible here.

**Preview pane.** Showing by default, so it is found rather than hunted for. **F9** hides
and shows it, and whichever way you leave it is how it opens next time. Drag the divider to
resize; it starts at half the window width.

**Status bar.** Three readings:

- **Ln and Col** — the caret's line and column, counted in *real lines of text*, not
  wrapped screen rows. Column 1 is the start of the line.
- **Preview** — off, read-only, click to jump, or editable.
- **Zoom** — the current zoom, shown in the accent colour when it is not 100%.

---

## 4a. Tabs: several documents at once

MiniPad keeps as many documents open as you like, each on its own tab, the way Notepad on
Windows 11 does. Nothing has to be closed before something else is opened.

**Every tab is a separate document.** It carries its own text, its own file path, its own
undo history, its own caret and scroll position, and its own text encoding and line-ending
style. Editing one tab cannot affect another, and saving one saves only that one.

### Working with tabs

| Action | How |
|---|---|
| New tab | `Ctrl+N`, the **+** at the end of the strip, or File > New Tab |
| Open files into tabs | `Ctrl+O` — select several at once and each gets a tab |
| Switch tab | Click it, `Ctrl+Tab` / `Ctrl+Shift+Tab`, or `Ctrl+PageDown` / `Ctrl+PageUp` |
| Jump to tab 1–8 | `Ctrl+Alt+1` … `Ctrl+Alt+8` |
| Jump to the last tab | `Ctrl+Alt+9`, however many there are |
| Close tab | The **×** on the tab, middle-click it, `Ctrl+W`, or File > Close Tab |
| Close all but one | File > Close Other Tabs |
| Save every unsaved tab | `Ctrl+Alt+S`, or File > Save All |

**One file, one tab.** Opening a file that is already open switches to its tab rather than
making a second copy, and **Save As refuses to write over a file another tab has open** —
in both cases because two tabs holding different versions of one file means whichever you
save second silently throws the other away. If you want to do it anyway, close the other
tab first.

`Ctrl+1`, `Ctrl+2` and `Ctrl+3` are **not** tab shortcuts here — they already insert
Markdown headings, which is why the numbered jumps take `Ctrl+Alt`.

### What the tabs show

A tab shows the file's name, or `Untitled`, `Untitled 2` and so on for documents that have
never been saved. **A leading asterisk means unsaved changes**, the same as in the title
bar. Close a numbered untitled tab and its number becomes free again, so you get
`Untitled 2` back rather than climbing to `Untitled 7`.

Long names are shortened with an ellipsis. Tabs share the width evenly and shrink as more
open, down to a readable minimum.

### When there are more tabs than fit

Once the tabs no longer fit, **a `‹` and a `›` arrow appear at either end of the strip**.
Each click moves the strip along by one tab, so nothing is ever unreachable — with ten
files open and only the last six showing, the left arrow walks you back to the first. The
mouse wheel over the strip does the same thing.

An arrow greys out when there is nothing further that way, rather than disappearing: a
button that vanishes is harder to aim at than one that simply stops responding.

You never have to scroll by hand to follow yourself around. Switching tabs by keyboard
scrolls the new tab into view, and so does narrowing the window — the tab you are working
in is always on screen.

### Two conveniences worth knowing

- **Opening a file into an untouched empty tab reuses that tab** rather than leaving an
  empty one behind.
- **Opening a file that is already open switches to its tab** instead of opening a second
  copy. Two views of one file could drift apart and then overwrite each other.

### Closing and unsaved work

Closing a tab with unsaved changes asks about *that* document, and brings it to the front
first so you can see what you are being asked about. Closing the window asks about each
unsaved tab in turn; cancelling any one of them abandons the whole close, so nothing you
already answered for is lost.

Closing the last remaining tab closes the window, as Notepad does.

### What is shared, and what is not

Word wrap, the font, the theme, the zoom level and the preview mode are settings of the
**editor**, not of a document, so changing any of them applies to every tab at once. The
preview pane is shared too, and always shows whichever tab is in front.

---

## 5. Working with files

### Opening

- **File > Open** (`Ctrl+O`) — select several files and each opens in its own tab
- **Drag files onto the window** (Windows only) — every dropped file gets a tab
- **Pass paths on the command line** — one tab each

Opening a `.md`, `.markdown`, `.mdown` or `.mkd` file turns the preview on automatically.

Opening a file never discards what you were working on: it opens a tab instead. You are
only asked about unsaved changes when something is actually being closed.

### Saving

- **File > Save** (`Ctrl+S`) — saves in place, or asks for a name if the file is new
- **File > Save As** (`Ctrl+Shift+S`)

### Encoding and line endings

MiniPad reads the file, notes how it was written, and writes it back the same way.

| What it detects | What it does on save |
|---|---|
| UTF-8 with a byte-order mark | Keeps the mark |
| UTF-8 without a mark | Does not add one |
| UTF-16 with a mark | Keeps UTF-16 and the mark |
| Windows line endings, CRLF | Writes CRLF |
| Unix line endings, LF only | Writes LF |
| Mixed line endings | Normalises to CRLF on Windows, LF on Linux |

New documents are UTF-8 with no byte-order mark, using CRLF on Windows and LF on Linux.

This matters more than it sounds. A tool that silently converts LF to CRLF turns a
one-word edit into a diff that touches every line of the file.

---

## 6. Editing text

Standard editing throughout: `Ctrl+X`, `Ctrl+C`, `Ctrl+V`, `Ctrl+A`, and `Ctrl+Z` to
undo. Tab inserts a real tab character.

**F5** inserts the current date and time at the caret.

**Format > Word Wrap** toggles wrapping. With wrap off you get a horizontal scrollbar.
Wrapping is display only — it never inserts line breaks into your file.

**Format > Font** picks the editor typeface and size. The size you choose is the 100% size;
zoom scales from it rather than compounding on it.

**Undo differs between builds.** On Windows, `Ctrl+Z` is the Win32 text box's single-step
undo. On Linux, Tk keeps a full undo stack with `Ctrl+Z` and `Ctrl+Y` to redo.

---

## 7. Writing Markdown

Everything below works in both builds and in exported HTML.

### Text styling

| Type this | To get |
|---|---|
| `**bold**` | **bold** |
| `*italic*` or `_italic_` | *italic* |
| `~~struck through~~` | ~~struck through~~ |
| `` `inline code` `` | `inline code` |
| `[link text](https://example.com)` | a link |
| `![alt text](picture.png)` | an image |

### Blocks

| Type this | To get |
|---|---|
| `# Heading` through `###### Heading` | headings, six levels |
| `- item` or `* item` or `+ item` | a bulleted list |
| `1. item` | a numbered list |
| `> quoted` | a block quote |
| `---` or `***` | a horizontal rule |
| three backticks, then a language | a fenced code block |

Indent a list item by two spaces to nest it. A tab counts as four spaces, so tab-indented
lists nest correctly too.

Blank lines separate paragraphs. Inside a paragraph, a single newline becomes a line break.

### Tables

```
| Host     | IP         | Aligned |
| :---     | ---:       | :---:   |
| korroban | 10.0.0.207 | yes     |
```

The second row sets alignment: `:---` left, `---:` right, `:---:` centred, `---` default.
To put a pipe inside a cell, escape it as `\|`.

### Escaping

Put a backslash before a Markdown character to make it literal:

| Type this | To get |
|---|---|
| `\*not italic\*` | \*not italic\* |
| `\_snake\_case\_` | \_snake\_case\_ |
| `` \` `` | a literal backtick |

Escapes are honoured for these characters: backslash, backtick, asterisk, underscore,
square and round brackets, hash, plus, minus, dot, exclamation mark, pipe, tilde,
greater-than and braces. Inside a code span no escaping happens — everything is literal
there, which is what you want.

### Not supported

Setext headings (underlined with `===`), reference-style links, footnotes, task list
checkboxes, definition lists, inline HTML, and automatic linking of bare URLs. Raw HTML in
your document is shown as text, not rendered — deliberately, so a Markdown file cannot
inject markup into the preview.

**Indented code blocks** are not supported either — the four-space form. Indentation already
means list nesting and list continuation here, and a block that could be either is worse than
a block that has one meaning. Use a fenced block instead; fences are unambiguous, and they
can name a language.

**Fenced blocks accept anything after the language.** ` ```js `, ` ```js title="a.js" `,
` ```{.python} ` and ` ```js:line-numbers ` all open a code block. Everything after the
language name is ignored rather than rejected, so a file written for another tool still
renders as code here.

---

## 8. The Markdown menu

These act on your selection, or insert a template if nothing is selected.

| Item | Shortcut | Effect |
|---|---|---|
| Bold | `Ctrl+B` | Wraps the selection in `**` |
| Italic | `Ctrl+I` | Wraps the selection in `*` |
| Strikethrough | — | Wraps the selection in `~~` |
| Inline Code | `Ctrl+/` on Windows | Wraps the selection in backticks |
| Code Block | — | Puts fences above and below |
| Heading 1, 2, 3 | `Ctrl+1`, `Ctrl+2`, `Ctrl+3` | Adds `#` markers to every selected line |
| Bulleted List | — | Adds `- ` to every selected line |
| Numbered List | — | Numbers the selected lines 1, 2, 3 |
| Blockquote | — | Adds `> ` to every selected line |
| Link | `Ctrl+K` | Builds `[text](url)` around the selection |
| Table | — | Inserts a small table skeleton |
| Horizontal Rule | — | Inserts `---` on its own line |

The line-based items work on whole lines of text, so applying a heading while a long
wrapped paragraph is selected puts the `#` at the true start of the paragraph, not at the
start of the screen row your caret happens to sit on.

`Ctrl+K` is smart about the selection: select text and it becomes the link label; select
something starting with `http://`, `https://` or `www.` and it becomes the target instead.

---

## 9. Live preview and its three modes

**The preview is showing when you first run MiniPad**, so you do not have to know it exists
to find it. **F9** hides and shows it, and that choice is remembered — turn it off and it
stays off, on this and every later launch, until you turn it back on.

It re-renders about half a second after you stop typing, and keeps its scroll position so
it does not jump while you write. With several tabs open the pane is shared, and always
shows whichever tab is in front.

The pane carries a caption along its top reading **Live Preview**, followed by what it is
currently doing — *updates as you type on the left*, *click any block to jump to its
source*, or *editable, your changes go back to the text*. It changes with the mode, so the
pane always explains itself. The editor is captioned **Markdown source** to match.

### Why the two panes only line up at the top

Line 1 of your text sits exactly level with line 1 of the rendering. After that they drift,
and that is unavoidable rather than unfinished:

- `# Heading` is one source line but renders nearly twice as tall
- A four-line table renders as three rows and a rule
- ` ``` ` fence markers take a source line each and render as nothing
- One long paragraph wraps to a different number of lines on each side

Nothing short of proportional scroll syncing — moving one pane by percentage when the other
moves — can keep them together, and that is a different feature with its own awkwardness.
The first line is the one worth guaranteeing, so that is the one MiniPad guarantees.

Choose how the pane behaves under **Markdown > Preview Mode**.

### Rendered, read-only

The default. The pane displays your document and takes no input. Clicking a link opens it
in your normal browser rather than replacing the preview.

### Rendered, click to jump to source

Click any heading, paragraph, list item, table row or code block and the editor caret
moves to the line that produced it. Useful for finding your place in a long document.

Every rendered block carries its source line number, so this stays accurate with word
wrap on.

### Rendered, editable

Type directly onto the rendered page. About half a second after you stop, your edits are
converted back into Markdown and the editor updates to match. Bold text looks bold while
you edit it.

Things worth knowing before you rely on it:

- **Converting rendered HTML back to Markdown cannot be perfect.** MiniPad's converter is
  built for the markup MiniPad produces, and repeated round trips are stable — the same
  text converted twice gives an identical result, so editing does not slowly reshape your
  file. Unusual constructions can still come back tidied.
- **Literal Markdown characters get escaped.** Type `_snake_case_` as plain text in the
  preview and the Markdown will read `\_snake\_case\_`. That is deliberate: without the
  backslashes it would turn into italics the next time it was rendered. Windows paths like
  `C:\Users\Tim` are left alone.
- **Pressing Enter starts a new paragraph.** The HTML engine inserts a block element, so a
  soft line break inside a paragraph is not preserved as one.
- **The undo history resets when a sync lands**, because the whole document text is
  replaced. Save before a long editing session in this mode.
- **On Linux this mode works differently** — see section 17.

Only edit in one pane at a time. Typing in the editor re-renders the preview, which would
discard what you were doing on the rendered side.

---

## 10. Exporting HTML

**File > Export as HTML** writes a complete, self-contained web page: one file, styles
embedded, no external resources, nothing to upload alongside it.

The export uses your current theme, so a dark theme produces a dark page. Both builds
produce the same HTML from the same Markdown.

---

## 11. Appearance: themes, colours and zoom

**Format > Appearance** opens the appearance screen. Everything applies immediately and is
saved as you change it — there is nothing to confirm. **Both builds have it**, and they read
the same settings keys.

### Mode

**Light**, **Dark**, or **System** to follow the desktop.

On Windows, System reads the personalisation setting and needs Windows 10 1607 or newer; on
older versions it stays light. On Linux, System asks `gsettings` for
`org.gnome.desktop.interface color-scheme`, which GNOME, Cinnamon, MATE and Budgie all
answer; on a desktop that does not answer, or with `gsettings` absent, it stays light rather
than guessing. The answer is read once per session — choosing **System** again re-reads it,
which is how you pick up a desktop that changed while MiniPad was open.

### Themes

Nine palettes, in the **Theme** list:

| Theme | Character |
|---|---|
| Light | White page, blue links |
| Dark | Near-black page, soft blue links |
| Solarized Light | Warm paper, muted ink |
| Solarized Dark | Deep teal, low contrast on purpose |
| Monokai | Classic editor dark with a green accent |
| Nord | Cool blue-grey |
| Gruvbox Dark | Warm retro dark with amber |
| Sepia | Aged paper, brown ink |
| High Contrast | Pure black and white with yellow accents |

Choosing a named theme overrides the Light/Dark/System choice. Picking a mode again clears
it and returns to following the mode.

**On contrast, precisely.** Body text meets the 4.5:1 accessibility ratio against the page
in every one of the nine palettes; the closest is Solarized Light at 4.99:1. *Secondary*
text is a different matter — Solarized, Nord, Gruvbox and Sepia are faithful renditions of
deliberately muted palettes, and their secondary tone reaches only 2.2:1 to 4.2:1. That tone
is kept as the palette intends it for blockquotes and the preview, but text in the window
chrome — the status bar, hints, tab labels — is lifted towards the body colour until it
clears 4.5:1. Solarized Light is the one palette that cannot reach 4.5:1 even at its own
body colour, topping out at 4.39:1. **If you need maximum legibility, use High Contrast**,
which is 21:1 for body text and 13.6:1 for everything else.

The preview stylesheet is generated from whichever palette is active, so rendered Markdown
always matches the editor rather than staying stubbornly white.

### Custom colours

**Background** and **Text** open a colour picker and override the theme's own colours.
**Reset to theme** clears the overrides. Overrides survive a theme change, so you can keep
your own background across different themes.

### Zoom

50% to 300%, affecting both the editor font and the preview.

| Action | How |
|---|---|
| Zoom in | `Ctrl+=`, or Format > Zoom In |
| Zoom out | `Ctrl+-`, or Format > Zoom Out |
| Reset to 100% | `Ctrl+0` |
| Any level | The slider on the appearance screen |
| By mouse | Hold `Ctrl` and scroll the wheel over the editor |

Zoom multiplies the font size you picked in **Format > Font** — it does not compound, so
returning to 100% returns to exactly your chosen size.

---

## 12. Bunnies

There are two games hidden in the About dialog. They do not touch your document.

Three ways in:

| Command | What it does |
|---|---|
| `About > Bunnies` | The tab itself |
| `Ctrl+Shift+B` | Jumps straight to that tab |
| Type `!bunnies` in the editor | Opens the tab |

The typed command is left in your text — an easter egg should not edit your file. Delete it
yourself.

### Minesweeper

| Level | Board | Mines |
|---|---|---|
| Beginner | 9 x 9 | 10 |
| Intermediate | 16 x 16 | 40 |
| Expert | 30 x 16 | 99 |

Left click reveals a square, right click flags one. The counter shows mines minus flags,
and the clock starts on your first click.

**The first click is always safe** — mines are placed after it, avoiding the square you
clicked and all eight of its neighbours, so you always open a pocket instead of a lone
number. Clear every safe square to win.

### Tic-tac-toe

You are X and move first. Three skill levels:

| Skill | How it plays |
|---|---|
| Easy | Random legal moves |
| Medium | Takes a win if there is one, blocks yours, otherwise prefers centre then corners |
| Hard | Full minimax search — cannot be beaten, only drawn |

Hard was verified by playing out every possible human strategy against it: 609 games,
zero losses. If you beat it, something is broken and I would like to know.

---

## 12a. Help: the manual inside the program

The manual you are reading is compiled into the program. Open it with **F1**, or from
**About > Help**.

The Help tab is searchable:

| Action | How |
|---|---|
| Search | Type in the box. Matches highlight as you type |
| Next match | `Enter`, or the Next button |
| Previous match | `Shift+Enter`, or the Previous button |
| Clear the search | `Escape` |

Searching is case-insensitive. The counter reads `3 of 27 matches`; the current match is
shown in orange and the rest in yellow. Search wraps around at either end.

Because the manual is embedded, the program needs no companion files. If a build is ever made
without it, Help falls back to reading `MANUAL.md` from the program's own folder.

---

## 13. Keyboard reference

### Windows

| Keys | Action |
|---|---|
| `Ctrl+N` | New tab |
| `Ctrl+O` | Open — several files at once if you like |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+Alt+S` | Save All |
| `Ctrl+W` | Close tab |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Next / previous tab |
| `Ctrl+PageDown` / `Ctrl+PageUp` | Next / previous tab |
| `Ctrl+Alt+1` … `Ctrl+Alt+9` | Jump to tab 1–8, or the last one |
| `Ctrl+Z` | Undo |
| `Ctrl+X`, `Ctrl+C`, `Ctrl+V` | Cut, copy, paste |
| `Ctrl+A` | Select all |
| `F5` | Insert date and time |
| `F9` | Show or hide the preview |
| `Ctrl+B` | Bold |
| `Ctrl+I` | Italic |
| `Ctrl+/` | Inline code |
| `Ctrl+1`, `Ctrl+2`, `Ctrl+3` | Heading 1, 2, 3 |
| `Ctrl+K` | Insert link |
| `Ctrl+=` | Zoom in |
| `Ctrl+-` | Zoom out |
| `Ctrl+0` | Reset zoom |
| `Ctrl` and scroll wheel | Zoom by mouse |
| `Ctrl+Shift+B` | Bunnies |
| `F1` | Help: the searchable manual |

### Linux

Same as above, with these differences:

| Keys | Action |
|---|---|
| `Ctrl+Y` | Redo — Linux keeps a full undo stack |
| `Ctrl+Shift+B` | Bunnies |
| `Ctrl++` `Ctrl+-` `Ctrl+0` | Zoom, also on the numeric keypad |
| `Ctrl` and scroll wheel | Zoom by mouse, over the editor or the preview |
| — | No `Ctrl+/` for inline code |

All the tab shortcuts above work identically on both builds.

---

## 14. Menu reference

| Menu | Items |
|---|---|
| **File** | New Tab, Open, Save, Save As, Save All, Close Tab, Close Other Tabs, Export as HTML, Exit |
| **Edit** | Undo, Redo on Linux, Cut, Copy, Paste, Select All, Time/Date |
| **Markdown** | Live Preview, Preview Mode, then the formatting helpers from section 8 |
| **Format** | Word Wrap, Font, Appearance, Zoom In, Zoom Out, Reset Zoom |
| **About** | Opens the About dialog: About, Help, License, Build and Libraries, Bunnies |

The **Build and Libraries** tab lists the exact version, build time, compiler, runtime,
operating system, every loaded library and what each one is used for. **Copy details** puts
all of it on the clipboard, which is the quickest way to tell someone what you are running.

---

## 15. Files and locations

### What ships

| File | Purpose |
|---|---|
| `MiniPad.exe` | The Windows program. Self-contained |
| `minipad.py` | The Linux program. Self-contained, standard library only |
| `MiniPad.ico` | The icon, if you want it for a shortcut. The exe already contains it |
| `MANUAL.md` | This manual. Also compiled into the exe, see About > Help |
| `LICENSE.txt` | License and terms of use. Also compiled into the exe, see About > License |
| `CHANGELOG.md` | What changed between versions. Mirrored in section 23 |
| `sample.md` | A short file exercising every Markdown feature |
| `build.ps1` | Rebuilds the Windows executable |
| `*.cs` | The Windows source, ten files |

### Settings

Your choices are kept in:

| Build | Location |
|---|---|
| Windows | `%APPDATA%\MiniPad\settings.txt` |
| Linux | `$XDG_CONFIG_HOME/minipad/settings.txt`, normally `~/.config/minipad/settings.txt` |

Both builds write the same keys in the same order, so a settings file copied from one to the
other is read correctly, Windows line endings and all.

It is plain text and safe to edit or delete — deleting it returns everything to defaults:

```
theme=system
preset=none
zoom=100
background=none
text=none
preview=on
fontfamily=none
fontsize=0
previewmode=0
wordwrap=on
window=none
maximised=off
```

The first six describe how MiniPad *looks*; the rest describe how you *work* with it, and
were added in 1.09 — before that they reset on every launch.

| Key | Means |
|---|---|
| `preview` | Whether the preview pane was showing |
| `fontfamily`, `fontsize` | Your choice from **Format > Font**. `none` and `0` mean the built-in default |
| `previewmode` | `0` read-only, `1` click to jump, `2` editable |
| `wordwrap` | `on` or `off` |
| `window` | Size and position. `none` means centre it at the default size |
| `maximised` | Whether the window was maximised |

Two of these are treated with suspicion when read back, deliberately. **A font is checked
against the machine actually running MiniPad** — a settings file that travelled from another
computer may name a font this one does not have, and the alternative is being silently given
a typeface you never chose. **A window position is only restored if it still lands on a
screen that exists**; unplug a second monitor and MiniPad returns to the middle of the
remaining one rather than opening where you cannot reach it.

A file written by an older version simply has fewer lines. Anything missing takes its
default, so upgrading never loses your settings and never fails to start.

### Portable settings

Normally MiniPad keeps that file in your user profile. **If a `settings.txt` exists in the
same folder as the program, MiniPad uses that one instead** and leaves your profile
untouched.

That is the whole mechanism: the file's presence is the switch. It suits a USB stick, or a
machine where you would rather not leave anything behind — move the program and its
`settings.txt` together and your preferences travel with them.

**Format > Portable Settings…** turns it on and off. Turning it on writes the file for you;
turning it off copies your settings back to your profile *before* deleting it, so nothing is
lost either way. You can equally create the file by hand with any text editor — an empty one
is enough, since MiniPad fills it in on the first change.

**About > Build & Libraries** always shows which file is in use, so the mode is never a
mystery.

If the program lives somewhere you cannot write to — `Program Files`, or a write-protected
stick — turning portable mode on will fail and say so plainly, and your settings carry on
being saved to your profile. If a portable file becomes unwritable *while* MiniPad is
running, it quietly falls back to the profile rather than losing your changes.

Nothing else is written outside your documents. No registry keys, no temporary files.

---

## 16. Building from source

### Windows

No SDK, no project file, no package manager. The C# compiler that ships inside Windows is
enough:

```bash
powershell -File "S:\Claude Projects\Test\MiniPad\build.ps1"
```

The script calls `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` with the eight
source files, `/win32icon` for the icon, and `/resource` twice to embed this manual and
the license. The source targets C# 5, which is what that compiler accepts — no string
interpolation, no `nameof`, no null-conditional operators.

Two things about the script are deliberate. It runs from its own folder and passes every
path as a **relative** name: this folder sits under a path containing a space, and that
makes PowerShell re-quote switches like `/resource:<path>,<name>`, which the compiler then
mistakes for a filename. And `MiniPad.exe` must not be running, or the compiler cannot
overwrite it.

### Linux

Nothing to build. `minipad.py` runs as it is.

---

## 17. Windows and Linux differences

Both builds share the Markdown engine, the file handling rules, the menus and the games.
These are the honest differences.

| | Windows | Linux |
|---|---|---|
| Preview rendering | MSHTML, the Windows HTML engine | Drawn with Tk text styling |
| Editable preview | Type on the rendered page; converts back to Markdown | Shows the Markdown source, styled in place, with markers visible |
| Appearance and zoom | Full: modes, nine themes, custom colours, zoom | Not yet implemented |
| Undo | Single step | Full undo stack, with redo |
| Drag and drop a file | Yes | No — needs the non-standard `tkdnd` extension |
| Window icon | From the executable | Embedded PNG, needs Tk 8.6 |

**Why editable preview differs.** Windows has an HTML engine that can be made editable, so
the rendered page itself accepts typing. Linux has no equivalent available from the standard
library, so instead of a lossy imitation the Linux pane shows your Markdown source with
headings, bold, code and quotes styled in place. Editing it is lossless by construction:
the text in that pane *is* the file.

Exported HTML comes from the same Markdown engine and the same generated stylesheet in both
builds, so a page exported from either looks the same and opens anywhere. It is not byte for
byte the same file: the Windows build also stamps a `data-line` attribute on every block —
its preview needs them to jump from the rendering to the source — and a meta tag for the IE
engine. Neither has any use in the Linux build, so neither is written there.

---

## 18. Troubleshooting

**The preview is blank or shows an error.**
The preview needs Internet Explorer 9 or newer present in Windows. It does not need IE to be
your browser, or even for IE to be visible — just installed, which it is on Windows 7 and up.

**Appearance mode "System" does not follow my Windows theme.**
Windows only exposes that setting on Windows 10 1607 and newer. On older versions MiniPad
falls back to Light. Choose Light or Dark directly, or pick a named theme.

**Everything looks too big or too small after I fiddled with zoom.**
`Ctrl+0`, or the **100%** button on the appearance screen. If a setting is stuck, delete
`%APPDATA%\MiniPad\settings.txt` and restart.

**On Linux: `ModuleNotFoundError: No module named 'tkinter'`.**
Install the Tk bindings: `sudo apt install python3-tk` on Debian and Ubuntu,
`python3-tkinter` on Fedora, `tk` on Arch.

**On Linux the window has no icon.**
Tk 8.5 cannot read PNG icon data. Harmless — everything else works.

**My editable-preview edits picked up backslashes.**
Expected, and protecting you. See section 9.

**The build fails with "cannot access the file because it is being used".**
`MiniPad.exe` is running. Close it and build again.

**A tab-indented list is not nesting.**
It should — a tab counts as four columns. Check the item is indented at least two columns
past its parent and uses a list marker.

**I lost my undo history in the editable preview.**
The sync replaces the document text, which clears the undo stack. Save first.

---

## 19. Known limitations, and why

Things MiniPad does not do, stated plainly so you are not hunting for them:

- **No find and replace.** The most conspicuous gap. Nothing prevents it; it is simply not
  built yet.
- **No printing.** Export to HTML and print from your browser, which also gives you page
  setup and print preview for free.
- **No spell check.**
- **No line numbers in the margin.** The status bar shows the caret's line.
- **No syntax highlighting inside fenced code blocks.** The language tag is recorded in the
  HTML as a class, so exported pages can be highlighted by a stylesheet, but MiniPad itself
  does not colour code.
- **One level of list nesting** in the renderer. Deeper nesting renders flat.
- **Markdown subset.** Section 7 lists exactly what is and is not supported. It covers
  ordinary documents, not every corner of CommonMark.
- **A pipe inside a table cell must be escaped** as `\|`.
- **Very large files.** It loads a whole document into memory and re-renders the preview
  from scratch. A 240 KB Markdown file renders in about 160 milliseconds; multi-megabyte
  files will feel it. Turn the preview off with F9 for those.

---

## 20. How this was tested

Stated so you know which claims are measured and which are reasoned.

**Measured** — eighteen suites, 894 individual checks, all passing as of 1.08 (455 on Windows, 439 on Linux):

- 24 Markdown round-trip checks: every construct survives Markdown to HTML to Markdown, and
  a second pass is byte-identical, so editing does not drift the document.
- 35 checks over the appearance system, layout at three window sizes, all nine palettes,
  contrast ratios, zoom scaling and settings persistence.
- Game logic: mine counts across 200 boards, first-click safety, flood reveal, flags, win
  and loss detection, out-of-bounds clicks; and every possible human strategy against Hard
  tic-tac-toe — 609 games, zero losses.
- Line mapping driven through the real window, confirming Ln/Col, click-to-jump and
  heading insertion land on true lines with word wrap on.
- The Help tab: manual loading, every section present, case-insensitive search, phrase
  search, literal treatment of regular-expression characters, and a single-letter search
  matching 2342 times without stalling.
- Lifecycle checks: the shared icon surviving repeated window open and close, timers
  stopping when their window closes, GDI handles staying flat across 300 theme changes,
  and every window building under every theme.
- Performance: 240 KB to HTML in about 160 ms, back again in about 80 ms, no runaway
  behaviour on 2000 stray emphasis markers.
- The Linux build: syntax valid on every Python from 3.4 to 3.13, 13 engine checks, source
  line anchors, the same game-logic suite, and the Help wiring.
- **Eight hundred random operations**, on each build: new tab, open, close, switch, type,
  toggle the preview, change the zoom, toggle word wrap — checking after *every step* that
  something is in front, the strip agrees with the documents, exactly one editor is showing,
  no two tabs claim one file, and the title matches the tab. This is the test for the bugs
  nobody thought to write a test for, and it earned its place immediately.
- **The Linux interface, drawn for real** under a virtual X server with Tk 8.6: the whole
  window built, then every widget read back to confirm what it actually became. Each of the
  nine palettes checked at the editor, the caret, the status bar, the menu bar and its
  drop-downs; every surface in every window checked against the set of colours the theme
  actually chose, so an unthemed widget is caught rather than merely unnoticed; and every
  label checked for readable contrast on the surface behind it. Zoom driven from 50% to
  300% and back, confirming the font returns to exactly the size you chose. The appearance
  window, About, Help, Minesweeper and tic-tac-toe all opened under a dark theme and
  audited the same way. Settings written, the program restarted, and the theme and zoom
  confirmed to come back.

**Reasoned but not run:** everything about Windows versions older than 11, and Python
versions other than 3.13. Those come from documented framework requirements and a syntax
and API audit.

**Still not covered:** how the window is decorated and how fonts land on any particular
desktop environment. The interface is now verified under a virtual X server, which proves
the widgets and their colours but not how GNOME or KDE will frame them.

**Twenty-one real bugs have been found and fixed by this testing**, one of them a crash and
two of them able to lose your work. In
1.01: word wrap making line-based features target the wrong text, a recursion overflow on
paragraphs with many styled spans, unsanitised image URLs, literal Markdown characters being
reinterpreted after a preview edit, and tabs not counting as indentation. In 1.03: a game
clock that kept running after its window closed, Ctrl and the scroll wheel zooming and
scrolling at the same time, and the `!bunnies` command firing on any text change rather
than on typing. In 1.04: Tk silently discarding a whole batch of colours because one option
in it was wrong for that widget, secondary text falling below the readable threshold on the
muted palettes, and search highlights leaving light text on pale yellow under a dark theme —
the last two in both builds. In 1.05: a tab opened after startup never being given the
current theme, so it appeared as a white page inside a dark window; and a tab edited and
then switched away from failing to gain its unsaved marker, because Tk delivers its
"modified" notification a moment later, by which time the wrong document was in front. In
1.06: narrowing the window leaving the active tab scrolled off the end of the strip, with
nothing on screen to say where it had gone.

In 1.07, a deliberate hunt through the places where tabs meet what came before them found
seven more: Save As overwriting a file another tab held, preview edits landing in the wrong
document, the preview going permanently blank when two renders collided, a keystroke marking
the wrong tab unsaved, a scheduled render outliving its window, a pending render forgotten
rather than cancelled, and the preview holding references to closed documents.

**Three were found by looking at a screenshot rather than by an assertion** — a white editor
inside a dark window, a tab with typing in it but no unsaved marker, and an active tab that
was nowhere to be seen. All three are now covered by tests as well.

**One was found by a random sweep**, at step 643 of 800.

---

## 21. License and terms of use

Copyright (c) 2026 Tim Broussard. All rights reserved.
Built in conversation with Legion (Claude, Anthropic), an AI assistant, working under Tim's
direction.

The full text is on the **License tab** of the About dialog, so it travels with the program
and never has to be fetched, and in `LICENSE.txt` alongside it. In short:

**You may** use MiniPad for anything, private or commercial, at no cost, on as many machines
as you own or administer. You may keep backups, and you may pass the unmodified program to
someone else free of charge as long as the license goes with it. Documents you create are
entirely yours — the license says nothing about them.

**You may not** copy, reuse, adapt or publish the source code, in whole or in part, in any
other project. You may not distribute modified or derived versions, sell or sublicense the
program, or remove the authorship credits.

Reading the source to see how it works is fine. Copying it into something else is not.

**No warranty.** The program is provided as is, with no warranty of any kind, and the author
is not liable for any damages or data loss arising from its use. Keep your own backups — this
is a text editor, and it writes to your files.

**Third-party code: none.** MiniPad uses no external libraries and no downloaded packages. It
calls only what the operating system already provides: the .NET Framework and the Windows
HTML engine on Windows, the Python standard library with Tk on Linux. There is nothing else
to attribute and no other license to comply with.

This is a plain-language license, written to be read rather than to survive a courtroom. It
is not legal advice. If MiniPad is ever distributed beyond a few machines, have a solicitor
look it over first.

---

## 22. Supporting FaleOfaz

**About > Donate** is the last tab in the About dialog. It is there because MiniPad's designer
asked for it, and it is the only part of the program that mentions anything outside MiniPad.

FaleOfaz is a DSPD-certified residential care provider in West Valley City, Utah, serving Salt
Lake County residents with disabilities, autism, and brain injuries. Their story, their words
and their contact details are on the tab, along with two buttons: one opens `faleofaz.org`, the
other opens their Venmo. Their logo is on the tab too, and clicking it opens their site.

**What the tab does not do.** It does not appear on startup, it never interrupts you, and
nothing about it is required. If you never open the About dialog you will never see it. That is
deliberate: MiniPad's whole design rule is that a feature must cost nothing to the people who
do not use it, and an appeal that pops up unbidden would break that rule badly.

**MiniPad still makes no network connections of its own.** Both buttons hand the address to
your desktop's browser — the same thing that already happens when you click a link in the
preview pane. Nothing is fetched, sent, or reported, and the tab works identically offline
apart from the browser having nothing to show.

**On donations, and what the tab deliberately does not say.** It makes **no claim** about tax
status, registration, or how gifts are used, and that is on purpose. Those are FaleOfaz's own
facts to state — they can change, MiniPad gets passed from person to person, and a detail that
is subtly wrong about someone else's charity is worse than no detail at all. The tab points
anyone with a specific question at FaleOfaz's own phone number and email, so they can answer
properly and in whatever way they prefer.

The one thing it does state is a fact about Venmo rather than about FaleOfaz: a Venmo
*business* profile does not issue a giving receipt automatically — only Venmo's verified
*Charity* profiles do. That is there so nobody assumes paperwork arrived when it did not.

**Their words are quoted with permission**, asked for and granted before the tab was built.
The logo is used with permission and is shown unaltered, on a white card so that it stays
legible in the dark themes rather than being inverted — a registered mark should not be
recoloured to suit a colour scheme.

**For maintainers.** Everything factual lives in named constants — the `FaleOfaz` class in
`About.cs` and the `FALEOFAZ_*` constants in `minipad.py` — so a detail that changes on their
end is a one-line edit in two files rather than a hunt through layout code. Both builds carry a
`PersonalNote` / `FALEOFAZ_PERSONAL_NOTE` string that is deliberately empty; put a sentence in
it and it appears at the foot of the tab, set apart and styled, with no other change needed.
Leave it empty and nothing is drawn at all — no rule, no gap, no heading. **The wording must
match between the two builds**; only the line breaks legitimately differ, because Windows
paints into fixed labels that do not wrap and Linux uses a wrapping text widget.

---

## 23. Version history

Also kept as `CHANGELOG.md` beside the program. There is no 1.02 — the version went from
1.01 to 1.03 at the owner's request.

### 1.11.1 — 8/16/26

An emergency fix for a crash that 1.11 introduced. **1.11 could fail to start**, closing with
*"Unhandled exception… Parameter is not valid."* before the window ever appeared. If you are on
1.11, replace it.

It was caused by one of 1.11's own fixes. To stop a font leak, 1.11 began freeing the font a
control was replacing — but Windows compares fonts **by value**, not by identity, so handing a
control a font that merely *equals* the one it already has changes nothing: the control keeps
what it had and discards what you gave it. MiniPad then freed the font the editor was still
using, and the next redraw drew with memory that had been given back. One tab was enough, and
applying the theme at startup was enough to trigger it.

A second, related fault is fixed too: changing the font with several tabs open handed *one*
font object to every tab, so tidying up after any one of them broke the rest.

The Linux build was never affected and changes only its version number.

### 1.11 — 8/9/26

A bug-fix release from a line-by-line read of both builds. No new features, twenty-two fixes.
The full detail is in `CHANGELOG.md`; the ones you might actually notice:

**Two of them could lose your work, and both are gone.** A file that was not UTF-8 — an older
Windows text file with a smart quote or a pound sign in it — used to open with those characters
replaced by `?`-like marks, and *saving wrote the replacements back*, destroying the original
bytes silently. Files are now decoded strictly and fall back to a byte-preserving code page, so
what you save is what you opened. Separately, a fenced code block written as
` ```js title="a.js" ` — the form several other tools use — lost its opening fence and rendered
its contents as ordinary prose with the Markdown interpreted, so `*p` turned into emphasis.
Anything after the language is now accepted and ignored, as it should be.

**Searching the manual no longer freezes the window.** Typing a single letter used to lock the
About dialog for seconds; it now answers in a small fraction of one. Above 400 hits only the
current match is highlighted, and the label says so — the count and Next/Previous are unaffected.

**The preview mode shown in the status bar is now the mode you are actually in.** Starting
MiniPad with *editable* remembered used to report "read-only" over a pane that accepted typing.

**On Linux, shortcuts no longer reach past an open dialog.** Ctrl+B, F5, Ctrl+A, Ctrl+W and
Ctrl+Shift+B used to act on the document behind the About window while you were typing in it.

**Typing plain prose into the editable preview no longer turns it into markup.** `1. hello` or
`# hi` came back as a numbered list or a heading; they now stay as you typed them.

**On Windows, About reads properly again** — the greys that distinguish headings from asides
were being overwritten by the theme and are now drawn from the palette, so they work on the dark
themes too. The zoom readout in Appearance no longer sits underneath the 100% button, and a
finished game of tic-tac-toe no longer greys out the X and O.

**Exported HTML.** The claim that both builds produced identical HTML was not true, and one real
difference behind it is fixed: the Linux stylesheet was missing a rule, so an exported page had a
stray margin above its first block. Both builds' exports of the same document are now identical
apart from the source-line attributes the Windows preview needs. The wording in this manual has
been corrected to say what is actually the case.

### 1.10 — 8/6/26

**A Donate tab, last in the About dialog, for FaleOfaz** — the DSPD-certified residential care
provider in West Valley City, Utah that MiniPad's designer supports. Their logo, their own
words, their contact details, and two buttons: one opens `faleofaz.org`, the other their
Venmo. Their words and their mark are used with permission, asked for and granted before any
of it was written. Section 22 covers the tab in full.

**It cannot interrupt anyone.** No splash, no prompt, no first-run notice, nothing on startup.
If you never open About, you never see it — a feature has to cost nothing to the people who do
not use it, and an appeal that appears unbidden would fail that test badly.

**MiniPad still makes no network connections of its own.** Both buttons hand the address to
your desktop's browser, exactly as clicking a link in the preview already did.

The tab makes **no claim** about tax status or how gifts are used — those are FaleOfaz's facts
to state, not MiniPad's, so anyone with a specific question is pointed at their own phone and
email. It does note one fact about Venmo: a *business* profile issues no giving receipt
automatically, only a verified *Charity* profile does.

Both builds carry a deliberately empty personal-note string. Put a sentence in it and it
appears at the foot of the tab; leave it empty and nothing at all is drawn.

Two Linux layout defects were found and fixed by photographing the running program rather than
by testing it: a quote that broke mid-sentence, and a caption silently clipped at the dialog
edge. Neither was visible to any logic check.

### 1.09 — 7/31/26

**MiniPad now remembers how you work, not only how it looks.** Your font, preview mode, word
wrap and window size and position are all kept between sessions. Previously they reset on
every launch — the font most annoyingly of all, since choosing one and losing it each morning
reads as a fault rather than a default. See section 15 for the full list of what is stored.

Two of them are checked rather than trusted when read back. A remembered **font** must exist
on the machine actually running MiniPad, or the built-in default is used — otherwise a
settings file brought from another computer would silently hand you a typeface you never
chose. A remembered **window position** must still land on a screen that exists, so
unplugging a second monitor returns MiniPad to the middle of the remaining one instead of
opening it where you cannot reach it.

**Portable settings.** If a `settings.txt` sits in the same folder as the program, MiniPad
uses it and leaves your user profile alone. The file's presence is the entire switch: no
prompt, no first-run question, nothing for anyone who does not want it. Made for USB sticks
and machines where you would rather not leave anything behind. **Format > Portable
Settings…** turns it on and off — switching off copies your settings back to your profile
before removing the file, so nothing is lost. **About > Build & Libraries** shows which file
is in use.

**Both panes are captioned** — "Markdown source" and "Live Preview" — so the second copy of
your text is explained rather than left to be guessed at. The preview caption also names what
the pane is currently doing.

**The two panes start at the same height**, so line 1 of your text is level with line 1 of the
rendering. Only the first line can be guaranteed: see section 9.

**Fixed:** the build report in About listed four source files when there are ten.

### 1.08 — 7/31/26

**The license is now a tab in the About dialog**, read from the copy compiled into the
program, so the terms travel with it and `LICENSE.txt` never has to be fetched. Copy it to
the clipboard or save a copy from there.

**The preview pane shows by default**, so it is found rather than hunted for. Hiding it with
`F9` is remembered on every later launch until you turn it back on.

**Changed:** the credit now reads "Built in conversation with Legion (Claude, Anthropic)"
rather than "Written by Legion" — accurate about how the work was divided, and clear that an
AI wrote the code rather than leaving a reader to assume a second person.

### 1.07 — 7/30/26

No new features — a deliberate hunt through the places where tabs meet everything that
existed before them. Seven bugs found and fixed, two of which could lose work.

**Fixed**

- Saving one tab over a file another tab already had open left both claiming it, and
  whichever you saved second silently threw the other away. Now refused, with a message
  saying which tab has the file
- Edits made in the preview pane could land in the wrong document, because the pane is
  shared and rendering it is asynchronous. They now go to the document the pane was drawn
  from, and are discarded if it has been closed
- The preview pane could go blank and stay blank when two renders started on top of each
  other — reachable by opening a file with the preview already open, or by Save All
- A keystroke could mark the wrong tab as unsaved
- Closing the window with a render pending printed a Tk error to the terminal, and
  rendering forgot pending work instead of cancelling it
- The preview kept references to closed documents

### 1.06 — 7/30/26

**Scroll arrows on the tab strip**, appearing as soon as the tabs no longer all fit, so a
tab scrolled off the end is never unreachable. Each click moves along by one tab; an arrow
greys out when there is nothing further that way. **The strip now spans the whole window**,
above the preview rather than inside the editor's half of the split, so many more tabs fit
before any scrolling is needed.

**Fixed**

- Narrowing the window could leave the tab you were working in scrolled off the end. The
  strip now rechecks its scroll position on every resize

### 1.05 — 7/30/26

**Tabs**, in both builds. Several documents open at once, each on its own tab, each keeping
its own text, path, undo history, caret, encoding and line endings. New tab with `Ctrl+N`
or the **+**; close with the tab's **×**, a middle-click or `Ctrl+W`; move about with
`Ctrl+Tab`, `Ctrl+PageDown` or `Ctrl+Alt+1`…`9`. Save All and Close Other Tabs are in the
File menu. Several files can be opened at once from the Open dialog, a drop, or the command
line. An untouched empty tab is reused, and a file that is already open is switched to
rather than opened twice. Closing the last tab closes the window.

**Fixed**

- A tab opened after startup was never given the current theme, so it appeared as a white
  page inside a dark window
- A tab edited and then switched away from did not gain its unsaved marker, because Tk
  delivers its modified notification a moment later, by which time the wrong document was
  in front

**Changed**

- File > New is now File > New Tab, and opening a file no longer asks about unsaved changes
- Closing the window asks about each unsaved tab in turn, showing each one first

### 1.04 — 7/30/26

The Linux build gains the appearance system and zoom that 1.03 listed as unfinished: the
same nine themes, the same modes, custom colours, and 50%–300% zoom on `Ctrl++`, `Ctrl+-`,
`Ctrl+0` and `Ctrl` with the wheel. **System** asks `gsettings` what the desktop prefers.
Both builds now write the same settings keys, so the file can be copied between them.
Exported HTML follows the theme on Linux too.

**Fixed**

- Buttons and list boxes were left unthemed on Linux: Tk refuses a whole `configure` call if
  one option is wrong for that widget, so a single bad option discarded every good one
- Secondary text was unreadable on the muted palettes — 2.9:1 on Solarized Dark. Chrome text
  is now lifted until it clears 4.5:1, while the preview keeps the palette's own tone.
  Fixed in both builds
- Search highlights in Help were light text on pale yellow under a dark theme. Fixed in both
  builds
- The bar beside a blockquote was skewed by the quote's italic
- The Bunnies descriptions were clipped in the Linux About dialog

**Changed**

- Radio buttons and check boxes take their panel's background rather than the button colour

### 1.03 — 7/30/26

**Added**

- Appearance screen: Light, Dark or System, nine named themes, custom background and text
  colours, and zoom from 50% to 300%, all saved automatically
- Zoom by `Ctrl+=`, `Ctrl+-`, `Ctrl+0`, the slider, or Ctrl and the scroll wheel
- Bunnies: Minesweeper with three board sizes, and tic-tac-toe with three skill levels
- Help tab: this manual, compiled into the program and searchable, opened with `F1`
- License and terms of use, as `LICENSE.txt` and inside the program
- A notepad icon at eight sizes, on the executable and every window
- A changelog

**Fixed**

- Minesweeper's clock kept running after its window closed, holding the closed window alive
- Ctrl and the scroll wheel zoomed and scrolled at the same time
- The `!bunnies` command could fire without being typed — loading a document ending with it
  was enough. It is now checked on key release, in both builds
- The build script failed on paths containing a space

**Changed**

- The About dialog is four tabs, resizable, with docked contents that fill it at any size
- Bunnies appears only as a tab, with no duplicate menu entry
- The preview stylesheet is generated from the active palette, so it matches the editor
- Exported HTML follows the current theme

**Still unfinished at the time:** the Linux build had the games, Bunnies and Help, but no
appearance system and no zoom. Resolved in 1.04.

### 1.01 — 7/29/26

First stamped version: the editor, file handling with encoding and line-ending
preservation, Markdown rendering, live preview with three modes, HTML export, the Markdown
helpers, and the Linux build.

Five bugs were found and fixed during its development: word wrap making line-based features
target the wrong text, a stack overflow on paragraphs with thousands of styled spans,
unsanitised image URLs, literal Markdown characters being reinterpreted after a preview
edit, and tabs not counting as indentation.

---

Designed by **Tim Broussard**. Built in conversation with **Legion** (Claude, Anthropic).

*Every idea in MiniPad is Tim's, and so is every judgement about what it should be. Legion
wrote the code, the tests and this manual. Neither half of that would have produced it
alone, which is why the credit says "in conversation" rather than naming an author.*
