#!/usr/bin/env python3
"""MiniPad - a small text editor with live Markdown preview (Linux build).

Designed by Tim Broussard. Built in conversation with Legion (Claude, Anthropic).

Runs on Ubuntu with the Python 3 standard library only:
    sudo apt install python3-tk
    python3 minipad.py [file]

The Windows build renders its preview with the Internet Explorer engine; there is
no equivalent on Linux, so this build renders Markdown directly into a Tk text
widget using styled tags. Exported HTML is identical between the two builds.
"""

import codecs
import datetime
import math
import os
import platform
import random
import re
import subprocess
import sys
import webbrowser

import tkinter as tk
from tkinter import colorchooser
from tkinter import filedialog
from tkinter import font as tkfont
from tkinter import messagebox
from tkinter import ttk

APP_NAME = "MiniPad"
VERSION = "1.09"
RELEASE_DATE = "7/31/26"
DESIGNER = "Tim Broussard"
AUTHOR = "Legion"

# "Built in conversation" rather than "written by": nothing here came from
# either side alone. Spelling out what Legion is matters more than the billing -
# without it a reader reasonably assumes a second person.
AUTHOR_NOTE = "Claude, Anthropic"
CREDIT = "Designed by %s\nBuilt in conversation with %s (%s)" % (
    DESIGNER, AUTHOR, AUTHOR_NOTE)

MARKDOWN_EXTENSIONS = (".md", ".markdown", ".mdown", ".mkd")

# Typing this in the editor opens About > Bunnies.
BUNNY_COMMAND = "!bunnies"

# The window icon: a small notepad, the same drawing as the Windows .ico,
# embedded as a base64 PNG so this stays a single file.
ICON_PNG_BASE64 = (
    "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAAXNSR0IArs4c6QAAAARnQU1BAACx"
    "jwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAF1SURBVFhHY2AYBYMVVNR3eaTVzrztXzTnP6UY"
    "ZE5p44QsdDtwgsrWCVEO+at+qyav/U8tbJ679m9p89QmdLuwgsTqmS/QDaAGDime9ZuBgYEJ3T4M"
    "YJ+zCEMzNbB19pL/DAwMguj2YQDvgrkYmqmB3fPngxwgjG4fBkiunPS/feWF/9FdB6mGaxed/Q8y"
    "lygHVDR0/f/79zfVMcjcoeeAN+/e/b97/yHZGKSfIgfsPnDsf1XLJLIxSD9FDgD5AmQIuRiknyIH"
    "UBOT5YAzF678n71oLdkYpJ8iB4CCET1eScEg/RQ5gJqYLAeMZkOQL9CzFikYpJ8iB1ATk+UAYrLh"
    "jdt3MSzDhslyACgY0eMVHSPndXyYLAdQE486gGgH1LVNeP3m7VsMAyjBP39+/1/Z1ANqFRN2QHPX"
    "5J51m3d+RTeEEnzk+KnfNc09hxgYGITQ7cMKGjsmTAEFGbUwyHJxcXEdBgYGdnS78AEWaJBRC5Nk"
    "OV0AAMTVxDrC2OeqAAAAAElFTkSuQmCC"
)

FILE_TYPES = [
    ("Text files", "*.txt"),
    ("Markdown", "*.md *.markdown"),
    ("All files", "*"),
]


# --------------------------------------------------------------------------
# Appearance: light / dark / system, named themes, custom colours and zoom
#
# A port of Theme.cs from the Windows build, deliberately kept faithful: the
# same palette names and the same settings keys, so a settings file written by
# either build means the same thing to the other.
# --------------------------------------------------------------------------


def parse_hex(colour):
    """'#rrggbb' -> (r, g, b)."""
    colour = colour.lstrip("#")
    return (int(colour[0:2], 16), int(colour[2:4], 16), int(colour[4:6], 16))


def to_hex(rgb):
    return "#%02x%02x%02x" % tuple(int(round(value)) for value in rgb)


def blend(first, second, amount):
    """Mix two colours; amount 0 is all `first`, 1 is all `second`."""
    a, b = parse_hex(first), parse_hex(second)
    return to_hex(tuple(a[n] + (b[n] - a[n]) * amount for n in range(3)))


def _luminance(colour):
    channels = []
    for value in parse_hex(colour):
        value /= 255.0
        channels.append(value / 12.92 if value <= 0.03928
                        else ((value + 0.055) / 1.055) ** 2.4)
    return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]


def contrast_ratio(first, second):
    """The WCAG contrast ratio between two colours, from 1:1 to 21:1."""
    a, b = _luminance(first), _luminance(second)
    return (max(a, b) + 0.05) / (min(a, b) + 0.05)


class Palette(object):
    """One named set of colours."""

    def __init__(self, name, dark, back, text, chrome, accent, code_back, border, dim):
        self.name = name
        self.dark = dark
        self.back = "#" + back        # editor background
        self.text = "#" + text        # editor text
        self.chrome = "#" + chrome    # menus, status bar, dialog surfaces
        self.accent = "#" + accent    # links and highlights
        self.code_back = "#" + code_back
        self.border = "#" + border
        self.dim = "#" + dim          # secondary text


class Theme(object):
    """The appearance settings, shared by every window."""

    NO_PRESET = "(follow light / dark choice)"
    MIN_ZOOM = 50
    MAX_ZOOM = 300

    mode = "system"                 # "light", "dark" or "system"
    preset_name = NO_PRESET         # NO_PRESET, or a palette name
    zoom = 100                      # percent
    custom_back = None
    custom_text = None

    # Not a colour, but this class owns settings.txt and one flag does not
    # justify a second settings file. On for a first run, so the preview is
    # found rather than hunted for; remembered after that.
    preview_on = True

    # How the program is used, rather than how it looks. These used to reset on
    # every launch, which made the editor feel like it was forgetting you - the
    # font most of all, since choosing one and losing it each morning reads as a
    # fault rather than a default.
    font_family = ""                # "" = the built-in default
    font_size = 0                   # 0  = the built-in default
    preview_mode = 0                # 0 read-only, 1 click-to-jump, 2 editable
    word_wrap = True
    window_geometry = ""            # Tk "WxH+X+Y"; "" = let Tk decide
    window_maximised = False

    PALETTES = [
        Palette("Light",           False, "ffffff", "181a1f", "f0f0f0", "0969da", "eff1f3", "d0d7de", "5a606a"),
        Palette("Dark",            True,  "181a1c", "e2e5e9", "2b2d31", "6cb6ff", "2b2d31", "3c3f44", "969ca5"),
        Palette("Solarized Light", False, "fdf6e3", "586e75", "eee8d5", "268bd2", "eee8d5", "d5cdb6", "93a1a1"),
        Palette("Solarized Dark",  True,  "002b36", "93a1a1", "073642", "268bd2", "073642", "10505f", "657b83"),
        Palette("Monokai",         True,  "272822", "f8f8f2", "3e3d32", "a6e22e", "3e3d32", "55554a", "a9a9a0"),
        Palette("Nord",            True,  "2e3440", "d8dee9", "3b4252", "88c0d0", "3b4252", "4c566a", "8f9bb0"),
        Palette("Gruvbox Dark",    True,  "282828", "ebdbb2", "3c3836", "fabd2f", "3c3836", "504945", "a89984"),
        Palette("Sepia",           False, "f4ecd8", "4b3f32", "e8dcc0", "8b5a2b", "e8dcc0", "cbbb9a", "7d6b57"),
        Palette("High Contrast",   True,  "000000", "ffffff", "000000", "ffff00", "1a1a1a", "ffffff", "d0d0d0"),
    ]

    # The desktop's own light/dark preference, asked for once and remembered.
    # Every colour lookup goes through base(), so asking the desktop each time
    # would launch a process on every repaint.
    _system_dark = None

    # ---- the palette in force ----

    @classmethod
    def preset_names(cls):
        return [cls.NO_PRESET] + [palette.name for palette in cls.PALETTES]

    @classmethod
    def find(cls, name):
        for palette in cls.PALETTES:
            if palette.name == name:
                return palette
        return None

    @classmethod
    def base(cls):
        """The palette in force, before any custom colour overrides."""
        if cls.preset_name != cls.NO_PRESET:
            preset = cls.find(cls.preset_name)
            if preset is not None:
                return preset
        dark = cls.mode == "dark" or (cls.mode == "system" and cls.system_prefers_dark())
        return cls.find("Dark" if dark else "Light")

    @classmethod
    def is_dark(cls):
        return cls.base().dark

    # ---- resolved colours ----

    @classmethod
    def editor_back(cls):
        return cls.custom_back or cls.base().back

    @classmethod
    def editor_text(cls):
        return cls.custom_text or cls.base().text

    @classmethod
    def window_back(cls):
        return cls.base().chrome

    @classmethod
    def menu_back(cls):
        return cls.base().chrome

    @classmethod
    def menu_text(cls):
        return cls.base().text

    @classmethod
    def menu_highlight(cls):
        return blend(cls.base().chrome, cls.base().accent, 0.35)

    @classmethod
    def button_back(cls):
        return blend(cls.window_back(), cls.menu_text(), 0.08)

    @classmethod
    def border(cls):
        return cls.base().border

    @classmethod
    def dim(cls):
        return cls.base().dim

    @classmethod
    def ui_dim(cls):
        """Secondary text for the window chrome, guaranteed readable.

        `dim` is the palette's own muted tone and is used verbatim in the
        preview stylesheet, where matching the palette is the point. Solarized,
        Nord, Gruvbox and Sepia keep that tone deliberately low-contrast, which
        suits a blockquote but leaves the status bar hard to read - Solarized
        Dark manages only 2.9:1. Chrome text is therefore lifted towards the
        body colour until it clears 4.5:1, without touching the palette itself.

        The lift stops at the body colour: secondary text should never be
        heavier than the text it sits beside. Solarized Light is the one
        palette that cannot reach 4.5:1 even there - its own body text manages
        4.39:1 on its chrome - so it ends up exactly as readable as the rest of
        that theme, which is the most the palette allows.
        """
        palette = cls.base()
        for step in range(0, 21):
            candidate = blend(palette.dim, palette.text, step / 20.0)
            if contrast_ratio(candidate, palette.chrome) >= 4.5:
                return candidate
        return palette.text   # the best this palette can manage

    @classmethod
    def ui_accent(cls):
        """The accent, lifted the same way, for accent text on the chrome.

        The zoom indicator uses it. Solarized Light's raw accent manages only
        3.0:1 against its own chrome, which is too faint for a status badge.
        """
        palette = cls.base()
        for step in range(0, 21):
            candidate = blend(palette.accent, palette.text, step / 20.0)
            if contrast_ratio(candidate, palette.chrome) >= 4.5:
                return candidate
        return palette.text

    @classmethod
    def accent(cls):
        return cls.base().accent

    @classmethod
    def code_back(cls):
        return cls.base().code_back

    @classmethod
    def good(cls):
        """Green for a win or a success, legible on this palette."""
        return "#5cc46e" if cls.is_dark() else "#147828"

    @classmethod
    def bad(cls):
        """Red for a loss or a failure, legible on this palette."""
        return "#ff7b72" if cls.is_dark() else "#aa1e1e"

    # ---- zoom ----

    @classmethod
    def clamp_zoom(cls, zoom):
        return max(cls.MIN_ZOOM, min(cls.MAX_ZOOM, int(zoom)))

    @classmethod
    def scale_font(cls, points):
        # floor(x + 0.5), because Python rounds halves to even and would turn
        # 10.5pt into 10 rather than 11.
        scaled = int(math.floor(points * cls.zoom / 100.0 + 0.5))
        return max(5, min(96, scaled))

    @classmethod
    def scale_pixels(cls, pixels):
        return max(1, int(pixels * cls.zoom / 100.0))

    # ---- the preview stylesheet, generated from the palette ----

    @classmethod
    def preview_css(cls):
        """Byte-for-byte the stylesheet the Windows build generates."""
        palette = cls.base()
        back = cls.editor_back()
        text = cls.editor_text()
        code = palette.code_back
        border = palette.border
        dim = palette.dim
        link = palette.accent
        size = int(math.floor(15.0 * cls.zoom / 100.0 + 0.5))

        return ("body{font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:" + str(size) + "px;"
                "line-height:1.6;color:" + text + ";background:" + back + ";margin:0;padding:24px 28px;}"
                "h1,h2,h3,h4,h5,h6{margin:24px 0 12px;line-height:1.25;font-weight:600;}"
                "h1{font-size:1.9em;border-bottom:1px solid " + border + ";padding-bottom:8px;}"
                "h2{font-size:1.45em;border-bottom:1px solid " + border + ";padding-bottom:6px;}"
                "h3{font-size:1.2em;} h4{font-size:1em;} h5,h6{font-size:.9em;color:" + dim + ";}"
                "p{margin:0 0 14px;} ul,ol{margin:0 0 14px;padding-left:26px;} li{margin:3px 0;}"
                "code{font-family:Consolas,monospace;font-size:.9em;background:" + code + ";"
                "padding:2px 5px;border-radius:4px;}"
                "pre{background:" + code + ";border:1px solid " + border + ";border-radius:6px;"
                "padding:12px 14px;overflow:auto;}"
                "pre code{background:none;padding:0;font-size:.88em;}"
                "blockquote{margin:0 0 14px;padding:2px 14px;color:" + dim + ";"
                "border-left:4px solid " + border + ";}"
                "table{border-collapse:collapse;margin:0 0 16px;}"
                "th,td{border:1px solid " + border + ";padding:6px 12px;}"
                "th{background:" + code + ";font-weight:600;}"
                "hr{border:0;border-top:1px solid " + border + ";margin:22px 0;}"
                "a{color:" + link + ";} img{max-width:100%;} del{color:" + dim + ";}")

    # ---- persistence ----

    # Portable mode is switched on by the mere PRESENCE of settings.txt beside the
    # script. There is deliberately no prompt and no first-run banner: it matters to
    # people running MiniPad from a USB stick or a locked-down machine, and everyone
    # else should never have to think about it. The Format menu item is its whole
    # advertising. Presence rather than a flag inside the file means a user can turn
    # it on with a text editor and off by deleting one file.

    @staticmethod
    def portable_settings_path():
        try:
            return os.path.join(os.path.dirname(os.path.abspath(__file__)), "settings.txt")
        except (OSError, NameError):
            return None

    @staticmethod
    def profile_settings_path():
        folder = os.environ.get("XDG_CONFIG_HOME") or os.path.join(os.path.expanduser("~"), ".config")
        return os.path.join(folder, "minipad", "settings.txt")

    @classmethod
    def is_portable(cls):
        try:
            path = cls.portable_settings_path()
            return path is not None and os.path.exists(path)
        except OSError:
            return False

    @classmethod
    def settings_path(cls):
        """Which file is actually in use - shown in About so the mode is never a mystery."""
        return cls.portable_settings_path() if cls.is_portable() else cls.profile_settings_path()

    @classmethod
    def make_portable(cls):
        """Returns None on success, or a human-readable reason it failed."""
        path = cls.portable_settings_path()
        if path is None:
            return "The program's own folder could not be determined."
        if os.path.exists(path):
            return None
        try:
            cls._write_to(path)
            return None
        except (IOError, OSError) as error:
            return str(error)

    @classmethod
    def make_not_portable(cls):
        """The caller must confirm first: this deletes a file. Settings are copied
        back to the profile before it goes, so switching off never loses a choice."""
        path = cls.portable_settings_path()
        if path is None or not os.path.exists(path):
            return None
        try:
            profile = cls.profile_settings_path()
            folder = os.path.dirname(profile)
            if not os.path.isdir(folder):
                os.makedirs(folder)
            cls._write_to(profile)
            os.remove(path)
            return None
        except (IOError, OSError) as error:
            return str(error)

    @classmethod
    def load(cls):
        cls.mode = "system"
        cls.preset_name = cls.NO_PRESET
        cls.zoom = 100
        cls.custom_back = None
        cls.custom_text = None
        cls.preview_on = True
        cls.font_family = ""
        cls.font_size = 0
        cls.preview_mode = 0
        cls.word_wrap = True
        cls.window_geometry = ""
        cls.window_maximised = False

        try:
            path = cls.settings_path()
            if not os.path.exists(path):
                return
            with open(path, "r", encoding="utf-8", errors="replace") as handle:
                lines = handle.read().replace("\r\n", "\n").split("\n")
        except (IOError, OSError):
            return

        for line in lines:
            if "=" not in line:
                continue
            key, value = line.split("=", 1)
            key = key.strip().lower()
            value = value.strip()

            if key == "theme":
                cls.mode = value.lower() if value.lower() in ("light", "dark") else "system"
            elif key == "preset":
                cls.preset_name = value if cls.find(value) is not None else cls.NO_PRESET
            elif key == "zoom":
                try:
                    cls.zoom = cls.clamp_zoom(int(value))
                except ValueError:
                    pass
            elif key == "background":
                cls.custom_back = cls._colour_or_none(value)
            elif key == "text":
                cls.custom_text = cls._colour_or_none(value)
            elif key == "preview":
                # Anything that is not "off" leaves it on, so a damaged value
                # errs towards showing the pane rather than hiding it.
                cls.preview_on = value.lower() != "off"
            elif key == "fontfamily":
                # Not validated here. A font that exists on the machine which wrote
                # the file may be absent on this one - portable settings travel on a
                # USB stick - so it is checked at the point of use instead.
                cls.font_family = "" if value.lower() == "none" else value
            elif key == "fontsize":
                try:
                    size = float(value)
                    if 5 <= size <= 72:
                        cls.font_size = size
                except ValueError:
                    pass
            elif key == "previewmode":
                try:
                    mode = int(value)
                    if 0 <= mode <= 2:
                        cls.preview_mode = mode
                except ValueError:
                    pass
            elif key == "wordwrap":
                cls.word_wrap = value.lower() != "off"
            elif key == "window":
                # Tk's own "WxH+X+Y" form, stored verbatim. Validated on the way in
                # so a damaged line cannot make the window unopenable.
                if re.match(r"^\d+x\d+([+-]-?\d+[+-]-?\d+)?$", value):
                    cls.window_geometry = value
            elif key == "maximised":
                cls.window_maximised = value.lower() == "on"

    @staticmethod
    def _colour_or_none(value):
        if not value or value.lower() == "none":
            return None
        try:
            return to_hex(parse_hex(value))
        except (ValueError, IndexError):
            return None

    @classmethod
    def _write_to(cls, path):
        with open(path, "w", encoding="utf-8") as handle:
            handle.write("theme=%s\n" % cls.mode)
            handle.write("preset=%s\n" % ("none" if cls.preset_name == cls.NO_PRESET
                                          else cls.preset_name))
            handle.write("zoom=%d\n" % cls.zoom)
            handle.write("background=%s\n" % (cls.custom_back or "none"))
            handle.write("text=%s\n" % (cls.custom_text or "none"))
            handle.write("preview=%s\n" % ("on" if cls.preview_on else "off"))
            handle.write("fontfamily=%s\n" % (cls.font_family or "none"))
            handle.write("fontsize=%s\n" % cls.font_size)
            handle.write("previewmode=%d\n" % cls.preview_mode)
            handle.write("wordwrap=%s\n" % ("on" if cls.word_wrap else "off"))
            handle.write("window=%s\n" % (cls.window_geometry or "none"))
            handle.write("maximised=%s\n" % ("on" if cls.window_maximised else "off"))

    @classmethod
    def save(cls):
        try:
            path = cls.settings_path()
            folder = os.path.dirname(path)
            if folder and not os.path.isdir(folder):
                os.makedirs(folder)
            cls._write_to(path)
        except (IOError, OSError):
            # A read-only home directory must not stop the editor working. If the
            # portable file has become unwritable - a stick flipped to read-only
            # mid-session - fall back rather than silently losing the settings.
            try:
                if cls.is_portable():
                    profile = cls.profile_settings_path()
                    folder = os.path.dirname(profile)
                    if not os.path.isdir(folder):
                        os.makedirs(folder)
                    cls._write_to(profile)
            except (IOError, OSError):
                pass

    @classmethod
    def set_font(cls, family, size):
        cls.font_family = family or ""
        cls.font_size = size
        cls.save()

    @classmethod
    def set_preview_mode(cls, mode):
        if not 0 <= mode <= 2 or cls.preview_mode == mode:
            return
        cls.preview_mode = mode
        cls.save()

    @classmethod
    def set_word_wrap(cls, wrap):
        if cls.word_wrap == wrap:
            return
        cls.word_wrap = wrap
        cls.save()

    @classmethod
    def set_window(cls, geometry, maximised):
        cls.window_geometry = geometry or ""
        cls.window_maximised = maximised
        cls.save()

    @classmethod
    def set_mode(cls, mode):
        cls.mode = mode
        if mode == "system":
            cls._system_dark = None     # ask the desktop again
        cls.save()

    @classmethod
    def set_preset(cls, name):
        cls.preset_name = name if (name != cls.NO_PRESET and cls.find(name)) else cls.NO_PRESET
        cls.save()

    @classmethod
    def set_zoom(cls, zoom):
        cls.zoom = cls.clamp_zoom(zoom)
        cls.save()

    @classmethod
    def set_preview_on(cls, on):
        if cls.preview_on == on:
            return          # no need to rewrite the file to say the same thing
        cls.preview_on = on
        cls.save()

    @classmethod
    def set_custom_back(cls, colour):
        cls.custom_back = colour
        cls.save()

    @classmethod
    def set_custom_text(cls, colour):
        cls.custom_text = colour
        cls.save()

    @classmethod
    def clear_custom_colours(cls):
        cls.custom_back = None
        cls.custom_text = None
        cls.save()

    # ---- what the desktop itself prefers ----

    @classmethod
    def system_prefers_dark(cls):
        if cls._system_dark is None:
            cls._system_dark = cls._ask_desktop()
        return cls._system_dark

    @staticmethod
    def _ask_desktop():
        """True when the desktop is set to dark. Light if it will not say.

        There is no cross-desktop API for this, so ask gsettings, which GNOME,
        Cinnamon, MATE and Budgie all answer. Anything else stays light rather
        than guessing wrongly.
        """
        for key in ("color-scheme", "gtk-theme"):
            try:
                result = subprocess.run(
                    ["gsettings", "get", "org.gnome.desktop.interface", key],
                    stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=3)
            except (OSError, ValueError, subprocess.SubprocessError):
                continue
            if result.returncode != 0:
                continue
            value = result.stdout.decode("utf-8", "replace").strip().strip("'\"").lower()
            if key == "color-scheme":
                if value == "prefer-dark":
                    return True
                if value == "prefer-light":
                    return False
                continue    # "default" means unset; let the theme name decide
            return "dark" in value
        return False


# --------------------------------------------------------------------------
# Painting the theme onto Tk widgets
#
# Tk has no theme engine, so every widget class is coloured by hand. A widget
# carrying _minipad_keep_colours is left alone - the Minesweeper board and the
# appearance preview colour themselves.
# --------------------------------------------------------------------------


def _configure(widget, **options):
    """Set what this widget understands and ignore the rest.

    Tk rejects the whole call if any one option is wrong for the widget - a
    Button has no selectcolor, a Listbox has no insertbackground - so a single
    bad option would silently discard every good one with it and leave the
    widget its default grey. On rejection, set them one at a time instead.
    """
    try:
        widget.configure(**options)
        return
    except tk.TclError:
        pass
    for name, value in options.items():
        try:
            widget.configure(**{name: value})
        except tk.TclError:
            pass


# Secondary text colours, held by name rather than by value so that a widget
# asking for "dim" is still dim after the palette changes underneath it.
INK_ROLES = {
    "dim": Theme.ui_dim,
    "accent": Theme.accent,
    "good": Theme.good,
    "bad": Theme.bad,
}


def ink_role(widget, role):
    """Mark a widget as wanting a secondary colour, and return it."""
    widget._minipad_ink = role
    return widget


def apply_theme_to(widget):
    """Colour a window and everything inside it."""
    if getattr(widget, "_minipad_keep_colours", False):
        return

    back = Theme.window_back()
    role = getattr(widget, "_minipad_ink", None)
    ink = INK_ROLES[role]() if role in INK_ROLES else Theme.menu_text()
    kind = widget.winfo_class()

    if kind in ("Tk", "Toplevel", "Frame", "Panedwindow"):
        _configure(widget, background=back)
    elif kind == "Labelframe":
        _configure(widget, background=back, foreground=ink,
                   highlightbackground=Theme.border())
    elif kind == "Label":
        _configure(widget, background=back, foreground=ink)
    elif kind == "Button":
        _configure(widget, background=Theme.button_back(), foreground=ink,
                   activebackground=Theme.menu_highlight(), activeforeground=ink,
                   highlightbackground=back, disabledforeground=Theme.ui_dim())
    elif kind in ("Radiobutton", "Checkbutton"):
        # These sit on a panel rather than standing proud of it, so they take
        # the panel's own background instead of the raised button colour.
        _configure(widget, background=back, foreground=ink,
                   activebackground=back, activeforeground=ink,
                   highlightbackground=back, selectcolor=Theme.editor_back(),
                   disabledforeground=Theme.ui_dim())
    elif kind in ("Text", "Entry", "Listbox", "Spinbox"):
        _configure(widget, background=Theme.editor_back(), foreground=Theme.editor_text(),
                   insertbackground=Theme.editor_text(),
                   selectbackground=Theme.menu_highlight(), selectforeground=ink,
                   highlightbackground=Theme.border())
    elif kind == "Scrollbar":
        _configure(widget, background=Theme.button_back(), troughcolor=back,
                   activebackground=Theme.menu_highlight(),
                   highlightbackground=back, borderwidth=1)
    elif kind == "Canvas":
        _configure(widget, background=back, highlightbackground=back)
    elif kind == "Scale":
        _configure(widget, background=back, foreground=ink, troughcolor=Theme.editor_back(),
                   activebackground=Theme.menu_highlight(), highlightbackground=back)
    elif kind == "Menu":
        _configure(widget, background=Theme.menu_back(), foreground=ink,
                   activebackground=Theme.menu_highlight(), activeforeground=ink,
                   selectcolor=Theme.accent(), disabledforeground=Theme.ui_dim())

    for child in widget.winfo_children():
        apply_theme_to(child)


def apply_ttk_theme(root):
    """Colour the ttk widgets, which ignore ordinary Tk colour options."""
    style = ttk.Style(root)
    try:
        # The stock themes paint from a fixed image set and drop any colour
        # given to them; clam honours what it is told.
        style.theme_use("clam")
    except tk.TclError:
        pass

    back = Theme.window_back()
    ink = Theme.menu_text()

    style.configure("TNotebook", background=back, borderwidth=0)
    style.configure("TNotebook.Tab", background=back, foreground=Theme.ui_dim(),
                    padding=(12, 6), borderwidth=1)
    style.map("TNotebook.Tab",
              background=[("selected", Theme.editor_back())],
              foreground=[("selected", Theme.editor_text())],
              expand=[("selected", (0, 0, 0, 0))])
    style.configure("TFrame", background=back)
    style.configure("TCombobox", fieldbackground=Theme.editor_back(),
                    background=Theme.button_back(), foreground=Theme.editor_text(),
                    arrowcolor=ink, bordercolor=Theme.border(),
                    lightcolor=back, darkcolor=back)
    style.map("TCombobox",
              fieldbackground=[("readonly", Theme.editor_back())],
              foreground=[("readonly", Theme.editor_text())],
              selectbackground=[("readonly", Theme.editor_back())],
              selectforeground=[("readonly", Theme.editor_text())])

    # The combobox drop-down is a plain Tk listbox that ttk does not own.
    root.option_add("*TCombobox*Listbox.background", Theme.editor_back())
    root.option_add("*TCombobox*Listbox.foreground", Theme.editor_text())
    root.option_add("*TCombobox*Listbox.selectBackground", Theme.menu_highlight())
    root.option_add("*TCombobox*Listbox.selectForeground", Theme.editor_text())


# --------------------------------------------------------------------------
# Markdown engine: inline spans
# --------------------------------------------------------------------------

class Span(object):
    """A run of text plus the styles that apply to it."""

    __slots__ = ("text", "styles", "url")

    def __init__(self, text, styles=frozenset(), url=None):
        self.text = text
        self.styles = frozenset(styles)
        self.url = url

    def with_style(self, style):
        return Span(self.text, self.styles | {style}, self.url)


_INLINE_PATTERNS = [
    # Escapes are matched first so \* never reads as emphasis.
    ("escape", re.compile(r"\\([\\`*_\[\]()#+\-.!|~>{}])")),
    ("code", re.compile(r"(`+)(.+?)\1", re.S)),
    ("image", re.compile(r"!\[([^\]]*)\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")),
    ("link", re.compile(r"\[([^\]]*)\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")),
    ("strong", re.compile(r"\*\*(\S(?:.*?\S)?)\*\*", re.S)),
    ("strong", re.compile(r"__(\S(?:.*?\S)?)__", re.S)),
    ("strike", re.compile(r"~~(\S(?:.*?\S)?)~~", re.S)),
    ("em", re.compile(r"\*(\S(?:.*?\S)?)\*", re.S)),
    ("em", re.compile(r"(?<![A-Za-z0-9_])_(\S(?:.*?\S)?)_(?![A-Za-z0-9_])", re.S)),
]


def parse_inline(text, styles=frozenset(), url=None):
    """Turn inline Markdown into a flat list of Span objects.

    The remainder after each match is consumed in a loop rather than by recursing,
    because recursing once per span overflowed the stack on paragraphs holding a
    couple of thousand of them.
    """
    spans = []

    while text:
        best_kind = None
        best_match = None
        for kind, pattern in _INLINE_PATTERNS:
            match = pattern.search(text)
            if match is not None and (best_match is None or match.start() < best_match.start()):
                best_kind, best_match = kind, match

        if best_match is None:
            spans.append(Span(text, styles, url))
            break

        if best_match.start() > 0:
            # Nothing matches earlier, so this leading run is plain text.
            spans.append(Span(text[:best_match.start()], styles, url))

        if best_kind == "escape":
            spans.append(Span(best_match.group(1), styles, url))
        elif best_kind == "code":
            spans.append(Span(best_match.group(2), styles | {"code"}, url))
        elif best_kind == "image":
            alt = best_match.group(1) or "image"
            spans.append(Span("[" + alt + "]", styles | {"image"}, best_match.group(2)))
        elif best_kind == "link":
            label = best_match.group(1) or best_match.group(2)
            spans.extend(parse_inline(label, styles | {"link"}, best_match.group(2)))
        elif best_kind == "strong":
            spans.extend(parse_inline(best_match.group(1), styles | {"bold"}, url))
        elif best_kind == "em":
            spans.extend(parse_inline(best_match.group(1), styles | {"italic"}, url))
        else:  # strike
            spans.extend(parse_inline(best_match.group(1), styles | {"strike"}, url))

        text = text[best_match.end():]

    return spans


# --------------------------------------------------------------------------
# Markdown engine: block structure
# --------------------------------------------------------------------------

_FENCE = re.compile(r"^\s{0,3}(?:```+|~~~+)\s*([A-Za-z0-9_+#-]*)\s*$")
_FENCE_END = re.compile(r"^\s{0,3}(?:```+|~~~+)\s*$")
_HEADING = re.compile(r"^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$")
_QUOTE = re.compile(r"^\s{0,3}>\s?")
_LIST_ITEM = re.compile(r"^\s*(?:[-*+]|\d+[.)])\s+\S")
_LIST_SPLIT = re.compile(r"^\s*(?:[-*+]|\d+[.)])\s+(.*)$")
_ORDERED = re.compile(r"^\d+[.)]\s")
_TABLE_SEPARATOR = re.compile(r"^\s*\|?\s*:?-{1,}:?\s*(?:\|\s*:?-{1,}:?\s*)*\|?\s*$")
_RULES = [
    re.compile(r"^\s{0,3}(?:\*\s*){3,}$"),
    re.compile(r"^\s{0,3}(?:-\s*){3,}$"),
    re.compile(r"^\s{0,3}(?:_\s*){3,}$"),
]


def _is_rule(line):
    return any(rule.match(line) for rule in _RULES)


def _is_table_separator(line):
    return "-" in line and _TABLE_SEPARATOR.match(line) is not None


def _indent(line):
    """Indentation in columns; a tab counts as four, so tabbed lists still nest."""
    columns = 0
    for character in line:
        if character == " ":
            columns += 1
        elif character == "\t":
            columns += 4
        else:
            break
    return columns


def _is_ordered(line):
    return _ORDERED.match(line.lstrip()) is not None


def _is_block_start(lines, i):
    line = lines[i]
    if _FENCE.match(line) or _HEADING.match(line) or _QUOTE.match(line):
        return True
    if _is_rule(line) or _LIST_ITEM.match(line):
        return True
    if "|" in line and i + 1 < len(lines) and _is_table_separator(lines[i + 1]):
        return True
    return False


def parse_blocks(text, line_offset=0):
    """Parse Markdown into a list of block dictionaries.

    Every block records the source line it started on, so the preview can map a
    click back to the matching place in the editor.
    """
    if text.startswith("\ufeff"):
        text = text[1:]  # a stray BOM would stop the first line matching any block
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    blocks = []
    i = 0

    while i < len(lines):
        line = lines[i]
        start = line_offset + i

        if not line.strip():
            i += 1
            continue

        fence = _FENCE.match(line)
        if fence:
            language = fence.group(1)
            i += 1
            collected = []
            while i < len(lines) and not _FENCE_END.match(lines[i]):
                collected.append(lines[i])
                i += 1
            if i < len(lines):
                i += 1  # closing fence
            blocks.append({"type": "code", "lang": language, "line": start,
                           "text": "\n".join(collected)})
            continue

        heading = _HEADING.match(line)
        if heading:
            blocks.append({
                "type": "heading",
                "level": len(heading.group(1)),
                "line": start,
                "spans": parse_inline(heading.group(2)),
            })
            i += 1
            continue

        if _is_rule(line):
            blocks.append({"type": "rule", "line": start})
            i += 1
            continue

        if _QUOTE.match(line):
            quote_start = i
            quoted = []
            while i < len(lines) and _QUOTE.match(lines[i]):
                quoted.append(_QUOTE.sub("", lines[i]))
                i += 1
            blocks.append({"type": "quote", "line": start,
                           "blocks": parse_blocks("\n".join(quoted),
                                                  line_offset + quote_start)})
            continue

        if "|" in line and i + 1 < len(lines) and _is_table_separator(lines[i + 1]):
            block, i = _parse_table(lines, i)
            block["line"] = start
            blocks.append(block)
            continue

        if _LIST_ITEM.match(line):
            block, i = _parse_list(lines, i, _indent(line))
            block["line"] = start
            blocks.append(block)
            continue

        paragraph = []
        while i < len(lines) and lines[i].strip() and not _is_block_start(lines, i):
            paragraph.append(lines[i].strip())
            i += 1
        if paragraph:
            blocks.append({"type": "paragraph", "line": start,
                           "spans": parse_inline("\n".join(paragraph))})
        else:
            i += 1  # never stall

    return blocks


def _parse_list(lines, i, base_indent):
    ordered = _is_ordered(lines[i])
    items = []

    while i < len(lines):
        line = lines[i]

        if not line.strip():
            # A blank line ends the list unless the same kind of list continues.
            nxt = i + 1
            if (nxt < len(lines) and _LIST_ITEM.match(lines[nxt])
                    and _indent(lines[nxt]) >= base_indent
                    and _is_ordered(lines[nxt]) == ordered):
                i += 1
                continue
            break

        # Switching between bulleted and numbered markers starts a new list.
        if (not _LIST_ITEM.match(line) or _indent(line) < base_indent
                or _is_ordered(line) != ordered):
            break

        item_indent = _indent(line)
        content = [_LIST_SPLIT.match(line).group(1)]
        i += 1
        nested = []

        while i < len(lines) and lines[i].strip():
            if _LIST_ITEM.match(lines[i]):
                if _indent(lines[i]) >= item_indent + 2:
                    child, i = _parse_list(lines, i, _indent(lines[i]))
                    nested.append(child)
                else:
                    break
            elif _indent(lines[i]) >= item_indent + 2:
                content.append(lines[i].strip())  # lazy continuation
                i += 1
            else:
                break

        items.append({"spans": parse_inline("\n".join(content)), "blocks": nested})

    return {"type": "list", "ordered": ordered, "items": items}, i


def _split_row(line):
    trimmed = line.strip()
    if trimmed.startswith("|"):
        trimmed = trimmed[1:]
    if trimmed.endswith("|") and not trimmed.endswith("\\|"):
        trimmed = trimmed[:-1]
    return [cell.strip() for cell in trimmed.split("|")]


def _parse_table(lines, i):
    header = _split_row(lines[i])
    specs = _split_row(lines[i + 1])
    i += 2

    align = []
    for spec in specs:
        left = spec.startswith(":")
        right = spec.endswith(":")
        if left and right:
            align.append("center")
        elif right:
            align.append("right")
        elif left:
            align.append("left")
        else:
            align.append(None)

    rows = []
    while i < len(lines) and lines[i].strip() and "|" in lines[i]:
        cells = _split_row(lines[i])
        cells += [""] * (len(header) - len(cells))
        rows.append(cells[:len(header)])
        i += 1

    return {"type": "table", "header": header, "align": align, "rows": rows}, i


# --------------------------------------------------------------------------
# HTML export - kept byte-for-byte compatible with the Windows build
# --------------------------------------------------------------------------

_CSS = (
    "body{font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:15px;line-height:1.6;"
    "color:#24292f;background:#ffffff;margin:0;padding:24px 28px;}"
    "h1,h2,h3,h4,h5,h6{margin:24px 0 12px;line-height:1.25;font-weight:600;}"
    "h1{font-size:1.9em;border-bottom:1px solid #d8dee4;padding-bottom:8px;}"
    "h2{font-size:1.45em;border-bottom:1px solid #d8dee4;padding-bottom:6px;}"
    "h3{font-size:1.2em;} h4{font-size:1em;} h5,h6{font-size:.9em;color:#57606a;}"
    "p{margin:0 0 14px;} ul,ol{margin:0 0 14px;padding-left:26px;} li{margin:3px 0;}"
    "code{font-family:Consolas,monospace;font-size:.9em;background:#eff1f3;"
    "padding:2px 5px;border-radius:4px;}"
    "pre{background:#f6f8fa;border:1px solid #d8dee4;border-radius:6px;padding:12px 14px;"
    "overflow:auto;} pre code{background:none;padding:0;font-size:.88em;}"
    "blockquote{margin:0 0 14px;padding:2px 14px;color:#57606a;border-left:4px solid #d0d7de;}"
    "table{border-collapse:collapse;margin:0 0 16px;}"
    "th,td{border:1px solid #d0d7de;padding:6px 12px;}"
    "th{background:#f6f8fa;font-weight:600;}"
    "tr:nth-child(even) td{background:#fbfcfd;}"
    "hr{border:0;border-top:1px solid #d8dee4;margin:22px 0;}"
    "a{color:#0969da;} img{max-width:100%;} del{color:#82878d;}"
)


def _escape(text):
    return (text.replace("&", "&amp;").replace("<", "&lt;")
                .replace(">", "&gt;").replace('"', "&quot;"))


def _safe_url(url):
    if re.match(r"^\s*(?:javascript|vbscript|data)\s*:", url, re.I):
        return "#"
    return _escape(url.strip())


def _spans_to_html(spans):
    out = []
    for span in spans:
        text = _escape(span.text).replace("\n", "<br />\n")
        if "code" in span.styles:
            text = "<code>" + text + "</code>"
        if "bold" in span.styles:
            text = "<strong>" + text + "</strong>"
        if "italic" in span.styles:
            text = "<em>" + text + "</em>"
        if "strike" in span.styles:
            text = "<del>" + text + "</del>"
        if "image" in span.styles:
            text = '<img src="%s" alt="%s" />' % (_safe_url(span.url or ""), _escape(span.text.strip("[]")))
        elif "link" in span.styles and span.url:
            text = '<a href="%s">%s</a>' % (_safe_url(span.url), text)
        out.append(text)
    return "".join(out)


def _blocks_to_html(blocks):
    html = []
    for block in blocks:
        kind = block["type"]
        if kind == "heading":
            level = str(block["level"])
            html.append("<h%s>%s</h%s>\n" % (level, _spans_to_html(block["spans"]), level))
        elif kind == "paragraph":
            html.append("<p>%s</p>\n" % _spans_to_html(block["spans"]))
        elif kind == "code":
            css = ' class="lang-%s"' % _escape(block["lang"]) if block["lang"] else ""
            html.append("<pre><code%s>%s\n</code></pre>\n" % (css, _escape(block["text"])))
        elif kind == "rule":
            html.append("<hr />\n")
        elif kind == "quote":
            html.append("<blockquote>\n%s</blockquote>\n" % _blocks_to_html(block["blocks"]))
        elif kind == "list":
            tag = "ol" if block["ordered"] else "ul"
            html.append("<%s>\n" % tag)
            for item in block["items"]:
                html.append("<li>" + _spans_to_html(item["spans"]))
                if item["blocks"]:
                    html.append("\n" + _blocks_to_html(item["blocks"]))
                html.append("</li>\n")
            html.append("</%s>\n" % tag)
        elif kind == "table":
            html.append("<table>\n<thead><tr>")
            for index, cell in enumerate(block["header"]):
                html.append("<th%s>%s</th>" % (_style(block["align"], index),
                                               _spans_to_html(parse_inline(cell))))
            html.append("</tr></thead>\n<tbody>\n")
            for row in block["rows"]:
                html.append("<tr>")
                for index, cell in enumerate(row):
                    html.append("<td%s>%s</td>" % (_style(block["align"], index),
                                                   _spans_to_html(parse_inline(cell))))
                html.append("</tr>\n")
            html.append("</tbody></table>\n")
    return "".join(html)


def _style(align, index):
    if index < len(align) and align[index]:
        return ' style="text-align:%s"' % align[index]
    return ""


def markdown_to_html(text, css=None):
    """Render a whole page. Pass Theme.preview_css() to follow the theme."""
    return ('<!DOCTYPE html>\n<html><head><meta charset="utf-8" />'
            '<style>' + (css if css is not None else _CSS) + '</style></head><body>\n'
            + _blocks_to_html(parse_blocks(text))
            + '</body></html>')


# --------------------------------------------------------------------------
# Documents and the tab strip
#
# Every document owns its own Text widget rather than sharing one between tabs.
# A Text widget holds its undo stack, caret and scroll position internally and
# none of the three can be lifted out and put back, so swapping text through a
# single widget would silently lose all of them on every tab change.
# --------------------------------------------------------------------------


class Document(object):
    def __init__(self, host, untitled_number):
        self.untitled_number = untitled_number
        self.path = None
        self.dirty = False
        self.encoding = "utf-8"
        self.newline = "\n"

        self.frame = tk.Frame(host)
        self.editor = tk.Text(self.frame, undo=True, wrap=tk.WORD,
                              padx=6, pady=4, relief=tk.FLAT)
        self.vscroll = tk.Scrollbar(self.frame, command=self.editor.yview)
        self.editor.configure(yscrollcommand=self.vscroll.set)
        self.hscroll = tk.Scrollbar(self.frame, orient=tk.HORIZONTAL,
                                    command=self.editor.xview)
        self.vscroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.editor.pack(side=tk.TOP, fill=tk.BOTH, expand=True)

    @property
    def name(self):
        if self.path is not None:
            return os.path.basename(self.path)
        return "Untitled" if self.untitled_number <= 1 else "Untitled %d" % self.untitled_number

    @property
    def tab_text(self):
        return ("*" if self.dirty else "") + self.name

    @property
    def is_unused(self):
        """A tab nobody has touched, which opening a file may reuse."""
        return (self.path is None and not self.dirty
                and not self.editor.get("1.0", "end-1c").strip())

    def is_same_file(self, path):
        if self.path is None or path is None:
            return False
        try:
            return os.path.samefile(self.path, path)
        except OSError:
            return os.path.abspath(self.path) == os.path.abspath(path)

    def set_wrap(self, wrap):
        self.editor.configure(wrap=tk.WORD if wrap else tk.NONE)
        if wrap:
            self.hscroll.pack_forget()
        else:
            self.hscroll.pack(side=tk.BOTTOM, fill=tk.X)
            self.editor.configure(xscrollcommand=self.hscroll.set)


class TabStrip(object):
    """The row of tabs, drawn on a canvas.

    ttk.Notebook was the obvious choice and was rejected: it has no close
    button, and adding one means creating image elements and rewriting the tab
    layout inside a ttk theme. Drawing the strip is less code and themes exactly
    like everything else here.

    Layout, left to right:  [<] [ tab ][ tab ][ tab ] [>] [+]
    The two arrows appear only when the tabs do not all fit.
    """

    MIN_WIDTH = 96
    MAX_WIDTH = 240
    NEW_WIDTH = 30
    CLOSE_SIZE = 14
    ARROW_WIDTH = 24

    def __init__(self, parent, on_select, on_close, on_new):
        self.on_select = on_select
        self.on_close = on_close
        self.on_new = on_new
        self.labels = []
        self.selected = -1
        self.scroll = 0
        # What the mouse is over: None, "tab", "close", "left", "right", "new".
        self.hover_part = None
        self.hover_tab = -1

        self.canvas = tk.Canvas(parent, height=30, highlightthickness=0, takefocus=0)
        # It paints every pixel of itself from the palette, so the generic
        # theming walk must not repaint it as a plain panel.
        self.canvas._minipad_keep_colours = True

        self.canvas.bind("<Button-1>", self.on_click)
        self.canvas.bind("<Button-2>", self.on_middle_click)
        self.canvas.bind("<Motion>", self.on_motion)
        self.canvas.bind("<Leave>", self.on_leave)
        # Narrowing the window can push the tab you are working in off the end,
        # so the scroll position is rechecked on every resize rather than only
        # when the tabs themselves change.
        self.canvas.bind("<Configure>", lambda event: self.on_resize())
        # X11 reports the wheel as buttons 4 and 5.
        self.canvas.bind("<Button-4>", lambda event: self.scroll_by(-1))
        self.canvas.bind("<Button-5>", lambda event: self.scroll_by(1))
        self.canvas.bind("<MouseWheel>",
                         lambda event: self.scroll_by(-1 if event.delta > 0 else 1))

    def pack(self, **options):
        self.canvas.pack(**options)

    # ---- geometry ----

    def font(self):
        return (self.canvas.tk.call("font", "actual", "TkDefaultFont", "-family"),
                Theme.scale_font(9))

    def height(self):
        return max(26, Theme.scale_font(9) * 2 + 12)

    def width(self):
        try:
            return max(1, self.canvas.winfo_width())
        except tk.TclError:
            return 1

    def strip_width(self):
        """Everything left of the new-tab button: the tabs and their arrows."""
        return max(0, self.width() - self.NEW_WIDTH)

    def tab_width(self):
        if not self.labels:
            return self.MIN_WIDTH
        even = self.strip_width() // max(1, len(self.labels))
        return max(self.MIN_WIDTH, min(self.MAX_WIDTH, even))

    def total_tabs_width(self):
        return len(self.labels) * self.tab_width()

    def overflowing(self):
        """True when the tabs cannot all be shown at once."""
        return self.total_tabs_width() > self.strip_width()

    def tabs_left(self):
        return self.ARROW_WIDTH if self.overflowing() else 0

    def viewport_width(self):
        reserved = self.ARROW_WIDTH * 2 if self.overflowing() else 0
        return max(0, self.strip_width() - reserved)

    def max_scroll(self):
        return max(0, self.total_tabs_width() - self.viewport_width())

    def clamp_scroll(self):
        self.scroll = max(0, min(self.max_scroll(), self.scroll))

    def can_scroll_left(self):
        return self.overflowing() and self.scroll > 0

    def can_scroll_right(self):
        return self.overflowing() and self.scroll < self.max_scroll()

    def ensure_visible(self, index):
        if index < 0 or index >= len(self.labels):
            return
        width = self.tab_width()
        left = index * width
        if left - self.scroll < 0:
            self.scroll = left
        elif left + width - self.scroll > self.viewport_width():
            self.scroll = left + width - self.viewport_width()
        self.clamp_scroll()

    def scroll_by(self, tabs):
        """Scroll by whole tabs, so a click always lands somewhere sensible."""
        before = self.scroll
        self.scroll += tabs * self.tab_width()
        self.clamp_scroll()
        if self.scroll != before:
            self.redraw()
        return "break"

    def tab_left(self, index):
        return self.tabs_left() + index * self.tab_width() - self.scroll

    def close_box(self, index):
        left = self.tab_left(index) + self.tab_width() - self.CLOSE_SIZE - 7
        top = (self.height() - self.CLOSE_SIZE) // 2
        return (left, top, left + self.CLOSE_SIZE, top + self.CLOSE_SIZE)

    def left_arrow_box(self):
        return (0, 0, self.ARROW_WIDTH, self.height())

    def right_arrow_box(self):
        return (self.strip_width() - self.ARROW_WIDTH, 0,
                self.strip_width(), self.height())

    def hit(self, x, y):
        """Returns (index, part) where part is one of the hover_part names."""
        if x >= self.width() - self.NEW_WIDTH:
            return (-1, "new")
        if self.overflowing():
            if x < self.ARROW_WIDTH:
                return (-1, "left")
            if x >= self.strip_width() - self.ARROW_WIDTH:
                return (-1, "right")
        if not (self.tabs_left() <= x < self.tabs_left() + self.viewport_width()):
            return (-1, None)

        width = self.tab_width()
        index = (x - self.tabs_left() + self.scroll) // width
        if index < 0 or index >= len(self.labels):
            return (-1, None)
        left, top, right, bottom = self.close_box(index)
        if left <= x <= right and top <= y <= bottom:
            return (index, "close")
        return (index, "tab")

    # ---- state ----

    def set_tabs(self, labels, selected):
        self.labels = list(labels)
        self.selected = selected
        self.clamp_scroll()
        self.ensure_visible(selected)
        self.redraw()

    def on_resize(self):
        self.clamp_scroll()
        self.ensure_visible(self.selected)
        self.redraw()

    # ---- drawing ----

    def redraw(self):
        canvas = self.canvas
        try:
            canvas.delete("all")
        except tk.TclError:
            return

        height = self.height()
        canvas.configure(height=height, background=Theme.window_back())
        width = self.width()
        tab_width = self.tab_width()
        font = self.font()

        canvas.create_line(0, height - 1, width, height - 1, fill=Theme.border())

        viewport_left = self.tabs_left()
        viewport_right = viewport_left + self.viewport_width()

        for index, label in enumerate(self.labels):
            left = self.tab_left(index)
            right = left + tab_width
            # Clip to the viewport so no tab bleeds over an arrow.
            if right <= viewport_left or left >= viewport_right:
                continue
            active = index == self.selected
            hovered = index == self.hover_tab and self.hover_part in ("tab", "close")

            fill = (Theme.editor_back() if active
                    else Theme.menu_highlight() if hovered
                    else Theme.window_back())
            canvas.create_rectangle(max(left, viewport_left), 0,
                                    min(right, viewport_right), height - 1,
                                    fill=fill, outline=fill)

            if active:
                canvas.create_rectangle(left, 0, right, 2,
                                        fill=Theme.ui_accent(), outline=Theme.ui_accent())
                canvas.create_line(left, 0, left, height - 1, fill=Theme.border())
                canvas.create_line(right - 1, 0, right - 1, height - 1, fill=Theme.border())
                # No bottom edge under the active tab: it joins the page below.
                canvas.create_line(left + 1, height - 1, right - 1, height - 1, fill=fill)
            else:
                canvas.create_line(right - 1, 6, right - 1, height - 7, fill=Theme.border())

            text_room = tab_width - self.CLOSE_SIZE - 24
            canvas.create_text(left + 9, height // 2, anchor="w",
                               text=self.shorten(label, text_room, font), font=font,
                               fill=Theme.editor_text() if active else Theme.ui_dim())

            cl, ct, cr, cb = self.close_box(index)
            if cr <= viewport_right:
                close_hot = index == self.hover_tab and self.hover_part == "close"
                if close_hot:
                    canvas.create_oval(cl, ct, cr, cb,
                                       fill=Theme.border(), outline=Theme.border())
                ink = Theme.editor_text() if (active or close_hot) else Theme.ui_dim()
                pad = 4
                canvas.create_line(cl + pad, ct + pad, cr - pad, cb - pad, fill=ink)
                canvas.create_line(cr - pad, ct + pad, cl + pad, cb - pad, fill=ink)

        # The arrows and the new-tab button are drawn last, so no tab can run
        # underneath them.
        if self.overflowing():
            self.draw_arrow(self.left_arrow_box(), True,
                            self.can_scroll_left(), self.hover_part == "left")
            self.draw_arrow(self.right_arrow_box(), False,
                            self.can_scroll_right(), self.hover_part == "right")

        left = width - self.NEW_WIDTH
        canvas.create_rectangle(left, 0, width, height - 1,
                                fill=Theme.window_back(), outline=Theme.window_back())
        if self.hover_part == "new":
            canvas.create_rectangle(left + 5, 5, width - 5, height - 6,
                                    fill=Theme.menu_highlight(), outline=Theme.menu_highlight())
        cx, cy = left + self.NEW_WIDTH // 2, height // 2
        ink = Theme.editor_text() if self.hover_part == "new" else Theme.ui_dim()
        canvas.create_line(cx - 5, cy, cx + 5, cy, fill=ink)
        canvas.create_line(cx, cy - 5, cx, cy + 5, fill=ink)
        canvas.create_line(left, height - 1, width, height - 1, fill=Theme.border())

    def draw_arrow(self, box, points_left, enabled, hovered):
        canvas = self.canvas
        left, top, right, bottom = box
        canvas.create_rectangle(left, top, right, bottom - 1,
                                fill=Theme.window_back(), outline=Theme.window_back())
        if hovered and enabled:
            canvas.create_rectangle(left + 4, top + 5, right - 4, bottom - 6,
                                    fill=Theme.menu_highlight(), outline=Theme.menu_highlight())

        # Greyed rather than hidden at the ends: a control that vanishes is
        # harder to aim at than one that simply stops responding.
        ink = (Theme.border() if not enabled
               else Theme.editor_text() if hovered
               else Theme.ui_dim())
        cx, cy = (left + right) // 2, (top + bottom) // 2
        reach = 4
        tip = cx - 2 if points_left else cx + 2
        tail = cx + 2 if points_left else cx - 2
        canvas.create_line(tail, cy - reach, tip, cy, fill=ink, width=2,
                           capstyle=tk.ROUND)
        canvas.create_line(tip, cy, tail, cy + reach, fill=ink, width=2,
                           capstyle=tk.ROUND)
        canvas.create_line(left, bottom - 1, right, bottom - 1, fill=Theme.border())

    def shorten(self, label, room, font):
        """Trim a label to fit, ending in an ellipsis rather than mid-letter."""
        if room <= 0:
            return ""
        try:
            measure = tkfont.Font(root=self.canvas, font=font).measure
        except tk.TclError:
            return label
        if measure(label) <= room:
            return label
        for length in range(len(label) - 1, 0, -1):
            candidate = label[:length] + "…"
            if measure(candidate) <= room:
                return candidate
        return "…"

    # ---- mouse ----

    def on_click(self, event):
        index, part = self.hit(event.x, event.y)
        if part == "new":
            self.on_new()
        elif part == "left":
            self.scroll_by(-1)
        elif part == "right":
            self.scroll_by(1)
        elif part == "close":
            self.on_close(index)
        elif part == "tab":
            self.on_select(index)
        return "break"

    def on_middle_click(self, event):
        index, part = self.hit(event.x, event.y)
        if part in ("tab", "close"):
            self.on_close(index)
        return "break"

    def on_motion(self, event):
        index, part = self.hit(event.x, event.y)
        if (index, part) == (self.hover_tab, self.hover_part):
            return
        self.hover_tab, self.hover_part = index, part
        self.redraw()

    def on_leave(self, event=None):
        if self.hover_tab == -1 and self.hover_part is None:
            return
        self.hover_tab, self.hover_part = -1, None
        self.redraw()


# --------------------------------------------------------------------------
# The Appearance window
#
# Every change applies immediately and is saved, so there is nothing to
# confirm and no Cancel button to promise something it could not deliver.
# --------------------------------------------------------------------------


class AppearanceWindow(object):
    def __init__(self, parent, on_change):
        self.on_change = on_change
        self.loading = True

        self.win = tk.Toplevel(parent)
        self.win.title("Appearance")
        self.win.transient(parent)
        self.win.resizable(False, False)

        outer = tk.Frame(self.win, padx=18, pady=14)
        outer.pack(fill=tk.BOTH, expand=True)

        tk.Label(outer, text="Appearance", font=("sans-serif", 16, "bold"),
                 anchor="w").pack(fill=tk.X)
        tk.Label(outer, text="Applies to the editor, the Markdown preview and every "
                             "window. Saved automatically.",
                 anchor="w").pack(fill=tk.X, pady=(2, 12))

        columns = tk.Frame(outer)
        columns.pack(fill=tk.X)

        # ---- mode ----
        mode_group = tk.LabelFrame(columns, text="Mode", padx=10, pady=8)
        mode_group.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.mode_var = tk.StringVar(value=Theme.mode)
        for label, value in (("Light", "light"), ("Dark", "dark"),
                             ("System  (follow the desktop)", "system")):
            tk.Radiobutton(mode_group, text=label, value=value, variable=self.mode_var,
                           anchor="w", command=self.on_mode_change).pack(fill=tk.X)

        # ---- named themes ----
        theme_group = tk.LabelFrame(columns, text="Theme", padx=10, pady=8)
        theme_group.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(12, 0))
        self.preset_box = ttk.Combobox(theme_group, state="readonly", width=26,
                                       values=Theme.preset_names())
        self.preset_box.pack(fill=tk.X)
        self.preset_box.bind("<<ComboboxSelected>>", lambda event: self.on_preset_change())
        tk.Label(theme_group, text="A named theme overrides the mode beside it.",
                 anchor="w", justify=tk.LEFT).pack(fill=tk.X, pady=(8, 0))

        # ---- custom colours ----
        colour_group = tk.LabelFrame(outer, text="Custom colours", padx=10, pady=8)
        colour_group.pack(fill=tk.X, pady=(12, 0))
        colour_row = tk.Frame(colour_group)
        colour_row.pack(fill=tk.X)

        tk.Button(colour_row, text="Background...", width=13,
                  command=lambda: self.pick_colour(True)).pack(side=tk.LEFT)
        self.back_swatch = tk.Label(colour_row, width=5, relief=tk.SUNKEN, borderwidth=1)
        self.back_swatch._minipad_keep_colours = True
        self.back_swatch.pack(side=tk.LEFT, padx=(8, 16))

        tk.Button(colour_row, text="Text...", width=10,
                  command=lambda: self.pick_colour(False)).pack(side=tk.LEFT)
        self.text_swatch = tk.Label(colour_row, width=5, relief=tk.SUNKEN, borderwidth=1)
        self.text_swatch._minipad_keep_colours = True
        self.text_swatch.pack(side=tk.LEFT, padx=(8, 16))

        tk.Button(colour_row, text="Reset to theme", command=self.reset_colours).pack(side=tk.LEFT)
        tk.Label(colour_group, text="Overrides sit on top of the chosen theme. "
                                    "Reset clears them.",
                 anchor="w").pack(fill=tk.X, pady=(8, 0))

        # ---- zoom ----
        zoom_group = tk.LabelFrame(outer, text="Zoom", padx=10, pady=8)
        zoom_group.pack(fill=tk.X, pady=(12, 0))
        zoom_row = tk.Frame(zoom_group)
        zoom_row.pack(fill=tk.X)
        self.zoom_scale = tk.Scale(zoom_row, from_=Theme.MIN_ZOOM, to=Theme.MAX_ZOOM,
                                   orient=tk.HORIZONTAL, resolution=10, length=330,
                                   showvalue=False, tickinterval=50,
                                   command=self.on_zoom_slide)
        self.zoom_scale.pack(side=tk.LEFT)
        self.zoom_label = tk.Label(zoom_row, text="100%", width=6,
                                   font=("sans-serif", 11, "bold"))
        self.zoom_label.pack(side=tk.LEFT, padx=10)
        tk.Button(zoom_row, text="100%", command=self.reset_zoom).pack(side=tk.LEFT)
        tk.Label(zoom_group, text="Ctrl+Plus, Ctrl+Minus and Ctrl+0 also work in the "
                                  "editor, as does Ctrl and the scroll wheel.",
                 anchor="w", justify=tk.LEFT).pack(fill=tk.X, pady=(6, 0))

        # ---- live sample ----
        self.sample = tk.Frame(outer, relief=tk.SUNKEN, borderwidth=1, padx=12, pady=10)
        self.sample._minipad_keep_colours = True
        self.sample.pack(fill=tk.X, pady=(12, 0))
        self.sample_heading = tk.Label(self.sample, text="Heading", anchor="w")
        self.sample_heading.pack(fill=tk.X)
        self.sample_body = tk.Label(self.sample, anchor="w")
        self.sample_body.pack(fill=tk.X, pady=(4, 0))
        self.sample_code = tk.Label(self.sample, text="code span", anchor="w")
        self.sample_code.pack(fill=tk.X, pady=(4, 0))

        tk.Button(outer, text="Close", width=10, command=self.win.destroy).pack(
            side=tk.RIGHT, pady=(14, 0))

        self.win.protocol("WM_DELETE_WINDOW", self.win.destroy)
        self.refresh(False)
        self.loading = False

    # ---- reacting to changes ----

    def on_mode_change(self):
        if self.loading:
            return
        Theme.set_mode(self.mode_var.get())
        # A named theme would mask the mode, so choosing a mode clears it.
        if Theme.preset_name != Theme.NO_PRESET:
            Theme.set_preset(Theme.NO_PRESET)
        self.refresh(True)

    def on_preset_change(self):
        if self.loading:
            return
        Theme.set_preset(self.preset_box.get())
        self.refresh(True)

    def on_zoom_slide(self, value):
        if self.loading:
            return
        Theme.set_zoom(int(float(value)))
        self.refresh(True)

    def reset_zoom(self):
        Theme.set_zoom(100)
        self.refresh(True)

    def reset_colours(self):
        Theme.clear_custom_colours()
        self.refresh(True)

    def pick_colour(self, background):
        current = Theme.editor_back() if background else Theme.editor_text()
        chosen = colorchooser.askcolor(color=current, parent=self.win,
                                       title="Background" if background else "Text")[1]
        if not chosen:
            return
        if background:
            Theme.set_custom_back(chosen)
        else:
            Theme.set_custom_text(chosen)
        self.refresh(True)

    # ---- pulling the window back in line with Theme ----

    def refresh(self, notify):
        was_loading = self.loading
        self.loading = True

        self.mode_var.set(Theme.mode)
        self.preset_box.set(Theme.preset_name)
        self.zoom_scale.set(Theme.zoom)
        self.zoom_label.configure(text="%d%%" % Theme.zoom)
        self.back_swatch.configure(background=Theme.editor_back())
        self.text_swatch.configure(background=Theme.editor_text())

        apply_ttk_theme(self.win)
        apply_theme_to(self.win)
        self.update_sample()

        self.loading = was_loading
        if notify:
            self.on_change()

    def update_sample(self):
        self.sample.configure(background=Theme.editor_back())
        self.sample_heading.configure(
            background=Theme.editor_back(), foreground=Theme.editor_text(),
            font=("sans-serif", Theme.scale_font(13), "bold"))
        self.sample_body.configure(
            text="Body text in the %s palette." % Theme.base().name.lower(),
            background=Theme.editor_back(), foreground=Theme.editor_text(),
            font=("sans-serif", Theme.scale_font(10)))
        self.sample_code.configure(
            background=Theme.code_back(), foreground=Theme.accent(),
            font=("monospace", Theme.scale_font(10)))


# --------------------------------------------------------------------------
# The editor
# --------------------------------------------------------------------------

class MiniPad(object):
    HEADING_SIZES = {1: 10, 2: 6, 3: 3, 4: 1, 5: 0, 6: 0}

    def __init__(self, root, startup_path=None):
        self.root = root
        self.documents = []
        self.current = None
        self.preview_document = None   # the document the preview pane was drawn from
        self.preview_visible = False
        self.word_wrap = Theme.word_wrap
        self.preview_job = None
        self.link_count = 0
        self.span_tags = set()

        # Preview pane behaviour, chosen from Markdown > Preview Mode.
        #   readonly - render only, no interaction
        #   jump     - clicking rendered text moves the editor caret to that line
        #   editable - the pane holds the Markdown source, styled in place, and
        #              typing in it edits the document
        remembered_mode = self.PREVIEW_MODE_NAMES[Theme.preview_mode]
        self.mode_var = tk.StringVar(value=remembered_mode)
        self.preview_mode = remembered_mode
        self.syncing = False

        self.base_family = "monospace"
        self.base_size = 11
        self.preview_family = "sans-serif"

        # A remembered font may have come from another machine - portable settings
        # travel on a USB stick - so it is checked against this machine's families
        # rather than trusted. Tk silently substitutes an unknown family, which
        # would leave the user with a font they did not choose and no explanation.
        if Theme.font_family:
            try:
                if Theme.font_family in tkfont.families(root):
                    self.base_family = Theme.font_family
            except tk.TclError:
                pass
        if Theme.font_size:
            self.base_size = int(Theme.font_size)

        # Only restore a geometry that still lands on a screen that exists.
        # Unplugging a second monitor would otherwise reopen the window at
        # coordinates nobody can reach, which looks like a failure to start.
        root.geometry(self.usable_geometry(root, Theme.window_geometry) or "1100x700")
        if Theme.window_maximised:
            try:
                root.state("zoomed")            # ignored by window managers that lack it
            except tk.TclError:
                pass
        root.protocol("WM_DELETE_WINDOW", self.on_close)
        self.set_window_icon(root)

        # The strip is packed before the panes and directly onto the window, so
        # it spans the full width above the preview rather than being squeezed
        # into the editor's half of the split.
        self.tabstrip = TabStrip(root, self.select_document,
                                 self.close_document, self.new_file)
        self.tabstrip.pack(side=tk.TOP, fill=tk.X)

        self.panes = tk.PanedWindow(root, orient=tk.HORIZONTAL, sashwidth=6,
                                    sashrelief=tk.RAISED)
        self.panes.pack(side=tk.TOP, fill=tk.BOTH, expand=True)

        self.editor_host = tk.Frame(self.panes)
        self.panes.add(self.editor_host, stretch="always")

        self.preview_frame = tk.Frame(self.panes)

        # A caption over the preview. Without it the second pane is unexplained:
        # a new user types on the left, sees the same words appear on the right,
        # and has no way to know why. The caption answers that, and naming the
        # mode explains why the pane behaves as it does.
        self.preview_caption = tk.Label(self.preview_frame, text="Live Preview",
                                        anchor=tk.W, padx=8)
        self.preview_caption.pack(side=tk.TOP, fill=tk.X)

        self.preview = tk.Text(self.preview_frame, wrap=tk.WORD, state=tk.DISABLED,
                               font=(self.preview_family, self.base_size),
                               padx=16, pady=12, relief=tk.FLAT,
                               background="#ffffff", cursor="arrow")
        self.preview_scroll = tk.Scrollbar(self.preview_frame, command=self.preview.yview)
        self.preview.configure(yscrollcommand=self.preview_scroll.set)
        self.preview_scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.preview.pack(side=tk.TOP, fill=tk.BOTH, expand=True)

        status_bar = tk.Frame(root)
        status_bar.pack(side=tk.BOTTOM, fill=tk.X)
        self.status = tk.Label(status_bar, text="Ln 1, Col 1", anchor=tk.W,
                               relief=tk.SUNKEN, padx=8)
        self.status.pack(side=tk.LEFT, fill=tk.X, expand=True)
        self.mode_status = tk.Label(status_bar, text="Preview: off", anchor=tk.E,
                                    relief=tk.SUNKEN, padx=8, width=24)
        self.mode_status.pack(side=tk.RIGHT)
        self.zoom_status = tk.Label(status_bar, text="100%", anchor=tk.E,
                                    relief=tk.SUNKEN, padx=8, width=7)
        self.zoom_status.pack(side=tk.RIGHT)

        self.build_menu()
        self.configure_preview_tags()

        self.preview.bind("<<Modified>>", self.on_preview_modified)
        self.preview.bind("<Button-1>", self.on_preview_click)
        self.bind_zoom_wheel(self.preview)

        # There is always at least one document, so self.current is never None.
        self.add_document(self.new_document())

        # The preview starts showing unless it was last turned off, so it is
        # found rather than hunted for.
        if Theme.preview_on:
            self.set_preview(True, remember=False)
        self.apply_theme()

        paths = startup_path if isinstance(startup_path, (list, tuple)) else [startup_path]
        opened = False
        for path in paths:
            if not path:
                continue
            self.load(path)
            opened = True
        if not opened:
            self.update_title()
        self.editor.focus_set()

    # ---- the document in front ----
    #
    # These stand in for what used to be plain attributes, so the rest of the
    # editor never had to learn that there is now more than one document.

    @property
    def editor(self):
        return self.current.editor

    @property
    def path(self):
        return self.current.path

    @path.setter
    def path(self, value):
        self.current.path = value

    @property
    def dirty(self):
        return self.current.dirty

    @dirty.setter
    def dirty(self, value):
        self.current.dirty = value

    @property
    def encoding(self):
        return self.current.encoding

    @encoding.setter
    def encoding(self, value):
        self.current.encoding = value

    @property
    def newline(self):
        return self.current.newline

    @newline.setter
    def newline(self, value):
        self.current.newline = value

    # ---- documents and tabs ----

    def new_document(self):
        document = Document(self.editor_host, self.next_untitled_number())
        document.editor.bind("<<Modified>>", self.on_modified)
        document.editor.bind("<KeyRelease>", self.on_editor_key_release)
        document.editor.bind("<ButtonRelease>", lambda event: self.update_position())
        self.bind_zoom_wheel(document.editor)
        document.set_wrap(self.word_wrap)
        # A tab opened after startup has missed every theme change so far, so it
        # is dressed on the way in rather than waiting for the next one.
        self.style_editor(document.editor)
        return document

    def next_untitled_number(self):
        """The lowest free number, so closing 'Untitled 2' frees that name."""
        taken = set(d.untitled_number for d in self.documents if d.path is None)
        candidate = 1
        while candidate in taken:
            candidate += 1
        return candidate

    def add_document(self, document):
        self.documents.append(document)
        self.select_document(len(self.documents) - 1)

    def select_document(self, index):
        if index < 0 or index >= len(self.documents):
            return
        if self.current is not None and self.current is not self.documents[index]:
            self.current.frame.pack_forget()
        self.current = self.documents[index]
        self.current.frame.pack(side=tk.TOP, fill=tk.BOTH, expand=True)

        self.update_title()
        self.update_position()
        self.render_preview()      # the preview follows whichever tab is in front
        self.editor.focus_set()

    def refresh_tabs(self):
        self.tabstrip.set_tabs([d.tab_text for d in self.documents],
                               self.documents.index(self.current))

    def close_document(self, index):
        """Close one tab. Returns False if the user cancelled."""
        if index < 0 or index >= len(self.documents):
            return True

        # Closing the last tab closes the window, as Notepad does. The asking is
        # left to on_close, or it would be asked twice.
        if len(self.documents) == 1:
            self.on_close()
            return True

        document = self.documents[index]
        if not self.confirm_discard(document):
            return False

        was_current = document is self.current
        self.documents.pop(index)
        document.frame.pack_forget()
        document.frame.destroy()
        # Never leave the preview pointing at something that no longer exists.
        if self.preview_document is document:
            self.preview_document = None

        if was_current:
            self.current = None
            self.select_document(min(index, len(self.documents) - 1))
        else:
            self.refresh_tabs()
        return True

    def close_current_tab(self):
        self.close_document(self.documents.index(self.current))

    def close_other_tabs(self):
        for document in list(self.documents):
            if document is self.current:
                continue
            if not self.close_document(self.documents.index(document)):
                return

    def select_relative_tab(self, step):
        if len(self.documents) < 2:
            return
        index = (self.documents.index(self.current) + step) % len(self.documents)
        self.select_document(index)

    def select_numbered_tab(self, number):
        # 9 means the last one however many there are, as browsers do.
        index = len(self.documents) - 1 if number == 9 else min(number - 1, len(self.documents) - 1)
        self.select_document(index)

    def on_editor_key_release(self, event=None):
        # Checked on key release rather than on <<Modified>>: loading a file also
        # changes the text, and a document ending with the command would otherwise
        # open the dialog by itself.
        self.update_position()
        self.check_for_bunny_command()

    def set_window_icon(self, window):
        """Apply the notepad icon. Harmless if the window manager ignores it."""
        try:
            self.icon_image = tk.PhotoImage(data=ICON_PNG_BASE64)
            window.iconphoto(True, self.icon_image)
        except tk.TclError:
            self.icon_image = None   # Tk older than 8.6 cannot read PNG data

    # ---- menus ----

    def build_menu(self):
        bar = tk.Menu(self.root)

        file_menu = tk.Menu(bar, tearoff=0)
        file_menu.add_command(label="New Tab", accelerator="Ctrl+N", command=self.new_file)
        file_menu.add_command(label="Open...", accelerator="Ctrl+O", command=self.open_file)
        file_menu.add_command(label="Save", accelerator="Ctrl+S", command=self.save)
        file_menu.add_command(label="Save As...", accelerator="Ctrl+Shift+S", command=self.save_as)
        file_menu.add_command(label="Save All", accelerator="Ctrl+Alt+S", command=self.save_all)
        file_menu.add_separator()
        file_menu.add_command(label="Close Tab", accelerator="Ctrl+W",
                              command=self.close_current_tab)
        file_menu.add_command(label="Close Other Tabs", command=self.close_other_tabs)
        file_menu.add_separator()
        file_menu.add_command(label="Export as HTML...", command=self.export_html)
        file_menu.add_separator()
        file_menu.add_command(label="Exit", command=self.on_close)
        bar.add_cascade(label="File", menu=file_menu)

        edit_menu = tk.Menu(bar, tearoff=0)
        edit_menu.add_command(label="Undo", accelerator="Ctrl+Z",
                              command=lambda: self.editor.event_generate("<<Undo>>"))
        edit_menu.add_command(label="Redo", accelerator="Ctrl+Y",
                              command=lambda: self.editor.event_generate("<<Redo>>"))
        edit_menu.add_separator()
        edit_menu.add_command(label="Cut", accelerator="Ctrl+X",
                              command=lambda: self.editor.event_generate("<<Cut>>"))
        edit_menu.add_command(label="Copy", accelerator="Ctrl+C",
                              command=lambda: self.editor.event_generate("<<Copy>>"))
        edit_menu.add_command(label="Paste", accelerator="Ctrl+V",
                              command=lambda: self.editor.event_generate("<<Paste>>"))
        edit_menu.add_separator()
        edit_menu.add_command(label="Select All", accelerator="Ctrl+A", command=self.select_all)
        edit_menu.add_command(label="Time/Date", accelerator="F5", command=self.insert_timestamp)
        bar.add_cascade(label="Edit", menu=edit_menu)

        md_menu = tk.Menu(bar, tearoff=0)
        md_menu.add_command(label="Live Preview", accelerator="F9", command=self.toggle_preview)

        mode_menu = tk.Menu(md_menu, tearoff=0)
        for label, value in (("Rendered (read-only)", "readonly"),
                             ("Rendered, click to jump to source", "jump"),
                             ("Editable source with live styling", "editable")):
            mode_menu.add_radiobutton(label=label, value=value, variable=self.mode_var,
                                      command=self.on_mode_change)
        md_menu.add_cascade(label="Preview Mode", menu=mode_menu)

        md_menu.add_separator()
        md_menu.add_command(label="Bold", accelerator="Ctrl+B",
                            command=lambda: self.wrap("**", "**"))
        md_menu.add_command(label="Italic", accelerator="Ctrl+I",
                            command=lambda: self.wrap("*", "*"))
        md_menu.add_command(label="Strikethrough", command=lambda: self.wrap("~~", "~~"))
        md_menu.add_command(label="Inline Code", command=lambda: self.wrap("`", "`"))
        md_menu.add_command(label="Code Block", command=lambda: self.wrap("```\n", "\n```"))
        md_menu.add_separator()
        md_menu.add_command(label="Heading 1", accelerator="Ctrl+1",
                            command=lambda: self.prefix_lines("# "))
        md_menu.add_command(label="Heading 2", accelerator="Ctrl+2",
                            command=lambda: self.prefix_lines("## "))
        md_menu.add_command(label="Heading 3", accelerator="Ctrl+3",
                            command=lambda: self.prefix_lines("### "))
        md_menu.add_separator()
        md_menu.add_command(label="Bulleted List", command=lambda: self.prefix_lines("- "))
        md_menu.add_command(label="Numbered List", command=lambda: self.prefix_lines(None, True))
        md_menu.add_command(label="Blockquote", command=lambda: self.prefix_lines("> "))
        md_menu.add_separator()
        md_menu.add_command(label="Link...", accelerator="Ctrl+K", command=self.insert_link)
        md_menu.add_command(label="Table", command=self.insert_table)
        md_menu.add_command(label="Horizontal Rule",
                            command=lambda: self.editor.insert(tk.INSERT, "\n---\n"))
        bar.add_cascade(label="Markdown", menu=md_menu)

        format_menu = tk.Menu(bar, tearoff=0)
        self.wrap_var = tk.BooleanVar(value=Theme.word_wrap)
        format_menu.add_checkbutton(label="Word Wrap", variable=self.wrap_var,
                                    command=self.toggle_word_wrap)
        format_menu.add_command(label="Font...", command=self.choose_font)
        format_menu.add_separator()
        format_menu.add_command(label="Appearance...", command=self.show_appearance)
        format_menu.add_separator()
        format_menu.add_command(label="Portable Settings...", command=self.toggle_portable)
        format_menu.add_separator()
        format_menu.add_command(label="Zoom In", accelerator="Ctrl++",
                                command=lambda: self.change_zoom(10))
        format_menu.add_command(label="Zoom Out", accelerator="Ctrl+-",
                                command=lambda: self.change_zoom(-10))
        format_menu.add_command(label="Reset Zoom", accelerator="Ctrl+0",
                                command=lambda: self.set_zoom(100))
        bar.add_cascade(label="Format", menu=format_menu)

        bar.add_command(label="About", command=self.show_about)

        self.root.configure(menu=bar)

        bindings = {
            "<Control-n>": self.new_file,
            "<Control-o>": self.open_file,
            "<Control-s>": self.save,
            "<Control-Shift-S>": self.save_as,
            "<Control-a>": self.select_all,
            "<F5>": self.insert_timestamp,
            "<F9>": self.toggle_preview,
            "<Control-b>": lambda: self.wrap("**", "**"),
            "<Control-i>": lambda: self.wrap("*", "*"),
            "<Control-k>": self.insert_link,
            "<Control-Key-1>": lambda: self.prefix_lines("# "),
            "<Control-Key-2>": lambda: self.prefix_lines("## "),
            "<Control-Key-3>": lambda: self.prefix_lines("### "),
            # Bunnies has no menu entry of its own - it is a tab inside About.
            "<Control-Shift-B>": lambda: self.show_about(on_bunnies=True),
            "<F1>": lambda: self.show_about(on_help=True),
            # Both the main row and the keypad, and both faces of the = key,
            # because that is where people reach for zoom.
            "<Control-plus>": lambda: self.change_zoom(10),
            "<Control-equal>": lambda: self.change_zoom(10),
            "<Control-KP_Add>": lambda: self.change_zoom(10),
            "<Control-minus>": lambda: self.change_zoom(-10),
            "<Control-underscore>": lambda: self.change_zoom(-10),
            "<Control-KP_Subtract>": lambda: self.change_zoom(-10),
            "<Control-Key-0>": lambda: self.set_zoom(100),
            # Tabs. Ctrl+1..9 is not used for this because it already means
            # Heading 1/2/3 here, so the numbered jumps take Ctrl+Alt instead.
            "<Control-w>": self.close_current_tab,
            "<Control-Alt-s>": self.save_all,
            "<Control-Tab>": lambda: self.select_relative_tab(1),
            "<Control-Shift-Tab>": lambda: self.select_relative_tab(-1),
            "<Control-ISO_Left_Tab>": lambda: self.select_relative_tab(-1),
            "<Control-Next>": lambda: self.select_relative_tab(1),
            "<Control-Prior>": lambda: self.select_relative_tab(-1),
        }
        for number in range(1, 10):
            bindings["<Control-Alt-Key-%d>" % number] = (
                lambda n=number: self.select_numbered_tab(n))
        for sequence, action in bindings.items():
            self.root.bind_all(sequence, self._as_handler(action))

    @staticmethod
    def _as_handler(action):
        def handler(event):
            action()
            return "break"  # stop Tk's own default handling
        return handler

    # ---- file operations ----

    # New opens a tab rather than replacing what is in front of you, which is
    # the whole point of having tabs.
    def new_file(self):
        self.add_document(self.new_document())

    def open_file(self):
        chosen = filedialog.askopenfilenames(parent=self.root, filetypes=FILE_TYPES)
        if not chosen:
            return
        if isinstance(chosen, str):     # some Tk builds hand back one string
            chosen = self.root.tk.splitlist(chosen)
        for path in chosen:
            self.load(path)

    def load(self, path):
        # Already open? Show that tab instead of opening a second view of one
        # file, which would let two copies drift apart and overwrite each other.
        for index, document in enumerate(self.documents):
            if document.is_same_file(path):
                self.select_document(index)
                return

        try:
            with open(path, "rb") as handle:
                data = handle.read()
        except (IOError, OSError) as error:
            messagebox.showwarning(APP_NAME, "Could not open the file:\n\n%s" % error,
                                   parent=self.root)
            return

        # An untouched empty tab is reused rather than left behind as clutter.
        if self.current is None or not self.current.is_unused:
            self.add_document(self.new_document())

        # Detect a byte-order mark and keep it, so saving reproduces the original
        # form of the file rather than silently adding or dropping the marker.
        if data.startswith(codecs.BOM_UTF8):
            self.encoding = "utf-8-sig"
        elif data.startswith(codecs.BOM_UTF16_LE) or data.startswith(codecs.BOM_UTF16_BE):
            self.encoding = "utf-16"
        else:
            self.encoding = "utf-8"
        raw = data.decode(self.encoding, errors="replace")

        # Remember the file's line-ending style so saving does not rewrite every line.
        self.newline = "\r\n" if "\r\n" in raw else "\n"
        text = raw.replace("\r\n", "\n").replace("\r", "\n")

        self.editor.delete("1.0", tk.END)
        self.editor.insert("1.0", text)
        self.editor.mark_set(tk.INSERT, "1.0")
        self.editor.edit_reset()
        self.path = path
        self.mark_clean()

        if path.lower().endswith(MARKDOWN_EXTENSIONS) and not self.preview_visible:
            self.toggle_preview()
        else:
            self.render_preview()

    def save(self):
        if self.path is None:
            return self.save_as()
        return self.write(self.path)

    def save_as(self):
        initial = os.path.basename(self.path) if self.path else "Untitled.txt"
        directory = os.path.dirname(self.path) if self.path else None
        chosen = filedialog.asksaveasfilename(parent=self.root, filetypes=FILE_TYPES,
                                              initialfile=initial, initialdir=directory,
                                              defaultextension=".txt")
        if not chosen:
            return False
        return self.write(chosen)

    def other_tab_holding(self, path):
        """The tab other than this one that already has this file open, if any.

        Two tabs claiming one file is the same hazard that opening guards
        against: each holds a different version, and whichever saves last wins
        silently. Opening is guarded by switching to the existing tab, but Save
        As can arrive at the same place from the other direction.
        """
        for document in self.documents:
            if document is not self.current and document.is_same_file(path):
                return document
        return None

    def write(self, path):
        if self.other_tab_holding(path) is not None:
            messagebox.showwarning(
                APP_NAME,
                "%s is already open in another tab.\n\n"
                "Close that tab first, or save this one under a different name."
                % os.path.basename(path), parent=self.root)
            return False

        content = self.editor.get("1.0", "end-1c")
        try:
            with open(path, "w", encoding=self.encoding, newline="") as handle:
                handle.write(content.replace("\n", self.newline))
        except (IOError, OSError) as error:
            messagebox.showwarning(APP_NAME, "Could not save the file:\n\n%s" % error,
                                   parent=self.root)
            return False
        self.path = path
        self.mark_clean()
        return True

    def export_html(self):
        stem = os.path.splitext(os.path.basename(self.path))[0] if self.path else "Untitled"
        chosen = filedialog.asksaveasfilename(
            parent=self.root, defaultextension=".html",
            initialfile=stem + ".html",
            initialdir=os.path.dirname(self.path) if self.path else None,
            filetypes=[("HTML files", "*.html"), ("All files", "*")])
        if not chosen:
            return
        try:
            with open(chosen, "w", encoding="utf-8") as handle:
                # The exported page follows the theme, as the Windows build does.
                handle.write(markdown_to_html(self.editor.get("1.0", "end-1c"),
                                              Theme.preview_css()))
        except (IOError, OSError) as error:
            messagebox.showwarning(APP_NAME, "Could not export:\n\n%s" % error,
                                   parent=self.root)

    def save_all(self):
        was_current = self.current
        for document in list(self.documents):
            if not document.dirty:
                continue
            self.select_document(self.documents.index(document))
            if not self.save():
                break           # the user cancelled a Save As; stop asking
        if was_current in self.documents:
            self.select_document(self.documents.index(was_current))

    def confirm_discard(self, document=None):
        """False when the user cancels an action that would lose changes."""
        document = document if document is not None else self.current
        if not document.dirty:
            return True

        # Show the document being asked about, so "Save changes to X?" is not a
        # question about something you cannot see.
        if document is not self.current:
            self.select_document(self.documents.index(document))

        answer = messagebox.askyesnocancel(APP_NAME, "Save changes to %s?" % document.name,
                                           parent=self.root)
        if answer is None:
            return False
        if not answer:
            return True
        return bool(self.save())

    def on_close(self):
        # Ask about each unsaved tab in turn; cancelling any one of them abandons
        # the whole close, so nothing already answered for is lost.
        for document in list(self.documents):
            if not self.confirm_discard(document):
                return
        # Only once the close is certain. Saving geometry before the unsaved-changes
        # prompt would record a position for a window the user then kept.
        self.save_window_geometry()
        self.shutdown()
        self.root.destroy()

    def shutdown(self):
        """Cancel anything scheduled, so nothing fires at a window that is gone.

        A pending `after` callback outlives the widget it was going to touch and
        Tk then reports `invalid command name ...render_preview`.
        """
        if self.preview_job is not None:
            try:
                self.root.after_cancel(self.preview_job)
            except tk.TclError:
                pass
            self.preview_job = None

    # ---- editing helpers ----

    def select_all(self):
        self.editor.tag_add(tk.SEL, "1.0", tk.END)
        self.editor.mark_set(tk.INSERT, "1.0")

    def insert_timestamp(self):
        self.editor.insert(tk.INSERT, datetime.datetime.now().strftime("%m/%d/%y %H:%M"))

    def _selection(self):
        try:
            return self.editor.index(tk.SEL_FIRST), self.editor.index(tk.SEL_LAST)
        except tk.TclError:
            return None, None

    def wrap(self, prefix, suffix):
        start, end = self._selection()
        if start is None:
            self.editor.insert(tk.INSERT, prefix + suffix)
            self.editor.mark_set(tk.INSERT, "%s-%dc" % (self.editor.index(tk.INSERT), len(suffix)))
        else:
            selected = self.editor.get(start, end)
            self.editor.delete(start, end)
            self.editor.insert(start, prefix + selected + suffix)
            self.editor.tag_remove(tk.SEL, "1.0", tk.END)
            self.editor.tag_add(tk.SEL, "%s+%dc" % (start, len(prefix)),
                                "%s+%dc" % (start, len(prefix) + len(selected)))
        self.editor.focus_set()

    def prefix_lines(self, marker, numbered=False):
        start, end = self._selection()
        if start is None:
            start = end = self.editor.index(tk.INSERT)
        first = int(start.split(".")[0])
        last = int(end.split(".")[0])

        for offset, line_number in enumerate(range(first, last + 1)):
            text = marker if not numbered else "%d. " % (offset + 1)
            self.editor.insert("%d.0" % line_number, text)
        self.editor.focus_set()

    def insert_link(self):
        start, end = self._selection()
        if start is None:
            self.editor.insert(tk.INSERT, "[text](url)")
            return
        selected = self.editor.get(start, end)
        self.editor.delete(start, end)
        if selected.startswith(("http://", "https://", "www.")):
            self.editor.insert(start, "[](%s)" % selected)
            self.editor.mark_set(tk.INSERT, "%s+1c" % start)
        else:
            self.editor.insert(start, "[%s](url)" % selected)
        self.editor.focus_set()

    def insert_table(self):
        self.editor.insert(tk.INSERT,
                           "| Column A | Column B |\n| --- | --- |\n| value | value |\n")

    # ---- view ----

    # Word wrap, the font and the theme are settings of the editor, not of one
    # document, so they apply to every tab at once.
    def toggle_word_wrap(self):
        self.word_wrap = bool(self.wrap_var.get())
        for document in self.documents:
            document.set_wrap(self.word_wrap)
        Theme.set_word_wrap(self.word_wrap)

    # Portable settings: on or off, with the consequences spelled out before
    # anything happens. Switching off deletes a file, so it asks first - and it
    # copies the settings back to the profile before deleting, so nothing is lost.
    def toggle_portable(self):
        if not Theme.is_portable():
            if not messagebox.askyesno(
                    "MiniPad - Portable Settings",
                    "Keep MiniPad's settings in a file next to the program itself, "
                    "rather than in your home directory?\n\n"
                    "This is for running MiniPad from a USB stick, or on a machine "
                    "where you would rather not leave anything behind.\n\n"
                    "The file would be:\n" + str(Theme.portable_settings_path()),
                    parent=self.root):
                return
            failure = Theme.make_portable()
            if failure:
                messagebox.showwarning(
                    "MiniPad",
                    "Portable settings could not be turned on.\n\n" + failure +
                    "\n\nThis usually means the program is in a folder you cannot "
                    "write to. Your settings are unchanged and are still being "
                    "saved to your home directory.",
                    parent=self.root)
                return
            messagebox.showinfo(
                "MiniPad",
                "Portable settings are on.\n\nSettings are now saved to:\n" +
                Theme.settings_path() +
                "\n\nMove the program and that file together and your preferences "
                "travel with it. Deleting the file returns MiniPad to using your "
                "home directory.",
                parent=self.root)
        else:
            if not messagebox.askyesno(
                    "MiniPad - Portable Settings",
                    "Turn portable settings off?\n\nYour current settings will be "
                    "copied back to your home directory first, and then this file "
                    "will be deleted:\n" + str(Theme.portable_settings_path()),
                    parent=self.root):
                return
            failure = Theme.make_not_portable()
            if failure:
                messagebox.showwarning("MiniPad",
                                       "The file could not be removed.\n\n" + failure,
                                       parent=self.root)
                return
            messagebox.showinfo(
                "MiniPad",
                "Portable settings are off. Settings are saved to:\n" + Theme.settings_path(),
                parent=self.root)

    @staticmethod
    def usable_geometry(root, geometry):
        """Return the saved geometry only if it lands on a screen that exists.

        Tk has no per-monitor API, so this checks against the whole virtual desktop
        rather than a specific display - enough to catch the real failure, which is
        a window remembered at coordinates on a monitor that is no longer attached.
        """
        if not geometry:
            return None
        match = re.match(r"^(\d+)x(\d+)([+-])(-?\d+)([+-])(-?\d+)$", geometry)
        if not match:
            return geometry             # a plain "WxH" carries no position to check
        width, height = int(match.group(1)), int(match.group(2))

        # Tk's geometry grammar has two position forms and they mean opposite
        # things. "+X" is measured from the left/top edge and may itself be
        # negative, written "+-100". A leading "-" instead measures from the
        # RIGHT/BOTTOM edge, which is on-screen by definition and needs no check.
        if match.group(3) == "-" or match.group(5) == "-":
            return geometry
        x, y = int(match.group(4)), int(match.group(6))
        try:
            screen_width = root.winfo_screenwidth()
            screen_height = root.winfo_screenheight()
        except tk.TclError:
            return None
        # Intersects rather than contains: a window nudged partly off the edge is
        # still perfectly usable and should not be thrown away.
        if x + width < 0 or y + height < 0 or x > screen_width or y > screen_height:
            return "%dx%d" % (width, height)        # keep the size, drop the position
        return geometry

    def save_window_geometry(self):
        try:
            maximised = self.root.state() == "zoomed"
        except tk.TclError:
            maximised = False
        try:
            # wm_geometry reports the screen size while zoomed, so a maximised
            # window would otherwise remember a size it can never return to.
            geometry = "" if maximised else self.root.winfo_geometry()
        except tk.TclError:
            return
        if geometry and not re.match(r"^\d+x\d+", geometry):
            return
        Theme.set_window(geometry or Theme.window_geometry, maximised)

    def choose_font(self):
        dialog = tk.Toplevel(self.root)
        dialog.title("Font")
        dialog.transient(self.root)
        dialog.resizable(False, False)

        tk.Label(dialog, text="Family").grid(row=0, column=0, padx=10, pady=(10, 2), sticky="w")
        tk.Label(dialog, text="Size").grid(row=0, column=1, padx=10, pady=(10, 2), sticky="w")

        families = sorted(set(tkfont.families(self.root)))
        listbox = tk.Listbox(dialog, height=12, exportselection=False, width=32)
        for family in families:
            listbox.insert(tk.END, family)
        if self.base_family in families:
            index = families.index(self.base_family)
            listbox.selection_set(index)
            listbox.see(index)
        listbox.grid(row=1, column=0, padx=10, sticky="ns")

        size_box = tk.Listbox(dialog, height=12, exportselection=False, width=6)
        sizes = [8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 24, 28]
        for size in sizes:
            size_box.insert(tk.END, size)
        if self.base_size in sizes:
            size_box.selection_set(sizes.index(self.base_size))
        size_box.grid(row=1, column=1, padx=10, sticky="ns")

        def apply_font():
            selection = listbox.curselection()
            size_selection = size_box.curselection()
            if selection:
                self.base_family = families[selection[0]]
            if size_selection:
                # Stored unzoomed, so zoom stays a multiplier on the chosen size
                # rather than compounding with it.
                self.base_size = sizes[size_selection[0]]
            self.apply_theme()
            Theme.set_font(self.base_family, self.base_size)
            dialog.destroy()

        buttons = tk.Frame(dialog)
        buttons.grid(row=2, column=0, columnspan=2, pady=10)
        tk.Button(buttons, text="OK", width=10, command=apply_font).pack(side=tk.LEFT, padx=5)
        tk.Button(buttons, text="Cancel", width=10, command=dialog.destroy).pack(side=tk.LEFT)

        apply_theme_to(dialog)
        dialog.grab_set()

    # ---- appearance and zoom ----

    def scaled(self, points=None):
        """A font size in points, after zoom."""
        return Theme.scale_font(self.base_size if points is None else points)

    def show_appearance(self):
        AppearanceWindow(self.root, self.apply_theme)

    def change_zoom(self, delta):
        self.set_zoom(Theme.zoom + delta)

    def set_zoom(self, percent):
        Theme.set_zoom(percent)
        self.apply_theme()

    def bind_zoom_wheel(self, widget):
        # X11 reports the wheel as buttons 4 and 5; other builds of Tk send
        # <MouseWheel>. Each handler returns "break", or the widget scrolls as
        # well as zooming.
        widget.bind("<Control-Button-4>", lambda event: self.on_zoom_wheel(1))
        widget.bind("<Control-Button-5>", lambda event: self.on_zoom_wheel(-1))
        widget.bind("<Control-MouseWheel>",
                    lambda event: self.on_zoom_wheel(1 if event.delta > 0 else -1))

    def on_zoom_wheel(self, direction):
        self.change_zoom(10 * direction)
        return "break"

    def update_zoom_label(self):
        self.zoom_status.configure(
            text="%d%%" % Theme.zoom,
            background=Theme.menu_back(),
            foreground=Theme.ui_dim() if Theme.zoom == 100 else Theme.ui_accent())

    def style_editor(self, widget):
        widget.configure(background=Theme.editor_back(), foreground=Theme.editor_text(),
                         insertbackground=Theme.editor_text(),
                         selectbackground=Theme.menu_highlight(),
                         selectforeground=Theme.editor_text(),
                         font=(self.base_family, self.scaled()))

    def apply_theme(self):
        """Push the current theme and zoom into every part of the window."""
        apply_ttk_theme(self.root)
        apply_theme_to(self.root)

        for document in self.documents:
            self.style_editor(document.editor)
        self.editor_host.configure(background=Theme.editor_back())
        self.preview.configure(background=Theme.editor_back(),
                               foreground=Theme.editor_text(),
                               insertbackground=Theme.editor_text(),
                               selectbackground=Theme.menu_highlight(),
                               selectforeground=Theme.editor_text(),
                               font=(self.preview_family, self.scaled()),
                               cursor="xterm" if self.preview_mode == "editable" else "arrow")
        self.panes.configure(background=Theme.border())
        self.tabstrip.redraw()      # the strip paints itself straight from Theme

        for label in (self.status, self.mode_status):
            label.configure(background=Theme.menu_back(), foreground=Theme.ui_dim())
        caption = getattr(self, "preview_caption", None)
        if caption is not None:
            caption.configure(background=Theme.menu_back(), foreground=Theme.ui_dim())
        self.update_zoom_label()

        self.configure_preview_tags()
        self.render_preview()

    def update_title(self):
        self.root.title("%s - %s" % (self.current.tab_text, APP_NAME))
        self.refresh_tabs()     # the tab carries the same name and the same asterisk

    def update_position(self):
        line, column = self.editor.index(tk.INSERT).split(".")
        self.status.configure(text="Ln %s, Col %d" % (line, int(column) + 1))

    def mark_clean(self):
        self.dirty = False
        self.editor.edit_modified(False)
        self.update_title()
        self.update_position()

    def document_for(self, widget):
        for document in self.documents:
            if document.editor is widget:
                return document
        return None

    def on_modified(self, event=None):
        # Which document changed is taken from the event, not from whichever tab
        # happens to be in front: <<Modified>> is queued rather than delivered on
        # the spot, so it can arrive after the tab has already been switched, and
        # reading self.editor would then mark the wrong document.
        document = self.document_for(event.widget) if event is not None else self.current
        if document is None or not document.editor.edit_modified():
            return
        document.editor.edit_modified(False)
        if self.syncing:
            return  # the text came from the preview pane; leave that pane alone

        # Only redraw the title and the strip when the asterisk actually appears,
        # rather than repainting the whole strip on every keystroke.
        was_dirty = document.dirty
        document.dirty = True
        if not was_dirty:
            self.update_title()
        if document is not self.current:
            return          # a background tab needs no caret or preview update

        self.update_position()
        if self.preview_visible:
            if self.preview_job is not None:
                self.root.after_cancel(self.preview_job)
            self.preview_job = self.root.after(450, self.render_preview)

    # ---- preview ----

    def toggle_preview(self):
        self.set_preview(not self.preview_visible)

    # `remember` is false only when restoring at startup: writing the setting
    # back on the way in would rewrite settings.txt every launch to say what it
    # already said.
    def set_preview(self, on, remember=True):
        self.preview_visible = on
        if on:
            self.panes.add(self.preview_frame, stretch="always")
            self.root.update_idletasks()
            self.panes.sash_place(0, self.panes.winfo_width() // 2, 0)
            self.render_preview()
        else:
            self.panes.forget(self.preview_frame)
        if remember:
            Theme.set_preview_on(on)
        self.update_mode_status()

    # The settings file stores the mode as a number so both builds read the same
    # file; the UI uses names because a radio button variable reads better as one.
    PREVIEW_MODE_NAMES = ("readonly", "jump", "editable")

    def on_mode_change(self):
        self.preview_mode = self.mode_var.get()
        try:
            Theme.set_preview_mode(self.PREVIEW_MODE_NAMES.index(self.preview_mode))
        except ValueError:
            pass
        if not self.preview_visible:
            self.toggle_preview()   # picking a mode implies wanting the pane
        else:
            self.render_preview()
        self.update_mode_status()

    def update_mode_status(self):
        if not self.preview_visible:
            text = "Preview: off"
        elif self.preview_mode == "jump":
            text = "Preview: click to jump"
        elif self.preview_mode == "editable":
            text = "Preview: editable source"
        else:
            text = "Preview: read-only"
        self.mode_status.configure(text=text)
        self.update_preview_caption()

    # The caption says what the pane IS, and then what it is currently DOING.
    # The second half matters: in editable mode the pane accepts typing, and a
    # user who does not know that will be surprised by it either way.
    def update_preview_caption(self):
        caption = getattr(self, "preview_caption", None)
        if caption is None:
            return
        if self.preview_mode == "jump":
            text = "Live Preview  -  click any block to jump to its source"
        elif self.preview_mode == "editable":
            text = "Live Preview  -  editable, your changes go back to the text"
        else:
            text = "Live Preview  -  updates as you type on the left"
        caption.configure(text=text)

    def on_preview_click(self, event):
        """Click-to-jump: move the editor caret to the clicked block's source line."""
        if self.preview_mode != "jump":
            return None
        index = self.preview.index("@%d,%d" % (event.x, event.y))
        for tag in self.preview.tag_names(index):
            if tag.startswith("src"):
                try:
                    line = int(tag[3:])
                except ValueError:
                    continue
                self.editor.mark_set(tk.INSERT, "%d.0" % (line + 1))
                self.editor.see(tk.INSERT)
                self.editor.focus_set()
                self.update_position()
                return "break"
        return None

    def on_preview_modified(self, event=None):
        """Editable mode: push what was typed in the preview back to the document."""
        if not self.preview.edit_modified():
            return
        self.preview.edit_modified(False)
        if self.preview_mode != "editable" or self.syncing:
            return

        # The preview is shared between tabs, so its edits belong to whichever
        # document it was rendered from - not to whichever tab happens to be in
        # front by the time this queued event is delivered.
        document = self.preview_document
        if document is None or document not in self.documents:
            return

        text = self.preview.get("1.0", "end-1c")
        if text == document.editor.get("1.0", "end-1c"):
            return

        self.syncing = True
        try:
            caret = document.editor.index(tk.INSERT)
            document.editor.delete("1.0", tk.END)
            document.editor.insert("1.0", text)
            try:
                document.editor.mark_set(tk.INSERT, caret)
            except tk.TclError:
                pass
        finally:
            self.syncing = False

        document.dirty = True
        self.update_title()
        if document is self.current:
            self.style_source()   # restyle in place; never re-inserts the text

    def configure_preview_tags(self):
        # Sizes and margins are rebuilt from the current zoom, and colours from
        # the current palette, so this is also what a theme change calls.
        base = self.scaled()
        step = Theme.scale_pixels
        self.span_tags.clear()  # rebuild inline tags against the new base font
        self.preview.configure(font=(self.preview_family, base))
        self.preview.tag_configure("code_block",
                                   font=("monospace", self.scaled(self.base_size - 1)),
                                   background=Theme.code_back(),
                                   lmargin1=step(18), lmargin2=step(18),
                                   spacing1=2, spacing3=2)
        self.preview.tag_configure("quote", foreground=Theme.dim(),
                                   font=(self.preview_family, base, "italic"),
                                   lmargin1=step(14), lmargin2=step(24))
        # The bar down the side of a quote gets its own upright tag: under the
        # quote's italic, a vertical block is skewed into something like a slash.
        self.preview.tag_configure("quote_mark", foreground=Theme.border(),
                                   font=(self.preview_family, base),
                                   lmargin1=step(14), lmargin2=step(24))
        self.preview.tag_configure("table", font=("monospace", self.scaled(self.base_size - 1)))
        self.preview.tag_configure("rule", foreground=Theme.border())
        for level, bump in self.HEADING_SIZES.items():
            self.preview.tag_configure("h%d" % level,
                                       font=(self.preview_family,
                                             self.scaled(self.base_size + bump), "bold"),
                                       spacing1=10, spacing3=6)
        for depth in range(4):
            self.preview.tag_configure("indent%d" % depth,
                                       lmargin1=step(14 + depth * 22),
                                       lmargin2=step(26 + depth * 22))

    def span_tag(self, styles, size):
        """Create (once) a tag for a combination of inline styles."""
        name = "span_%d_%s" % (size, "-".join(sorted(styles)) or "plain")
        if name in self.span_tags:
            return name

        family = "monospace" if "code" in styles else self.preview_family
        weight = "bold" if "bold" in styles else "normal"
        slant = "italic" if "italic" in styles else "roman"
        options = {"font": (family, size, "%s %s" % (weight, slant))}
        if "code" in styles:
            options["background"] = Theme.code_back()
        if "strike" in styles:
            options["overstrike"] = True
        if "link" in styles or "image" in styles:
            options["foreground"] = Theme.accent()
            options["underline"] = True
        self.preview.tag_configure(name, **options)
        self.span_tags.add(name)
        return name

    def render_preview(self):
        # Cancel rather than merely forget a pending debounced render. Dropping
        # the handle leaves the callback scheduled with nothing able to stop it,
        # so it renders again for no reason - and if the window has closed by
        # then, Tk reports "invalid command name ...render_preview".
        if self.preview_job is not None:
            try:
                self.root.after_cancel(self.preview_job)
            except tk.TclError:
                pass
            self.preview_job = None

        if not self.preview_visible:
            # With the pane hidden there is nothing for an edit to belong to,
            # and holding the reference would keep a closed tab's widgets alive.
            self.preview_document = None
            return
        if self.syncing:
            return
        # Remember whose document this is, so edits made in the pane go back to
        # the right one even if the tab changes before they are delivered.
        self.preview_document = self.current

        position = self.preview.yview()[0]
        editable = self.preview_mode == "editable"
        self.syncing = True
        self.preview.configure(state=tk.NORMAL)
        try:
            self.preview.delete("1.0", tk.END)
            self.link_count = 0
            if editable:
                self.preview.insert("1.0", self.editor.get("1.0", "end-1c"))
                self.style_source()
            else:
                self.render_blocks(parse_blocks(self.editor.get("1.0", "end-1c")), 0)
        finally:
            self.preview.configure(state=tk.NORMAL if editable else tk.DISABLED,
                                   cursor="xterm" if editable else "arrow")
            self.preview.edit_modified(False)
            self.preview.yview_moveto(position)
            self.syncing = False

    # ---- editable mode: style the Markdown source in place ----

    _SOURCE_INLINE = [
        ("code", re.compile(r"`[^`]+`")),
        ("bold", re.compile(r"\*\*\S(?:.*?\S)?\*\*")),
        ("strike", re.compile(r"~~\S(?:.*?\S)?~~")),
        ("italic", re.compile(r"(?<!\*)\*(?!\*)\S(?:.*?\S)?(?<!\*)\*(?!\*)")),
        ("link", re.compile(r"!?\[[^\]]*\]\([^)\s]+\)")),
    ]

    def style_source(self):
        """Apply styling to the Markdown source shown in the preview pane.

        The markers stay visible and the text is never rewritten, so editing here
        can never reshape the document - what you see is the file itself.
        """
        for tag in list(self.span_tags) + ["code_block", "quote"] + \
                ["h%d" % level for level in self.HEADING_SIZES]:
            self.preview.tag_remove(tag, "1.0", tk.END)

        lines = self.preview.get("1.0", "end-1c").split("\n")
        in_fence = False

        for number, line in enumerate(lines, start=1):
            start = "%d.0" % number
            end = "%d.end" % number

            if _FENCE.match(line) or _FENCE_END.match(line):
                self.preview.tag_add("code_block", start, end)
                in_fence = not in_fence
                continue
            if in_fence:
                self.preview.tag_add("code_block", start, end)
                continue

            heading = _HEADING.match(line)
            if heading:
                level = len(heading.group(1))
                self.preview.tag_add("h%d" % level, start, end)
                continue

            if _QUOTE.match(line):
                self.preview.tag_add("quote", start, end)

            for style, pattern in self._SOURCE_INLINE:
                for match in pattern.finditer(line):
                    tag = self.span_tag(frozenset([style]), self.scaled())
                    self.preview.tag_add(tag,
                                         "%d.%d" % (number, match.start()),
                                         "%d.%d" % (number, match.end()))

    def render_blocks(self, blocks, depth):
        for block in blocks:
            # Tag the block's whole range with its source line, for click-to-jump.
            block_start = self.preview.index("end-1c")
            kind = block["type"]
            if kind == "heading":
                level = block["level"]
                self.render_spans(block["spans"],
                                  extra=("h%d" % level,),
                                  size=self.scaled(self.base_size + self.HEADING_SIZES[level]))
                self.insert("\n\n")
            elif kind == "paragraph":
                self.render_spans(block["spans"], extra=("indent%d" % min(depth, 3),))
                self.insert("\n\n")
            elif kind == "code":
                for line in block["text"].split("\n"):
                    self.insert(line + "\n", ("code_block",))
                self.insert("\n")
            elif kind == "rule":
                self.insert("─" * 60 + "\n\n", ("rule",))
            elif kind == "quote":
                self.render_quote(block["blocks"], depth)
            elif kind == "list":
                self.render_list(block, depth)
            elif kind == "table":
                self.render_table(block)

            line = block.get("line")
            if line is not None:
                self.preview.tag_add("src%d" % line, block_start,
                                     self.preview.index("end-1c"))

    def render_quote(self, blocks, depth):
        for block in blocks:
            if block["type"] == "paragraph":
                self.insert("▎ ", ("quote_mark",))
                self.render_spans(block["spans"], extra=("quote",))
                self.insert("\n\n")
            else:
                self.render_blocks([block], depth + 1)

    def render_list(self, block, depth):
        for index, item in enumerate(block["items"]):
            bullet = "%d. " % (index + 1) if block["ordered"] else "• "
            tag = "indent%d" % min(depth, 3)
            self.insert("    " * depth + bullet, (tag,))
            self.render_spans(item["spans"], extra=(tag,))
            self.insert("\n")
            for nested in item["blocks"]:
                self.render_list(nested, depth + 1)
        if depth == 0:
            self.insert("\n")

    def render_table(self, block):
        def plain(cell):
            return "".join(span.text for span in parse_inline(cell))

        header = [plain(cell) for cell in block["header"]]
        rows = [[plain(cell) for cell in row] for row in block["rows"]]
        widths = [len(cell) for cell in header]
        for row in rows:
            for index, cell in enumerate(row):
                if index < len(widths):
                    widths[index] = max(widths[index], len(cell))

        def format_row(cells):
            parts = []
            for index, cell in enumerate(cells):
                width = widths[index] if index < len(widths) else len(cell)
                align = block["align"][index] if index < len(block["align"]) else None
                if align == "right":
                    parts.append(cell.rjust(width))
                elif align == "center":
                    parts.append(cell.center(width))
                else:
                    parts.append(cell.ljust(width))
            return "  " + " │ ".join(parts)

        self.insert(format_row(header) + "\n", ("table",))
        self.insert("  " + "─┼─".join("─" * width for width in widths) + "\n",
                    ("table",))
        for row in rows:
            self.insert(format_row(row) + "\n", ("table",))
        self.insert("\n")

    def render_spans(self, spans, extra=(), size=None):
        size = size or self.scaled()
        for span in spans:
            tags = list(extra) + [self.span_tag(span.styles, size)]
            if span.url and ("link" in span.styles or "image" in span.styles):
                self.link_count += 1
                link_tag = "link%d" % self.link_count
                self.preview.tag_configure(link_tag)
                self.preview.tag_bind(link_tag, "<Button-1>", self._open_link(span.url))
                self.preview.tag_bind(link_tag, "<Enter>",
                                      lambda event: self.preview.configure(cursor="hand2"))
                self.preview.tag_bind(link_tag, "<Leave>",
                                      lambda event: self.preview.configure(cursor="arrow"))
                tags.append(link_tag)
            self.insert(span.text, tuple(tags))

    @staticmethod
    def _open_link(url):
        def handler(event):
            if re.match(r"^\s*(?:javascript|vbscript|data)\s*:", url, re.I):
                return
            try:
                webbrowser.open(url)
            except Exception:
                pass
        return handler

    def insert(self, text, tags=()):
        self.preview.insert(tk.END, text, tags)

    # ---- about ----

    # ---- help: the owner's manual, searchable ----

    @staticmethod
    def read_bundled(names, missing):
        """A file from beside the script. Windows compiles these into the exe."""
        folder = os.path.dirname(os.path.abspath(__file__))
        for name in names:
            path = os.path.join(folder, name)
            if os.path.exists(path):
                try:
                    with open(path, "r", encoding="utf-8", errors="replace") as handle:
                        return handle.read()
                except (IOError, OSError):
                    break
        return "%s\n\nPut %s in the same folder as minipad.py:\n  %s" % (
            missing, names[0], folder)

    @staticmethod
    def read_manual():
        return MiniPad.read_bundled(("MANUAL.md", "manual.md"),
                                    "The manual could not be found.")

    @staticmethod
    def read_license():
        return MiniPad.read_bundled(("LICENSE.txt", "license.txt"),
                                    "The license could not be found.")

    def build_help_page(self, page):
        wrapper = tk.Frame(page, padx=12, pady=10)
        wrapper.pack(fill=tk.BOTH, expand=True)

        bar = tk.Frame(wrapper)
        bar.pack(fill=tk.X, pady=(0, 8))
        tk.Label(bar, text="Search").pack(side=tk.LEFT)

        self.help_entry = tk.Entry(bar, width=34)
        self.help_entry.pack(side=tk.LEFT, padx=(8, 8))
        self.help_entry.bind("<KeyRelease>", lambda event: self.help_search(True))
        self.help_entry.bind("<Return>", lambda event: self.help_step(1))
        self.help_entry.bind("<Shift-Return>", lambda event: self.help_step(-1))
        self.help_entry.bind("<Escape>", lambda event: self.help_clear())

        tk.Button(bar, text="Previous", command=lambda: self.help_step(-1)).pack(side=tk.LEFT)
        tk.Button(bar, text="Next", command=lambda: self.help_step(1)).pack(side=tk.LEFT, padx=4)
        self.help_result = tk.Label(bar, text="Enter to jump, Shift+Enter to go back",
                                    fg=Theme.ui_dim())
        self.help_result.pack(side=tk.LEFT, padx=8)

        body = tk.Frame(wrapper)
        body.pack(fill=tk.BOTH, expand=True)
        self.help_text = tk.Text(body, wrap=tk.WORD, relief=tk.SUNKEN, borderwidth=1,
                                 padx=10, pady=8,
                                 font=("sans-serif", Theme.scale_font(10)))
        scroll = tk.Scrollbar(body, command=self.help_text.yview)
        self.help_text.configure(yscrollcommand=scroll.set)
        scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.help_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        self.help_text.tag_configure("h1", font=("sans-serif", Theme.scale_font(15), "bold"),
                                     spacing1=10, spacing3=6)
        self.help_text.tag_configure("h2", font=("sans-serif", Theme.scale_font(12), "bold"),
                                     spacing1=8, spacing3=4)
        self.help_text.tag_configure("h3", font=("sans-serif", Theme.scale_font(11), "bold"),
                                     spacing1=6)
        self.help_text.tag_configure("mono", font=("monospace", Theme.scale_font(9)))
        # The highlights name their own ink as well as their background: on a
        # dark theme the text is pale, and pale on yellow cannot be read.
        self.help_text.tag_configure("hit", background="#fff49e", foreground="#1a1a1a")
        self.help_text.tag_configure("current", background="#ffb03e", foreground="#1a1a1a")

        self.render_manual()
        self.help_text.configure(state=tk.DISABLED)
        self.help_matches = []
        self.help_index = -1
        self.help_query = ""

    def render_manual(self):
        """Lay the manual out for reading: headings weighted, code monospaced."""
        in_fence = False
        for line in self.read_manual().replace("\r\n", "\n").split("\n"):
            tag = None
            if line.lstrip().startswith("```"):
                in_fence = not in_fence
                continue
            if in_fence or line.startswith("|"):
                tag = "mono"
            elif line.startswith("### "):
                tag, line = "h3", line[4:]
            elif line.startswith("## "):
                tag, line = "h2", line[3:]
            elif line.startswith("# "):
                tag, line = "h1", line[2:]
            self.help_text.insert(tk.END, line + "\n", (tag,) if tag else ())

    def help_clear(self):
        self.help_entry.delete(0, tk.END)
        self.help_search(False)

    def help_search(self, jump):
        query = self.help_entry.get()
        self.help_text.tag_remove("hit", "1.0", tk.END)
        self.help_text.tag_remove("current", "1.0", tk.END)
        self.help_matches = []
        self.help_index = -1
        self.help_query = query

        if query:
            start = "1.0"
            while True:
                length = tk.IntVar()
                position = self.help_text.search(query, start, stopindex=tk.END,
                                                 nocase=True, count=length)
                if not position or not length.get():
                    break
                end = "%s+%dc" % (position, length.get())
                self.help_text.tag_add("hit", position, end)
                self.help_matches.append((position, end))
                start = end

        if jump and self.help_matches:
            self.help_index = 0
            self.help_show_current()
        self.help_update_result()

    def help_step(self, direction):
        if self.help_entry.get() != self.help_query:
            self.help_search(True)
            return "break"
        if not self.help_matches:
            return "break"
        if self.help_index < 0:
            self.help_index = 0 if direction > 0 else len(self.help_matches) - 1
        else:
            self.help_index = (self.help_index + direction) % len(self.help_matches)
        self.help_show_current()
        self.help_update_result()
        return "break"

    def help_show_current(self):
        self.help_text.tag_remove("current", "1.0", tk.END)
        start, end = self.help_matches[self.help_index]
        self.help_text.tag_add("current", start, end)
        self.help_text.see(start)

    def help_update_result(self):
        if not self.help_query:
            text, colour = "Enter to jump, Shift+Enter to go back", Theme.ui_dim()
        elif not self.help_matches:
            text, colour = "no matches", Theme.bad()
        else:
            text = "%d of %d match%s" % (self.help_index + 1, len(self.help_matches),
                                         "" if len(self.help_matches) == 1 else "es")
            colour = Theme.ui_dim()
        self.help_result.configure(text=text, fg=colour)

    def show_about(self, on_bunnies=False, on_help=False):
        dialog = tk.Toplevel(self.root)
        dialog.title("About " + APP_NAME)
        dialog.transient(self.root)
        # Wider than the Windows dialog: Linux default fonts are broader, and
        # at 640 the Bunnies descriptions were clipped at both ends.
        dialog.geometry("780x620")

        notebook = ttk.Notebook(dialog)
        notebook.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        about_page = tk.Frame(notebook)
        help_page = tk.Frame(notebook)
        license_page = tk.Frame(notebook)
        build_page = tk.Frame(notebook)
        bunnies_page = tk.Frame(notebook)
        notebook.add(about_page, text="About")
        notebook.add(help_page, text="Help")
        notebook.add(license_page, text="License")
        notebook.add(build_page, text="Build & Libraries")
        notebook.add(bunnies_page, text="Bunnies")

        # --- About ---
        header = tk.Frame(about_page, padx=18, pady=14)
        header.pack(fill=tk.X)
        tk.Label(header, text="%s %s" % (APP_NAME, VERSION),
                 font=("sans-serif", 20, "bold"), anchor="w").pack(fill=tk.X)
        ink_role(tk.Label(header, text="A small text editor with live Markdown preview",
                          anchor="w"), "dim").pack(fill=tk.X)
        tk.Label(header,
                 text="%s\nDate: %s" % (CREDIT, RELEASE_DATE),
                 font=("sans-serif", 11), justify=tk.LEFT, anchor="w").pack(fill=tk.X, pady=(10, 0))
        ink_role(tk.Label(header,
                          text="Copyright (c) 2026 %s. All rights reserved.\n"
                               "Free to use. The source code may not be copied or reused -\n"
                               "the full terms are on the License tab.\n\n"
                               "The full owner's manual is under Help, where it is searchable.\n"
                               "F1 opens it from anywhere. There is something under Bunnies, too."
                               % DESIGNER,
                          justify=tk.LEFT, anchor="w"), "dim").pack(fill=tk.X, pady=(14, 0))

        # --- Build & Libraries ---
        body = tk.Frame(build_page, padx=12, pady=12)
        body.pack(fill=tk.BOTH, expand=True)
        details = tk.Text(body, wrap=tk.NONE, font=("monospace", 9), relief=tk.SUNKEN,
                          borderwidth=1)
        scroll = tk.Scrollbar(body, command=details.yview)
        details.configure(yscrollcommand=scroll.set)
        scroll.pack(side=tk.RIGHT, fill=tk.Y)
        details.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        details.insert("1.0", self.about_report())
        details.configure(state=tk.DISABLED)

        def copy_details():
            self.root.clipboard_clear()
            self.root.clipboard_append(self.about_report())

        tk.Button(build_page, text="Copy details", command=copy_details).pack(
            side=tk.LEFT, padx=12, pady=(0, 12))

        # --- Help ---
        self.build_help_page(help_page)

        # --- License ---
        self.build_license_page(license_page)

        # --- Bunnies ---
        self.build_bunnies_page(bunnies_page)

        tk.Button(dialog, text="OK", width=10, command=dialog.destroy).pack(
            side=tk.RIGHT, padx=12, pady=(0, 12))

        apply_ttk_theme(dialog)
        apply_theme_to(dialog)

        if on_bunnies:
            notebook.select(bunnies_page)
        elif on_help:
            notebook.select(help_page)
            self.help_entry.focus_set()

        dialog.grab_set()

    def build_license_page(self, page):
        """The license, shown in the program so LICENSE.txt need not be fetched."""
        wrapper = tk.Frame(page, padx=12, pady=10)
        wrapper.pack(fill=tk.BOTH, expand=True)

        body = tk.Frame(wrapper)
        body.pack(fill=tk.BOTH, expand=True)
        self.license_text = tk.Text(body, wrap=tk.WORD, relief=tk.SUNKEN, borderwidth=1,
                                    padx=10, pady=8,
                                    font=("monospace", Theme.scale_font(9)))
        scroll = tk.Scrollbar(body, command=self.license_text.yview)
        self.license_text.configure(yscrollcommand=scroll.set)
        scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.license_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        self.license_text.insert("1.0", self.read_license())
        self.license_text.configure(state=tk.DISABLED)

        actions = tk.Frame(wrapper)
        actions.pack(fill=tk.X, pady=(8, 0))
        tk.Button(actions, text="Copy license",
                  command=self.copy_license).pack(side=tk.LEFT)
        tk.Button(actions, text="Save a copy...",
                  command=self.save_license).pack(side=tk.LEFT, padx=6)

    def copy_license(self):
        self.root.clipboard_clear()
        self.root.clipboard_append(self.read_license())

    def save_license(self):
        chosen = filedialog.asksaveasfilename(
            parent=self.root, defaultextension=".txt", initialfile="LICENSE.txt",
            filetypes=[("Text files", "*.txt"), ("All files", "*")])
        if not chosen:
            return
        try:
            with open(chosen, "w", encoding="utf-8") as handle:
                handle.write(self.read_license())
        except (IOError, OSError) as error:
            messagebox.showwarning(APP_NAME, "Could not save the license:\n\n%s" % error,
                                   parent=self.root)

    def build_bunnies_page(self, page):
        """The easter eggs, and the commands that reach them."""
        wrapper = tk.Frame(page, padx=18, pady=14)
        wrapper.pack(fill=tk.BOTH, expand=True)

        tk.Label(wrapper, text="Bunnies", font=("sans-serif", 16, "bold"),
                 anchor="w").pack(fill=tk.X)
        ink_role(tk.Label(wrapper, text="Games hiding inside a text editor. "
                                        "Nothing here touches your document.",
                          anchor="w"), "dim").pack(fill=tk.X, pady=(2, 12))

        tk.Label(wrapper, text="Commands", font=("sans-serif", 10, "bold"),
                 anchor="w").pack(fill=tk.X)
        commands = tk.Text(wrapper, height=6, wrap=tk.NONE, font=("monospace", 9),
                           relief=tk.SUNKEN, borderwidth=1)
        commands.pack(fill=tk.X, pady=(4, 14))
        commands.insert("1.0",
                        "  !bunnies            type it in the editor to open this tab\n"
                        "  Ctrl+Shift+B        open this tab from anywhere\n"
                        "  About > Bunnies     the way you found it\n"
                        "\n"
                        "  The typed command is left in your text - delete it yourself.")
        commands.configure(state=tk.DISABLED)

        tk.Label(wrapper, text="Games", font=("sans-serif", 10, "bold"),
                 anchor="w").pack(fill=tk.X)

        mines_row = tk.Frame(wrapper)
        mines_row.pack(fill=tk.X, pady=(6, 10))
        tk.Button(mines_row, text="Minesweeper", width=14,
                  command=lambda: MinesweeperWindow(self.root)).pack(side=tk.LEFT)
        ink_role(tk.Label(mines_row, justify=tk.LEFT,
                          text="Beginner 9x9/10, Intermediate 16x16/40, Expert 30x16/99.\n"
                               "Left click reveals, right click flags. First click is "
                               "always safe."), "dim").pack(side=tk.LEFT, padx=12)

        tic_row = tk.Frame(wrapper)
        tic_row.pack(fill=tk.X, pady=(0, 10))
        tk.Button(tic_row, text="Tic-tac-toe", width=14,
                  command=lambda: TicTacToeWindow(self.root)).pack(side=tk.LEFT)
        ink_role(tk.Label(tic_row, justify=tk.LEFT,
                          text="Easy plays at random, Medium blocks and takes wins,\n"
                               "Hard is full minimax - it cannot be beaten, only drawn."
                          ), "dim").pack(side=tk.LEFT, padx=12)

        ink_role(tk.Label(wrapper,
                          text="Designed by %s. Built in conversation with %s."
                               % (DESIGNER, AUTHOR),
                          anchor="w"), "dim").pack(fill=tk.X, pady=(10, 0))

    # Typing the command in the document opens the Bunnies tab. The text is left
    # exactly as typed - an easter egg should not edit someone's file.
    def check_for_bunny_command(self):
        caret = self.editor.index(tk.INSERT)
        line, column = caret.split(".")
        if int(column) < len(BUNNY_COMMAND):
            return
        start = "%s.%d" % (line, int(column) - len(BUNNY_COMMAND))
        if self.editor.get(start, caret).lower() != BUNNY_COMMAND:
            return

        # Only fire when the command stands on its own, not inside a longer word.
        if int(column) - len(BUNNY_COMMAND) > 0:
            before = self.editor.get("%s.%d" % (line, int(column) - len(BUNNY_COMMAND) - 1), start)
            if before and (before.isalnum() or before == "_"):
                return

        self.show_about(on_bunnies=True)

    def about_report(self):
        script = os.path.abspath(sys.argv[0] if sys.argv and sys.argv[0] else __file__)
        try:
            built = datetime.datetime.fromtimestamp(os.path.getmtime(script))
            built_text = built.strftime("%Y-%m-%d %H:%M:%S")
            size_text = "{:,} bytes".format(os.path.getsize(script))
        except OSError:
            built_text = "(unknown)"
            size_text = "(unknown)"

        lines = [
            "BUILD",
            "  Version          " + VERSION,
            "  Release date     " + RELEASE_DATE,
            "  Script           " + script,
            "  Modified         " + built_text,
            "  Size             " + size_text,
            "  Language         Python 3 (no compilation step)",
            "  Source           minipad.py (single file, standard library only)",
            "",
            "RUNTIME",
            "  Python           " + sys.version.split()[0] + " (" + platform.python_implementation() + ")",
            "  Tcl/Tk           " + str(self.root.tk.call("info", "patchlevel")),
            "  Platform         " + platform.platform(),
            "  Distribution     " + linux_distribution(),
            "  Display server   " + display_server(),
            "  Machine          " + platform.node() + " (" + platform.machine() + ")",
            "",
            "APPEARANCE",
            "  Mode             " + Theme.mode,
            "  Theme            " + ("(none)" if Theme.preset_name == Theme.NO_PRESET
                                     else Theme.preset_name),
            "  Palette in use   " + Theme.base().name + (" (dark)" if Theme.is_dark() else " (light)"),
            "  Custom colours   " + ("background %s" % Theme.custom_back if Theme.custom_back else "none")
                                  + (", text %s" % Theme.custom_text if Theme.custom_text else ""),
            "  Zoom             %d%%" % Theme.zoom,
            "  Desktop prefers  " + ("dark" if Theme.system_prefers_dark() else "light")
                                  + " (asked of gsettings)",
            "  Settings mode    " + ("Portable - kept beside the program"
                                     if Theme.is_portable()
                                     else "Normal - kept in your home directory"),
            "  Settings file    " + Theme.settings_path(),
            "  Switch with      Format > Portable Settings...",
            "",
            "LINUX LIBRARIES USED",
            "  tkinter          UI toolkit binding (Debian/Ubuntu package python3-tk)",
            "  libtk / libtcl   the Tk widget toolkit itself, drawing through X11",
            "                   or XWayland",
            "  re               regular expressions for the Markdown parser",
            "  webbrowser       opens preview links via xdg-open",
            "  os, sys, platform  file, path and environment access",
            "",
            "  No third-party packages, no pip install, and no network access.",
            "  Rendering differs from the Windows build by necessity: Windows hosts",
            "  MSHTML (the Internet Explorer engine) in a WebBrowser control, while",
            "  this build styles the Markdown directly with Tk text tags. Exported",
            "  HTML is identical from either build.",
            "",
            "FEATURES",
            "  Text       open, save, save as, command-line path, encoding and",
            "             line-ending preserved, word wrap, font chooser",
            "  Appearance light, dark or follow the desktop, nine named themes,",
            "             custom background and text colours, and zoom from 50%",
            "             to 300%. Format > Appearance. Saved between sessions.",
            "  Markdown   live preview (F9), headings, bold, italic, strikethrough,",
            "             inline and fenced code, quotes, nested lists, pipe",
            "             tables, links, images, rules, export as HTML",
            "  Preview    three modes under Markdown > Preview Mode:",
            "             read-only, click-to-jump-to-source, and editable.",
            "             Editable shows the Markdown source styled in place, with",
            "             the markers visible, so typing there edits the file",
            "             directly and nothing can be reshaped by a conversion.",
            "             The Windows build instead lets you type onto the rendered",
            "             page and converts the result back to Markdown, which its",
            "             HTML engine makes possible.",
            "",
            "  Not present on Linux: drag-and-drop of files onto the window, which",
            "  needs the non-standard tkdnd extension.",
        ]
        return "\n".join(lines)


# ==========================================================================
# The Bunnies: easter-egg games, reached from About > Bunnies
#
# The rules live in plain functions and classes with no UI attached, so they can
# be tested without a display.
# ==========================================================================

class MinesweeperBoard(object):
    """Minesweeper rules. States: 'playing', 'won', 'lost'."""

    def __init__(self, width, height, mines, rng=None):
        self.width = width
        self.height = height
        # Leave room for the safe opening: the first click and its neighbours.
        self.mines = max(1, min(mines, max(1, width * height - 9)))
        self.rng = rng or random.Random()
        self.mine = [[False] * height for _ in range(width)]
        self.revealed = [[False] * height for _ in range(width)]
        self.flagged = [[False] * height for _ in range(width)]
        self.placed = False
        self.state = "playing"

    def in_bounds(self, x, y):
        return 0 <= x < self.width and 0 <= y < self.height

    def is_mine(self, x, y):
        return self.in_bounds(x, y) and self.mine[x][y]

    def is_revealed(self, x, y):
        return self.in_bounds(x, y) and self.revealed[x][y]

    def is_flagged(self, x, y):
        return self.in_bounds(x, y) and self.flagged[x][y]

    @property
    def flag_count(self):
        return sum(1 for column in self.flagged for cell in column if cell)

    @property
    def revealed_count(self):
        return sum(1 for column in self.revealed for cell in column if cell)

    def adjacent(self, x, y):
        count = 0
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if (dx or dy) and self.is_mine(x + dx, y + dy):
                    count += 1
        return count

    def toggle_flag(self, x, y):
        if self.state != "playing" or not self.in_bounds(x, y):
            return
        if self.revealed[x][y]:
            return
        self.flagged[x][y] = not self.flagged[x][y]

    def reveal(self, x, y):
        if self.state != "playing" or not self.in_bounds(x, y):
            return
        if self.flagged[x][y] or self.revealed[x][y]:
            return

        if not self.placed:
            self._place_mines(x, y)

        if self.mine[x][y]:
            self.revealed[x][y] = True
            self.state = "lost"
            for mx in range(self.width):
                for my in range(self.height):
                    if self.mine[mx][my]:
                        self.revealed[mx][my] = True
            return

        # Flood outwards from empty cells, iteratively so a big board is safe.
        pending = [(x, y)]
        while pending:
            cx, cy = pending.pop()
            if not self.in_bounds(cx, cy):
                continue
            if self.revealed[cx][cy] or self.flagged[cx][cy] or self.mine[cx][cy]:
                continue
            self.revealed[cx][cy] = True
            if self.adjacent(cx, cy):
                continue
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if dx or dy:
                        pending.append((cx + dx, cy + dy))

        if self.revealed_count == self.width * self.height - self.mines:
            self.state = "won"

    def _place_mines(self, safe_x, safe_y):
        candidates = [(x, y)
                      for x in range(self.width)
                      for y in range(self.height)
                      if abs(x - safe_x) > 1 or abs(y - safe_y) > 1]
        for _ in range(min(self.mines, len(candidates))):
            pick = self.rng.randrange(len(candidates))
            x, y = candidates.pop(pick)
            self.mine[x][y] = True
        self.placed = True


TTT_EMPTY = " "
TTT_HUMAN = "X"
TTT_COMPUTER = "O"

_TTT_LINES = ((0, 1, 2), (3, 4, 5), (6, 7, 8),
              (0, 3, 6), (1, 4, 7), (2, 5, 8),
              (0, 4, 8), (2, 4, 6))


def ttt_winner(board):
    """'X' or 'O' if won, 'D' for a draw, None while the game is still going."""
    for line in _TTT_LINES:
        first = board[line[0]]
        if first != TTT_EMPTY and first == board[line[1]] == board[line[2]]:
            return first
    return None if TTT_EMPTY in board else "D"


def _ttt_immediate(board, player):
    for line in _TTT_LINES:
        own = 0
        free = -1
        for cell in line:
            if board[cell] == player:
                own += 1
            elif board[cell] == TTT_EMPTY:
                free = cell
            else:
                own = -9
                break
        if own == 2 and free >= 0:
            return free
    return -1


def _ttt_minimax(board, computer_turn, depth):
    winner = ttt_winner(board)
    if winner == TTT_COMPUTER:
        return 10 - depth
    if winner == TTT_HUMAN:
        return depth - 10
    if winner == "D":
        return 0

    best = None
    for n in range(9):
        if board[n] != TTT_EMPTY:
            continue
        board[n] = TTT_COMPUTER if computer_turn else TTT_HUMAN
        score = _ttt_minimax(board, not computer_turn, depth + 1)
        board[n] = TTT_EMPTY
        if best is None:
            best = score
        elif computer_turn:
            best = max(best, score)
        else:
            best = min(best, score)
    return best if best is not None else 0


def ttt_best_move(board, skill, rng):
    """skill is 'easy', 'medium' or 'hard'. Returns a cell index, or -1 if full."""
    free = [n for n in range(9) if board[n] == TTT_EMPTY]
    if not free:
        return -1

    if skill == "easy":
        return rng.choice(free)

    if skill == "medium":
        win = _ttt_immediate(board, TTT_COMPUTER)
        if win >= 0:
            return win
        block = _ttt_immediate(board, TTT_HUMAN)
        if block >= 0:
            return block
        if board[4] == TTT_EMPTY:
            return 4
        corners = [c for c in (0, 2, 6, 8) if board[c] == TTT_EMPTY]
        return rng.choice(corners) if corners else rng.choice(free)

    best_score = None
    best_moves = []
    working = list(board)
    for move in free:
        working[move] = TTT_COMPUTER
        score = _ttt_minimax(working, False, 0)
        working[move] = TTT_EMPTY
        if best_score is None or score > best_score:
            best_score = score
            best_moves = [move]
        elif score == best_score:
            best_moves.append(move)
    return rng.choice(best_moves)


class MinesweeperWindow(object):
    CELL = 22
    NUMBER_COLOURS = {1: "#0000dc", 2: "#007800", 3: "#dc0000", 4: "#000082",
                      5: "#820000", 6: "#008282", 7: "#000000", 8: "#5a5a5a"}
    LEVELS = {"beginner": (9, 9, 10), "intermediate": (16, 16, 40), "expert": (30, 16, 99)}

    def __init__(self, parent):
        self.rng = random.Random()
        self.level = "beginner"
        self.board = None
        self.seconds = 0
        self.started = False
        self.tick_job = None

        self.win = tk.Toplevel(parent)
        self.win.title("Minesweeper")
        self.win.resizable(False, False)
        self.win.transient(parent)

        menubar = tk.Menu(self.win)
        game_menu = tk.Menu(menubar, tearoff=0)
        game_menu.add_command(label="New", command=lambda: self.new_game(self.level))
        game_menu.add_separator()
        game_menu.add_command(label="Beginner  (9 x 9, 10 mines)",
                              command=lambda: self.new_game("beginner"))
        game_menu.add_command(label="Intermediate  (16 x 16, 40 mines)",
                              command=lambda: self.new_game("intermediate"))
        game_menu.add_command(label="Expert  (30 x 16, 99 mines)",
                              command=lambda: self.new_game("expert"))
        game_menu.add_separator()
        game_menu.add_command(label="Close", command=self.close)
        menubar.add_cascade(label="Game", menu=game_menu)
        self.win.configure(menu=menubar)

        header = tk.Frame(self.win)
        header.pack(fill=tk.X, padx=8, pady=6)
        self.mine_label = tk.Label(header, text="Mines 0", font=("monospace", 10, "bold"))
        self.mine_label.pack(side=tk.LEFT)
        self.time_label = tk.Label(header, text="Time 0", font=("monospace", 10, "bold"))
        self.time_label.pack(side=tk.LEFT, padx=14)
        self.status_label = tk.Label(header, text="", font=("sans-serif", 9, "bold"))
        self.status_label.pack(side=tk.LEFT)

        # The board keeps its classic grey whatever the theme is doing: the
        # number colours are the game, and they are unreadable on anything else.
        self.canvas = tk.Canvas(self.win, highlightthickness=0, background="#bdbdbd")
        self.canvas._minipad_keep_colours = True
        self.canvas.pack(padx=1, pady=(0, 1))
        self.canvas.bind("<Button-1>", self.on_left_click)
        self.canvas.bind("<Button-3>", self.on_right_click)

        self.win.protocol("WM_DELETE_WINDOW", self.close)
        apply_theme_to(self.win)
        self.new_game(self.level)

    def close(self):
        if self.tick_job is not None:
            self.win.after_cancel(self.tick_job)
            self.tick_job = None
        self.win.destroy()

    def new_game(self, level):
        self.level = level
        width, height, mines = self.LEVELS[level]
        self.board = MinesweeperBoard(width, height, mines, self.rng)
        self.seconds = 0
        self.started = False
        if self.tick_job is not None:
            self.win.after_cancel(self.tick_job)
            self.tick_job = None
        self.time_label.configure(text="Time 0")
        self.status_label.configure(text="")
        self.canvas.configure(width=width * self.CELL, height=height * self.CELL)
        self.update_mine_label()
        self.draw()

    def update_mine_label(self):
        self.mine_label.configure(text="Mines %d" % (self.board.mines - self.board.flag_count))

    def tick(self):
        self.seconds += 1
        self.time_label.configure(text="Time %d" % self.seconds)
        self.tick_job = self.win.after(1000, self.tick)

    def on_left_click(self, event):
        if self.board.state != "playing":
            return
        x, y = event.x // self.CELL, event.y // self.CELL
        if not self.board.in_bounds(x, y):
            return
        if not self.started:
            self.started = True
            self.tick_job = self.win.after(1000, self.tick)
        self.board.reveal(x, y)
        self.after_move()

    def on_right_click(self, event):
        if self.board.state != "playing":
            return
        x, y = event.x // self.CELL, event.y // self.CELL
        self.board.toggle_flag(x, y)
        self.after_move()

    def after_move(self):
        self.update_mine_label()
        if self.board.state == "won":
            self.stop_clock()
            self.status_label.configure(text="Cleared in %ds" % self.seconds, fg=Theme.good())
        elif self.board.state == "lost":
            self.stop_clock()
            self.status_label.configure(text="Boom. Game > New to try again", fg=Theme.bad())
        self.draw()

    def stop_clock(self):
        if self.tick_job is not None:
            self.win.after_cancel(self.tick_job)
            self.tick_job = None

    def draw(self):
        cell = self.CELL
        self.canvas.delete("all")
        for x in range(self.board.width):
            for y in range(self.board.height):
                left, top = x * cell, y * cell
                right, bottom = left + cell, top + cell

                if not self.board.is_revealed(x, y):
                    self.canvas.create_rectangle(left, top, right, bottom,
                                                 fill="#c6c6c6", outline="#8f8f8f")
                    self.canvas.create_line(left + 1, top + 1, right - 1, top + 1, fill="#efefef")
                    self.canvas.create_line(left + 1, top + 1, left + 1, bottom - 1, fill="#efefef")
                    if self.board.is_flagged(x, y):
                        self.canvas.create_text(left + cell / 2, top + cell / 2, text="!",
                                                fill="#b22222", font=("sans-serif", 10, "bold"))
                    continue

                self.canvas.create_rectangle(left, top, right, bottom,
                                             fill="#dedede", outline="#c0c0c0")
                if self.board.is_mine(x, y):
                    self.canvas.create_oval(left + 6, top + 6, right - 6, bottom - 6, fill="black")
                    continue

                count = self.board.adjacent(x, y)
                if count:
                    self.canvas.create_text(left + cell / 2, top + cell / 2, text=str(count),
                                            fill=self.NUMBER_COLOURS.get(count, "black"),
                                            font=("sans-serif", 10, "bold"))


class TicTacToeWindow(object):
    def __init__(self, parent):
        self.rng = random.Random()
        self.board = [TTT_EMPTY] * 9
        self.cells = []
        self.human_wins = 0
        self.computer_wins = 0
        self.draws = 0

        self.win = tk.Toplevel(parent)
        self.win.title("Tic-tac-toe")
        self.win.resizable(False, False)
        self.win.transient(parent)

        top = tk.Frame(self.win)
        top.pack(fill=tk.X, padx=12, pady=(12, 6))
        tk.Label(top, text="Skill").pack(side=tk.LEFT)
        self.skill_var = tk.StringVar(value="medium")
        for label, value in (("Easy", "easy"), ("Medium", "medium"), ("Hard", "hard")):
            tk.Radiobutton(top, text=label, value=value, variable=self.skill_var,
                           command=self.new_game).pack(side=tk.LEFT, padx=4)

        grid = tk.Frame(self.win)
        grid.pack(padx=12)
        for n in range(9):
            button = tk.Button(grid, text="", width=3, height=1,
                               font=("sans-serif", 22, "bold"),
                               command=lambda index=n: self.on_click(index))
            button.grid(row=n // 3, column=n % 3, padx=2, pady=2)
            self.cells.append(button)

        self.status = tk.Label(self.win, text="", font=("sans-serif", 10, "bold"))
        self.status.pack(padx=12, pady=(8, 0), anchor="w")
        self.score = ink_role(tk.Label(self.win, text=""), "dim")
        self.score.pack(padx=12, anchor="w")

        buttons = tk.Frame(self.win)
        buttons.pack(fill=tk.X, padx=12, pady=10)
        tk.Button(buttons, text="New game", command=self.new_game).pack(side=tk.LEFT)
        tk.Button(buttons, text="Close", command=self.win.destroy).pack(side=tk.RIGHT)

        apply_theme_to(self.win)
        self.new_game()

    def new_game(self):
        self.board = [TTT_EMPTY] * 9
        for button in self.cells:
            button.configure(text="", state=tk.NORMAL)
        self.status.configure(text="Your turn - you are X", fg=Theme.menu_text())
        self.update_score()

    def update_score(self):
        self.score.configure(text="You %d   MiniPad %d   Draws %d"
                                  % (self.human_wins, self.computer_wins, self.draws))

    def on_click(self, index):
        if self.board[index] != TTT_EMPTY or ttt_winner(self.board) is not None:
            return
        self.play(index, TTT_HUMAN)
        if self.finish():
            return
        reply = ttt_best_move(self.board, self.skill_var.get(), self.rng)
        if reply >= 0:
            self.play(reply, TTT_COMPUTER)
        self.finish()

    def play(self, index, player):
        self.board[index] = player
        colour = Theme.accent() if player == TTT_HUMAN else Theme.bad()
        # disabledforeground as well, so the finished board stays readable once
        # the buttons are switched off.
        self.cells[index].configure(text=player, fg=colour, disabledforeground=colour)

    def finish(self):
        winner = ttt_winner(self.board)
        if winner is None:
            return False
        for button in self.cells:
            button.configure(state=tk.DISABLED)
        if winner == TTT_HUMAN:
            self.human_wins += 1
            self.status.configure(text="You win.", fg=Theme.good())
        elif winner == TTT_COMPUTER:
            self.computer_wins += 1
            self.status.configure(text="MiniPad wins.", fg=Theme.bad())
        else:
            self.draws += 1
            self.status.configure(text="A draw.", fg=Theme.menu_text())
        self.update_score()
        return True


def linux_distribution():
    try:
        with open("/etc/os-release", "r", encoding="utf-8") as handle:
            for line in handle:
                if line.startswith("PRETTY_NAME="):
                    return line.split("=", 1)[1].strip().strip('"')
    except (IOError, OSError):
        pass
    return "(unknown)"


def display_server():
    if os.environ.get("WAYLAND_DISPLAY"):
        return "Wayland (Tk via XWayland)"
    if os.environ.get("DISPLAY"):
        return "X11 (" + os.environ["DISPLAY"] + ")"
    return "(none detected)"


def main():
    Theme.load()        # before any window exists, so nothing flashes untinted
    root = tk.Tk()
    root.title(APP_NAME)
    MiniPad(root, sys.argv[1:])     # every path on the command line gets a tab
    root.mainloop()


if __name__ == "__main__":
    main()
