# Starts the built MiniPad.exe and confirms it actually reaches a window.
#
# WHY THIS EXISTS
#   1.11 shipped a crash that stopped the program starting at all. It got through a
#   review that read every line of both builds and ran harnesses over markdown
#   conversion, HTML round-trips, control layout and cross-build parity - none of
#   which ever launched the program. Every test loaded the assembly and called into
#   it; none started the process.
#
#   A test that calls your code is not a test that your program runs. This script is
#   the cheapest possible check of the thing all the others assumed.
#
# USAGE
#   .\verify-launch.ps1            # exit 0 = reached a window, 1 = did not
#   .\verify-launch.ps1 -Screenshot   # also save proof to verify-launch.png
#
# Run it after build.ps1, before committing a release.

param([switch]$Screenshot)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$exe = Join-Path $PSScriptRoot "MiniPad.exe"
if (-not (Test-Path $exe)) { Write-Host "MiniPad.exe not found - run build.ps1 first" -ForegroundColor Red; exit 1 }

"testing: $exe"
"built  : $((Get-Item $exe).LastWriteTime)"

# A copy left running from a previous check would make this pass without proving anything.
Get-Process MiniPad -ErrorAction SilentlyContinue | Stop-Process -Force

$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 5
$p.Refresh()

if ($p.HasExited) {
    Write-Host "FAIL - exited with code $($p.ExitCode) before showing a window" -ForegroundColor Red
    exit 1
}

# The .NET unhandled-exception dialog is a SEPARATE window owned by another process,
# so the app can be "still running" while sitting behind a crash box. Check for it.
$crash = Get-Process | Where-Object { $_.MainWindowTitle -match 'Microsoft \.NET Framework|has stopped working' }
if ($crash) {
    Write-Host "FAIL - crash dialog present: $($crash.MainWindowTitle -join ', ')" -ForegroundColor Red
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

if (-not $p.MainWindowTitle) {
    Write-Host "FAIL - process is alive but has no window title" -ForegroundColor Red
    Stop-Process -Id $p.Id -Force
    exit 1
}
"window : '$($p.MainWindowTitle)'"

if ($Screenshot) {
    $shot = Join-Path $PSScriptRoot "verify-launch.png"
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "shot   : $shot"
}

Stop-Process -Id $p.Id -Force
Write-Host "PASS - launched to a window, no crash dialog" -ForegroundColor Green
exit 0
