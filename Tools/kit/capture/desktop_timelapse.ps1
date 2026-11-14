<#
.SYNOPSIS
  Desktop timelapse for the owner: one screenshot every N seconds into a UTC-named session folder, no ffmpeg needed.

.DESCRIPTION
  Start:   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/kit/capture/desktop_timelapse.ps1 [-Out <dir>] [-IntervalSec 10]
  Stop:    ... desktop_timelapse.ps1 -Stop
  Status:  ... desktop_timelapse.ps1 -Status
  One shot: ... desktop_timelapse.ps1 -Once -Out <dir>

  - Frames go to <Out>/<YYYYMMDDTHHMMSSZ>/<YYYYMMDDTHHMMSSZ>.jpg (UTC everywhere; the WoO timelapse mixed local time
    and UTC). -Out defaults to <workspace>/Recordings/timelapse. A second session started in the same second gets a
    _2 suffix, so two sessions never share a folder.
  - Capture uses System.Drawing (the default engine, works without ffmpeg). -Engine ffmpeg uses gdigrab only when
    ffmpeg is already on PATH, and falls back to System.Drawing otherwise; the kit never installs ffmpeg. ffmpeg
    frames are renamed to UTC stamps (session start + (N-1) x interval) while recording and when it ends.
  - A separate worker process does the capturing in a visible, minimized console window titled
    "GSK desktop timelapse: RECORDING", so the owner can always see that the screen is being recorded. It is always
    Windows PowerShell 5.1 (System32\WindowsPowerShell\v1.0\powershell.exe), whichever host started the front end.
    Its PID, process start time (UTC, read through the process handle of the new worker, never looked up again by
    number), session folder and image name go to .harness/timelapse.pid (lines 1-4). Starts are serialised by
    .harness/timelapse.start.lock: a second start while one runs, or while another start is in progress, exits 75.
  - -Stop first checks that the PID's start time (within 2 s) and image name still match the file, holding a
    handle so the PID cannot be reused meanwhile. It then withdraws the PID file: the worker notices within about a
    second, finishes, writes _session.json (reason "stopped") and exits. Only if it has not exited after 15 s is it
    killed by PID tree. A dead or reused PID means the file is stale: it is deleted and nothing is killed. If the
    process facts cannot be read, nothing is killed and the exit code is 75 (CONTRACTS.md section 1.6). Nothing is
    ever killed by name.
  - The worker stops by itself when the PID file no longer names it, after -MaxFrames frames, after 20 capture
    errors in a row (for example a locked screen), or when free disk space drops below -MinFreeGB (both engines).
    Each session folder gets _session.json {start_utc, end_utc, frames, engine, interval_sec, reason, pid}.
  - Exit codes: 0 ok, 1 capture or start failed, 2 usage, 3 no kit workspace, 75 already running, starting, or
    unverifiable.
  - -Json prints one JSON line. Compile frames without ffmpeg:
      uv run --project Tools/kit/capture Tools/kit/capture/frames_to_gif.py --in <session dir> --out <gif>
      uv run --project Tools/kit/capture Tools/kit/capture/contact_sheet.py --in <session dir> --out <png> --select every:30
  Windows PowerShell 5.1 compatible (also runs as a front end under PowerShell 7).
#>
[CmdletBinding()]
param(
  [string]$Out,
  [ValidateRange(1, 86400)][int]$IntervalSec = 10,
  [switch]$Stop,
  [switch]$Status,
  [switch]$Once,
  [ValidateSet('drawing', 'ffmpeg')][string]$Engine = 'drawing',
  [ValidateRange(0, 10000000)][int]$MaxFrames = 0,
  [ValidateRange(0, 16384)][int]$MaxWidth = 1920,
  [ValidateRange(10, 100)][int]$Quality = 80,
  [double]$MinFreeGB = 10,
  [string]$Workspace,
  [switch]$Json,
  [switch]$Worker
)

$ErrorActionPreference = 'Stop'
$script:KitPidFileRel = '.harness\timelapse.pid'
$script:StartLockRel = '.harness\timelapse.start.lock'
$script:StopGraceSec = 15
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
try { [Console]::OutputEncoding = $script:Utf8NoBom } catch { }

function Get-UtcNow {
  if ($env:GSK_NOW) {
    try { return ([DateTime]::Parse($env:GSK_NOW, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal)) } catch { }
  }
  return (Get-Date).ToUniversalTime()
}
function Format-Iso([DateTime]$d) { return $d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [Globalization.CultureInfo]::InvariantCulture) }
function Format-Stamp([DateTime]$d) { return $d.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", [Globalization.CultureInfo]::InvariantCulture) }

function Write-Result([System.Collections.IDictionary]$Result, [int]$Code, [string[]]$Text) {
  if ($Json) {
    [Console]::Out.WriteLine(($Result | ConvertTo-Json -Compress -Depth 6))
  } elseif ($Text) {
    foreach ($line in $Text) { [Console]::Out.WriteLine($line) }
  } else {
    foreach ($k in $Result.Keys) { [Console]::Out.WriteLine(('{0}: {1}' -f $k, $Result[$k])) }
  }
  exit $Code
}
function Fail([int]$Code, [string]$Message, [System.Collections.IDictionary]$Extra) {
  $r = [ordered]@{ ok = $false; exit = $Code; error = $Message }
  if ($Extra) { foreach ($k in $Extra.Keys) { $r[$k] = $Extra[$k] } }
  [Console]::Error.WriteLine("error: $Message")
  Write-Result $r $Code @("error: $Message")
}

function Find-Workspace {
  $starts = @()
  if ($Workspace) { $starts += $Workspace }
  elseif ($env:CLAUDE_PROJECT_DIR) { $starts += $env:CLAUDE_PROJECT_DIR }
  else { $starts += (Get-Location).Path; $starts += $PSScriptRoot }
  foreach ($s in $starts) {
    if (-not $s) { continue }
    $d = [System.IO.Path]::GetFullPath($s)
    while ($d) {
      if (Test-Path -LiteralPath (Join-Path $d 'Tools\kit\kit.json') -PathType Leaf) { return $d }
      $parent = [System.IO.Path]::GetDirectoryName($d)
      if (-not $parent -or $parent -eq $d) { break }
      $d = $parent
    }
    if ($Workspace) { break }
  }
  return $null
}

function Get-RelPosix([string]$Base, [string]$Path) {
  if (-not $Path) { return $null }
  $full = [System.IO.Path]::GetFullPath($Path)
  if ($Base) {
    $b = [System.IO.Path]::GetFullPath($Base).TrimEnd('\') + '\'
    if ($full.StartsWith($b, [StringComparison]::OrdinalIgnoreCase)) { return $full.Substring($b.Length).Replace('\', '/') }
  }
  return $full.Replace('\', '/')
}

function Resolve-Dir([string]$Path) {
  if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
  return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}

function Write-TextNoBom([string]$Path, [string]$Text) {
  $dir = [System.IO.Path]::GetDirectoryName($Path)
  if (-not (Test-Path -LiteralPath $dir)) { [void][System.IO.Directory]::CreateDirectory($dir) }
  $tmp = "$Path.$PID.tmp"
  [System.IO.File]::WriteAllText($tmp, $Text, $script:Utf8NoBom)
  Move-Item -LiteralPath $tmp -Destination $Path -Force
}

# One argument for a Start-Process argument line: quoted, with trailing backslashes doubled so a path such as C:\
# does not escape the closing quote and swallow the next arguments (Windows paths cannot contain '"').
function ConvertTo-ProcessArg([string]$Value) { return '"' + ($Value -replace '(\\+)$', '$1$1') + '"' }

# The only kill in this script: by PID tree (CONTRACTS.md section 1.6), never by name. taskkill writes to stderr when
# the target or a tree member has already exited; under 'Stop' that would be a terminating error, so it runs with a
# local 'Continue' and the caller judges the result by the process handle, not by this exit code.
function Invoke-TreeKill([int]$ProcId) {
  $ErrorActionPreference = 'Continue'
  try {
    $null = & taskkill.exe /PID $ProcId /T /F 2>&1
    return $LASTEXITCODE
  } catch { return -1 }
}

# $null when there is no PID file; busy=$true when it exists but stays unreadable (another process has it open).
function Read-PidFile([string]$Path) {
  $lines = $null
  for ($try = 0; $try -lt 5 -and $null -eq $lines; $try++) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try { $lines = @([System.IO.File]::ReadAllLines($Path) | ForEach-Object { $_.Trim() }) }
    catch [System.IO.FileNotFoundException], [System.IO.DirectoryNotFoundException] { return $null }
    catch { Start-Sleep -Milliseconds 50 }
  }
  if ($null -eq $lines) { return @{ valid = $false; busy = $true; procId = 0; start = $null; session = $null; image = $null; raw = '' } }
  $info = @{ valid = $false; busy = $false; procId = 0; start = $null; session = $null; image = $null; raw = ($lines -join ' | ') }
  $n = 0
  if ($lines.Count -ge 2 -and [int]::TryParse($lines[0], [ref]$n) -and $n -gt 0) {
    $st = [DateTime]::MinValue
    $styles = [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal
    if ([DateTime]::TryParseExact($lines[1], "yyyy-MM-dd'T'HH:mm:ss'Z'", [Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$st)) {
      $info.valid = $true; $info.procId = $n; $info.start = $st
      if ($lines.Count -ge 3 -and $lines[2]) { $info.session = $lines[2] }
      if ($lines.Count -ge 4 -and $lines[3]) { $info.image = $lines[3] }
    }
  }
  return $info
}

# Deletes the PID file, retrying while the worker happens to be reading it. $true when it is gone.
function Remove-PidFile([string]$Path) {
  for ($i = 0; $i -lt 20; $i++) {
    if (-not (Test-Path -LiteralPath $Path)) { return $true }
    try { Remove-Item -LiteralPath $Path -Force -ErrorAction Stop } catch { Start-Sleep -Milliseconds 100 }
  }
  return (-not (Test-Path -LiteralPath $Path))
}

function Get-ProcStart([int]$ProcId) {
  $p = $null
  try { $p = Get-Process -Id $ProcId -ErrorAction Stop } catch { return @{ state = 'dead' } }
  try { return @{ state = 'alive'; start = $p.StartTime.ToUniversalTime(); name = $p.ProcessName } } catch { }
  try {
    $c = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId=$ProcId" -ErrorAction Stop
    if ($c -and $c.CreationDate) { return @{ state = 'alive'; start = $c.CreationDate.ToUniversalTime(); name = [System.IO.Path]::GetFileNameWithoutExtension([string]$c.Name) } }
  } catch { }
  return @{ state = 'unknown' }
}

# The PID matches the file when its start time is within 2 s and (when recorded) its image name is the same.
function Test-PidFacts($File, [DateTime]$Start, [string]$Name) {
  $delta = [Math]::Abs(($Start - $File.start).TotalSeconds)
  if (-not ($delta -le 2)) { return ('PID {0} now belongs to another process (start differs by {1:N0} s)' -f $File.procId, $delta) }
  if ($File.image -and $Name -and ($Name -ne $File.image)) { return ('PID {0} now belongs to {1}, not {2}' -f $File.procId, $Name, $File.image) }
  return $null
}

# running | stale | unknown | none, plus the parsed file
function Get-TimelapseState([string]$PidPath) {
  $f = Read-PidFile $PidPath
  if ($null -eq $f) { return @{ state = 'none'; file = $null } }
  if ($f.busy) { return @{ state = 'unknown'; file = $f; reason = 'the PID file stays locked by another process' } }
  if (-not $f.valid) { return @{ state = 'stale'; file = $f; reason = 'unreadable pid file' } }
  $p = Get-ProcStart $f.procId
  if ($p.state -eq 'dead') { return @{ state = 'stale'; file = $f; reason = 'process is gone' } }
  if ($p.state -eq 'unknown') { return @{ state = 'unknown'; file = $f; reason = 'cannot read the process start time' } }
  $why = Test-PidFacts $f $p.start $p.name
  if ($null -eq $why) { return @{ state = 'running'; file = $f; proc = $p } }
  return @{ state = 'stale'; file = $f; reason = $why }
}

# A handle to the PID-file process, re-verified through that handle. While the handle is open Windows cannot reuse
# the PID, so the checks, the wait and a fallback kill all concern the same process.
function Open-VerifiedProcess($File) {
  $h = $null
  try { $h = Get-Process -Id $File.procId -ErrorAction Stop } catch { return @{ state = 'dead'; reason = 'process is gone' } }
  $start = $null; $name = $null
  try { $null = $h.Handle; $start = $h.StartTime.ToUniversalTime(); $name = $h.ProcessName } catch { return @{ state = 'unknown'; reason = 'cannot open the process' } }
  if ($h.HasExited) { return @{ state = 'dead'; reason = 'process is gone' } }
  $why = Test-PidFacts $File $start $name
  if ($null -ne $why) { return @{ state = 'stale'; reason = $why } }
  return @{ state = 'running'; proc = $h }
}

function Enter-StartLock([string]$Path) {
  $dir = [System.IO.Path]::GetDirectoryName($Path)
  if (-not (Test-Path -LiteralPath $dir)) { [void][System.IO.Directory]::CreateDirectory($dir) }
  for ($attempt = 0; $attempt -lt 2; $attempt++) {
    try {
      $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
      try {
        $bytes = $script:Utf8NoBom.GetBytes(('{0} {1}' -f $PID, (Format-Iso ((Get-Date).ToUniversalTime()))) + "`n")
        $fs.Write($bytes, 0, $bytes.Length)
      } finally { $fs.Dispose() }
      return $true
    } catch [System.IO.IOException], [System.UnauthorizedAccessException] {
      $age = 0
      try { $age = ((Get-Date).ToUniversalTime() - [System.IO.File]::GetLastWriteTimeUtc($Path)).TotalSeconds } catch { }
      if ($age -le 60) { return $false }
      Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue  # a start that crashed a minute ago
    }
  }
  return $false
}
function Exit-StartLock([string]$Path) { Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue }

function New-SessionDir([string]$Root) {
  $stamp = Format-Stamp (Get-UtcNow)
  $dir = Join-Path $Root $stamp
  $i = 2
  while (Test-Path -LiteralPath $dir) { $dir = Join-Path $Root ('{0}_{1}' -f $stamp, $i); $i++ }
  [void][System.IO.Directory]::CreateDirectory($dir)
  return $dir
}

function Get-FreeGB([string]$Dir) {
  try {
    $drive = New-Object System.IO.DriveInfo ([System.IO.Path]::GetPathRoot($Dir))
    return ($drive.AvailableFreeSpace / 1GB)
  } catch { return [double]::MaxValue }  # a UNC share: no DriveInfo; keep recording
}

function Initialize-Capture {
  if (-not ('GskNative.Dpi' -as [type])) {
    Add-Type -Namespace GskNative -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
  }
  [void][GskNative.Dpi]::SetProcessDPIAware()
  Add-Type -AssemblyName System.Drawing
  Add-Type -AssemblyName System.Windows.Forms
}

function Save-DesktopFrame([string]$Path) {
  $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
  $outBmp = $bmp
  try {
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try { $g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size) } finally { $g.Dispose() }
    if ($MaxWidth -gt 0 -and $bmp.Width -gt $MaxWidth) {
      $h = [int][Math]::Round($bmp.Height * $MaxWidth / $bmp.Width)
      $outBmp = New-Object System.Drawing.Bitmap $MaxWidth, $h
      $g2 = [System.Drawing.Graphics]::FromImage($outBmp)
      try {
        $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g2.DrawImage($bmp, 0, 0, $MaxWidth, $h)
      } finally { $g2.Dispose() }
    }
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' } | Select-Object -First 1
    $ep = New-Object System.Drawing.Imaging.EncoderParameters 1
    $ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$Quality)
    $tmp = "$Path.tmp"
    $outBmp.Save($tmp, $codec, $ep)
    Move-Item -LiteralPath $tmp -Destination $Path -Force
    return @($outBmp.Width, $outBmp.Height)
  } finally {
    if ($outBmp -ne $bmp) { $outBmp.Dispose() }
    $bmp.Dispose()
  }
}

function Get-FramePath([string]$Dir) {
  $stamp = Format-Stamp (Get-UtcNow)
  $p = Join-Path $Dir "$stamp.jpg"
  $i = 2
  while (Test-Path -LiteralPath $p) { $p = Join-Path $Dir ('{0}_{1}.jpg' -f $stamp, $i); $i++ }
  return $p
}

# A JPEG ends with the EOI marker FF D9; a frame cut off by a kill does not.
function Test-JpegComplete([string]$Path) {
  try {
    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
      if ($fs.Length -lt 4) { return $false }
      [void]$fs.Seek(-2, [System.IO.SeekOrigin]::End)
      $b1 = $fs.ReadByte(); $b2 = $fs.ReadByte()
      return (($b1 -eq 0xFF) -and ($b2 -eq 0xD9))
    } finally { $fs.Dispose() }
  } catch { return $false }
}

# ffmpeg writes frame_000001.jpg, frame_000002.jpg, ...; give each its UTC stamp (start + (N-1) x interval). While
# ffmpeg runs the newest frame may still be open, so it waits for the final pass (-All), which also drops a frame
# that a kill cut short.
function Rename-FfmpegFrames([string]$Dir, [DateTime]$StartUtc, [int]$Interval, [bool]$All) {
  $items = @(Get-ChildItem -LiteralPath $Dir -Filter 'frame_*.jpg' -ErrorAction SilentlyContinue | Sort-Object Name)
  if (-not $All) {
    if ($items.Count -le 1) { return }
    $items = @($items[0..($items.Count - 2)])
  }
  foreach ($it in $items) {
    $n = 0
    if (-not [int]::TryParse($it.BaseName.Substring(6), [ref]$n) -or $n -lt 1) { continue }
    if ($All -and -not (Test-JpegComplete $it.FullName)) {
      Remove-Item -LiteralPath $it.FullName -Force -ErrorAction SilentlyContinue
      continue
    }
    $stamp = Format-Stamp ($StartUtc.AddSeconds(($n - 1) * $Interval))
    $dest = Join-Path $Dir "$stamp.jpg"
    $i = 2
    while (Test-Path -LiteralPath $dest) { $dest = Join-Path $Dir ('{0}_{1}.jpg' -f $stamp, $i); $i++ }
    try { Move-Item -LiteralPath $it.FullName -Destination $dest -ErrorAction Stop } catch { }
  }
}

# -Strict: only a readable file naming this process counts. Otherwise a file that stays unreadable for a moment (the
# front end is writing or deleting it) counts as still ours, and the loop asks again a second later.
function Test-StillOwner([string]$PidPath, [switch]$Strict) {
  $f = Read-PidFile $PidPath
  if ($null -eq $f) { return $false }
  if ($f.busy) { return (-not $Strict) }
  return ($f.valid -and $f.procId -eq $PID)
}

# ------------------------------------------------------------------ worker (separate, visible process)
if ($Worker) {
  $ws = Find-Workspace
  if (-not $ws -or -not $Out) { exit 3 }
  $pidPath = Join-Path $ws $script:KitPidFileRel
  $session = $Out
  [void][System.IO.Directory]::CreateDirectory($session)
  $errLog = Join-Path $session '_errors.log'
  $deadline = (Get-Date).AddSeconds(15)
  while (-not (Test-StillOwner $pidPath -Strict)) {
    if ((Get-Date) -gt $deadline) { exit 75 }
    Start-Sleep -Milliseconds 200
  }
  $started = Get-UtcNow
  try { $Host.UI.RawUI.WindowTitle = "GSK desktop timelapse: RECORDING the screen every $IntervalSec s (stop: desktop_timelapse.ps1 -Stop)" } catch { }
  [Console]::Out.WriteLine("Recording one desktop frame every $IntervalSec s into $session")
  [Console]::Out.WriteLine('Stop with: powershell -File Tools/kit/capture/desktop_timelapse.ps1 -Stop  (or close this window)')
  $frames = 0; $errors = 0; $reason = $null
  $usedEngine = $Engine
  try {
    if ($Engine -eq 'ffmpeg') {
      $ff = Get-Command ffmpeg -ErrorAction SilentlyContinue
      if (-not $ff) { $usedEngine = 'drawing' }
    }
    if ($usedEngine -eq 'ffmpeg') {
      $rate = '1/{0}' -f $IntervalSec
      $scale = if ($MaxWidth -gt 0) { "scale='min($MaxWidth,iw)':-2" } else { 'null' }
      $ffArgs = @('-hide_banner', '-loglevel', 'error', '-f', 'gdigrab', '-framerate', $rate, '-i', 'desktop', '-vf', $scale, '-q:v', '3')
      if ($MaxFrames -gt 0) { $ffArgs += @('-frames:v', "$MaxFrames") }
      $ffArgs += (ConvertTo-ProcessArg (Join-Path $session 'frame_%06d.jpg'))
      if ((Get-FreeGB $session) -lt $MinFreeGB) { $reason = 'low_disk' }
      else {
        $ffStart = Get-UtcNow
        $ffp = Start-Process -FilePath $ff.Source -ArgumentList ($ffArgs -join ' ') -NoNewWindow -PassThru
        $null = $ffp.Handle
        try {
          while (-not $ffp.HasExited) {
            if (-not (Test-StillOwner $pidPath)) { $reason = 'stopped'; break }
            if ((Get-FreeGB $session) -lt $MinFreeGB) { $reason = 'low_disk'; break }
            Rename-FfmpegFrames $session $ffStart $IntervalSec $false
            Start-Sleep -Seconds 1
          }
        } finally {
          if (-not $ffp.HasExited) { [void](Invoke-TreeKill $ffp.Id) }
          [void]$ffp.WaitForExit(10000)
          Rename-FfmpegFrames $session $ffStart $IntervalSec $true
        }
        $frames = @(Get-ChildItem -LiteralPath $session -Filter '*.jpg' -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^\d{8}T\d{6}Z(_\d+)?\.jpg$' }).Count
        if (-not $reason) { $reason = $(if ($MaxFrames -gt 0 -and $frames -ge $MaxFrames) { 'max_frames' } else { 'ffmpeg_exited' }) }
      }
    } else {
      Initialize-Capture
      while ($true) {
        if (-not (Test-StillOwner $pidPath)) { $reason = 'stopped'; break }
        if (($frames % 30) -eq 0 -and (Get-FreeGB $session) -lt $MinFreeGB) { $reason = 'low_disk'; break }
        try {
          [void](Save-DesktopFrame (Get-FramePath $session))
          $frames++; $errors = 0
        } catch {
          $errors++
          [System.IO.File]::AppendAllText($errLog, ('{0} {1}{2}' -f (Format-Iso (Get-UtcNow)), $_.Exception.Message, [Environment]::NewLine), $script:Utf8NoBom)
          if ($errors -ge 20) { $reason = 'capture_errors'; break }
        }
        if ($MaxFrames -gt 0 -and $frames -ge $MaxFrames) { $reason = 'max_frames'; break }
        for ($s = 0; $s -lt $IntervalSec; $s++) {
          Start-Sleep -Seconds 1
          if (-not (Test-StillOwner $pidPath)) { break }
        }
      }
    }
  } catch {
    $reason = 'error: ' + $_.Exception.Message
  } finally {
    if (-not $reason) { $reason = 'stopped' }
    $summary = [ordered]@{ schema = 1; start_utc = (Format-Iso $started); end_utc = (Format-Iso (Get-UtcNow)); frames = $frames; engine = $usedEngine; interval_sec = $IntervalSec; reason = $reason; pid = $PID }
    try { Write-TextNoBom (Join-Path $session '_session.json') (($summary | ConvertTo-Json -Depth 3) + "`n") } catch { }
    if (Test-StillOwner $pidPath -Strict) { [void](Remove-PidFile $pidPath) }
  }
  exit 0
}

# ------------------------------------------------------------------ front end
$modes = @($Stop, $Status, $Once) | Where-Object { $_ }
if ($modes.Count -gt 1) { Fail 2 'use only one of -Stop, -Status, -Once' $null }

if ($Once) {
  $ws = Find-Workspace
  if (-not $Out) {
    if (-not $ws) { Fail 3 'no kit workspace (Tools/kit/kit.json) found; pass -Out or -Workspace' $null }
    $Out = Join-Path $ws 'Recordings\timelapse\_once'
  }
  $dir = Resolve-Dir $Out
  [void][System.IO.Directory]::CreateDirectory($dir)
  try {
    Initialize-Capture
    $file = Get-FramePath $dir
    $size = Save-DesktopFrame $file
  } catch {
    Fail 1 ('capture failed: ' + $_.Exception.Message) $null
  }
  $rel = Get-RelPosix $ws $file
  Write-Result ([ordered]@{ ok = $true; action = 'once'; file = $rel; size = @($size[0], $size[1]); engine = 'drawing' }) 0 @("wrote $rel ($($size[0])x$($size[1]))")
}

$ws = Find-Workspace
if (-not $ws) { Fail 3 'no kit workspace (Tools/kit/kit.json) found; run inside a project or pass -Workspace' $null }
$pidPath = Join-Path $ws $script:KitPidFileRel
$pidRel = Get-RelPosix $ws $pidPath
$st = Get-TimelapseState $pidPath

if ($Status) {
  $r = [ordered]@{ ok = $true; action = 'status'; status = $(if ($st.state -eq 'none') { 'not_running' } else { $st.state }); pid_file = $pidRel }
  if ($st.file -and $st.file.valid) { $r.pid = $st.file.procId; $r.process_start_utc = (Format-Iso $st.file.start); $r.session_dir = (Get-RelPosix $ws $st.file.session); $r.image = $st.file.image }
  if ($st.reason) { $r.reason = $st.reason }
  Write-Result $r 0 @("timelapse: $($r.status)")
}

if ($Stop) {
  switch ($st.state) {
    'none' { Write-Result ([ordered]@{ ok = $true; action = 'stop'; status = 'not_running'; pid_file = $pidRel }) 0 @('timelapse: not running') }
    'stale' {
      [void](Remove-PidFile $pidPath)
      $r = [ordered]@{ ok = $true; action = 'stop'; status = 'stale'; reason = $st.reason; killed = $false; pid_file = $pidRel }
      if ($st.file.valid) { $r.pid = $st.file.procId }
      Write-Result $r 0 @("timelapse: stale pid file removed ($($st.reason)); nothing killed")
    }
    'unknown' { Fail 75 ("cannot verify PID {0} ({1}); nothing killed" -f $st.file.procId, $st.reason) ([ordered]@{ action = 'stop'; status = 'unknown'; killed = $false; pid = $st.file.procId }) }
    'running' {
      $target = $st.file.procId
      $v = Open-VerifiedProcess $st.file
      if ($v.state -eq 'unknown') { Fail 75 ("cannot verify PID {0} ({1}); nothing killed" -f $target, $v.reason) ([ordered]@{ action = 'stop'; status = 'unknown'; killed = $false; pid = $target }) }
      if ($v.state -ne 'running') {
        [void](Remove-PidFile $pidPath)
        Write-Result ([ordered]@{ ok = $true; action = 'stop'; status = 'stale'; reason = $v.reason; killed = $false; pid = $target; pid_file = $pidRel }) 0 @("timelapse: stale pid file removed ($($v.reason)); nothing killed")
      }
      $h = $v.proc
      # 1) graceful: withdraw the PID file; the worker sees it is no longer the owner, leaves its loop and writes
      #    _session.json in its finally block
      [void](Remove-PidFile $pidPath)
      $graceful = $h.WaitForExit($script:StopGraceSec * 1000)
      $forced = $false
      if (-not $graceful) {
        # 2) it did not leave in time (a hung capture): kill its tree. $h still holds the process open, so this PID
        #    is the verified worker and not a reused number.
        $forced = $true
        [void](Invoke-TreeKill $target)
        [void]$h.WaitForExit(10000)
      }
      $alive = -not $h.HasExited
      [void](Remove-PidFile $pidPath)
      if ($alive) { Fail 1 "PID $target did not exit after taskkill" ([ordered]@{ action = 'stop'; status = 'kill_failed'; pid = $target }) }
      $sessionJson = $null
      if ($st.file.session) {
        $sj = Join-Path $st.file.session '_session.json'
        if (Test-Path -LiteralPath $sj -PathType Leaf) { $sessionJson = Get-RelPosix $ws $sj }
      }
      Write-Result ([ordered]@{ ok = $true; action = 'stop'; status = 'stopped'; graceful = $graceful; killed = $forced; pid = $target; session_dir = (Get-RelPosix $ws $st.file.session); session_json = $sessionJson; pid_file = $pidRel }) 0 @("timelapse: stopped PID $target$(if ($forced) { ' (killed after ' + $script:StopGraceSec + ' s)' } else { '' })")
    }
  }
}

# ------------------------------------------------------------------ start (serialised by the start lock)
$lockPath = Join-Path $ws $script:StartLockRel
if (-not (Enter-StartLock $lockPath)) { Fail 75 'another timelapse start is in progress; try again in a moment' ([ordered]@{ action = 'start'; status = 'starting' }) }
$proc = $null; $session = $null; $started = $false
$staleNote = $null; $usedEngine = $Engine; $warn = @()
try {
  $st = Get-TimelapseState $pidPath  # again, now that no other start can run
  if ($st.state -eq 'running') { Fail 75 ("a timelapse is already running (PID {0})" -f $st.file.procId) ([ordered]@{ action = 'start'; status = 'running'; pid = $st.file.procId; session_dir = (Get-RelPosix $ws $st.file.session) }) }
  if ($st.state -eq 'unknown') { Fail 75 ("cannot verify PID {0} in {1}; not starting a second capture" -f $st.file.procId, $pidRel) ([ordered]@{ action = 'start'; status = 'unknown' }) }
  if ($st.state -eq 'stale') {
    if (-not (Remove-PidFile $pidPath)) { Fail 75 "cannot remove the stale $pidRel" ([ordered]@{ action = 'start'; status = 'unknown' }) }
    $staleNote = $st.reason
  }
  if (-not $Out) { $Out = Join-Path $ws 'Recordings\timelapse' }
  $session = New-SessionDir (Resolve-Dir $Out)
  if ($Engine -eq 'ffmpeg' -and -not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { $usedEngine = 'drawing'; $warn += 'ffmpeg not on PATH; using System.Drawing (the kit does not install ffmpeg)' }
  $psExe = 'powershell.exe'
  if ($env:SystemRoot) {
    $cand = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (Test-Path -LiteralPath $cand -PathType Leaf) { $psExe = $cand }
  }
  $argList = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (ConvertTo-ProcessArg $PSCommandPath),
    '-Worker', '-Out', (ConvertTo-ProcessArg $session), '-IntervalSec', "$IntervalSec", '-MaxFrames', "$MaxFrames", '-MaxWidth', "$MaxWidth",
    '-Quality', "$Quality", '-MinFreeGB', ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $MinFreeGB)),
    '-Engine', $usedEngine, '-Workspace', (ConvertTo-ProcessArg $ws)) -join ' '
  $proc = Start-Process -FilePath $psExe -ArgumentList $argList -WindowStyle Minimized -PassThru
  # every fact comes from this process object's own handle: no second lookup by PID number that a reused PID could answer
  $null = $proc.Handle
  $image = $proc.ProcessName
  $startUtc = $proc.StartTime.ToUniversalTime()
  if ($proc.HasExited -or -not $image) { Fail 1 'the capture worker exited immediately' ([ordered]@{ action = 'start' }) }
  Write-TextNoBom $pidPath (("{0}`n{1}`n{2}`n{3}`n" -f $proc.Id, (Format-Iso $startUtc), $session, $image))
  $started = $true
} catch {
  Fail 1 ('could not start the capture worker: ' + $_.Exception.Message) ([ordered]@{ action = 'start' })
} finally {
  if (-not $started) {
    try { if ($proc -and -not $proc.HasExited) { [void](Invoke-TreeKill $proc.Id) } } catch { }
    try {
      if ($session -and (Test-Path -LiteralPath $session) -and -not (Get-ChildItem -LiteralPath $session -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $session -Force -ErrorAction SilentlyContinue
      }
    } catch { }
  }
  Exit-StartLock $lockPath
}
$r = [ordered]@{ ok = $true; action = 'start'; status = 'running'; pid = $proc.Id; process_start_utc = (Format-Iso $startUtc); image = $image; session_dir = (Get-RelPosix $ws $session); engine = $usedEngine; interval_sec = $IntervalSec; max_frames = $MaxFrames; pid_file = $pidRel; warnings = $warn }
if ($staleNote) { $r.stale_pid_file_removed = $staleNote }
Write-Result $r 0 @("timelapse started: PID $($proc.Id), frames every $IntervalSec s into $(Get-RelPosix $ws $session)", 'stop with: -Stop')
