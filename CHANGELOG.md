# MiniPad Changelog

Every release, newest first. This file is the record of what changed between versions; it
is mirrored in section 23 of `MANUAL.md`, so the same information is searchable inside the
program under **About > Help** (or **F1**).

Designed by Tim Broussard. Built in conversation with Legion (Claude, Anthropic).

---

## 1.11.1 — 8/16/26

An emergency fix for a crash 1.11 introduced. **1.11 could fail to start at all**, closing
with *"Unhandled exception… Parameter is not valid."* before the window appeared. If you are
on 1.11, replace it.

The cause was one of 1.11's own fixes. 1.11 started freeing the font a control was replacing,
to stop a leak of one font per document per zoom step. That was the right idea and the wrong
implementation.

### Fixed — crash on startup

- **MiniPad no longer disposes a font it is still using.** WinForms compares fonts **by
  value**, not by identity, so assigning a font that merely *equals* the current one is a
  silent no-op: the control keeps its original instance and quietly discards the new one.
  1.11 then freed the original, leaving every repaint drawing with released memory. The
  failure surfaced as `ArgumentException: Parameter is not valid` from `Font.ToHfont()`,
  far from the actual mistake. It needed only **one tab** and fired during startup, because
  applying the theme restyles the editor a second time at an unchanged size.
- **Changing the font with more than one tab open no longer breaks the other tabs.** The
  font chooser handed a *single* font object to every editor. Restyling any one of them
  freed it for all of them, so the remaining tabs threw the same exception on their next
  repaint. Each editor now gets its own instance.

Both paths now go through one helper that frees whichever font ends up unused, decided by
reference identity rather than value. The leak 1.11 set out to fix stays fixed.

### Note

- The Linux build was never affected. It hands Tk plain `(family, size)` tuples, which Tk
  owns and frees itself; there is no manual disposal to get wrong. Its version moves to
  1.11.1 only to stay in step with the Windows build.

---

## 1.11 — 8/9/26

A bug-fix release, from an exhaustive read of both builds end to end. No new features.
Twenty-two findings, all fixed. Four of them were verified failing before the fix and
passing after, against the shipped binary rather than the source.

### Fixed — data loss

- **A file that is not UTF-8 is no longer silently corrupted.** Both builds decoded every
  file as UTF-8 with invalid bytes replaced by `U+FFFD`, and then *saved the replacement
  characters back*. Opening a Windows-1252 text file holding a smart quote or a pound sign
  and saving it destroyed that byte, with no warning and no way back. Decoding is now
  strict: if the file is not UTF-8 it is read as the system code page (Windows) or
  cp1252/latin-1 (Linux), which map every byte to a character and back, so saving
  reproduces the original file.
- **A fenced code block with anything after the language no longer loses its contents.**
  ` ```js title="a.js" `, ` ```{.python} ` and ` ```js:line-numbers ` are ordinary in
  other tools. The opening fence was dropped, the code inside was rendered as a *paragraph*
  with its Markdown interpreted — so `*p` became emphasis and `_name_` became italics — and
  the closing fence became an empty `<pre>`. Everything after the language is now accepted
  as an info string, which is what CommonMark specifies.
  - The cause was two regexes that were supposed to agree and did not: the fence parser
    required end-of-line after the language, the "does a block start here" test only
    checked the prefix. There is now one pattern, used by both.
  - Belt and braces: the parser's "never stall" branch used to advance past a line it could
    not classify, **discarding it**. It now emits the text instead. A wrong-looking
    paragraph is visible and reportable; a vanished line is neither.

### Fixed — freezes and leaks

- **Searching the manual no longer freezes the About dialog.** One letter matches about
  5,000 times in the manual, and every highlight is a native call. The old code did that
  work three times per keystroke with no debounce. Now the search waits for a pause in
  typing, repaints only the match that changed, and above 400 hits highlights just the
  current one — the count stays exact either way. Measured on the Linux build: **28 ms** for
  a query that used to lock the window for seconds.
- **Minesweeper no longer allocates a GDI brush per cell per repaint** — up to ~960 of them
  on Expert, none disposed, on every click.
- **The tab strip no longer leaks a `Region` per repaint.** `Graphics.Clip` is a getter that
  allocates; assigning it back does not dispose it, and repaints happen on every hover.
- **The FaleOfaz logo is decoded once**, not on every open of the About dialog. A
  `PictureBox` does not dispose its own `Image`, so each open leaked a ~300 KB bitmap.
- **Fonts, and every common dialog, are disposed.** `StyleEditor` replaced a control's font
  without freeing the old one, which meant a leaked font per document per zoom step.
- **Linux: the preview no longer registers three Tcl commands per link per render.** Tkinter
  frees those only when the widget dies, and the preview re-renders while you type. The URL
  is now looked up at click time so one binding serves every render.
- **Windows: the desktop light/dark preference is read once**, not on every colour lookup —
  it was opening the registry a couple of hundred times per theme change. The Linux build
  had always cached it.

### Fixed — wrong or misleading behaviour

- **The status bar no longer lies about the preview mode at startup.** Launching with the
  remembered mode set to *editable* showed "Preview: read-only" over a pane that was in fact
  editable. The label was set before the mode was restored, and nothing set it again.
- **Linux: keyboard shortcuts no longer fire from inside dialogs.** `bind_all` puts a
  binding on a tag every widget carries, so with About open Ctrl+B typed `**` into the
  hidden document, F5 stamped a timestamp into it, Ctrl+A select-alled the editor instead of
  the search box, Ctrl+W closed a tab nobody could see, and Ctrl+Shift+B opened a *second*
  About dialog whose widgets replaced the first one's, leaving its search wired to a dead
  control.
- **Linux: applying a list or heading marker no longer marks one line too many.** Selecting
  whole lines leaves the selection ending at column 0 of the next line, and that line was
  being prefixed too.
- **Editable preview no longer reshapes prose on the way back.** Typing `1. hello`, `# hi`,
  `> quoted` or `- dashed` as ordinary text came back as a list, heading, quote or bullet.
  Line-start markers are now escaped — and only at a line start, so `C:\Users\Tim` is still
  left alone.
- **Windows: the About dialog's colour hierarchy exists again.** Four different greys were
  assigned across the About, Bunnies and Donate tabs and then all four were overwritten by
  the theme walk, so every label ended up the same colour — and editing those values changed
  nothing, which is what made it invisible. Secondary text is now asked for *by role* and
  resolved from the palette, which also fixes it on the dark themes, where a grey chosen
  against a white dialog was never going to be readable. This is how the Linux build has
  always done it.
- **Windows: the zoom readout no longer sits under the 100% button** in Appearance. At any
  three-digit zoom — most of the 50–300 range — the autosizing label overlapped it by 5px.
- **Windows: the finished tic-tac-toe board stays readable.** Disabling the cells greyed the
  X and O out at exactly the moment you want to look at them. The click guard already
  refused moves after the game ended, so the disabling was pure cost.
- **Settings saved after a write failure are no longer lost on restart.** If the portable
  file became unwritable, both builds fell back to writing the profile copy — but left the
  portable file in place, and its *existence* is what selects portable mode, so the next
  launch read the stale one. The dead file is now removed.
- **Linux: `data:`, `javascript:` and `vbscript:` links are refused centrally**, in
  `open_in_browser`, rather than in one caller that happened to remember.
- **A malformed numeric HTML entity can no longer crash the editor.** `&#55296;` is half a
  surrogate pair and `ConvertFromUtf32` throws on it; the guard allowed it through. The
  HTML-to-Markdown converter is also now wrapped — it was the one risky call in the file
  that was not.

### Fixed — documentation that did not match the program

- **"Exported HTML is identical from either build" was false**, and it was asserted in four
  places including the Build & Libraries tab users read. The Windows export also stamps a
  `data-line` attribute on every block, which its preview needs for click-to-jump, and a
  meta tag for the IE engine. The wording now says what is actually true: same engine, same
  stylesheet, same appearance, not byte-for-byte.
- **And one real difference behind that claim is fixed.** The Linux stylesheet was missing
  `body>*:first-child{margin-top:0;}`, so a page exported from Linux carried a stray top
  margin the Windows one did not — while the Python docstring claimed the two stylesheets
  were byte-for-byte identical. With that rule added, and `data-line` plus the meta tag
  normalised away, the two builds' exports of a mixed corpus are now **character-for-character
  identical** — tested, not asserted.
- **Indented code blocks are documented as unsupported.** Four-space code was silently
  rendered as a paragraph and was not in the "Not supported" list. Fenced blocks are, and
  the manual now also says they accept an info string.
- Removed a dark stylesheet and two `ToHtml` overloads that nothing called. The dead copy had
  drifted to describe table striping the real stylesheet does not have, which is worse than
  no code because it reads like documentation. Also removed two unreachable methods on the
  About dialog.
- Corrected a comment in the Linux build that declared any hard newline in the FaleOfaz text
  a bug. Mid-sentence, yes; between sentences it is a deliberate paragraph break, and two of
  those strings use one.

### Unchanged, and verified so

- The FaleOfaz wording is identical between the two builds — all twelve constants plus the
  story, compared word for word. Both files demand this in capitals; it holds.
- Both games still play correctly, the first Minesweeper click is still always safe, and
  hard tic-tac-toe still cannot be beaten.

---

## 1.10 — 8/6/26

### Added

- **A Donate tab, last in the About dialog, for FaleOfaz** — the DSPD-certified residential
  care provider in West Valley City, Utah that MiniPad's designer supports. It carries their
  logo, their own words about who they are and why, their contact details, and two buttons:
  one opens `faleofaz.org`, the other their Venmo. Clicking the logo opens their site too.
  Their words and their mark are used with permission, asked for and granted before any of
  this was written.

  **It cannot interrupt anyone.** No splash, no prompt, no first-run notice, nothing on
  startup — if you never open About, you never see it. That was the whole design constraint:
  a feature has to cost nothing to the people who do not use it, and an appeal that appears
  unbidden would fail that test badly. It is the same rule that got tooltips rejected in
  `DECISIONS.md`, applied in the other direction.

  **MiniPad still makes no network connections of its own.** Both buttons hand the address to
  the desktop's browser, exactly as clicking a link in the preview pane already did. Nothing
  is fetched, sent or reported, and the tab behaves identically offline.

  **The tab makes no claim about tax status or how gifts are used, on purpose.** Those are
  FaleOfaz's own facts to state — they can change, MiniPad gets passed from person to person,
  and a detail that is subtly wrong about someone else's charity is worse than no detail at
  all. Anyone with a specific question is pointed at FaleOfaz's own phone number and email, so
  they can answer properly and however they prefer. *(An earlier draft asserted a 501(c)(3)
  registration; it was removed in favour of this, at the designer's suggestion, because the
  organisation's own site and Instagram still brand as an LLC and the claim could not be
  verified from here.)*

  The one status-ish thing it does say is a fact about **Venmo**, not about FaleOfaz: a
  *business* profile issues no giving receipt automatically, since only verified *Charity*
  profiles do. That is there so nobody assumes paperwork arrived when it did not.

- **Room for a personal note at the foot of the tab.** Both builds carry a deliberately empty
  string (`FaleOfaz.PersonalNote`, `FALEOFAZ_PERSONAL_NOTE`). Put a sentence in it and it
  appears, styled and set apart, with no other change anywhere. Leave it empty and **nothing**
  is drawn — no rule, no gap, no orphaned heading.

### Changed

- The logo is shown unaltered on a white card rather than being inverted for the dark themes.
  Inverting is the usual trick for black line art, and it was rejected here: a registered mark
  belonging to someone else should not be recoloured to suit a colour scheme.
- Corrected the executable size quoted in `README.md` (267 KB) and `MANUAL.md` (242 KB). Both
  were stale, and they disagreed with each other. It is now 306 KB, logo included.
- The manual gained section 22, *Supporting FaleOfaz*; version history moved to section 23.

### Fixed

- Two layout defects in the Linux Donate tab, both found by screenshotting the running program
  and invisible to every logic check that passed over them: the mission quote broke
  mid-sentence, because a hard newline that is correct in a fixed Windows label is a bug inside
  a wrapping Tk text widget; and the note beside the buttons was quietly clipped at the dialog
  edge, since nothing about a Tk label reports that its own text did not fit.

- **Two Windows-only layout bugs, where the buttons were drawn on top of the story text.** The
  screenshot that found the Linux faults could not have found these — the two builds share no
  layout code — so the Windows tab was measured instead, by loading the built assembly,
  constructing the dialog without ever showing it, and reading every control's real bounds.
  Both causes are worth knowing because neither produces an error of any kind:

  - **`Label.PreferredHeight` is the height of a *single line* of text.** Advancing a layout by
    it past a multi-line label under-measures by however many lines there are. The story block
    is 324px tall; `PreferredHeight` reported roughly 250.
  - **An AutoSize label measures itself with the font it holds at that instant.** A label that
    has not yet been added to its parent still has the WinForms default font, not this dialog's
    Segoe UI 9 — so it reports one size, silently grows when it inherits, and leaves the
    position already computed from the smaller figure. Over fifteen lines the accumulated drift
    was enough to put the buttons through the middle of the paragraph.

  Positions now come from each control's actual `Bottom`, and every label on the page is given
  an explicit font so measurement never depends on parenting order.

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
- **Both panes are captioned** — "Markdown source" on the left, "Live Preview" on the right.
  Until now the second pane was unexplained: you type on the left, the same words appear on
  the right, and nothing tells you why. The preview caption also says what the pane is
  currently doing — *"updates as you type on the left"*, or *"click any block to jump to its
  source"*, or *"editable, your changes go back to the text"*. The last of those is the one
  that most earns its place: in editable mode the pane accepts typing, and a user who does
  not know that is going to be surprised either way.
- **The two panes now start at the same height.** Line 1 of the source sits level with line 1
  of the rendering. Previously the preview began about 70px lower on Windows and 30px on
  Linux, from a mixture of body padding, the first heading's own top margin, and (briefly)
  a caption over only one pane.

  This is alignment of the *first* line, not of every line, and that is a real limit rather
  than an unfinished job: `# Heading` is one source line but renders nearly twice as tall, a
  four-line table renders as three rows and a rule, and ` ``` ` fence markers occupy a source
  line while rendering as nothing at all. After the first heading any two documents drift
  apart permanently. Keeping them in step would need proportional scroll syncing, which is a
  different feature.

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
