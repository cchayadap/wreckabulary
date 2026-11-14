<#
.SYNOPSIS
  Upload a release build to a Steam BETA branch with steamcmd, using the builder account's cached login only.

.DESCRIPTION
  game-studio-kit, game-ship (CONTRACTS.md 4.9; research_ship.md 3.7). Windows PowerShell 5.1 compatible.

    powershell -NoProfile -ExecutionPolicy Bypass -File Tools\kit\ship\steampipe\upload.ps1 `
      -BuildDir Builds\<id> -Branch internal [-Preview] [-Builder <account>] [-SteamCmd <steamcmd.exe>] `
      [-AppId <id>] [-DepotId <id>] [-Workspace <W>] [-DryRun] [-SkipReleaseCheck] [-TimeoutMinutes 120] [-Json]

  An upload takes longer than one agent tool call: agents start this script in the background (run_in_background)
  and wait for its JSON line. Only one upload runs at a time (U\Builds\_steampipe\upload.lock, and no second
  steamcmd.exe from the same folder): Steam allows one session per account, so a second login would end the first
  upload midway.

  Human boundary (never crossed by this script):
    - There is no password parameter. The human runs "steamcmd +login <builder>" once, typing the password and the
      Steam Guard code; steamcmd caches the token in <steamcmd>\config\config.vdf. This script refuses to run without
      that cached login (the builder must be an account key inside the "Accounts" block) and only ever passes
      "+login <builder>". A password login would issue a new Steam Guard token.
    - The default branch is never set live: -Branch default is refused. Valve does not allow it from a script anyway;
      the human promotes a build at partner.steamgames.com/apps/builds/<AppID>.
  Refusals that hold even with -SkipReleaseCheck: dev-* and qa-* build folders; a ContentRoot that is not exactly one
  player build (W, U, U\Builds, a folder holding the project, .git, .env, .mcp.json or nested builds, not exactly one
  <Product>.exe with its <Product>_Data); a build whose build.json / packed_assets.json are missing or do not say kind
  release and development false. Without -SkipReleaseCheck, any blocker from release_check.mjs --build also refuses;
  -SkipReleaseCheck is accepted only with -Preview or -DryRun. -Preview uploads nothing (Steam's dry run). -DryRun
  renders the VDF files and prints the command without starting steamcmd.
  AppId and DepotId default to Docs\ship\release.json "app_id" and "depot_id_win64"; Builder defaults to $env:GSK_STEAM_BUILDER;
  steamcmd to $env:GSK_STEAMCMD, then C:\steamcmd\steamcmd.exe, then PATH.
  Exit: 0 ok, 1 refused or steamcmd failed, 2 usage, 3 steamcmd or node missing.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$BuildDir,
  [Parameter(Mandatory = $true)][string]$Branch,
  [switch]$Preview,
  [string]$Builder,
  [string]$SteamCmd,
  [string]$AppId,
  [string]$DepotId,
  [string]$Workspace,
  [switch]$DryRun,
  [switch]$SkipReleaseCheck,
  [int]$TimeoutMinutes = 120,
  [switch]$Json
)

$ErrorActionPreference = 'Stop'
$Utf8 = New-Object System.Text.UTF8Encoding($false)
$script:lockHandle = $null

function Get-UtcStamp([string]$Format) {
  return (Get-Date).ToUniversalTime().ToString($Format, [Globalization.CultureInfo]::InvariantCulture)
}

function Finish([int]$Code, [hashtable]$Result, [string]$Message) {
  $Result['ok'] = ($Code -eq 0)
  $Result['exit'] = $Code
  if ($Message) { $Result['message'] = $Message }
  if ($script:lockHandle) { try { $script:lockHandle.Dispose() } catch { } }
  if ($Json) {
    [Console]::Out.WriteLine((ConvertTo-Json -InputObject $Result -Depth 6 -Compress))
  } else {
    $prefix = 'OK'
    if ($Code -ne 0) { $prefix = 'REFUSED' }
    [Console]::Out.WriteLine("${prefix}: $Message")
  }
  exit $Code
}

# Full path without a trailing separator; never treats [ or ] as wildcards.
function Get-FullPath([string]$Path) {
  return [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Test-SamePath([string]$A, [string]$B) {
  if (-not $A -or -not $B) { return $false }
  return [string]::Equals((Get-FullPath $A), (Get-FullPath $B), [StringComparison]::OrdinalIgnoreCase)
}

function Test-UnderPath([string]$Child, [string]$Parent) {
  $c = Get-FullPath $Child
  $p = Get-FullPath $Parent
  return ([string]::Equals($c, $p, [StringComparison]::OrdinalIgnoreCase) -or $c.StartsWith($p + '\', [StringComparison]::OrdinalIgnoreCase))
}

# True when config.vdf has <name> as an account key inside an "Accounts" block (a substring test would match
# "Steam" or "Valve" in any config.vdf).
function Test-CachedLogin([string]$Vdf, [string]$Name) {
  $keyRe = '(?i)"' + [regex]::Escape($Name) + '"\s*\{'
  $i = $Vdf.IndexOf('"Accounts"', [StringComparison]::OrdinalIgnoreCase)
  while ($i -ge 0) {
    $open = $Vdf.IndexOf('{', $i)
    if ($open -lt 0) { return $false }
    $depth = 0
    $end = $Vdf.Length - 1
    for ($k = $open; $k -lt $Vdf.Length; $k++) {
      $ch = $Vdf[$k]
      if ($ch -eq [char]'{') { $depth++ }
      elseif ($ch -eq [char]'}') { $depth--; if ($depth -eq 0) { $end = $k; break } }
    }
    if ($Vdf.Substring($open, $end - $open + 1) -match $keyRe) { return $true }
    $i = $Vdf.IndexOf('"Accounts"', $end, [StringComparison]::OrdinalIgnoreCase)
  }
  return $false
}

$result = @{ build = $BuildDir; branch = $Branch; preview = [bool]$Preview; dry_run = [bool]$DryRun; utc = (Get-UtcStamp "yyyy-MM-dd'T'HH:mm:ss'Z'") }

if ($SkipReleaseCheck -and -not ($Preview -or $DryRun)) {
  Finish 2 $result 'refused: -SkipReleaseCheck is accepted only with -Preview or -DryRun; a real upload always runs release_check.mjs'
}

# Workspace (W) and Unity project (U) from Tools\kit\kit.json. PowerShell 5.1 cannot start in a folder whose name has
# [ or ] (it falls back to $PSHOME), so the process's own current directory is searched too.
function Find-Workspace([string]$Start) {
  $dir = $Start
  while ($dir) {
    if (Test-Path -LiteralPath (Join-Path $dir 'Tools\kit\kit.json')) { return $dir }
    $parent = [IO.Path]::GetDirectoryName($dir)
    if (-not $parent -or $parent -eq $dir) { break }
    $dir = $parent
  }
  return $null
}
$ws = $null
if ($Workspace) { $ws = (Resolve-Path -LiteralPath $Workspace).ProviderPath }
else {
  $starts = @()
  $here = Get-Location
  if ($here.Provider.Name -eq 'FileSystem') { $starts += $here.ProviderPath }
  $starts += [Environment]::CurrentDirectory
  foreach ($start in $starts) { if (-not $ws) { $ws = Find-Workspace $start } }
}
if (-not $ws) { Finish 3 $result 'no Tools\kit\kit.json found above the current folder; run from the workspace or pass -Workspace' }
$unity = $ws
try {
  $kit = [IO.File]::ReadAllText((Join-Path $ws 'Tools\kit\kit.json')) | ConvertFrom-Json
  if ($kit.unity_subdir -and $kit.unity_subdir -ne '.') { $unity = Join-Path $ws ($kit.unity_subdir -replace '/', '\') }
} catch { Finish 3 $result "cannot read Tools\kit\kit.json: $($_.Exception.Message)" }

# Build folder.
$buildPath = $null
if ([IO.Path]::IsPathRooted($BuildDir)) { $cands = @($BuildDir) } else { $cands = @((Join-Path $unity $BuildDir), (Join-Path $ws $BuildDir)) }
foreach ($c in $cands) { if (Test-Path -LiteralPath $c -PathType Container) { $buildPath = Get-FullPath (Resolve-Path -LiteralPath $c).ProviderPath; break } }
if (-not $buildPath) { Finish 2 $result "build folder not found: $BuildDir" }
$leaf = [IO.Path]::GetFileName($buildPath)
$result['build'] = $buildPath -replace '\\', '/'
if ($leaf -match '^(?i)dev-') { Finish 1 $result "refused: $leaf is a development build (it carries the Unity Pipeline runtime); upload only a release build" }
if ($leaf -match '^(?i)qa-') { Finish 1 $result "refused: $leaf is a Release-QA build (QaRunner inside); upload the stripped release build" }

# The depot ContentRoot must be exactly one player build: never the workspace, the project or a folder of builds.
foreach ($root in @($ws, $unity, (Join-Path $unity 'Builds'), (Join-Path $ws 'Builds'))) {
  if (Test-SamePath $buildPath $root) { Finish 1 $result "refused: $buildPath is the workspace, the Unity project or the Builds folder, not one player build" }
}
if (Test-UnderPath $ws $buildPath) { Finish 1 $result "refused: $buildPath contains the workspace" }
foreach ($marker in @('Tools\kit\kit.json', '.git', '.env', '.mcp.json', 'Assets', 'ProjectSettings', 'Library')) {
  if (Test-Path -LiteralPath (Join-Path $buildPath $marker)) { Finish 1 $result "refused: $leaf contains $marker, so it is not a player build folder" }
}
$dataDirs = @(Get-ChildItem -LiteralPath $buildPath -Directory -Force | Where-Object { $_.Name -match '(?i)_Data$' })
if ($dataDirs.Count -ne 1) { Finish 1 $result "refused: $leaf has $($dataDirs.Count) <Product>_Data folders; a Windows player build has exactly one" }
$product = $dataDirs[0].Name.Substring(0, $dataDirs[0].Name.Length - 5)
if (-not (Test-Path -LiteralPath (Join-Path $buildPath ($product + '.exe')) -PathType Leaf)) { Finish 1 $result "refused: $leaf has $($dataDirs[0].Name) but no $product.exe beside it" }
$nested = @(Get-ChildItem -LiteralPath $buildPath -Recurse -Force -ErrorAction SilentlyContinue | Where-Object {
    ($_.PSIsContainer -and $_.Name -match '^(?i)(dev|qa)-') -or
    ((-not $_.PSIsContainer) -and $_.Name -match '^(?i)(UnityPlayer\.dll|build\.json|packed_assets\.json)$' -and -not (Test-SamePath $_.DirectoryName $buildPath))
  } | Select-Object -First 3)
if ($nested.Count) { Finish 1 $result "refused: $leaf holds another build ($($nested[0].FullName)); upload one player build folder" }
$metaSeen = $false
$metaRelease = $false
foreach ($meta in @('build.json', 'packed_assets.json')) {
  $p = Join-Path $buildPath $meta
  if (Test-Path -LiteralPath $p -PathType Leaf) {
    $metaSeen = $true
    try { $doc = [IO.File]::ReadAllText($p) | ConvertFrom-Json } catch { Finish 1 $result "refused: cannot parse $meta" }
    if ($doc.development -eq $true) { Finish 1 $result "refused: $meta says this is a development player" }
    if ($doc.kind -and $doc.kind -ne 'release') { Finish 1 $result "refused: $meta kind is '$($doc.kind)', not release" }
    if ($doc.kind -eq 'release' -and $doc.development -eq $false) { $metaRelease = $true }
  }
}
if (-not $metaSeen) { Finish 1 $result "refused: $leaf has neither build.json nor packed_assets.json (BuildReportExport writes them), so nothing says it is a release build" }
if (-not $metaRelease) { Finish 1 $result "refused: neither build.json nor packed_assets.json in $leaf says kind 'release' with development false" }

# Branch: a beta branch only.
$b = $Branch.Trim()
if (-not $b) { Finish 2 $result 'refused: -Branch is empty' }
if ($b -ieq 'default' -or $b -ieq 'public') { Finish 1 $result "refused: the default branch is never set live by a script; upload to a beta branch (for example internal) and let the human promote it in Steamworks" }
if ($b -notmatch '^[A-Za-z0-9_.-]+$') { Finish 2 $result "refused: branch name '$b' has characters Steam branch names do not use" }

# App and depot ids.
$release = $null
$releaseFile = Join-Path $ws 'Docs\ship\release.json'
if (Test-Path -LiteralPath $releaseFile) { try { $release = [IO.File]::ReadAllText($releaseFile) | ConvertFrom-Json } catch { $release = $null } }
if (-not $AppId -and $release -and $release.app_id) { $AppId = [string]$release.app_id }
if (-not $DepotId -and $release -and $release.depot_id_win64) { $DepotId = [string]$release.depot_id_win64 }
if ($AppId -notmatch '^[0-9]+$') { Finish 2 $result 'no AppId: pass -AppId or set app_id in Docs\ship\release.json' }
if ($DepotId -notmatch '^[0-9]+$') { Finish 2 $result 'no DepotId: pass -DepotId or set depot_id_win64 in Docs\ship\release.json' }
$result['app_id'] = $AppId
$result['depot_id'] = $DepotId

# Builder account (user name only).
if (-not $Builder) { $Builder = $env:GSK_STEAM_BUILDER }
if (-not $Builder) { Finish 2 $result 'no builder account: pass -Builder or set GSK_STEAM_BUILDER (the account name only, never a password)' }
if ($Builder -notmatch '^[A-Za-z0-9_.@-]+$') { Finish 2 $result 'refused: -Builder must be a plain Steam account name' }
$result['builder'] = $Builder

# steamcmd and its cached login.
$exe = $null
foreach ($c in @($SteamCmd, $env:GSK_STEAMCMD, 'C:\steamcmd\steamcmd.exe')) { if ($c -and (Test-Path -LiteralPath $c -PathType Leaf)) { $exe = (Resolve-Path -LiteralPath $c).ProviderPath; break } }
if (-not $exe) { $cmd = Get-Command steamcmd -ErrorAction SilentlyContinue; if ($cmd) { $exe = $cmd.Source } }
if (-not $exe) { Finish 3 $result 'steamcmd not found: pass -SteamCmd, set GSK_STEAMCMD, or install it to C:\steamcmd (human step)' }
$result['steamcmd'] = $exe -replace '\\', '/'
$configVdf = Join-Path ([IO.Path]::GetDirectoryName($exe)) 'config\config.vdf'
if (-not (Test-Path -LiteralPath $configVdf -PathType Leaf)) {
  Finish 1 $result "refused: no cached steamcmd login ($configVdf is missing). HUMAN STEP: run `"$exe +login $Builder`" once, type the password and Steam Guard code yourself, then re-run this script"
}
if (-not (Test-CachedLogin ([IO.File]::ReadAllText($configVdf)) $Builder)) {
  Finish 1 $result "refused: config.vdf has no cached login for '$Builder' in its Accounts block. HUMAN STEP: run `"$exe +login $Builder`" once yourself"
}

# release_check on this build (licences, notices, dev/QA runtime, steam_appid.txt).
if (-not $SkipReleaseCheck) {
  $node = Get-Command node -ErrorAction SilentlyContinue
  if (-not $node) { Finish 3 $result 'node not found (needed for release_check.mjs)' }
  $rc = Join-Path $ws 'Tools\kit\ship\release_check.mjs'
  $prev = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  Push-Location -LiteralPath $ws
  try { $rcOut = & $node.Source $rc --build $buildPath --json 2>$null } finally { Pop-Location; $ErrorActionPreference = $prev }
  $line = @($rcOut | Where-Object { $_ -and $_.Trim() }) | Select-Object -Last 1
  $rcDoc = $null
  try { $rcDoc = $line | ConvertFrom-Json } catch { $rcDoc = $null }
  if (-not $rcDoc) { Finish 1 $result 'refused: release_check.mjs gave no result' }
  if (-not $rcDoc.ok) {
    $ids = @($rcDoc.blockers | ForEach-Object { $_.id }) -join ', '
    $result['blockers'] = @($rcDoc.blockers)
    Finish 1 $result "refused: release_check.mjs found blockers ($ids)"
  }
}

# One upload at a time. The handle stays open for the script's lifetime; Windows releases it if the process dies.
$steamDir = Join-Path $unity 'Builds\_steampipe'
[IO.Directory]::CreateDirectory($steamDir) | Out-Null
$lockPath = Join-Path $steamDir 'upload.lock'
try {
  $script:lockHandle = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
} catch {
  Finish 1 $result "refused: another upload is running ($lockPath is held); wait for it to finish"
}
$pidBytes = [Text.Encoding]::ASCII.GetBytes([string]$PID)
$script:lockHandle.SetLength(0)
$script:lockHandle.Write($pidBytes, 0, $pidBytes.Length)
$script:lockHandle.Flush()
if (-not $DryRun) {
  $running = @()
  try { $running = @(Get-CimInstance Win32_Process -Filter "Name='steamcmd.exe'" -ErrorAction Stop | Where-Object { $_.ExecutablePath -and [string]::Equals($_.ExecutablePath, $exe, [StringComparison]::OrdinalIgnoreCase) }) } catch { $running = @() }
  if ($running.Count) { Finish 1 $result "refused: steamcmd is already running (PID $($running[0].ProcessId)); Steam allows one session per account, so a second login would end that upload" }
}

# Render the VDF files next to the builds (never inside the build folder that gets uploaded).
$stamp = Get-UtcStamp 'yyyyMMddTHHmmssZ'
$stage = Join-Path $steamDir $leaf
$output = Join-Path $steamDir 'output'
$logDir = Join-Path $unity 'Logs'
foreach ($p in @($stage, $output, $logDir)) {
  if (Test-UnderPath $p $buildPath) { Finish 1 $result "refused: $p would be inside the uploaded folder $buildPath" }
}
[IO.Directory]::CreateDirectory($stage) | Out-Null
[IO.Directory]::CreateDirectory($output) | Out-Null
$version = ''
if ($release -and $release.version) { $version = [string]$release.version }
$desc = ("$leaf $version $stamp" -replace '\s+', ' ').Trim()
$previewValue = '0'
if ($Preview) { $previewValue = '1' }
$tmplDir = $PSScriptRoot
$depotVdf = Join-Path $stage 'depot_win64.vdf'
$appVdf = Join-Path $stage 'app_build.vdf'
$map = @{
  '{{APP_ID}}' = $AppId; '{{DEPOT_ID_WIN64}}' = $DepotId; '{{DESC}}' = $desc; '{{CONTENT_ROOT}}' = ($buildPath.TrimEnd('\') + '\');
  '{{BUILD_OUTPUT}}' = ($output.TrimEnd('\') + '\'); '{{PREVIEW}}' = $previewValue; '{{SETLIVE}}' = $b; '{{DEPOT_VDF}}' = $depotVdf
}
foreach ($pair in @(@('depot_win64.vdf.tmpl', $depotVdf), @('app_build.vdf.tmpl', $appVdf))) {
  $text = [IO.File]::ReadAllText((Join-Path $tmplDir $pair[0]))
  foreach ($k in $map.Keys) { $text = $text.Replace($k, $map[$k]) }
  if ($text -match '\{\{[A-Z_0-9]+\}\}') { Finish 2 $result "template $($pair[0]) has an unfilled placeholder" }
  if ($text -match '(?i)"SetLive"\s+"(default|public)"') { Finish 1 $result 'refused: SetLive default' }
  [IO.File]::WriteAllText($pair[1], $text, $Utf8)
}
$result['app_build_vdf'] = $appVdf -replace '\\', '/'
$result['depot_vdf'] = $depotVdf -replace '\\', '/'
$stArgs = @('+login', $Builder, '+run_app_build', $appVdf, '+quit')
$result['command'] = @($exe) + $stArgs

if ($DryRun) { Finish 0 $result "dry run: rendered $appVdf; would run steamcmd +login $Builder +run_app_build <vdf> +quit (branch $b, preview $previewValue)" }

[IO.Directory]::CreateDirectory($logDir) | Out-Null
$log = Join-Path $logDir ("steampipe_" + $stamp + ".log")
$errLog = Join-Path $logDir ("steampipe_" + $stamp + ".err.log")
$argLine = '+login ' + $Builder + ' +run_app_build "' + $appVdf + '" +quit'
# System.Diagnostics.Process instead of Start-Process: Start-Process treats [ and ] in redirect paths as wildcards.
# stdin is closed at once: if the cached login has expired, steamcmd's password prompt reads EOF and fails instead of
# waiting forever for a password that this script must never type.
$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = $exe
$psi.Arguments = $argLine
$psi.WorkingDirectory = [IO.Path]::GetDirectoryName($exe)
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$proc = New-Object Diagnostics.Process
$proc.StartInfo = $psi
$outFs = New-Object IO.FileStream($log, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
$errFs = New-Object IO.FileStream($errLog, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
try {
  $null = $proc.Start()
} catch {
  $outFs.Dispose(); $errFs.Dispose()
  Finish 3 $result "cannot start steamcmd: $($_.Exception.Message)"
}
$proc.StandardInput.Close()
$outTask = $proc.StandardOutput.BaseStream.CopyToAsync($outFs)
$errTask = $proc.StandardError.BaseStream.CopyToAsync($errFs)
$timedOut = -not $proc.WaitForExit($TimeoutMinutes * 60000)
if ($timedOut) { & taskkill.exe /PID $proc.Id /T /F 2>$null | Out-Null }
$proc.WaitForExit()
try { $null = $outTask.Wait(30000); $null = $errTask.Wait(30000) } catch { }
$outFs.Dispose()
$errFs.Dispose()
$result['log'] = $log -replace '\\', '/'
if ($timedOut) { Finish 1 $result "steamcmd did not finish within $TimeoutMinutes minutes and was stopped (PID tree); see $log" }
$code = $proc.ExitCode
$result['steamcmd_exit'] = $code
$joined = ''
if (Test-Path -LiteralPath $log) { $joined = [IO.File]::ReadAllText($log) }
if (Test-Path -LiteralPath $errLog) { $joined = $joined + "`n" + [IO.File]::ReadAllText($errLog) }
$m = [regex]::Match($joined, 'BuildID\s+(\d+)')
if ($m.Success) { $result['steam_build_id'] = $m.Groups[1].Value }
if ($code -ne 0 -or $joined -match '(?i)\bERROR!|Login Failure|Invalid Password|Two-factor') {
  Finish 1 $result "steamcmd failed (exit $code); see $log. If it asks for a password or Steam Guard code, the cached login expired: the human logs in again"
}
Finish 0 $result "uploaded $leaf to branch $b (preview $previewValue); promote to default by hand in Steamworks"
