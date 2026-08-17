# MiniPad Design Decisions

Ideas that were considered and **not** built, and a few that were built only after an argument
worth remembering. `CHANGELOG.md` records what MiniPad does; this file records what it
deliberately doesn't, and why.

It exists so that a proposal already thought through doesn't have to be thought through from
scratch six months later, and so that anyone new to the project — or a future Claude session —
can see the reasoning rather than guessing at it from absence.

**Re-raising something here is not a mistake.** Circumstances change, and a decision made under
one set of constraints deserves to be re-examined under another. That is what the
*"What would change this"* line on each entry is for: it names the specific thing that would
make the old answer wrong. If that thing has happened, reopen it. If it hasn't, the answer stands.

Designed by Tim Broussard. Built in conversation with Legion (Claude, Anthropic).

---

## The test most of these come down to

MiniPad's charter is **non-intrusive, one click, no nonsense**. In practice that resolves to a
single question, asked of every proposed feature:

> **What does the person who never wants this have to do about it?**
>
> Nothing, they never notice it → the feature is free, judge it on its own merits.
> Close it, dismiss it, uncheck it, wait for it → the cost is paid by everyone, forever, to
> benefit a few.

A feature is not judged by how much its users like it. It is judged by its cost to the people
who never use it.

---

## Who frees a replaced `Font` — **by reference identity, never by value** (8/16/26)

**Background:** 1.10 leaked a `Font` every time a control's font was replaced — and
`StyleEditor` runs on every theme change and every zoom step, so it was one leaked font per
document per press of `Ctrl+Plus`. 1.11 fixed that by freeing the old font after assigning
the new one. **That fix introduced a crash that stopped MiniPad starting at all**, and 1.11.1
is the correction.

**Decided:** a replaced font is still freed — the leak fix stands — but *which* font gets
freed is decided by **`ReferenceEquals` on what the control is holding afterwards**, never by
assuming the assignment took. This lives in one helper, `ReplaceFont`, and both callers
(`StyleEditor` and `ApplyEditorFont`) go through it.

**Why, and this is the part worth remembering:**

- **`Control.Font` compares by VALUE, not identity.** Assigning a `Font` that merely *equals*
  the one already set is a silent no-op: WinForms keeps the original instance and discards
  the one you handed it. Freeing "the old one" therefore frees the font the control is *still
  using*, and every later repaint draws with released memory.
- **The symptom appears nowhere near the mistake.** It surfaces as
  `ArgumentException: Parameter is not valid` thrown from `Font.ToHfont()` deep inside
  `Control.get_FontHandle()` — a stack with none of MiniPad's own code in it. Nothing points
  at the line that actually did the damage.
- **It needed only one tab, and fired during startup**, because applying the theme restyles
  the editor a second time at an unchanged size. The value is identical, so the assignment
  no-ops, so the live font is freed — before the window is ever shown.
- **A value comparison in the guard would silently reintroduce it.** `Font` overloads neither
  `==` nor `Equals` to reference semantics, so `if (box.Font != replacement)` reads correctly
  and is always false. The `ReferenceEquals` call is load-bearing and must not be "tidied".

**A second instance of the same class of bug, fixed at the same time:** the font chooser
handed **one** `Font` object to every open editor. Restyling any one of them freed it for all
of them. Each editor now gets its own instance — a control's font is owned by that control
alone, not shared across documents.

**The general lesson:** *"I assigned it, therefore it holds it"* is an assumption, not a
fact. Before freeing anything on the grounds that something else replaced it, check that the
replacement actually happened.

**Not affected, and not an oversight:** the Linux build hands Tk plain `(family, size)`
tuples. Tk owns and frees them; there is no manual disposal to get wrong.

**What would change this:** moving the Windows build off WinForms. The behaviour being worked
around is `Control.Font`'s value-equality semantics specifically.

---

## Whether a release must launch the program — **yes, and it now has a script** (8/16/26)

**Decided:** `verify-launch.ps1` runs after `build.ps1` and before a release is committed. It
starts the built executable, waits, and fails if the process exits, if no window title
appears, or if a .NET crash dialog is on screen.

**Why:** 1.11 was a bug-fix release produced by a line-by-line read of ~8,800 lines across
both builds. It found and fixed twenty-two real defects, four verified failing-then-passing
against the shipped binary. **It also shipped a crash that prevented the program starting,
and nothing in that process could have caught it** — because every harness loaded the
assembly and called into it, and not one of them started the process.

> **A test that calls your code is not a test that your program runs.**

The gap was invisible precisely *because* the testing was thorough: reflection harnesses,
headless layout checks, HTML round-trips and cross-build parity all passed, which felt like
coverage. They cover units. The one thing never exercised was the whole.

**Cost:** about five seconds per release. It is the cheapest check in the project and the
only one that would have caught the worst bug in it.

**What would change this:** nothing. If anything, it should grow — a second pass that opens
a file, changes the theme, and zooms would have caught this bug from three more directions.

---

## Tooltips / a feature-discovery surface — **declined** (7/31/26)

**Proposed:** tooltips, and possibly a dedicated hint box, to make MiniPad's features more
discoverable — the underlying goal being "highlight the features without being obnoxious."

**Declined.** No mechanism was built.

**Why:**

- **MiniPad cannot know it is your first run.** By design it stores nothing unless you turn on
  portable settings, so every launch looks identical to the first. Anything built to "show once"
  therefore shows *every single time*, or else requires writing the very config file the program
  exists to avoid. This rules out splash screens, welcome tours, first-run wizards and
  "Did you know?" bars on architecture, not on taste. That is the decisive point.
- **Clippy's actual failure was not offering help** — it was arriving uninvited, guessing intent
  and guessing wrong, interrupting work in progress, and requiring dismissal. Dismissal is a cost,
  levied on everyone, every time.
- **The two mechanisms already in place cover most of it.** Menu items display their own keyboard
  shortcuts, which teaches on the user's schedule at the moment they are already looking. The full
  manual is embedded in the program under **About > Help** (`F1`), offline, always.
- Tooltips on self-explanatory items (*Open*, *Save*) are not help but noise, and they spend the
  credibility of the two or three hints that would have earned their place.

**Considered but not needed:** a hint line in the existing status bar, shown only while a menu is
open. Stateless, costs no screen space, and unlike floating tooltips it works for keyboard users.
Rejected for now only because it is a mechanism, and the cheaper fix comes first — see below.

**What would change this:** evidence that real users are missing real features. The honest first
step is not a feature at all but an **audit**: check that everything MiniPad does appears somewhere
a person would look for it, and that its menu label says what it does. If a menu item needs a
tooltip to explain it, the label is the bug, and renaming costs nothing and helps everyone —
including the people who never hover. Only if that audit comes back clean is a hint mechanism
worth revisiting.

---

## Windows right-click "Open with MiniPad" shell menu — **declined** (7/31/26)

**Proposed:** add MiniPad to the Windows Explorer right-click menu for files, the way Notepad
appears.

**Declined by Tim** on charter grounds: *"its niche is a non-intrusive, one-click, no-nonsense
proggy. I'm violating that charter to accommodate less than 5% of the users."*

**Why:** it is technically modest — registry entries under the file-type or `*` shell keys, no DLL
required for a plain verb — but it is an installer-shaped change to a program that is otherwise a
single portable executable you can run from a USB stick and delete without trace. It writes to the
machine on behalf of a minority, and something has to clean it up afterwards.

**What would change this:** MiniPad growing a real installer for other reasons. Shell integration
is a reasonable *optional checkbox in an installer that already exists*; it is not a reason to
build one.

---

## Splash screen asking whether to enable persistent settings — **declined; the feature shipped without it** (7/31/26)

**Proposed:** on launch, offer the user a choice between a default session and one that saves
settings to a file beside the executable.

**Portable settings shipped. The prompt did not.** The presence of `settings.txt` next to the
program is the entire switch, and **Format > Portable Settings…** toggles it.

**Why:** a launch-time prompt is paid for by every user on every launch, to configure something
that matters to people running from a USB stick or a locked-down machine and to nobody else. It
also collides with "one click" directly — it puts a question between the user and their document.
The same outcome was available with no prompt at all.

**What would change this:** nothing foreseeable. If portable mode proves undiscoverable, the fix
is where it is *documented*, not a prompt.

---

## Which preferences persist — **all of them** (7/31/26)

**Decided:** font family and size, preview mode, word wrap, and window size and position, rather
than theme alone.

**Why:** remembering the theme but forgetting the font reads as a fault rather than a default — the
program appears to half-remember you, keeping how it *looked* but forgetting how you *worked*.
A partial memory is more irritating than none.

---

## Captioning the preview pane — **adopted, over an initial objection** (7/31/26)

**Proposed:** label the right-hand pane "Live Preview".

**Adopted.** Recorded here because the first answer was wrong and the reasoning is worth keeping.

**Why:** the objection was that the label is redundant — the pane is visibly a preview — and costs
22px permanently. The reframing that settled it: the caption does not identify the pane, it
explains **why the screen is split at all**, which nothing else in the interface did. A new user
types on the left and sees the same words appear on the right with no stated reason.

**The general lesson:** "this is self-evident" is a judgement made by someone who already knows.
A caption that explains *why something exists* is a different thing from a label that restates what
is already visible, and only the second is redundant.

---

## Aligning every line between the panes — **out of scope** (7/31/26)

**Done:** the first line of the source now sits level with the first line of the rendering.

**Not done, and not an unfinished job:** keeping *every* line in step. `# Heading` is one source
line but renders nearly twice as tall; a four-line table renders as three rows and a rule;
` ``` ` fence markers occupy a source line and render as nothing at all. Two documents drift apart
permanently after the first heading. Per-line alignment is not achievable by adjusting padding —
it is proportional scroll syncing, a different feature with its own scrolling behaviour and its own
bugs.

**What would change this:** a decision to build scroll syncing deliberately, as a feature, with
that name on it.
