# Game Studio Kit host lib: the two Task Scheduler jobs of a workspace (CONTRACTS section 4.3, pinned).
# Names: gsk-watchdog-<slug> (every minute) and gsk-supervisor-<slug> (every 5 minutes), slug = taskSlug(W leaf).
# The tasks never run code inside W: registration copies the host scripts to a per-user folder outside W
# (%LOCALAPPDATA%\GameStudioKit\host\<slug>-<W hash>), which agent shells cannot write (guard G6), and the task
# description records each copy's SHA-256 for kit doctor. Dot-source after host.ps1.

# What the tasks run, relative to the host folder: the two task scripts, the launcher a supervisor relaunch opens,
# and the libs they dot-source.
$script:GskTaskHostFiles = @('hygiene_watchdog.ps1', 'supervisor.ps1', 'launch_claude.ps1', 'lib\json.ps1', 'lib\host.ps1', 'lib\tasks.ps1')

function Get-GskTaskHostDir {
  param([Parameter(Mandatory = $true)][string]$Workspace)
  $w = $Workspace.TrimEnd('\')
  $slug = ConvertTo-GskTaskSlug ([IO.Path]::GetFileName($w))
  $tag = (Get-GskSha256Hex -Text $w.ToLowerInvariant()).Substring(0, 8)
  return [IO.Path]::Combine((Get-GskLocalAppData), 'GameStudioKit', 'host', "$slug-$tag")
}

function Get-GskTaskSpecs {
  param([Parameter(Mandatory = $true)][string]$Workspace, [Parameter(Mandatory = $true)][string]$HostDir)
  $slug = ConvertTo-GskTaskSlug ([IO.Path]::GetFileName($Workspace.TrimEnd('\')))
  return @(
    [pscustomobject]@{ name = "gsk-watchdog-$slug"; script = [IO.Path]::Combine($HostDir, 'hygiene_watchdog.ps1'); interval_min = 1; what = 'hygiene watchdog' },
    [pscustomobject]@{ name = "gsk-supervisor-$slug"; script = [IO.Path]::Combine($HostDir, 'supervisor.ps1'); interval_min = 5; what = 'supervisor' }
  )
}

# [{file, sha256}] for the host files under $Dir (sha256 $null for a missing file).
function Get-GskHostFileHashes {
  param([Parameter(Mandatory = $true)][string]$Dir)
  $out = New-Object 'System.Collections.Generic.List[object]'
  foreach ($rel in $script:GskTaskHostFiles) {
    $p = [IO.Path]::Combine($Dir, $rel)
    $h = $null
    if ([IO.File]::Exists($p)) { $h = Get-GskSha256Hex -Path $p }
    $out.Add([ordered]@{ file = ($rel -replace '\\', '/'); sha256 = $h })
  }
  return ,$out
}

# Copies the host files from $SourceDir to $DestDir (overwriting older copies) and checks each copy's hash against its
# source. Returns the hashes; throws when a source is missing or a copy differs.
function Copy-GskHostFiles {
  param([Parameter(Mandatory = $true)][string]$SourceDir, [Parameter(Mandatory = $true)][string]$DestDir)
  $src = Get-GskHostFileHashes -Dir $SourceDir
  foreach ($s in $src) { if ($null -eq $s.sha256) { throw "missing host file $($s.file) in $SourceDir" } }
  foreach ($rel in $script:GskTaskHostFiles) {
    $to = [IO.Path]::Combine($DestDir, $rel)
    New-GskParentDir $to
    [IO.File]::Copy([IO.Path]::Combine($SourceDir, $rel), $to, $true)
  }
  $dst = Get-GskHostFileHashes -Dir $DestDir
  for ($i = 0; $i -lt $src.Count; $i++) {
    if ($src[$i].sha256 -ne $dst[$i].sha256) { throw "copy of $($src[$i].file) in $DestDir does not match its source" }
  }
  return ,$dst
}

function Get-GskTaskDescription {
  param([string]$What, [string]$Workspace, [string]$HostDir, $Hashes)
  $parts = New-Object 'System.Collections.Generic.List[string]'
  foreach ($h in $Hashes) { $parts.Add("$($h.file)=$($h.sha256)") }
  return "Game Studio Kit $What for $Workspace. Runs the copy in $HostDir; sha256: $($parts -join '; ')"
}

# The -Workspace value as the task passes it. A trailing backslash before the closing quote would escape it.
function Get-GskTaskWorkspaceArg {
  param([Parameter(Mandatory = $true)][string]$Workspace)
  if ($Workspace.EndsWith('\')) { return "$Workspace." }
  return $Workspace
}

# conhost --headless keeps a console window from flashing up every minute and taking focus from the owner or a
# windowed QA player (Windows 11).
function Get-GskTaskArgument {
  param([Parameter(Mandatory = $true)][string]$Script, [Parameter(Mandatory = $true)][string]$Workspace)
  $w = Get-GskTaskWorkspaceArg $Workspace
  return "--headless powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$Script`" -Workspace `"$w`""
}

# A registered task by exact name: {name, path, execute, argument}, or $null. Read-only.
function Get-GskExistingTask {
  param([Parameter(Mandatory = $true)][string]$Name)
  $fake = Get-GskFake
  if ($null -ne $fake) {
    foreach ($t in @(Get-GskField $fake 'scheduled_tasks' @())) {
      if ($null -ne $t -and [string](Get-GskField $t 'name' '') -ceq $Name) {
        return [pscustomobject]@{ name = $Name; path = '\'; execute = [string](Get-GskField $t 'execute' 'conhost.exe'); argument = [string](Get-GskField $t 'argument' '') }
      }
    }
    return $null
  }
  try {
    $t = @(Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue)
    if ($t.Length -eq 0 -or $null -eq $t[0]) { return $null }
    $act = @($t[0].Actions)
    $exe = ''
    $arg = ''
    if ($act.Length -gt 0 -and $null -ne $act[0]) { $exe = [string]$act[0].Execute; $arg = [string]$act[0].Arguments }
    return [pscustomobject]@{ name = [string]$t[0].TaskName; path = [string]$t[0].TaskPath; execute = $exe; argument = $arg }
  } catch { return $null }
}

# Does a registered task run for this workspace? (Two workspaces with the same folder name share task names.)
function Test-GskTaskForWorkspace {
  param([Parameter(Mandatory = $true)]$Existing, [Parameter(Mandatory = $true)][string]$Workspace)
  $needle = "-Workspace `"$(Get-GskTaskWorkspaceArg $Workspace)`""
  return ([string]$Existing.argument).IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0
}
