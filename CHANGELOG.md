# MiniPad Changelog

Every release, newest first. This file is the record of what changed between versions; it
is mirrored in section 22 of `MANUAL.md`, so the same information is searchable inside the
program under **About > Help** (or **F1**).

Designed by Tim Broussard. Built in conversation with Legion (Claude, Anthropic).

---

## 1.09 — 7/31/26

### Added

- **Preferences are remembered between sessions.** Font family and size, preview mode, word
  wrap, and window size and position now persist. Previously they reset on every launch,
  which made MiniPad feel as though it half-remembered you — it kept how it looked but forgot
  how you worked. The font was the worst of it: choosing one and losing it each morning reads
  as a fault rather than a default.
- **Portable settings.** A `settings.txt` in the same folder as the program is used in
  preference to the one in your user profile, which is left untouched. The file's presence is
  the whole switch — there is deliberately no prompt and no first-run banner, because the
  feature matters to people running from a USB stick or a locked-down machine and nobody else
  should have to think about it. **Format > Portable Settings…** turns it on and off; turning
  it off copies your settings back to your profile *before* deleting the file.
- **About > Build & Libraries** now reports which settings file is in use, and whether
  portable mode is on.

### Changed

- Settings are loaded before any control is built, rather than partway through. The font,
  word wrap, preview mode and window size all have to be known before the things that use
  them exist, and a control built with a default and corrected afterwards flickers visibly on
  a slow machine.

### Fixed

- The build report in About listed four source files when there are ten.

### Notes on the design

Two remembered values are treated with suspicion when read back, and both guard against the
same class of problem — a settings file that is perfectly valid somewhere else.

A **font** is checked against the machine actually running MiniPad. Tk and WinForms both
substitute silently for an unknown family, so a file carried from another computer would hand
you a typeface you never chose, with nothing to explain it.

A **window position** is only restored if it still lands on a screen that exists. Otherwise
unplugging a second monitor would reopen MiniPad at coordinates nobody can reach, which looks
exactly like a program failing to start. The check uses intersection rather than containment,
so a window deliberately nudged half off the edge is left alone.

Writing beside the program is not always permitted — `Program Files` and write-protected
sticks both refuse. Turning portable mode on reports that plainly instead of failing
silently, and a portable file that becomes unwritable mid-session falls back to the profile
rather than losing the session's changes.

## 1.08 — 7/31/26

### Added

- **The license is now a tab in the About dialog**, read from the copy compiled into the
  program, so the terms travel with it and `LICENSE.txt` never has to be fetched to be
  read. **Copy license** puts it on the clipboard and **Save a copy...** writes it out.
- **The preview pane shows by default**, so it is found rather than hunted for. Hiding it
  with `F9` is remembered: turn it off and it stays off on every later launch until you
  turn it back on. Stored as `preview=on|off` in the settings file, which both builds read
  and write; a settings file from an earlier version has no such line and defaults to on.

### Changed

- **The credit now reads "Built in conversation with Legion (Claude, Anthropic)"** rather
  than "Written by Legion". Two reasons. The first is accuracy about authorship: every idea
  in MiniPad, and every judgement about what it should be, is Tim's; Legion wrote the code,
  the tests and the manual; neither half would have produced it alone. The second matters
  more — "Written by Legion" left a reader to assume a second person, and anyone looking at
  this program deserves to know an AI wrote the code, not least because it should change
  how much they verify before trusting it.

---

## 1.07 — 7/30/26

No new features. A deliberate hunt through the places where tabs meet everything that
existed before them, which is where the interesting faults were hiding. Seven were found
and fixed; two of them could lose your work.

### Fixed

- **Saving one tab over a file another tab already had open** left both tabs claiming that
  file, each holding a different version, and whichever you saved second silently threw the
  other away. Opening a file already open has always switched to its tab instead of making
  a second copy; Save As could reach the same broken state from the other direction. It is
  now refused, with a message saying which tab has the file and what to do about it.
  **Both builds.**
- **Edits made in the preview pane could land in the wrong document.** The pane is shared
  between tabs and rendering it is asynchronous, so between switching tabs and the pane
  catching up there was a window where an edit typed into it was applied to whichever tab
  was now in front — overwriting it. Edits now go to the document the pane was actually
  drawn from, and are discarded if that document has since been closed. **Both builds.**
- **The preview pane could go blank and stay blank.** Rendering starts a navigation that
  only finishes when the message loop runs, and starting a second before the first
  finished lost both. Selecting a tab renders, so this was reachable by opening a file
  while the preview was already open, or by anything that selects several tabs in a row
  such as Save All. Renders arriving on top of one another are now queued behind the one in
  flight. **Windows.**
- **A keystroke marked the wrong tab as unsaved** if the text that changed was not the tab
  in front. Only reachable together with the preview bug above, but wrong on its own terms.
  **Windows.**
- **Closing the window with a preview render still pending** printed
  `invalid command name ...render_preview` to the terminal, because the scheduled callback
  outlived the window it was going to draw into. Pending work is now cancelled on the way
  out. **Linux.**
- **Rendering the preview forgot a pending render instead of cancelling it**, leaving a
  callback scheduled that nothing could stop — so it ran again for no reason, and was the
  source of the error above. **Linux.**
- **The preview kept a reference to closed documents**, holding their widgets alive after
  the tab was gone. **Both builds.**

### How they were found

Two came from asking what the *shared* preview does when there is more than one document,
which is the question tabs raised and nothing had answered. One came from asking what Save
As can do that Open is already careful about. The rest came from a new sweep that performs
eight hundred random operations — new tab, open, close, switch, type, toggle the preview,
change the zoom, toggle word wrap — and checks after **every single step** that the window
still describes itself consistently: something is in front, the strip agrees with the
documents, exactly one editor is showing, no two tabs claim one file, the title matches.
It found the last of them at step 643.

---

## 1.06 — 7/30/26

Follow-up to the tabs in 1.05, from use: with enough files open there was no way to get
*back* to the first one.

### Added

- **Scroll arrows at both ends of the tab strip**, appearing as soon as the tabs no longer
  all fit. Each click moves along by one tab, so no tab is ever unreachable. An arrow greys
  out when there is nothing further that way rather than disappearing — a button that
  vanishes is harder to aim at than one that stops responding. The mouse wheel over the
  strip does the same thing, but it was undiscoverable on its own, which was the bug.

### Changed

- **The tab strip now spans the whole window**, above the preview pane, instead of sitting
  inside the editor's half of the split. Tabs get the full width to spread out in, so many
  more fit before any scrolling is needed at all — with the preview open, roughly twice as
  many.

### Fixed

- **Narrowing the window could leave the tab you were working in scrolled off the end**,
  with nothing to show where it had gone. The strip now rechecks its scroll position on
  every resize, not only when the tabs themselves change.

---

## 1.05 — 7/30/26

**Tabs.** Several documents open at once, each on its own tab, in both builds.

### Added

- **A tab per document**, the way Notepad on Windows 11 does it. Every tab keeps its own
  text, file path, undo history, caret, scroll position, encoding and line-ending style, so
  editing or saving one cannot touch another.
- **New tab** with `Ctrl+N` or the **+** at the end of the strip. **Close** with the tab's
  **×**, a middle-click, `Ctrl+W`, or File > Close Tab. **Close Other Tabs** and
  **Save All** (`Ctrl+Alt+S`) are in the File menu.
- **Moving between tabs**: click, `Ctrl+Tab` / `Ctrl+Shift+Tab`, `Ctrl+PageDown` /
  `Ctrl+PageUp`, or `Ctrl+Alt+1`…`8` for a numbered tab and `Ctrl+Alt+9` for the last one.
  The numbered jumps take `Ctrl+Alt` because `Ctrl+1`…`3` already insert headings.
- **Opening several files at once**: multi-select in the Open dialog, drop a group of files
  on the window, or pass several paths on the command line. Each gets a tab.
- Opening a file into an untouched empty tab **reuses** that tab, and opening a file that is
  already open **switches to it** rather than making a second copy that could drift apart
  from the first and overwrite it.
- Tabs show the unsaved asterisk, shorten long names with an ellipsis, share the width
  evenly as more open, and scroll under the mouse wheel once they no longer fit.
- Closing a numbered untitled tab frees its number, so you get `Untitled 2` back rather
  than climbing to `Untitled 7`.

### Fixed

- **A tab opened after startup was never given the current theme**, so under a dark theme
  it appeared as a white page with black text inside an otherwise dark window. New tabs are
  now styled as they are created rather than waiting for the next theme change.
- **A tab edited and then switched away from did not get its unsaved marker.** Tk delivers
  its `<<Modified>>` notification a moment after the change rather than on the spot, so it
  could arrive once a different tab was already in front, and the handler then read the
  wrong widget. It now takes the document from the event.

### Changed

- **File > New** is now **File > New Tab**, and opening a file no longer asks about unsaved
  changes — it opens a tab instead. You are only asked when something is actually closing.
- Closing the window asks about each unsaved tab in turn, bringing each to the front first
  so the question is about something you can see. Cancelling any one abandons the close.
- Closing the last remaining tab closes the window, as Notepad does.
- Word wrap, the font, the theme and the zoom apply to every tab at once; the preview pane
  is shared and follows whichever tab is in front.

---

## 1.04 — 7/30/26

The Linux build catches up: it now has the appearance system and zoom that 1.03 listed as
unfinished. Three legibility defects were found while testing it, two of which also affected
the Windows build and are fixed in both.

### Added

- **Appearance on Linux** (`Format > Appearance`): the same nine themes, the same light /
  dark / system modes, the same custom background and text colours, and the same 50%–300%
  zoom as the Windows build — including the live sample panel. **System** asks `gsettings`
  what the desktop prefers, since Linux has no single API for it, and stays light on a
  desktop that will not say.
- **Zoom on Linux**: `Ctrl++`, `Ctrl+-`, `Ctrl+0`, the numeric keypad, the appearance
  slider, and `Ctrl` with the scroll wheel. The status bar shows the level and highlights it
  when it is not 100%. Editor text, preview text, headings and indents all scale.
- **A shared settings file.** Both builds write the same five keys in the same order, so a
  settings file copied between them is understood, Windows line endings and all. Linux keeps
  it at `~/.config/minipad/settings.txt`.
- Exported HTML on Linux now follows the theme, as it already did on Windows, so the two
  builds produce the same page again.
- The **Build & Libraries** tab reports the active theme, zoom, what the desktop prefers,
  and where the settings live.

### Fixed

- **Buttons and list boxes were left unthemed** — Tk rejects an entire `configure` call if
  any single option is wrong for that widget, and a Button has no `selectcolor` while a
  Listbox has no `insertbackground`. One bad option silently discarded every good one,
  leaving those widgets their default grey inside a dark window. Options are now applied
  individually when a batch is refused.
- **Secondary text was unreadable on the muted palettes** — 2.9:1 on Solarized Dark, 2.2:1
  on Solarized Light, where 4.5:1 is the accessibility threshold. Text in the window chrome
  is now lifted towards the body colour until it clears 4.5:1, while blockquotes and the
  preview keep the palette's own tone. **Fixed in both builds.**
- **Search highlights in Help were unreadable on a dark theme** — the matches set a pale
  yellow background but left the text its light foreground. They now set their own dark ink.
  **Fixed in both builds.**
- The bar beside a blockquote was skewed into something like a slash, because it inherited
  the quote's italic. It has its own upright style now.
- The Bunnies descriptions were clipped at both ends in the Linux About dialog, which was
  sized for Windows' narrower fonts.

### Changed

- Radio buttons and check boxes take their panel's background instead of the raised button
  colour, so they no longer look like a row of buttons.

---

## 1.03 — 7/30/26

The first release since the program was first stamped 1.01. Everything below was built
after that stamp.

### Added

- **Appearance screen** (`Format > Appearance`): Light, Dark or System, nine named themes
  (Solarized Light and Dark, Monokai, Nord, Gruvbox Dark, Sepia, High Contrast, plus plain
  Light and Dark), custom background and text colours, and zoom from 50% to 300%.
  Everything applies immediately and is saved to `%APPDATA%\MiniPad\settings.txt`.
- **Zoom** by `Ctrl+=`, `Ctrl+-`, `Ctrl+0`, the appearance slider, or Ctrl and the scroll
  wheel. The status bar shows the current level.
- **Bunnies** — two games in the About dialog: Minesweeper with three board sizes, and
  tic-tac-toe with three skill levels, the hardest of which cannot be beaten. Reached from
  the Bunnies tab, `Ctrl+Shift+B`, or by typing `!bunnies` in the editor.
- **Help tab**: this manual, compiled into the program and searchable, with match
  highlighting, next and previous, and a match counter. `F1` opens it from anywhere.
- **License and terms of use**, as `LICENSE.txt` and inside the program.
- **A notepad icon**, drawn at eight sizes from 16 to 256 pixels, on the executable and on
  every window. The Linux build carries the same drawing as an embedded PNG.
- **This changelog.**

### Fixed

- **Minesweeper's clock kept running after its window closed**, holding the closed window
  alive and writing to controls that no longer existed. Timers are now stopped and disposed
  when their window closes, in the games and in the editor.
- **Ctrl and the scroll wheel zoomed and scrolled at the same time.** The wheel event is
  now marked handled, so Ctrl and wheel only zooms.
- **The `!bunnies` command could fire without being typed.** It was checked on every text
  change, so loading a document that happened to end with the command — or any programmatic
  change that left the caret just after it — could open the dialog by itself. It is now
  checked on key release, so only real typing triggers it. Fixed in both builds.
- **The build script failed on paths containing a space.** PowerShell re-quoted the
  `/resource:<path>,<name>` switch and the compiler read it as a filename. The script now
  runs from its own folder and uses relative paths throughout.

### Changed

- **The About dialog is now four tabs**: About, Help, Build and Libraries, and Bunnies. It
  is resizable, and its contents are docked so they fill the window at any size.
- **Bunnies appears only as a tab**, with no duplicate entry in the About menu.
- The preview stylesheet is generated from the active palette, so rendered Markdown matches
  the editor instead of always being white.
- Exported HTML follows the current theme.

### Known to be unfinished

- The Linux build has the games, the Bunnies tab and Help, but **no appearance system and
  no zoom** yet. *(Resolved in 1.04.)*

---

## 1.01 — 7/29/26

First stamped version.

### Added

- Plain-text editor: open, save, save as, drag-and-drop, command-line path, word wrap, font
  chooser, undo, cut, copy, paste, select all, date and time insertion, and a status bar
  showing the caret's true line and column.
- Text encoding and line endings preserved exactly as found, including byte-order marks and
  the choice between CRLF and LF.
- Markdown rendering: headings, bold, italic, strikethrough, inline and fenced code,
  blockquotes, nested lists, pipe tables with alignment, links, images and horizontal rules.
- Live preview (`F9`) with three modes: rendered read-only, rendered with click-to-jump to
  the matching source line, and rendered and editable with changes converted back to
  Markdown.
- Export as a self-contained HTML page.
- Markdown editing helpers with keyboard shortcuts.
- A Linux build, `minipad.py`, from the same design.

### Fixed during development

- **Word wrap made line-based features target the wrong text.** The Win32 text box counts
  wrapped display rows, not lines, so the status bar, click-to-jump and heading insertion
  all worked on the wrong line. Replaced with logical-line mapping.
- **A crash on long paragraphs**: the Linux inline parser recursed once per styled span and
  overflowed the stack at around two thousand of them. Made iterative.
- **Image URLs were not sanitised** where link URLs were, so `javascript:` could reach an
  `img` tag.
- **Literal Markdown characters were reinterpreted** after an edit in the preview, turning
  text like `_snake_case_` into italics. Characters are now escaped on the way out, and
  backslash escapes are understood on the way in.
- **Tabs did not count as indentation**, so tab-indented nested lists rendered flat.

---

## A note on version numbers

There is no 1.02. The version went from 1.01 to 1.03 at the owner's request.
