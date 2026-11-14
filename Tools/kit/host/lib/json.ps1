# Game Studio Kit host lib: time, JSON and file writing for Windows PowerShell 5.1.
# Follows the CONTRACTS section 4.3 recipe: UTC strings (never [DateTime] into ConvertTo-Json), ConvertTo-Json -Depth 8,
# UTF-8 without BOM through [IO.File] with absolute paths, atomic replace for documents, one append per JSONL line.
# Never use >, >>, Out-File, Set-Content or Add-Content for kit files (UTF-16LE or BOM + CRLF under -NoProfile).

$script:GskUtf8 = New-Object System.Text.UTF8Encoding($false)
$script:GskUtcPattern = '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$'

# Test seams. GSK_NOW, GSK_HOME, GSK_HOST_FAKE and GSK_HOST_NO_TOAST are honoured only after the calling script's
# -TestSeams switch ran Enable-GskTestSeams. Task Scheduler actions and Windows Terminal tabs never pass it, so a seam
# made persistent in the user environment (setx) cannot fake the machine facts, pin the clock or hide toasts.
$script:GskSeamVars = @('GSK_HOST_FAKE', 'GSK_NOW', 'GSK_HOME', 'GSK_HOST_NO_TOAST')
$script:GskSeamsOn = $false

function Enable-GskTestSeams { $script:GskSeamsOn = $true }

function Test-GskTestSeams { return [bool]$script:GskSeamsOn }

function Get-GskSeamValue {
  param([Parameter(Mandatory = $true)][string]$Name)
  if (-not $script:GskSeamsOn) { return $null }
  $v = [Environment]::GetEnvironmentVariable($Name, 'Process')
  if ([string]::IsNullOrEmpty($v)) { return $null }
  return $v
}

# Seam variables present in this process while seams are off: they leaked into a production environment.
function Get-GskSeamLeaks {
  $out = New-Object 'System.Collections.Generic.List[string]'
  if ($script:GskSeamsOn) { return ,$out }
  foreach ($n in $script:GskSeamVars) {
    if (-not [string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($n, 'Process'))) { $out.Add($n) }
  }
  return ,$out
}

# Removes the seams from this process, so a child (Claude Code and its hooks) never inherits them.
function Clear-GskSeamEnv {
  foreach ($n in $script:GskSeamVars) { [Environment]::SetEnvironmentVariable($n, [NullString]::Value, 'Process') }
}

function ConvertFrom-GskUtc {
  param([AllowNull()][AllowEmptyString()][string]$Text)
  if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
  $t = $Text.Trim()
  if ($t -notmatch $script:GskUtcPattern) { return $null }
  $styles = [Globalization.DateTimeStyles]'AdjustToUniversal, AssumeUniversal'
  $d = [DateTime]::MinValue
  if ([DateTime]::TryParse($t, [Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$d)) { return $d }
  return $null
}

function Format-GskUtc {
  param([Parameter(Mandatory = $true)][DateTime]$Date)
  $u = $Date
  if ($u.Kind -ne [DateTimeKind]::Utc) { $u = $u.ToUniversalTime() }
  return $u.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [Globalization.CultureInfo]::InvariantCulture)
}

function Format-GskFileStamp {
  param([Parameter(Mandatory = $true)][DateTime]$Date)
  $u = $Date
  if ($u.Kind -ne [DateTimeKind]::Utc) { $u = $u.ToUniversalTime() }
  return $u.ToString("yyyyMMdd'T'HHmmss'Z'", [Globalization.CultureInfo]::InvariantCulture)
}

# "Now" for every host script. Under -TestSeams, GSK_NOW (ISO UTC) pins it (CONTRACTS section 1.2).
function Get-GskNow {
  $pinned = Get-GskSeamValue 'GSK_NOW'
  if ($pinned) {
    $d = ConvertFrom-GskUtc $pinned
    if ($null -ne $d) { return $d }
  }
  return [DateTime]::UtcNow
}

function Get-GskSha256Hex {
  param([byte[]]$Bytes, [string]$Text, [string]$Path)
  $b = $Bytes
  if ($PSBoundParameters.ContainsKey('Text')) { $b = $script:GskUtf8.GetBytes($Text) }
  if ($Path) { $b = [IO.File]::ReadAllBytes($Path) }
  if ($null -eq $b) { $b = New-Object byte[] 0 }
  $sha = [Security.Cryptography.SHA256]::Create()
  try { $h = $sha.ComputeHash($b) } finally { $sha.Dispose() }
  return (($h | ForEach-Object { $_.ToString('x2') }) -join '')
}

function ConvertTo-GskJson {
  param([Parameter(Mandatory = $true)][AllowNull()]$InputObject, [switch]$Compress)
  if ($Compress) { return (ConvertTo-Json -InputObject $InputObject -Depth 8 -Compress) }
  return (ConvertTo-Json -InputObject $InputObject -Depth 8)
}

function Assert-GskAbsolute {
  param([Parameter(Mandatory = $true)][string]$Path)
  if (-not [IO.Path]::IsPathRooted($Path) -or $Path -match '^[\\/][^\\/]') { throw "kit files need an absolute path: $Path" }
}

function New-GskParentDir {
  param([Parameter(Mandatory = $true)][string]$Path)
  $dir = [IO.Path]::GetDirectoryName($Path)
  if ($dir -and -not [IO.Directory]::Exists($dir)) { [void][IO.Directory]::CreateDirectory($dir) }
}

function Write-GskTextAtomic {
  param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)
  Assert-GskAbsolute $Path
  New-GskParentDir $Path
  $tmp = "$Path.tmp-$PID"
  [IO.File]::WriteAllText($tmp, $Text, $script:GskUtf8)
  $attempt = 0
  while ($true) {
    try {
      if ([IO.File]::Exists($Path)) { [IO.File]::Replace($tmp, $Path, [NullString]::Value) } else { [IO.File]::Move($tmp, $Path) }
      return
    } catch {
      $attempt++
      if ($attempt -ge 5) {
        try { [IO.File]::Delete($tmp) } catch { }
        throw
      }
      Start-Sleep -Milliseconds 100
    }
  }
}

# The exact text Write-GskJsonAtomic writes (LF endings, final LF).
function ConvertTo-GskJsonDocument {
  param([Parameter(Mandatory = $true)][AllowNull()]$InputObject)
  return (((ConvertTo-GskJson -InputObject $InputObject) -replace "`r`n", "`n") + "`n")
}

function Write-GskJsonAtomic {
  param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][AllowNull()]$InputObject)
  Write-GskTextAtomic -Path $Path -Text (ConvertTo-GskJsonDocument -InputObject $InputObject)
}

function Add-GskJsonl {
  param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$InputObject)
  Assert-GskAbsolute $Path
  New-GskParentDir $Path
  $line = ConvertTo-GskJson -InputObject $InputObject -Compress
  [IO.File]::AppendAllText($Path, $line + "`n", $script:GskUtf8)
}

# Reads UTF-8 (with or without BOM) and UTF-16 with BOM; shares the file with writers. $null when missing.
function Read-GskText {
  param([Parameter(Mandatory = $true)][string]$Path)
  if (-not [IO.File]::Exists($Path)) { return $null }
  $fs = New-Object IO.FileStream($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
  try {
    $sr = New-Object IO.StreamReader($fs, $script:GskUtf8, $true)
    try { return $sr.ReadToEnd() } finally { $sr.Dispose() }
  } finally { $fs.Dispose() }
}

# Parsed JSON document, or $null when the file is missing, empty or invalid.
function Read-GskJson {
  param([Parameter(Mandatory = $true)][string]$Path)
  $text = Read-GskText $Path
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  try { return (ConvertFrom-Json -InputObject $text) } catch { return $null }
}

# Parsed JSON from UTF-8 bytes (a leading BOM is skipped), or $null.
function ConvertFrom-GskJsonBytes {
  param([AllowNull()][byte[]]$Bytes)
  if ($null -eq $Bytes -or $Bytes.Length -eq 0) { return $null }
  $start = 0
  if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) { $start = 3 }
  $text = $script:GskUtf8.GetString($Bytes, $start, $Bytes.Length - $start)
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  try { return (ConvertFrom-Json -InputObject $text) } catch { return $null }
}

# The last $Max parseable lines of a JSONL file (oldest first) as a List[object] (never $null: an empty array
# returned from a 5.1 function arrives as $null). Reads at most the final 256 KiB; skips bad lines.
function Read-GskJsonlTail {
  param([Parameter(Mandatory = $true)][string]$Path, [int]$Max = 200)
  $out = New-Object 'System.Collections.Generic.List[object]'
  if (-not [IO.File]::Exists($Path)) { return ,$out }
  $fs = New-Object IO.FileStream($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
  try {
    $window = 262144
    $skipFirst = $false
    if ($fs.Length -gt $window) { [void]$fs.Seek(-$window, [IO.SeekOrigin]::End); $skipFirst = $true }
    $sr = New-Object IO.StreamReader($fs, $script:GskUtf8, $true)
    try { $text = $sr.ReadToEnd() } finally { $sr.Dispose() }
  } finally { $fs.Dispose() }
  $lines = $text -split "`n"
  $start = 0
  if ($skipFirst) { $start = 1 }
  for ($i = $start; $i -lt $lines.Length; $i++) {
    $l = $lines[$i].Trim()
    if ($l.Length -eq 0) { continue }
    try { $out.Add((ConvertFrom-Json -InputObject $l)) } catch { }
  }
  if ($out.Count -gt $Max) { $out.RemoveRange(0, $out.Count - $Max) }
  return ,$out
}

# Field of a parsed JSON object or dictionary, or $Default when absent (safe under Set-StrictMode).
function Get-GskField {
  param([AllowNull()]$Object, [Parameter(Mandatory = $true)][string]$Name, [AllowNull()]$Default = $null)
  if ($null -eq $Object) { return $Default }
  if ($Object -is [System.Collections.IDictionary]) {
    if ($Object.Contains($Name)) { return $Object[$Name] }
    return $Default
  }
  $p = $Object.PSObject.Properties[$Name]
  if ($null -eq $p) { return $Default }
  return $p.Value
}

# One JSON line on stdout: the result every host script ends with (consumers read the last non-empty line).
function Write-GskResult {
  param([Parameter(Mandatory = $true)]$InputObject)
  [Console]::Out.WriteLine((ConvertTo-GskJson -InputObject $InputObject -Compress))
}

function Write-GskNote {
  param([Parameter(Mandatory = $true)][string]$Message)
  [Console]::Error.WriteLine($Message)
}
