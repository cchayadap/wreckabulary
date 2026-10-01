#!/usr/bin/env bash
set -euo pipefail
task_repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
task_editor="${UNITY_EDITOR_PATH:-/workspace/.cloud-setup/Unity6000.6.3f1/Editor/Unity}"
task_results_root="${WRECK_VERIFY_RESULTS:-$task_repo_root/Logs/Verification}"
mkdir -p "$task_results_root"
task_results="$(mktemp -d "$task_results_root/run.XXXXXXXX")"
echo "Current Unity verification results: $task_results"
if [[ ! -x "$task_editor" ]]; then
  echo "Unity6000.6.3f1 is required. Set UNITY_EDITOR_PATH to its executable." >&2
  exit 2
fi
"$task_editor" -batchmode -nographics -quit -projectPath "$task_repo_root" \
  -executeMethod Wreckabulary.EditorTools.ProjectSetup.Run -logFile "$task_results/setup.log"
for task_platform in EditMode PlayMode; do
  "$task_editor" -batchmode -nographics -projectPath "$task_repo_root" \
    -runTests -testPlatform "$task_platform" -testResults "$task_results/$task_platform.xml" \
    -logFile "$task_results/$task_platform.log"
  python3 - "$task_results/$task_platform.xml" <<'PY'
import pathlib,sys,xml.etree.ElementTree as ET
p=pathlib.Path(sys.argv[1])
if not p.exists(): raise SystemExit('Unity did not create current test results')
r=ET.parse(p).getroot()
total=int(r.get('total','0')); failed=int(r.get('failed','0'))
print(p.name,dict(r.attrib))
if total==0 or failed or r.get('result') not in ('Passed','Success'): raise SystemExit(1)
PY
done
if [[ "${1:-}" == "--build" ]]; then
  for task_target in Linux Web; do
    "$task_editor" -batchmode -nographics -quit -projectPath "$task_repo_root" \
      -executeMethod "Wreckabulary.EditorTools.ProductionBuild.$task_target" \
      -logFile "$task_results/build-$task_target.log"
  done
fi
