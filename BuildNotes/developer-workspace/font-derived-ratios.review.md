# Font base-material ratio normalization

STATUS: SOURCE TRACE COMPLETE — MINIMAL FIX PREPARED OUTSIDE ASSETS

Read-only investigation of root-reported post-Shop-setup differences in two saved font base materials. Root reports no glyph, atlas or authored style changes, and preserves post-setup files in `.harness/recovery/developer-workspace-2026-10-26T08-13-56-734Z`. The earlier 77-file strict preservation baseline has not passed. Native Play61 is root-owned and active; no live source/assets or native processes will be changed by this investigation.

## Confirmed source mechanism

`LobbyTypographySetup.MakeFont` (line 117 onward) creates and configures each base font material but does not call `ShaderUtilities.UpdateShaderRatios` on it. The call at line 154 applies only to separately copied presets. Fonts and their base materials are saved at lines 77–93 with their initial ratios still equal to one.

`LobbyShopLayoutSetup.Text` (lines 392–399) assigns the saved font and its base material directly. The installed TMP font setter calls `LoadFontAsset`; `TextMeshProUGUI.LoadFontAsset` (1201–1254) selects the font's base material and calls `GetPaddingForMaterial`. TMP's padding code invokes `ShaderUtilities.UpdateShaderRatios` (line 342), which writes ratio A/C based on existing gradient, weight and underlay inputs. The Shop generator uses Display and Body, consistent with Black remaining untouched.

The values exactly match the package's formula (`m_clamp = 1`, weight `max(0,.75)/4 = .1875`, no face/outline/underlay dilation):

| Base font | Gradient scale | Calculated A | Calculated C |
| --- | --- | --- | --- |
| LilitaOne | 13 | 12/13 = 0.9230769 | (12/13) × (1 − .1875) = 0.75 |
| Nunito ExtraBold | 11 | 10/11 = 0.90909094 | (10/11) × (1 − .1875) = 0.7386364 |

The scoped live Git diff independently shows only these four field replacements in the two font assets. There is no Black diff and no other hunk in those files. This is derived-material normalization; it does not show authored color, outline, weight, glyph, atlas or font-style loss. Source confirms the in-memory write chain. The precise native editor flush call is not separately instrumented here.

## Smallest fix and honest preservation boundary

Initialize the base material's ratios in MakeFont before a fresh default is saved. Do not normalize existing assets in the verify branch and do not add runtime/editor-wide font interception. The two-file candidate at `font-ratios/` adds this single production call and extends the existing saved-new-font assertion to compare each base material with a temporary copy after the public TMP ratio calculation. No test case or assembly reference is added; the existing fixture remains six cases.

Per root's explicit decision, retain the two real normalized fonts as intentional defaults. The original 77-file baseline was not byte-identical: four derived fields changed. Do not describe that initial setup as strict preservation. Repeated Shop setup was separately byte-identical across its 39 outputs. Root additionally reports Play081310 passed 61/61 and preserved both the 77-file post-setup baseline and 39 Shop files. That supports the runtime ownership boundary, not retrospective identity with the earlier unnormalized baseline.

The current Black base material remains unnormalized because Shop creation did not use it. A future explicit editor use may normalize it by the same mechanism; the prospective MakeFont fix does not retroactively rewrite existing files. If that occurs, record the bounded derived-field change explicitly rather than silently exempting it from hashing or restoring it repeatedly. Runtime copies remain responsible for ordinary Play glyph/material mutations.

No live source or saved assets were edited. No native calls were made. Root will integrate the draft after its active native work and run R0 plus the existing six typography cases.
