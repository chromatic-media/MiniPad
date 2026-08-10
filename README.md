# MiniPad

A small plain-text editor that also renders Markdown, live, as you type.

It opens and saves ordinary `.txt` and `.md` files, and it never changes anything in your
file that you did not change yourself. No network access, no telemetry, no installer, and
no third-party libraries — it calls only what the operating system already provides.

**Version 1.11**

---

## Two builds, one design

| Build | File | Requires |
|---|---|---|
| **Windows** | `MiniPad.exe` | Nothing — a self-contained 308 KB executable (.NET Framework 4, built into Windows 8 and newer) |
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
