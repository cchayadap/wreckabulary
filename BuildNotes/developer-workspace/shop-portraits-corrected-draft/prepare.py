from pathlib import Path
import difflib, hashlib, json
out = Path(__file__).resolve().parent
root = out.parents[2]
path = 'Assets/_Project/Editor/Art/ShopPreviewImporter.cs'
baseline = out / 'baseline' / path
baseline.parent.mkdir(parents=True, exist_ok=True)
original = (root / path).read_bytes()
if baseline.exists() and baseline.read_bytes() != original:
    raise RuntimeError('Live baseline changed; inspect before regenerating this draft.')
baseline.write_bytes(original)
candidate = out / 'candidate' / path
candidate.parent.mkdir(parents=True, exist_ok=True)
value = (out / 'ShopPreviewImporter.cs').read_text(encoding='utf-8').replace('\r\n', '\n')
candidate.write_bytes(value.encode())
patch = 'diff --git a/' + path + ' b/' + path + '\n' + ''.join(difflib.unified_diff(original.decode().replace('\r\n', '\n').splitlines(keepends=True), value.splitlines(keepends=True), fromfile='a/' + path, tofile='b/' + path))
(out / 'shop-portraits-corrected.patch').write_bytes(patch.encode())
manifest = {'status': 'DRAFT_SOURCE_REVIEW_ONLY', 'requires': 'Avatar follow-up VerifyExistingProfile(required:true)', 'files': [{'path': path, 'baseline_sha256': hashlib.sha256(original).hexdigest(), 'candidate_sha256': hashlib.sha256(value.encode()).hexdigest()}], 'patch_sha256': hashlib.sha256(patch.encode()).hexdigest()}
(out / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(json.dumps(manifest, indent=2))
