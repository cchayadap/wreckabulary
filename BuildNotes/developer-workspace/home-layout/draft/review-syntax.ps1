$ErrorActionPreference = 'Stop'
Add-Type -Path 'C:/Program Files/dotnet/sdk/9.0.313/Roslyn/bincore/Microsoft.CodeAnalysis.dll'
Add-Type -Path 'C:/Program Files/dotnet/sdk/9.0.313/Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll'
$reviewRoot = Split-Path -Parent $PSScriptRoot
$patchText = [IO.File]::ReadAllText((Join-Path $reviewRoot 'home-layout.patch'))
$firstBlock = ($patchText -split '(?m)^diff --git ')[1]
$originalLines = @($firstBlock -split '\r?\n' | Where-Object { $_.StartsWith('+') -and -not $_.StartsWith('+++') } | ForEach-Object { $_.Substring(1) })
$parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
function Read-HomeSyntax([string]$name, [string]$code) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($code, $parseOptions, $name, [Text.Encoding]::UTF8, [Threading.CancellationToken]::None)
    $errors = @($tree.GetDiagnostics() | Where-Object { $_.Severity -eq [Microsoft.CodeAnalysis.DiagnosticSeverity]::Error } | ForEach-Object {
        [pscustomobject]@{ id=$_.Id; message=$_.GetMessage(); line=$_.Location.GetLineSpan().StartLinePosition.Line+1 }
    })
    [pscustomobject]@{ path=$name; errors=$errors }
}
$before = Read-HomeSyntax 'original-patch/LobbyHomeLayoutSetup.cs' ($originalLines -join "`n")
$after = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Assets') -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object {
    Read-HomeSyntax ([IO.Path]::GetRelativePath($PSScriptRoot, $_.FullName).Replace('\', '/')) ([IO.File]::ReadAllText($_.FullName))
})
$result = [ordered]@{
    recorded_utc=[DateTime]::UtcNow.ToString('o')
    method='Roslyn CSharpSyntaxTree.ParseText; CSharp9; syntax only, not semantic compilation or native validation'
    compiler=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree].Assembly.FullName
    before=$before
    after=$after
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'review-syntax.json') -Encoding utf8NoBOM
[pscustomobject]@{ originalErrors=$before.errors.Count; reviewedFiles=$after.Count; reviewedErrors=@($after | ForEach-Object { $_.errors }).Count } | ConvertTo-Json
if (@($after | ForEach-Object { $_.errors }).Count -gt 0) { exit 1 }
