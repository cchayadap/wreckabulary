from pathlib import Path
import difflib, hashlib, json
out=Path(__file__).resolve().parent
root=out.parents[3]
path='Assets/_Project/Editor/Art/ShopPreviewImporter.cs'
base=(root/path).read_bytes()
baseline=out/'baseline'/path
baseline.parent.mkdir(parents=True,exist_ok=True)
if baseline.exists() and baseline.read_bytes()!=base: raise RuntimeError('Live baseline changed; inspect before refreshing.')
baseline.write_bytes(base)
value=base.decode().replace('\r\n','\n')
before='Nonpublishing courier lighting comparison: current preview lighting, neutral front/fill 1.15/0.65, then the same neutral directions at 1.55/0.85. All use the saved corrected head, original materials/tints, Idle at zero, pose, camera and ambient. A to B is a lighting-preset comparison; B to C changes intensities only.'
after='Nonpublishing courier lighting comparison: neutral front/fill 0.8/0.4 and 0.7/0.35, with the same neutral directions as prior B/C. Both use the saved corrected head, original materials/tints, Idle at zero, pose, camera and ambient. Only intensities change from prior B/C; old A/B/C evidence remains untouched.'
assert value.count(before)==1
value=value.replace(before,after)
before='new[] { "A-current", "B-neutral-front", "C-neutral-front-brighter" }'
after='new[] { "D-neutral-front-soft", "E-neutral-front-softer" }'
assert value.count(before)==1
value=value.replace(before,after)
value=value.replace('SHOP_PORTRAIT_LIGHTING: comparisons=3;', 'SHOP_PORTRAIT_LIGHTING: comparisons=2;')
before='                if (result.control == "B-neutral-front" || result.control == "C-neutral-front-brighter")'
after='''                if (result.control == "B-neutral-front" || result.control == "C-neutral-front-brighter"
                    || result.control == "D-neutral-front-soft" || result.control == "E-neutral-front-softer")'''
assert value.count(before)==1
value=value.replace(before,after)
before='                    { preview.lights[0].intensity = 1.55f; preview.lights[1].intensity = .85f; }'
after=before+'''
                    else if (result.control == "D-neutral-front-soft")
                    { preview.lights[0].intensity = .8f; preview.lights[1].intensity = .4f; }
                    else if (result.control == "E-neutral-front-softer")
                    { preview.lights[0].intensity = .7f; preview.lights[1].intensity = .35f; }'''
assert value.count(before)==1
value=value.replace(before,after)
candidate=out/'candidate'/path; candidate.parent.mkdir(parents=True,exist_ok=True); candidate.write_bytes(value.encode())
patch='diff --git a/'+path+' b/'+path+'\n'+''.join(difflib.unified_diff(base.decode().replace('\r\n','\n').splitlines(keepends=True),value.splitlines(keepends=True),fromfile='a/'+path,tofile='b/'+path))
(out/'lighting-soft.patch').write_bytes(patch.encode())
manifest={'status':'DRAFT_SOURCE_REVIEW_ONLY','files':[{'path':path,'baseline_sha256':hashlib.sha256(base).hexdigest(),'candidate_sha256':hashlib.sha256(value.encode()).hexdigest()}],'patch_sha256':hashlib.sha256(patch.encode()).hexdigest()}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps(manifest,indent=2))
