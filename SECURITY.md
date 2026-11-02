# Security Policy

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

**While this repository is private**, everyone who can see it has been invited personally,
so report it straight to the maintainer — a direct message or email is fine. There is no
need for a formal channel among people who already know each other.

**If this repository is ever made public**, use GitHub's private vulnerability reporting
instead: the **Security** tab, then **Report a vulnerability**. That opens a conversation
visible only to you and the maintainer, so a flaw is not published before it is fixed.
(That feature only exists on public repositories, which is why it is not the route today.)

Please include:

- What version you are on (`About > About`, or the version at the top of `MANUAL.md`)
- Which build — Windows `MiniPad.exe` or Linux `minipad.py`
- The steps to reproduce it, and a sample document if one is involved
- What you think an attacker could achieve

You will get a reply. This is a small project maintained by one person, so please allow a
few days rather than a few hours. If a fix is warranted, you will be credited in
`CHANGELOG.md` unless you would rather not be.

## Supported versions

| Version | Supported |
|---|---|
| 1.12 (current) | Yes |
| Anything earlier | No — please update first |

Only the latest version is maintained. There are no long-term support branches.

## What MiniPad's attack surface actually is

Being honest about this is more useful than a generic policy, because it tells you where
to look.

MiniPad makes **one kind of network connection: the update check** (Windows build, since
1.12). It is an HTTPS GET for
`https://raw.githubusercontent.com/chromatic-media/MiniPad/main/About.cs`, sent at most once
a day automatically (switch it off with **File > Check for Updates Automatically**) or when
you choose **File > Check for Updates...**. The request carries a `MiniPad/<version>`
User-Agent and nothing else: no file names, text, settings or identifier. The reply is read
only for a version number, capped at 512 KB, and never run, opened or shown. The links the
update bar opens are fixed in the program and do not come from the reply, so a forged reply
can at worst announce a version that does not exist. MiniPad never downloads or replaces
itself. The browser does the download when you click **Download**.

Apart from that, MiniPad sends no telemetry and fetches no remote content. It uses no
third-party libraries — only the .NET Framework and the Windows HTML rendering control on
Windows, and the Python standard library with Tk on Linux. So there is no dependency chain
to compromise.

What it *does* do is **parse files you open and render them**. That is the surface:

- Markdown parsing (`Markdown.cs`, and the equivalent in `minipad.py`)
- HTML-to-Markdown conversion, used when you paste HTML into the editor
  (`HtmlToMarkdown.cs`)
- Rendering the preview, which on Windows uses the system `WebBrowser` control
- Reading and writing files, including encoding and line-ending detection

**A malicious `.md` file is the realistic threat.** If you can make MiniPad execute code,
corrupt an unrelated file, or write outside the path the user chose, by getting someone to
open or paste a document, that is a vulnerability and we want to hear about it.

### Existing hardening, so you can test whether it holds

- **All document text is HTML-escaped before rendering.** `<`, `>`, `&` and `"` are
  converted to entities, so raw HTML written in a Markdown file is displayed as text, not
  executed. Script tags in a document do not run.
- **`javascript:`, `vbscript:` and `data:` URLs in links are rejected** and replaced with
  `#`.
- **Unknown URL schemes are never handed to the shell.** Only `http`, `https`, `mailto`
  and `file` are passed on; anything else is ignored.
- Preview keyboard shortcuts are disabled and script errors are suppressed.

If you find a way around any of these, that is exactly the kind of report this policy is
for.

### One behaviour to be aware of, by design

**Clicking a link in the preview can open a local file.** Relative links are resolved next
to the document you are editing, and `file://` URLs are passed to the operating system —
in both cases via the normal "open this file" mechanism, the same as double-clicking it in
Explorer.

That is intentional, so that a link to a sibling document works. But it means a document
from an untrusted source could offer you a link that launches a program sitting beside it.
**Treat links in documents you did not write with the same caution you would treat links in
an email.** MiniPad does not open anything without a deliberate click.

We consider this documented behaviour rather than a flaw, but a report showing it can be
triggered *without* a user click, or made to reach a file outside the document's own
folder, would be a genuine vulnerability.

## Out of scope

These are not vulnerabilities in MiniPad, and reports about them will be closed:

- The absence of a code signature on `MiniPad.exe`, and the SmartScreen warning that
  results. It is unsigned; that is a cost decision, not a defect.
- Settings stored in plain text at `%APPDATA%\MiniPad\settings.txt` or
  `~/.config/minipad/settings.txt`. They contain only theme, zoom and preview
  preferences — nothing sensitive is stored anywhere.
- Anything requiring an attacker who already has write access to your machine or your
  files. If they can replace `MiniPad.exe`, the editor is not what is protecting you.
- Vulnerabilities in Windows, the .NET Framework, the system HTML control, or Python and
  Tk themselves. Report those to their vendors — though do tell us if MiniPad uses them in
  a way that makes an underlying flaw exploitable when it otherwise would not be.
- Denial of service by opening an enormous or deliberately pathological file. It is a
  desktop text editor; the worst case is that you close it.

## A word on the licence

MiniPad is not open source — see [`LICENSE.txt`](LICENSE.txt). You are welcome to read the
source to investigate a security issue, and to quote the relevant lines in a report. That
is explicitly fine. Copying it into another project is not.
