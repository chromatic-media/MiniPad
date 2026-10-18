<div align="center">

# MiniPad
### A little space for your words.

Write notes, keep lists, and turn plain text into neatly formatted pages as you type.

**Free to use · Windows & Linux · Works offline**

### [↓ Get MiniPad for Windows](https://github.com/tcdeveloper27/MiniPad/raw/refs/heads/main/MiniPad.exe)

One small file. No installer. No extra files to collect.

[Read the owner's manual](MANUAL.md) · [See what's new](CHANGELOG.md)

</div>

---

## Welcome! Start here

You don't need to understand the code or download everything on this page to use MiniPad.

1. **Get the Windows app** using the link above. The file you want is **MiniPad.exe**.
2. **Open MiniPad.exe** once it has downloaded. MiniPad's manual describes it as a portable app: you can keep it wherever you like, with nothing to install.
3. **Start writing.** Press **F1** whenever you want the built-in help.

**Using Linux?** MiniPad also comes as [minipad.py](minipad.py). It needs Python 3 and Tk; the [Linux getting-started guide](MANUAL.md#linux-1) explains the requirements and how to run it.

> **Can't reach the download?** This repository is currently private. You'll need to be signed into a GitHub account with access to it.

## Small app, useful touches

| For everyday writing | When you want a little more |
|---|---|
| Open and save ordinary text files | Preview formatted Markdown as you type |
| Keep several documents open in tabs | Export your writing as HTML |
| Choose a light or dark appearance | Explore colour palettes and adjust the zoom |
| Work offline | Read the searchable manual inside the app |

**New to Markdown?** It's a way to format text with simple marks, such as `#` before a heading. MiniPad shows a preview beside your writing so you can see the result. You can also use it for ordinary plain-text notes.

## A few handy keys

| Press | To |
|---|---|
| **F1** | Open the built-in manual |
| **F9** | Show or hide the preview |
| **Ctrl+T** | Open a new tab |

## Help and more information

- **[Owner's manual](MANUAL.md)** — features, requirements, and troubleshooting.
- **[Version history](CHANGELOG.md)** — changes and fixes.
- **[Licence](LICENSE.txt)** — free for personal and commercial use; restrictions apply to modifying and reusing the source.

Designed by **Tim Broussard**. Built in conversation with **Legion** (Claude, Anthropic), working under his direction.

---

<details>
<summary><strong>Technical details, building from source, and full project notes</strong></summary>

## Project overview

A small plain-text editor that also renders Markdown, live, as you type.

It opens and saves ordinary `.txt` and `.md` files, and it never changes anything in your
file that you did not change yourself. No network access, no telemetry, no installer, and
no third-party libraries — it calls only what the operating system already provides.

**Version 1.11.2**

---

## Two builds, one design

| Build | File | Requires |
|---|---|---|
| **Windows** | `MiniPad.exe` | Nothing — a self-contained 309 KB executable (.NET Framework 4, built into Windows 8 and newer) |
| **Linux** | `minipad.py` | Python 3 and `python3-tk` |

Both read and write the same files, render the same Markdown, export identical HTML, and
share the same settings keys.

---

## What it does

**Live Markdown preview, in three modes** — the preview is on the first time you run it, so
you do not have to discover it. `F9` toggles it, and that choice is remembered.

- *Rendered, read-only* — the default. Links open in your browser.
- *Click to jump to source* — click any heading, list item or table row and the caret moves
  to the line that produced it.
- *Rendered, editable* — type onto the rendered page; your edits are converted back into
  Markdown and the editor updates to match.

**Tabs** — several documents open at once, with scroll arrows when they overflow the strip.

**Appearance** — light, dark, or follow the desktop, plus nine palettes (Solarized Light and
Dark, Monokai, Nord, Gruvbox Dark, Sepia, High Contrast). Body text meets the 4.5:1
accessibility contrast ratio in every one. Zoom applies to the editor and preview together.

**The manual, inside the program** — press `F1` anywhere for the full owner's manual,
searchable. Nothing to download.

**HTML export**, and HTML pasted into the editor is converted to Markdown.

**Two games**, hidden in the About dialog. `Ctrl+Shift+B`, or type `!bunnies` in the editor.

### Design rules it follows

- Your file's text encoding and line endings are preserved exactly as found.
- Nothing is written to disk unless you save it. Settings are the one exception.

---

## Getting started

**Windows** — download `MiniPad.exe` and run it. There is nothing to install and nothing
to place beside it; the icon, manual and license are all inside the executable.

**Linux** — install Tk if you do not have it, then run the script:

```bash
sudo apt install python3-tk      # Debian, Ubuntu, Mint
python3 minipad.py
```

Either build accepts file paths on the command line:

```bash
python3 minipad.py notes.md todo.md
```

### A few keys worth knowing

| Key | Does |
|---|---|
| `F1` | The manual, searchable, inside the program |
| `F9` | Show or hide the live preview |
| `Ctrl+T` | New tab |
| `Ctrl+Shift+B` | Bunnies |

The full keyboard reference is in section 13 of the manual.

---

## Building from source

**Windows** — no .NET SDK required. The build uses the C# compiler that ships with
.NET Framework 4, which is present on every Windows install:

```powershell
.\build.ps1
```

If PowerShell refuses to run the script, its execution policy is blocking `.ps1` files.
Run it for that one process instead, which changes no system setting:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Then check that it actually starts:

```powershell
.\verify-launch.ps1
```

This launches the built executable and fails if it exits, shows no window, or raises a .NET
crash dialog. It takes about five seconds and exists because 1.11 shipped a crash that stopped
MiniPad starting at all — past a review that read every line and ran harnesses over conversion,
layout and round-tripping, none of which ever started the process. Run it before every release.
The reasoning is in `DECISIONS.md`.

**Linux** — nothing to build. `minipad.py` is the program.

---

## Documentation

| File | What it is |
|---|---|
| [`MANUAL.md`](MANUAL.md) | The full owner's manual — 23 sections, also readable in-program with `F1` |
| [`CHANGELOG.md`](CHANGELOG.md) | Version history |
| [`DECISIONS.md`](DECISIONS.md) | Ideas considered and deliberately **not** built, and why |
| [`LICENSE.txt`](LICENSE.txt) | Licence and terms of use |

---

## Licence

**This is not open-source software.** It is free to use, privately or commercially, on as
many machines as you like, and you may pass the unmodified program on to others at no
charge. But the source may **not** be copied, reused, adapted, or published elsewhere, and
modified or derived versions may not be distributed.

Reading the source to understand how it works is fine. Copying it into something else is
not. See [`LICENSE.txt`](LICENSE.txt) for the exact terms.

Copyright © 2026 Tim Broussard. All rights reserved.

---

## Credit

Designed by **Tim Broussard**.
Built in conversation with **Legion** (Claude, Anthropic), working under his direction.

Neither line is decoration. The design, the decisions and the direction are Tim's; the code,
the testing and the manual were written in dialogue with an AI assistant. Saying only one of
those would misrepresent how it was made.

</details>
