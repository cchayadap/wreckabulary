# Game Studio Kit host lib (Windows PowerShell 5.1): workspace paths, system facts, process identity, Claude Code
# session detection, the launch route, alerts and toasts. Dot-source it; it dot-sources json.ps1.
#
# Test seam (only once a script enabled the seams, see json.ps1): when GSK_HOST_FAKE names a JSON file, every OS fact comes from that
# file, and launches, claude starts and toasts are appended to launches.jsonl / claude_starts.jsonl / toasts.jsonl next
# to it instead of happening. Nothing here kills a process, and the only files it deletes are its own temp files and
# alert flags the calling script owns.

. ([IO.Path]::Combine($PSScriptRoot, 'json.ps1'))

$script:GskHostLibDir = $PSScriptRoot
$script:GskFakeCache = $null
$script:GskFakeLoaded = $false
$script:GskLevelRank = @{ info = 0; warn = 1; stop = 2; emergency = 3 }
$script:GskLaunchEnvAllow = @('CLAUDE_CODE_STOP_HOOK_BLOCK_CAP', 'CLAUDE_CODE_WORKFLOW_MAX_CONCURRENT_AGENTS')
# Claude Code session ids are UUIDs; run names follow run.ps1 -Name. Anything else never reaches a claude command line.
$script:GskSessionIdPattern = '^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$'
$script:GskRunNamePattern = '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$'
# Command-line budgets: CreateProcess takes 32767 characters, cmd.exe 8191 (less what the npm shim adds).
$script:GskMaxCmdChars = @{ exe = 32000; node = 32000; cmd = 7800 }

function Test-GskFake { return [bool](Get-GskSeamValue 'GSK_HOST_FAKE') }

function Get-GskFake {
  $path = Get-GskSeamValue 'GSK_HOST_FAKE'
  if (-not $path) { return $null }
  if (-not $script:GskFakeLoaded) {
    $script:GskFakeCache = Read-GskJson $path
    if ($null -eq $script:GskFakeCache) { throw "GSK_HOST_FAKE does not name a readable JSON file: $path" }
    $script:GskFakeLoaded = $true
  }
  return $script:GskFakeCache
}

function Get-GskFakeDir { return [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath((Get-GskSeamValue 'GSK_HOST_FAKE'))) }

function Get-GskHome {
  $h = Get-GskSeamValue 'GSK_HOME'
  if ($h) { return $h }
  return [Environment]::GetFolderPath('UserProfile')
}

# %LOCALAPPDATA%; with the seams on and GSK_HOME set, <GSK_HOME>\AppData\Local, so tests never write the real one.
function Get-GskLocalAppData {
  $h = Get-GskSeamValue 'GSK_HOME'
  if ($h) { return [IO.Path]::Combine($h, 'AppData', 'Local') }
  return [Environment]::GetFolderPath('LocalApplicationData')
}

function Test-GskSessionId {
  param([AllowNull()][AllowEmptyString()][string]$Id)
  return ([bool]$Id -and $Id -match $script:GskSessionIdPattern)
}

function Test-GskRunName {
  param([AllowNull()][AllowEmptyString()][string]$Name)
  return ([bool]$Name -and $Name -cmatch $script:GskRunNamePattern)
}

# Is the kit installed in W? The scheduled tasks stop doing anything once W/Tools/kit/kit.json is gone.
function Test-GskKitInstalled {
  param([Parameter(Mandatory = $true)][string]$Workspace)
  return [IO.File]::Exists([IO.Path]::Combine($Workspace, 'Tools', 'kit', 'kit.json'))
}

# Newest last-write time of any file under $Dir (at most $Max files are looked at), or $null.
function Get-GskNewestWrite {
  param([Parameter(Mandatory = $true)][string]$Dir, [int]$Max = 5000)
  if (-not [IO.Directory]::Exists($Dir)) { return $null }
  $best = $null
  $n = 0
  try {
    foreach ($f in [IO.Directory]::EnumerateFiles($Dir, '*', [IO.SearchOption]::AllDirectories)) {
      $t = [IO.File]::GetLastWriteTimeUtc($f)
      if ($null -eq $best -or $t -gt $best) { $best = $t }
      $n++
      if ($n -ge $Max) { break }
    }
  } catch { }
  return $best
}

function Get-GskCount {
  param([AllowNull()]$Value)
  if ($null -eq $Value) { return 0 }
  if ($Value -is [System.Collections.ICollection]) { return $Value.Count }
  return 1
}

# W as an absolute path. Without -Workspace, W is three levels above this host folder when Tools/kit/kit.json is there.
function Resolve-GskWorkspace {
  param([AllowEmptyString()][AllowNull()][string]$Workspace, [Parameter(Mandatory = $true)][string]$ScriptDir)
  if ([string]::IsNullOrWhiteSpace($Workspace)) {
    $guess = [IO.Path]::GetFullPath([IO.Path]::Combine($ScriptDir, '..', '..', '..'))
    if ([IO.File]::Exists([IO.Path]::Combine($guess, 'Tools', 'kit', 'kit.json'))) { return $guess.TrimEnd('\') }
    throw 'missing -Workspace (and this script is not installed under <W>/Tools/kit/host)'
  }
  $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Workspace)
  $full = [IO.Path]::GetFullPath($full)
  if (-not [IO.Directory]::Exists($full)) { throw "workspace folder not found: $full" }
  $full = $full.TrimEnd('\', '/')
  if ($full -match '^[A-Za-z]:$') { $full += '\' }
  return $full
}

function Get-GskHarnessPath {
  param([Parameter(Mandatory = $true)][string]$Workspace, [string[]]$Parts = @())
  $all = @($Workspace, '.harness') + $Parts
  return [IO.Path]::Combine([string[]]$all)
}

function ConvertTo-GskTaskSlug {
  param([AllowEmptyString()][AllowNull()][string]$Name)
  $s = ([string]$Name).ToLowerInvariant() -creplace '[^a-z0-9]+', '-'
  $s = $s -creplace '^-+|-+$', ''
  if ($s.Length -gt 40) { $s = $s.Substring(0, 40) }
  $s = $s -creplace '-+$', ''
  if ($s.Length -eq 0) { return 'project' }
  return $s
}

# ---------------------------------------------------------------- processes

function New-GskProcInfo {
  param([int]$Id, [string]$Name, [string]$Image, [string]$CommandLine, [AllowNull()]$StartUtc, [AllowNull()]$PrivateBytes, [AllowNull()]$WorkingSet)
  return [pscustomobject]@{
    pid = $Id; name = $Name; image = $Image; command_line = $CommandLine
    start_utc = $StartUtc; private_bytes = $PrivateBytes; working_set = $WorkingSet
  }
}

function ConvertFrom-GskCimProcess {
  param([Parameter(Mandatory = $true)]$Cim)
  $start = $null
  if ($null -ne $Cim.CreationDate) { $start = ([DateTime]$Cim.CreationDate).ToUniversalTime() }
  $priv = $null
  if ($null -ne $Cim.PrivatePageCount) { $priv = [double]$Cim.PrivatePageCount }
  $ws = $null
  if ($null -ne $Cim.WorkingSetSize) { $ws = [double]$Cim.WorkingSetSize }
  return (New-GskProcInfo -Id ([int]$Cim.ProcessId) -Name ([string]$Cim.Name) -Image ([string]$Cim.ExecutablePath) `
      -CommandLine ([string]$Cim.CommandLine) -StartUtc $start -PrivateBytes $priv -WorkingSet $ws)
}

function ConvertFrom-GskFakeProcess {
  param([Parameter(Mandatory = $true)]$Fake)
  $priv = $null
  $pm = Get-GskField $Fake 'private_mb' $null
  if ($null -ne $pm) { $priv = [double]$pm * 1MB }
  $ws = $null
  $wm = Get-GskField $Fake 'working_set_mb' $null
  if ($null -ne $wm) { $ws = [double]$wm * 1MB }
  return (New-GskProcInfo -Id ([int](Get-GskField $Fake 'pid' 0)) -Name ([string](Get-GskField $Fake 'name' '')) `
      -Image ([string](Get-GskField $Fake 'image' '')) -CommandLine ([string](Get-GskField $Fake 'command_line' '')) `
      -StartUtc (ConvertFrom-GskUtc ([string](Get-GskField $Fake 'start_utc' ''))) -PrivateBytes $priv -WorkingSet $ws)
}

# Candidate Claude Code processes (claude.exe, node.exe), as a List[object] of New-GskProcInfo objects.
function Get-GskProcessList {
  $list = New-Object 'System.Collections.Generic.List[object]'
  $fake = Get-GskFake
  if ($null -ne $fake) {
    foreach ($f in @(Get-GskField $fake 'processes' @())) { if ($null -ne $f) { $list.Add((ConvertFrom-GskFakeProcess $f)) } }
    return ,$list
  }
  $procs = Get-CimInstance -ClassName Win32_Process -Filter "Name='claude.exe' OR Name='node.exe'" -ErrorAction Stop
  foreach ($p in @($procs)) { if ($null -ne $p) { $list.Add((ConvertFrom-GskCimProcess $p)) } }
  return ,$list
}

function Get-GskProcessById {
  param([int]$Id)
  if ($Id -le 0) { return $null }
  $fake = Get-GskFake
  if ($null -ne $fake) {
    foreach ($f in @(Get-GskField $fake 'processes' @())) {
      if ($null -ne $f -and [int](Get-GskField $f 'pid' 0) -eq $Id) { return (ConvertFrom-GskFakeProcess $f) }
    }
    return $null
  }
  $p = @(Get-CimInstance -ClassName Win32_Process -Filter "ProcessId=$Id" -ErrorAction SilentlyContinue)
  if ($p.Length -eq 0 -or $null -eq $p[0]) { return $null }
  return (ConvertFrom-GskCimProcess $p[0])
}

# The executable path of a running process as Windows reports it (for a PID file's image), or $Fallback.
function Get-GskProcessImage {
  param([int]$Id, [AllowEmptyString()][string]$Fallback = '')
  $p = Get-GskProcessById $Id
  if ($null -ne $p -and $p.image) { return [string]$p.image }
  return $Fallback
}

# Is $Id still the process a PID file recorded? status: match | mismatch | dead | unknown (CONTRACTS section 1.6).
function Test-GskPidMatch {
  param([int]$Id, [AllowNull()]$StartUtc, [AllowNull()][AllowEmptyString()][string]$Image, [double]$ToleranceSec = 2)
  $p = Get-GskProcessById $Id
  if ($null -eq $p) { return [pscustomobject]@{ status = 'dead'; process = $null } }
  if ($null -eq $p.start_utc) { return [pscustomobject]@{ status = 'unknown'; process = $p } }
  if ($null -ne $StartUtc) {
    if ([Math]::Abs(($p.start_utc - [DateTime]$StartUtc).TotalSeconds) -gt $ToleranceSec) { return [pscustomobject]@{ status = 'mismatch'; process = $p } }
  }
  if ($Image -and $p.image) {
    $a = [IO.Path]::GetFileName($Image.Replace('/', '\')).ToLowerInvariant()
    $b = [IO.Path]::GetFileName($p.image.Replace('/', '\')).ToLowerInvariant()
    if ($a -ne $b) { return [pscustomobject]@{ status = 'mismatch'; process = $p } }
  }
  return [pscustomobject]@{ status = 'match'; process = $p }
}

# ---------------------------------------------------------------- system facts

function Get-GskDiskInfo {
  param([Parameter(Mandatory = $true)][string]$Workspace)
  $fake = Get-GskFake
  if ($null -ne $fake) {
    $d = Get-GskField $fake 'disk' $null
    if ($null -eq $d) { return $null }
    return [pscustomobject]@{ drive = [string](Get-GskField $d 'drive' 'C:'); free_gb = [double](Get-GskField $d 'free_gb' 0); total_gb = [double](Get-GskField $d 'total_gb' 0) }
  }
  try {
    $root = [IO.Path]::GetPathRoot($Workspace)
    $di = New-Object IO.DriveInfo($root)
    return [pscustomobject]@{
      drive = $root.TrimEnd('\')
      free_gb = [Math]::Round($di.AvailableFreeSpace / 1GB, 1)
      total_gb = [Math]::Round($di.TotalSize / 1GB, 1)
    }
  } catch { return $null }
}

# Commit charge and available RAM. Perf class first (not localised, unlike Get-Counter paths), then Win32_OperatingSystem.
function Get-GskMemoryInfo {
  $fake = Get-GskFake
  if ($null -ne $fake) {
    $m = Get-GskField $fake 'memory' $null
    if ($null -eq $m) { return $null }
    return [pscustomobject]@{
      commit_used_pct = [double](Get-GskField $m 'commit_used_pct' 0); commit_limit_mb = [double](Get-GskField $m 'commit_limit_mb' 0)
      available_mb = [double](Get-GskField $m 'available_mb' 0); source = 'fake'
    }
  }
  try {
    $m = @(Get-CimInstance -ClassName Win32_PerfFormattedData_PerfOS_Memory -ErrorAction Stop)[0]
    $limit = [double]$m.CommitLimit
    if ($limit -le 0) { throw 'no commit limit' }
    return [pscustomobject]@{
      commit_used_pct = [Math]::Round(100.0 * [double]$m.CommittedBytes / $limit, 1); commit_limit_mb = [Math]::Round($limit / 1MB)
      available_mb = [double]$m.AvailableMBytes; source = 'perf'
    }
  } catch { }
  try {
    $os = @(Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop)[0]
    $total = [double]$os.TotalVirtualMemorySize
    return [pscustomobject]@{
      commit_used_pct = [Math]::Round(100.0 * ($total - [double]$os.FreeVirtualMemory) / $total, 1); commit_limit_mb = [Math]::Round($total / 1024)
      available_mb = [Math]::Round([double]$os.FreePhysicalMemory / 1024); source = 'os'
    }
  } catch { return $null }
}

function Get-GskPagefileMb {
  $fake = Get-GskFake
  if ($null -ne $fake) {
    $v = Get-GskField $fake 'pagefile_mb' $null
    if ($null -eq $v) { return $null }
    return [double]$v
  }
  try {
    $sum = 0.0
    $n = 0
    foreach ($pf in @(Get-CimInstance -ClassName Win32_PageFileUsage -ErrorAction Stop)) {
      if ($null -ne $pf) { $sum += [double]$pf.AllocatedBaseSize; $n++ }
    }
    if ($n -eq 0) { return $null }
    return $sum
  } catch { return $null }
}

# Sleep and hibernate timeouts on AC power in minutes (0 = never), read-only through powercfg /q. The last two hex
# values of the output are the AC and DC indexes, which avoids parsing localised labels. $null when unreadable.
function Get-GskPowerTimeouts {
  $fake = Get-GskFake
  if ($null -ne $fake) {
    $p = Get-GskField $fake 'power' $null
    if ($null -eq $p) { return $null }
    return [pscustomobject]@{ standby_ac_min = [double](Get-GskField $p 'standby_ac_min' 0); hibernate_ac_min = [double](Get-GskField $p 'hibernate_ac_min' 0) }
  }
  $vals = @{}
  foreach ($setting in @('STANDBYIDLE', 'HIBERNATEIDLE')) {
    try {
      $out = & powercfg.exe /q SCHEME_CURRENT SUB_SLEEP $setting 2>$null
      if ($LASTEXITCODE -ne 0) { return $null }
      $hex = [regex]::Matches((@($out) -join "`n"), '0x([0-9a-fA-F]{8})')
      if ($hex.Count -lt 2) { return $null }
      $vals[$setting] = [Math]::Round([Convert]::ToInt64($hex[$hex.Count - 2].Groups[1].Value, 16) / 60.0, 1)
    } catch { return $null }
  }
  return [pscustomobject]@{ standby_ac_min = $vals['STANDBYIDLE']; hibernate_ac_min = $vals['HIBERNATEIDLE'] }
}

# ---------------------------------------------------------------- Claude Code sessions

# Windows argv split (CommandLineToArgvW rules for arguments after the program name).
function Split-GskCommandLine {
  param([AllowEmptyString()][AllowNull()][string]$CommandLine)
  $argv = New-Object 'System.Collections.Generic.List[string]'
  if ([string]::IsNullOrEmpty($CommandLine)) { return ,$argv }
  $sb = New-Object System.Text.StringBuilder
  $inQuotes = $false
  $has = $false
  $i = 0
  $n = $CommandLine.Length
  while ($i -lt $n) {
    $c = $CommandLine[$i]
    if ($c -eq [char]'\') {
      $bs = 0
      while ($i -lt $n -and $CommandLine[$i] -eq [char]'\') { $bs++; $i++ }
      if ($i -lt $n -and $CommandLine[$i] -eq [char]'"') {
        [void]$sb.Append([char]'\', [int][Math]::Floor($bs / 2))
        if ($bs % 2 -eq 1) { [void]$sb.Append([char]'"'); $i++ }
      } else {
        [void]$sb.Append([char]'\', $bs)
      }
      $has = $true
      continue
    }
    if ($c -eq [char]'"') {
      if ($inQuotes -and ($i + 1) -lt $n -and $CommandLine[$i + 1] -eq [char]'"') { [void]$sb.Append([char]'"'); $i += 2; $has = $true; continue }
      $inQuotes = -not $inQuotes
      $has = $true
      $i++
      continue
    }
    if (-not $inQuotes -and ($c -eq [char]' ' -or $c -eq [char]"`t")) {
      if ($has) { $argv.Add($sb.ToString()); [void]$sb.Clear(); $has = $false }
      $i++
      continue
    }
    [void]$sb.Append($c)
    $has = $true
    $i++
  }
  if ($has) { $argv.Add($sb.ToString()) }
  return ,$argv
}

# One argument quoted for CreateProcess, so that Split-GskCommandLine (and the C runtime) reads it back unchanged.
# -Always quotes even a plain argument (the cmd.exe route: & | < > ^ ( ) are literal only inside quotes).
function Format-GskArg {
  param([AllowEmptyString()][AllowNull()][string]$Arg, [switch]$Always)
  $a = [string]$Arg
  if ($a.Length -eq 0) { return '""' }
  if (-not $Always -and $a -notmatch '[\s"]') { return $a }
  $sb = New-Object System.Text.StringBuilder
  [void]$sb.Append([char]'"')
  $bs = 0
  foreach ($ch in $a.ToCharArray()) {
    if ($ch -eq [char]'\') { $bs++; continue }
    if ($ch -eq [char]'"') { [void]$sb.Append([char]'\', ($bs * 2 + 1)); [void]$sb.Append([char]'"'); $bs = 0; continue }
    if ($bs -gt 0) { [void]$sb.Append([char]'\', $bs); $bs = 0 }
    [void]$sb.Append($ch)
  }
  if ($bs -gt 0) { [void]$sb.Append([char]'\', ($bs * 2)) }
  [void]$sb.Append([char]'"')
  return $sb.ToString()
}

function ConvertTo-GskArgString {
  param([AllowEmptyCollection()][string[]]$Argv, [switch]$AlwaysQuote)
  $parts = New-Object 'System.Collections.Generic.List[string]'
  foreach ($a in $Argv) { $parts.Add((Format-GskArg $a -Always:$AlwaysQuote)) }
  return ($parts -join ' ')
}

# A Claude Code CLI process: claude.exe that is not the desktop app or one of its Electron helpers, or node running
# the npm package. The desktop app's own Claude.exe (WindowsApps\Claude_* or AnthropicClaude) is never a CLI.
function Test-GskClaudeCli {
  param([Parameter(Mandatory = $true)]$Proc)
  $name = ([string]$Proc.name).ToLowerInvariant()
  $img = ([string]$Proc.image).Replace('/', '\').ToLowerInvariant()
  $cl = [string]$Proc.command_line
  if ($name -eq 'node.exe' -or $name -eq 'node') { return ($cl -match '@anthropic-ai[\\/]+claude-code') }
  if ($name -ne 'claude.exe' -and $name -ne 'claude') { return $false }
  if ($cl -match '(^|\s)"?--type=') { return $false }
  if ($img -match '\\windowsapps\\claude_' -or $img -match '\\anthropicclaude\\') { return $false }
  return $true
}

# Names (-n/--name) and session references (-r/--resume/--session-id) on a Claude Code command line.
function Get-GskClaudeIdentity {
  param([AllowEmptyString()][AllowNull()][string]$CommandLine)
  $argv = Split-GskCommandLine $CommandLine
  $names = New-Object 'System.Collections.Generic.List[string]'
  $refs = New-Object 'System.Collections.Generic.List[string]'
  $cont = $false
  for ($i = 1; $i -lt $argv.Count; $i++) {
    $a = $argv[$i]
    $hasNext = ($i + 1) -lt $argv.Count
    if ($a -ceq '-n' -or $a -ceq '--name') { if ($hasNext) { $names.Add($argv[$i + 1]); $i++ }; continue }
    if ($a.StartsWith('--name=')) { $names.Add($a.Substring(7)); continue }
    if ($a -ceq '-r' -or $a -ceq '--resume' -or $a -ceq '--session-id') {
      if ($hasNext -and -not $argv[$i + 1].StartsWith('-')) { $refs.Add($argv[$i + 1]); $i++ }
      continue
    }
    if ($a.StartsWith('--resume=')) { $refs.Add($a.Substring(9)); continue }
    if ($a.StartsWith('--session-id=')) { $refs.Add($a.Substring(13)); continue }
    if ($a -ceq '-c' -or $a -ceq '--continue') { $cont = $true }
  }
  return [pscustomobject]@{ names = $names; refs = $refs; continue = $cont }
}

function Get-GskClaudeSlug {
  param([Parameter(Mandatory = $true)][string]$Dir)
  return ($Dir -creplace '[^A-Za-z0-9]', '-')
}

# ~/.claude/projects folders of W: the exact slug folder, plus <first 200 chars>-<hash> folders for long slugs.
function Get-GskClaudeProjectDirs {
  param([Parameter(Mandatory = $true)][string]$Workspace, [Parameter(Mandatory = $true)][string]$HomeDir)
  $out = New-Object 'System.Collections.Generic.List[string]'
  $root = [IO.Path]::Combine($HomeDir, '.claude', 'projects')
  $slug = Get-GskClaudeSlug $Workspace
  $exact = [IO.Path]::Combine($root, $slug)
  if ([IO.Directory]::Exists($exact)) { $out.Add($exact) }
  if ($slug.Length -gt 200 -and [IO.Directory]::Exists($root)) {
    $prefix = ($slug.Substring(0, 200) + '-').ToLowerInvariant()
    foreach ($d in [IO.Directory]::GetDirectories($root)) {
      $n = [IO.Path]::GetFileName($d).ToLowerInvariant()
      if ($n.StartsWith($prefix) -and $n -ne $slug.ToLowerInvariant()) { $out.Add($d) }
    }
  }
  return ,$out
}

# ~/.claude/projects/<any>/<id>.jsonl for a session id, or $null.
function Find-GskTranscript {
  param([Parameter(Mandatory = $true)][string]$Id, [Parameter(Mandatory = $true)][string]$HomeDir)
  if ($Id -notmatch '^[0-9A-Fa-f][0-9A-Fa-f-]{7,}$') { return $null }
  $root = [IO.Path]::Combine($HomeDir, '.claude', 'projects')
  if (-not [IO.Directory]::Exists($root)) { return $null }
  foreach ($d in [IO.Directory]::GetDirectories($root)) {
    $p = [IO.Path]::Combine($d, "$Id.jsonl")
    if ([IO.File]::Exists($p)) { return $p }
  }
  return $null
}

function Test-GskSameDir {
  param([string]$A, [string]$B)
  if (-not $A -or -not $B) { return $false }
  $x = $A.Replace('/', '\').TrimEnd('\')
  $y = $B.Replace('/', '\').TrimEnd('\')
  return [string]::Equals($x, $y, [StringComparison]::OrdinalIgnoreCase)
}

# Classifies live Claude Code processes for W:
#   ours    - the recorded .harness/claude.pid (start time and image match), or a command line naming the run
#             (-n <name>), the recorded session id, or a session whose transcript lives in W's project folder;
#   other   - a command line resuming a session whose transcript lives in another project's folder;
#   unknown - anything else (for example a bare `claude` or `claude --continue` typed by hand): it may be W's session.
function Get-GskClaudeSessions {
  param(
    [Parameter(Mandatory = $true)][string]$Workspace,
    [AllowNull()][AllowEmptyString()][string]$SessionId,
    [AllowNull()][string[]]$Names,
    [AllowNull()][AllowEmptyString()][string]$TranscriptPath,
    [AllowNull()]$ClaudePid
  )
  $homeDir = Get-GskHome
  $wsDirs = Get-GskClaudeProjectDirs -Workspace $Workspace -HomeDir $homeDir
  if ($TranscriptPath) {
    $td = [IO.Path]::GetDirectoryName($TranscriptPath.Replace('/', '\'))
    $known = $false
    foreach ($d in $wsDirs) { if (Test-GskSameDir $d $td) { $known = $true } }
    if ($td -and -not $known) { $wsDirs.Add($td) }
  }
  $nameSet = @()
  foreach ($n in @($Names)) { if ($n) { $nameSet += $n } }
  $ours = New-Object 'System.Collections.Generic.List[object]'
  $other = New-Object 'System.Collections.Generic.List[object]'
  $unknown = New-Object 'System.Collections.Generic.List[object]'
  $pidOurs = 0
  $pidProc = $null
  if ($null -ne $ClaudePid) {
    $cp = [int](Get-GskField $ClaudePid 'pid' 0)
    if ($cp -gt 0) {
      $m = Test-GskPidMatch -Id $cp -StartUtc (ConvertFrom-GskUtc ([string](Get-GskField $ClaudePid 'process_start_utc' ''))) -Image ([string](Get-GskField $ClaudePid 'image' ''))
      if ($m.status -eq 'match') { $pidOurs = $cp; $pidProc = $m.process }
    }
  }
  foreach ($p in (Get-GskProcessList)) {
    if ($pidOurs -gt 0 -and $p.pid -eq $pidOurs) { $ours.Add($p); $pidProc = $null; continue }
    if (-not (Test-GskClaudeCli $p)) { continue }
    $idn = Get-GskClaudeIdentity $p.command_line
    $verdict = 'unknown'
    foreach ($n in $idn.names) { if ($nameSet -contains $n) { $verdict = 'ours' } }
    foreach ($r in $idn.refs) {
      if ($verdict -eq 'ours') { break }
      if ($SessionId -and [string]::Equals($r, $SessionId, [StringComparison]::OrdinalIgnoreCase)) { $verdict = 'ours'; break }
      if ($nameSet -contains $r) { $verdict = 'ours'; break }
      $t = $null
      if ($r.EndsWith('.jsonl', [StringComparison]::OrdinalIgnoreCase)) { $t = $r } else { $t = Find-GskTranscript -Id $r -HomeDir $homeDir }
      if ($t) {
        $tdir = [IO.Path]::GetDirectoryName($t.Replace('/', '\'))
        $inWs = $false
        foreach ($d in $wsDirs) { if (Test-GskSameDir $d $tdir) { $inWs = $true } }
        if ($inWs) { $verdict = 'ours'; break }
        $verdict = 'other'
      }
    }
    if ($verdict -eq 'ours') { $ours.Add($p) } elseif ($verdict -eq 'other') { $other.Add($p) } else { $unknown.Add($p) }
  }
  if ($null -ne $pidProc) { $ours.Add($pidProc) }
  return [pscustomobject]@{ ours = $ours; other = $other; unknown = $unknown }
}

# Get-GskClaudeSessions for W with what .harness records: run.json and session.json names, the session id and its
# transcript, and claude.pid; plus -Names and -SessionId from the caller.
function Get-GskWorkspaceSessions {
  param([Parameter(Mandatory = $true)][string]$Workspace, [AllowNull()][string[]]$Names, [AllowNull()][AllowEmptyString()][string]$SessionId)
  $run = Read-GskJson (Get-GskHarnessPath $Workspace @('run.json'))
  $session = Read-GskJson (Get-GskHarnessPath $Workspace @('session.json'))
  $all = @()
  foreach ($n in (@($Names) + @([string](Get-GskField $run 'name' ''), [string](Get-GskField $session 'name' '')))) { if ($n) { $all += $n } }
  $sid = [string](Get-GskField $session 'session_id' '')
  if ($SessionId) { $sid = $SessionId }
  return (Get-GskClaudeSessions -Workspace $Workspace -SessionId $sid -Names $all -TranscriptPath ([string](Get-GskField $session 'transcript_path' '')) `
      -ClaudePid (Read-GskJson (Get-GskHarnessPath $Workspace @('claude.pid'))))
}

function Get-GskPidList {
  param([AllowNull()]$Procs)
  $ids = New-Object 'System.Collections.Generic.List[int]'
  if ($null -eq $Procs) { return ,$ids }
  foreach ($p in $Procs) { if ($null -ne $p) { $ids.Add([int]$p.pid) } }
  return ,$ids
}

# ---------------------------------------------------------------- launching claude (run.ps1, supervisor.ps1)

function Get-GskLaunchEnv {
  param([int]$MaxAgents = 6)
  $e = [ordered]@{}
  $e['CLAUDE_CODE_STOP_HOOK_BLOCK_CAP'] = '200'
  $e['CLAUDE_CODE_WORKFLOW_MAX_CONCURRENT_AGENTS'] = [string]$MaxAgents
  return $e
}

function Protect-GskWtArg {
  param([AllowEmptyString()][string]$Arg)
  return ($Arg -replace ';', '\;')
}

function Get-GskTabTitle {
  param([AllowNull()][AllowEmptyString()][string]$Name)
  $t = ([string]$Name) -creplace '[^A-Za-z0-9 ._-]', ''
  if ($t.Length -gt 40) { $t = $t.Substring(0, 40) }
  if ($t.Trim().Length -eq 0) { return 'gsk' }
  return "gsk $t"
}

# The claude program to start: {file, route, prefix[], shim}. route exe = claude.exe; node = node.exe running the npm
# package's own entry script (prefix), so no cmd.exe parses the arguments and claude.pid records Claude Code itself;
# cmd = an npm .cmd shim whose package entry cannot be found (arguments are then flattened and quoted). $null when
# claude is not on PATH.
function Resolve-GskClaudeCommand {
  $exe = $null
  $fake = Get-GskFake
  if ($null -ne $fake) {
    $exe = [string](Get-GskField $fake 'claude_exe' 'C:\fake\claude.exe')
  } else {
    foreach ($n in @('claude.exe', 'claude.cmd', 'claude.bat')) {
      $c = @(Get-Command $n -CommandType Application -ErrorAction SilentlyContinue)
      if ($c.Length -gt 0 -and $null -ne $c[0]) { $exe = $c[0].Source; break }
    }
  }
  if (-not $exe) { return $null }
  if ($exe -notmatch '\.(cmd|bat)$') { return [pscustomobject]@{ file = $exe; route = 'exe'; prefix = @(); shim = $null } }
  $dir = [IO.Path]::GetDirectoryName($exe)
  $pkgDir = [IO.Path]::Combine($dir, 'node_modules', '@anthropic-ai', 'claude-code')
  $pkg = Read-GskJson ([IO.Path]::Combine($pkgDir, 'package.json'))
  $bin = Get-GskField $pkg 'bin' $null
  $target = ''
  if ($bin -is [string]) { $target = $bin } else { $target = [string](Get-GskField $bin 'claude' '') }
  if ($target) {
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($pkgDir, $target))
    if ($full.StartsWith($pkgDir + '\', [StringComparison]::OrdinalIgnoreCase) -and [IO.File]::Exists($full)) {
      if ($full -match '\.exe$') { return [pscustomobject]@{ file = $full; route = 'exe'; prefix = @(); shim = $exe } }
      if ($full -match '\.(js|mjs|cjs)$') {
        # The npm shim prefers node.exe next to itself, then node on PATH.
        $node = $null
        $local = [IO.Path]::Combine($dir, 'node.exe')
        if ([IO.File]::Exists($local)) { $node = $local }
        elseif ($null -ne $fake) { $node = [string](Get-GskField $fake 'node_exe' '') }
        else {
          $c = @(Get-Command 'node.exe' -CommandType Application -ErrorAction SilentlyContinue)
          if ($c.Length -gt 0 -and $null -ne $c[0]) { $node = $c[0].Source }
        }
        if ($node) { return [pscustomobject]@{ file = $node; route = 'node'; prefix = @($full); shim = $exe } }
      }
    }
  }
  return [pscustomobject]@{ file = $exe; route = 'cmd'; prefix = @(); shim = $exe }
}

# The final argv and argument string for a Resolve-GskClaudeCommand result, with the command-line length and its limit.
function Get-GskClaudeCommandLine {
  param([Parameter(Mandatory = $true)]$Command, [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Argv)
  $final = @(@($Command.prefix) + @($Argv))
  $always = $false
  if ($Command.route -eq 'cmd') {
    # cmd.exe cuts arguments at a newline, expands %VAR% even inside quotes and cannot carry a quote: flatten those,
    # then quote every argument so & | < > ^ ( ) stay literal.
    $final = @($final | ForEach-Object { ((([string]$_ -replace "[`r`n]+", ' ') -replace '"', "'") -replace '%', ' pct') })
    $always = $true
  }
  $argString = ConvertTo-GskArgString -Argv $final -AlwaysQuote:$always
  return [pscustomobject]@{
    route = $Command.route; file = $Command.file; argv = $final; arguments = $argString
    chars = ([string]$Command.file).Length + 3 + $argString.Length; max = $script:GskMaxCmdChars[$Command.route]
  }
}

# $null when $Argv has exactly the shape run.ps1 and supervisor.ps1 produce for $Mode, else the reason:
#   [-n <name>] [--continue | --resume=<uuid>] --permission-mode auto <prompt>
function Test-GskLaunchArgs {
  param([AllowEmptyCollection()][string[]]$Argv, [string]$Mode)
  $n = @($Argv).Length
  if ($n -lt 3) { return 'launch plan args are too short' }
  $prompt = $Argv[$n - 1]
  if ([string]::IsNullOrWhiteSpace($prompt) -or $prompt.StartsWith('-')) { return 'the last launch plan arg must be the prompt' }
  $seen = @{}
  $i = 0
  while ($i -lt $n - 1) {
    $a = [string]$Argv[$i]
    $next = $(if ($i + 1 -lt $n - 1) { [string]$Argv[$i + 1] } else { $null })
    if ($a -ceq '-n' -and -not $seen.ContainsKey('n') -and (Test-GskRunName $next)) { $seen['n'] = $true; $i += 2; continue }
    if ($a -ceq '--continue' -and -not $seen.ContainsKey('c')) { $seen['c'] = $true; $i++; continue }
    if ($a.StartsWith('--resume=') -and -not $seen.ContainsKey('r') -and (Test-GskSessionId $a.Substring(9))) { $seen['r'] = $true; $i++; continue }
    if ($a -ceq '--permission-mode' -and -not $seen.ContainsKey('p') -and $next -ceq 'auto') { $seen['p'] = $true; $i += 2; continue }
    $shown = $a
    if ($shown.Length -gt 40) { $shown = $shown.Substring(0, 40) + '...' }
    return "launch plan arg not allowed: $shown"
  }
  if (-not $seen.ContainsKey('p')) { return 'launch plan args must include --permission-mode auto' }
  $c = $seen.ContainsKey('c')
  $r = $seen.ContainsKey('r')
  if ($Mode -eq 'new' -and ($c -or $r)) { return 'a new-session plan cannot continue or resume' }
  if ($Mode -eq 'continue' -and (-not $c -or $r)) { return 'a continue plan needs --continue (and no --resume)' }
  if ($Mode -eq 'resume' -and (-not $r -or $c)) { return 'a resume plan needs --resume=<session id> (and no --continue)' }
  return $null
}

# The newest launch plan in .harness/launch (*.json, or *.json.used once launch_claude took it), dated by its file-name
# stamp: {path, utc, used}, or $null. Stamps more than 5 minutes in the future are ignored.
function Get-GskLatestPlan {
  param([Parameter(Mandatory = $true)][string]$Workspace, [Parameter(Mandatory = $true)][DateTime]$Now)
  $dir = Get-GskHarnessPath $Workspace @('launch')
  if (-not [IO.Directory]::Exists($dir)) { return $null }
  $best = $null
  foreach ($f in [IO.Directory]::GetFiles($dir)) {
    $m = [regex]::Match([IO.Path]::GetFileName($f), '^(\d{8}T\d{6}Z)-\d+\.json(\.used)?$')
    if (-not $m.Success) { continue }
    $d = [DateTime]::MinValue
    if (-not [DateTime]::TryParseExact($m.Groups[1].Value, "yyyyMMdd'T'HHmmss'Z'", [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]'AdjustToUniversal, AssumeUniversal', [ref]$d)) { continue }
    if (($d - $Now).TotalMinutes -gt 5) { continue }
    if ($null -eq $best -or $d -gt $best.utc) { $best = [pscustomobject]@{ path = $f; utc = $d; used = $m.Groups[2].Success } }
  }
  return $best
}

# A launch counts as pending while its plan is younger than $Minutes and no claude.pid was written after it.
function Test-GskLaunchPending {
  param([Parameter(Mandatory = $true)][string]$Workspace, [Parameter(Mandatory = $true)][DateTime]$Now, [double]$Minutes)
  $p = Get-GskLatestPlan -Workspace $Workspace -Now $Now
  if ($null -eq $p -or ($Now - $p.utc).TotalMinutes -ge $Minutes) { return $null }
  $cp = Read-GskJson (Get-GskHarnessPath $Workspace @('claude.pid'))
  $lu = ConvertFrom-GskUtc ([string](Get-GskField $cp 'launched_utc' ''))
  if ($null -ne $lu -and $lu -ge $p.utc) { return $null }
  return $p
}

# Serialises check-and-launch between run.ps1 and supervisor.ps1 for one workspace. $null when another launch holds
# it past $TimeoutMs.
function Enter-GskLaunchLock {
  param([Parameter(Mandatory = $true)][string]$Workspace, [int]$TimeoutMs = 20000)
  $name = 'Local\gsk-launch-' + (Get-GskSha256Hex -Text $Workspace.TrimEnd('\').ToLowerInvariant()).Substring(0, 16)
  $m = New-Object System.Threading.Mutex($false, $name)
  $got = $false
  try {
    $got = $m.WaitOne($TimeoutMs)
  } catch {
    $e = $_.Exception
    while ($null -ne $e -and -not ($e -is [System.Threading.AbandonedMutexException])) { $e = $e.InnerException }
    if ($null -eq $e) { $m.Dispose(); throw }
    $got = $true
  }
  if (-not $got) { $m.Dispose(); return $null }
  return $m
}

function Exit-GskLaunchLock {
  param([AllowNull()]$Lock)
  if ($null -eq $Lock) { return }
  try { $Lock.ReleaseMutex() } catch { }
  $Lock.Dispose()
}

# Writes the launch plan to .harness/launch/<stamp>-<pid>.json and opens a Windows Terminal tab (window "gsk", start
# folder W) that runs launch_claude.ps1 on it; without wt.exe, a new PowerShell console does the same. The tab's
# command line carries the plan's SHA-256, so launch_claude runs only the plan this call wrote. Only W-relative plan
# paths and the launcher path pass through wt; the prompt never does.
function Start-GskClaudeTab {
  param(
    [Parameter(Mandatory = $true)][string]$Workspace,
    [Parameter(Mandatory = $true)][ValidateSet('new', 'continue', 'resume')][string]$Mode,
    [Parameter(Mandatory = $true)][string[]]$ClaudeArgs,
    [AllowEmptyString()][string]$Title = 'gsk',
    [Parameter(Mandatory = $true)]$LaunchEnv,
    [Parameter(Mandatory = $true)][DateTime]$Now,
    [switch]$AllowParallel,
    [switch]$DryRun
  )
  $stamp = Format-GskFileStamp $Now
  $planRel = ".harness\launch\$stamp-$PID.json"
  $planAbs = [IO.Path]::Combine($Workspace, $planRel)
  $plan = [ordered]@{
    schema = 1; utc = (Format-GskUtc $Now); workspace = $Workspace; mode = $Mode; title = $Title
    args = @($ClaudeArgs); env = $LaunchEnv; allow_parallel = [bool]$AllowParallel
  }
  $planText = ConvertTo-GskJsonDocument -InputObject $plan
  $planSha = Get-GskSha256Hex -Text $planText
  $launcherAbs = [IO.Path]::GetFullPath([IO.Path]::Combine($script:GskHostLibDir, '..', 'launch_claude.ps1'))
  $wsPrefix = $Workspace.TrimEnd('\') + '\'
  $launcherArg = $launcherAbs
  if ($launcherAbs.StartsWith($wsPrefix, [StringComparison]::OrdinalIgnoreCase)) { $launcherArg = $launcherAbs.Substring($wsPrefix.Length) }
  $wt = $null
  $fake = Get-GskFake
  if ($null -ne $fake) {
    if ([bool](Get-GskField $fake 'wt' $true)) { $wt = 'wt.exe' }
  } else {
    $cmd = @(Get-Command 'wt.exe' -CommandType Application -ErrorAction SilentlyContinue)
    if ($cmd.Length -gt 0 -and $null -ne $cmd[0]) { $wt = $cmd[0].Source }
  }
  $route = 'console'
  $file = [IO.Path]::Combine($env:SystemRoot, 'System32', 'WindowsPowerShell', 'v1.0', 'powershell.exe')
  if ($wt) {
    $route = 'wt'
    $file = $wt
    $argv = @('-w', 'gsk', 'nt', '-d', (Protect-GskWtArg $Workspace), '--title', (Protect-GskWtArg $Title),
      'powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Protect-GskWtArg $launcherArg), '-Plan', (Protect-GskWtArg $planRel),
      '-PlanSha256', $planSha)
  } else {
    $argv = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $launcherAbs, '-Plan', $planAbs, '-PlanSha256', $planSha)
  }
  $argString = ConvertTo-GskArgString $argv
  $result = [ordered]@{ route = $route; file = $file; arguments = $argString; plan = ($planRel -replace '\\', '/'); plan_sha256 = $planSha; launched = $false }
  if ($DryRun) { return $result }
  Write-GskTextAtomic -Path $planAbs -Text $planText
  if (Test-GskFake) {
    Add-GskJsonl -Path ([IO.Path]::Combine((Get-GskFakeDir), 'launches.jsonl')) -InputObject ([ordered]@{
        utc = (Format-GskUtc $Now); route = $route; file = $file; arguments = $argString; plan = $result.plan; plan_sha256 = $planSha
        workspace = $Workspace; mode = $Mode; args = @($ClaudeArgs); env = $LaunchEnv
      })
    $result.launched = $true
    $result['faked'] = $true
    return $result
  }
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $file
  $psi.Arguments = $argString
  $psi.WorkingDirectory = $Workspace
  $psi.UseShellExecute = ($route -eq 'console')
  $proc = [System.Diagnostics.Process]::Start($psi)
  $result.launched = $true
  if ($null -ne $proc) { $result['launcher_pid'] = $proc.Id }
  return $result
}

# Refuse to start Claude Code from inside a Claude Code session (guard G15's reasoning: a nested session would run
# this project's UserPromptSubmit hook on agent-chosen text). Claude Code sets CLAUDECODE for its tool processes.
function Test-GskInsideClaude { return [bool]$env:CLAUDECODE }

# ---------------------------------------------------------------- alerts and toasts

function Get-GskLevelRank {
  param([AllowNull()][string]$Level)
  if ($Level -and $script:GskLevelRank.ContainsKey($Level)) { return $script:GskLevelRank[$Level] }
  return -1
}

# Writes .harness/alerts/<name> (harness.schema.json#/$defs/alert, plus since_utc and by). created/escalated tell the
# caller whether to toast.
function Set-GskAlert {
  param(
    [Parameter(Mandatory = $true)][string]$Workspace, [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][ValidateSet('info', 'warn', 'stop', 'emergency')][string]$Level,
    [Parameter(Mandatory = $true)][string]$Message, [AllowNull()]$Value, [Parameter(Mandatory = $true)][DateTime]$Now,
    [Parameter(Mandatory = $true)][string]$By
  )
  $path = Get-GskHarnessPath $Workspace @('alerts', $Name)
  $old = Read-GskJson $path
  $existed = [IO.File]::Exists($path)
  $since = Format-GskUtc $Now
  $prev = $null
  if ($null -ne $old) {
    $s = [string](Get-GskField $old 'since_utc' '')
    if (-not $s) { $s = [string](Get-GskField $old 'utc' '') }
    if ($null -ne (ConvertFrom-GskUtc $s)) { $since = $s }
    $prev = [string](Get-GskField $old 'level' '')
  }
  $doc = [ordered]@{ utc = (Format-GskUtc $Now); level = $Level; message = $Message; value = $Value; since_utc = $since; by = $By }
  Write-GskJsonAtomic -Path $path -InputObject $doc
  return [pscustomobject]@{
    name = $Name; level = $Level; created = (-not $existed)
    escalated = ($existed -and (Get-GskLevelRank $Level) -gt (Get-GskLevelRank $prev))
  }
}

# Removes an alert flag, but only one the calling script owns (its writer), and only inside .harness/alerts.
function Remove-GskOwnAlert {
  param([Parameter(Mandatory = $true)][string]$Workspace, [Parameter(Mandatory = $true)][string]$Name, [Parameter(Mandatory = $true)][string[]]$Owned)
  if ($Owned -cnotcontains $Name) { throw "refusing to remove an alert this script does not own: $Name" }
  $path = Get-GskHarnessPath $Workspace @('alerts', $Name)
  if (-not [IO.File]::Exists($path)) { return $false }
  [IO.File]::Delete($path)
  return $true
}

function Send-GskToast {
  param([Parameter(Mandatory = $true)][string]$Title, [Parameter(Mandatory = $true)][string]$Text, [Parameter(Mandatory = $true)][DateTime]$Now)
  if (Test-GskFake) {
    Add-GskJsonl -Path ([IO.Path]::Combine((Get-GskFakeDir), 'toasts.jsonl')) -InputObject ([ordered]@{ utc = (Format-GskUtc $Now); title = $Title; text = $Text })
    return $true
  }
  if (Get-GskSeamValue 'GSK_HOST_NO_TOAST') { return $false }
  try {
    [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]
    $t = [Security.SecurityElement]::Escape($Title)
    $b = [Security.SecurityElement]::Escape($Text)
    $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $xml.LoadXml("<toast><visual><binding template=`"ToastGeneric`"><text>$t</text><text>$b</text></binding></visual></toast>")
    $toast = New-Object Windows.UI.Notifications.ToastNotification($xml)
    $appId = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'
    [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId).Show($toast)
    return $true
  } catch { return $false }
}

# Minutes since the file was last written; $null when it is missing or dated more than 5 minutes in the future (a
# clock change would otherwise make it look fresh forever).
function Get-GskFileAgeMinutes {
  param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][DateTime]$Now)
  if (-not [IO.File]::Exists($Path)) { return $null }
  $age = [Math]::Round(($Now - [IO.File]::GetLastWriteTimeUtc($Path)).TotalMinutes, 1)
  if ($age -lt -5) { return $null }
  return $age
}
