# Builds MiniPad.exe using the C# compiler shipped with .NET Framework 4
# (no .NET SDK required - it is present on every Windows install).
#
# Everything is passed as a RELATIVE path on purpose. This folder sits under
# "S:\Claude Projects\...", and the space in that path makes PowerShell re-quote
# switches like /resource:<path>,<name>, which csc then mistakes for a filename.

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

Push-Location $PSScriptRoot
try {
    $sources = @(
        "MiniPad.cs",
        "Markdown.cs",
        "HtmlToMarkdown.cs",
        "About.cs",
        "Help.cs",
        "Games.cs",
        "Theme.cs",
        "Appearance.cs",
        "Document.cs",
        "TabStrip.cs"
    )

    & $csc /nologo /target:winexe /optimize+ /out:MiniPad.exe /win32icon:MiniPad.ico `
        /resource:MANUAL.md,MiniPad.MANUAL.md `
        /resource:LICENSE.txt,MiniPad.LICENSE.txt `
        /reference:System.dll `
        /reference:System.Drawing.dll `
        /reference:System.Windows.Forms.dll `
        $sources

    if ($LASTEXITCODE -eq 0) {
        "Built $(Join-Path $PSScriptRoot 'MiniPad.exe')"
    } else {
        "Build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
