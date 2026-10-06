import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

parser=argparse.ArgumentParser();parser.add_argument('--repo',default='.')
args=parser.parse_args();repo=Path(args.repo).resolve();art=repo/'Web/public/art'
subprocess.run([sys.executable,str(repo/'Tools/AssetPipeline/verify_web.py'),'--repo',str(repo)],check=True)
report=json.loads((art/'avatar-mobile-audit.json').read_text())
render=json.loads((art/'avatar-mobile-comparison.json').read_text())
for entry in render['comparison']:
    if entry['sha256']!=hashlib.sha256((art/entry['model']).read_bytes()).hexdigest():
        raise RuntimeError('Comparison image references stale model output; rerender first')
if not (art/'avatar-mobile-comparison.png').is_file():raise RuntimeError('Missing actual model comparison')
manifest=json.loads((art/'manifest.json').read_text())
manifest['avatar'].update(mobilePath='art/avatar-mobile.glb',
    mobileTriangles=report['after_triangles'],mobileDefaultTriangles=report['default_after'],
    mobileBytes=(art/'avatar-mobile.glb').stat().st_size,mobileSha256=report['output_sha256'])
(art/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({k:v for k,v in manifest['avatar'].items() if k.startswith('mobile')}))
