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
