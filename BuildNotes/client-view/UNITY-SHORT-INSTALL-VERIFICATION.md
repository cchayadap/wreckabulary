# Short-path Unity candidate installation verification

STATUS: COMPLETE — the existing installer finished with exit 0 at 03:04:46Z after owner approval. Bounded read-only installation integrity checks PASS at 03:05:21Z; compilation/native/player acceptance remains pending. The earlier approval-wait observations are preserved below. No installer, editor, UI/UAC interaction, process termination, or package mutation by this lane.

Root-owned wrapper PID: `34936`; script: `.harness/delivery/install-unity-short.ps1`; status: `.harness/delivery/unity-short-install-status.json`. Intended fresh destination: `C:/Users/mrkl6/wb64/6000.6.4f1`.

Purpose: observe the actual download/install result and then check the named Sprite metadata/source and ObjectDispatcher assembly-definition files omitted at long paths in the first installation. Existing diagnosis remains `UNITY-PATCH-PACKAGE-DIAGNOSIS.md`; file presence alone will not establish compilation or gameplay success.

## Observations

| UTC | Bounded observation |
| --- | --- |
| 2026-10-24 01:45:34 | Wrapper status `running`, started 01:41:34.6272243Z, exit/finish absent; PID 34936 alive. Wrapper script requests exact `6000.6.4f1`, changeset `12bfff696524`, `--download-directory wb64 --no-elevate --format json` from `C:/Users/mrkl6`. Stdout/stderr are empty; target directory has not yet appeared. No basis to label this UAC waiting or install completion. |
| 2026-10-24 01:47:55 | Still `running`; exact installer cache file is 1,239,195,719 bytes and was being written at this observation. Target/editor absent; stdout/stderr empty. This is observed download progress, not verified download completion or a UAC-state observation. |
| 2026-10-24 01:50:44 | Download 1,745,598,923 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 01:53:11 | Download 2,227,804,295 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 01:55:29 | Download 2,600,231,079 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 01:57:45 | Download 2,948,313,522 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 02:00:13 | Download 3,458,117,318 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 02:02:46 | Download 3,779,101,684 bytes, active write timestamp; status `running`, target/editor absent, logs empty. |
| 2026-10-24 02:04:58 | Installer file is 3,851,126,776 bytes, last written 02:03:31.2111835Z. Root reports download complete and has asked the owner to approve the required Windows prompt. This lane directly observes `consent` PID 52800, wrapper `running`, target directory present but `Editor/Unity.exe` absent, and empty output logs. The process fact supports the reported UAC wait; this lane did not view or interact with the prompt. |
| 2026-10-24 02:07:31 and 02:09:59 | `consent` PID 52800 still present at both checks; wrapper `running`, editor absent, output logs empty. No completion or failure recorded. |

Known download file, supplied by root and read directly: `C:/Users/mrkl6/AppData/Roaming/UnityHub/downloads/portable-624547c3fbda70f2/UnitySetup64-6000.6.4f1.exe`. No hashing while it is being written. The installer may automatically remove this temporary cache after success; preserve the wrapper's final record before drawing conclusions from cache absence.

## Handoff and exact next read-only verification

Root already asked the owner to approve the required Windows prompt. Do not ask again, retry installation, kill helpers, or infer approval from elapsed time. On the owner's reply, or before root's next heavy job, read the existing wrapper record. Continue only after it records completion and a successful exit; directory or `Unity.exe` presence during extraction is insufficient.

At the 02:09:59Z handoff, the following checks were prepared but not run. They were performed after the final successful status at 03:05:21Z, with selected-file hashes added to the receipt below. They read final status, the signed editor version, all six relevant Sprite source/meta pairs, and the named Core bridge asmdef/meta; they do not execute Unity:

```powershell
$shortStatus = Get-Content -LiteralPath '.harness/delivery/unity-short-install-status.json' -Raw | ConvertFrom-Json
$shortStatus | ConvertTo-Json
if ($shortStatus.status -ne 'finished' -or $shortStatus.exitCode -ne 0) { throw 'Installation is not verified complete.' }
$shortEditor = 'C:/Users/mrkl6/wb64/6000.6.4f1/Editor'
$shortExe = Join-Path $shortEditor 'Unity.exe'
(Get-Item -LiteralPath $shortExe).VersionInfo | Select-Object FileVersion,ProductVersion,CompanyName
Get-AuthenticodeSignature -LiteralPath $shortExe | Select-Object Status,StatusMessage,@{n='Signer';e={$_.SignerCertificate.Subject}}
$shortPackages = Join-Path $shortEditor 'Data/Resources/PackageManager/BuiltInPackages'
$spriteBase = Join-Path $shortPackages 'com.unity.2d.sprite/Editor/SpriteEditorModule/SpriteFrameModule'
$spriteSources = @(
    'Overlay/SpriteFrameModeToolStripBase.cs',
    'SpriteFameModuleDataProviderOverride.cs',
    'SpriteFrameEditor/SpriteFrameEditorMode.cs',
    'SpriteFrameEditor/SpriteFrameEditorModeToggle.cs',
    'SpriteFrameEditor/SpriteFrameScenePreview.cs',
    'UndoObject/SpriteFrameModeUndoObject.cs'
)
$criticalFiles = @($spriteSources | ForEach-Object { Join-Path $spriteBase $_; Join-Path $spriteBase ($_ + '.meta') })
$bridge = Join-Path $shortPackages 'com.unity.render-pipelines.core/Runtime/ObjectDispatcher/Unity.InternalAPIEngineBridge.ObjectDispatcher.Runtime.asmdef'
$criticalFiles += @($bridge, ($bridge + '.meta'))
$criticalFiles | ForEach-Object { [pscustomobject]@{Path=$_;Characters=$_.Length;Exists=(Test-Path -LiteralPath $_ -PathType Leaf)} }
Get-Content -LiteralPath $bridge -Raw
foreach ($packageName in @('com.unity.2d.sprite','com.unity.render-pipelines.core')) {
    $files = @(Get-ChildItem -LiteralPath (Join-Path $shortPackages $packageName) -Recurse -File)
    [pscustomobject]@{Package=$packageName;Files=$files.Count;LongestPath=($files.FullName | ForEach-Object {$_.Length} | Measure-Object -Maximum).Maximum}
}
Get-FileHash -LiteralPath 'Packages/manifest.json','Packages/packages-lock.json' -Algorithm SHA256
Get-FileHash -LiteralPath (Join-Path $shortEditor 'Data/Managed/UnityEngine/UnityEngine.CoreModule.dll') -Algorithm SHA256
```

Acceptance: the executable must identify `6000.6.4f1` and have a valid expected Unity publisher signature; every named critical file must exist. Read the bridge's actual assembly name, do not synthesize metadata. Compare bounded package counts with the failed long install: Sprite **245 files**, Core **2,895 files**, each longest path **259**. Record differences; do not require whole-package identity with .3 because the candidate may legitimately include other changes. The old short core-wrapper source and .3/.4 CoreModule hash baselines are in `UNITY-PATCH-PACKAGE-DIAGNOSIS.md`. Hash only selected installed files after completion, not the active download/extraction tree.

Project package baseline SHA-256 at handoff:

- `Packages/manifest.json`: `db1c638bd54a4bb37306ccaeded26e463fb1ef2a3fc249be09348dfa435196e3`.
- `Packages/packages-lock.json`: `028ab0ccf1da79acc545b2bf9a55a9f26bf5edab950e664cc7d550e138d6a61c`.
- Existing install wrapper: `789496fe54944478cdb86058328735398a84af79523f40c22a5f4162731095c8`.
- Still-running status snapshot read at handoff: `4441ebabfb341a012b30886290187d4be04bba7e0a1eecf1afc8e8e0807ef995`; this file is expected to change when root's wrapper finishes.

Only after these checks should root select the new toolchain, permit ordinary package resolution and rerun compilation/native/build gates. File presence and a valid signature do not prove package compilation, the physical spawn diagnosis, Windows performance, or multiplayer acceptance. All previous installation and user work remains preserved.

## Completed verification — 2026-10-24 03:05:21Z

Owner approval was reported by root; this lane resumed monitoring only the existing status record. The first read still showed `running`. The next read after a 60-second wait showed `finished`, exit `0`, finished UTC `2026-10-24T03:04:46.7793093Z`. No second installation was started. The signed executable and bounded installed-file checks then ran read-only.

- Exact editor: `C:/Users/mrkl6/wb64/6000.6.4f1/Editor/Unity.exe`.
- Product version: `6000.6.4f1_12bfff696524`; file version: `6000.6.4.1228799`; company: Unity Technologies.
- Authenticode: **Valid**, signer **Unity Technologies SF**, DigiCert code-signing issuer; certificate thumbprint `228FB6411B0A144478C86AAA3CD9473C43A8ABA7`. Full signer/timestamp facts are retained in the JSON receipt.
- **14/14 named critical files exist**: six Sprite source/meta pairs and the ObjectDispatcher bridge asmdef/meta. Their exact paths, character lengths and SHA-256 values are recorded individually. No metadata was synthesized or copied from another installation.
- The bridge declares `Unity.InternalAPIEngineBridge.ObjectDispatcher.Runtime`, restoring the expected assembly boundary in the installed package. The next compile must still establish that Unity actually resolves and compiles this package correctly.

| Bounded package tree | Prior long-install files | Fresh short-install files | Additional files | Fresh longest absolute path |
| --- | ---: | ---: | ---: | ---: |
| `com.unity.2d.sprite` | 245 | 253 | 8 | 201 characters |
| `com.unity.render-pipelines.core` | 2,895 | 3,107 | 212 | 237 characters |

Prior counts are the recorded read-only inventory in `UNITY-PATCH-PACKAGE-DIAGNOSIS.md`; the fresh counts were measured only after installation completed. Both old trees stopped at 259-character paths. The short tree supplies every previously identified omitted Sprite/bridge file; this confirms that the fresh short installation repairs those specific omissions. The selected hashes below also show that the existing wrapper/engine definitions did not require a code or visibility patch. This does not audit every installed component or establish the installer's exact internal failing call.

- Project manifest and package lock match the handoff SHA-256 values exactly: **unchanged**.
- `UnityEngine.CoreModule.dll`: `a575473c494efdc06d6e066ced4eefd3368e5040e8199d7c5155611672151a2a` — matches the previously inspected .3 and long .4 binary.
- `ObjectDispatcher.cs`: `0ebd2cdc73a3ef803202f40403696192f4eb253c14304d0b92d857ddbcce65df` — matches the earlier source.
- All four previously hashed shorter Sprite source files match their recorded hashes. The two additional Sprite sources and all six metadata files now have direct receipts.
- Final status SHA-256: `404374e5a2411af34e10b9f15f803ec5802dcfc05099853059bab6d4033654c4`; wrapper SHA remains `789496fe54944478cdb86058328735398a84af79523f40c22a5f4162731095c8`.

Machine-readable evidence: [unity-short-install-integrity-20261024.json](unity-short-install-integrity-20261024.json). Result: **PASS — bounded installation integrity only**. Root was notified immediately after final status and again after the checks. Root owns subsequent editor selection, normal package resolution, compiler/native/player gates and commits. This lane did not launch Unity or change source, package files, caches, installer state, or any other report.
