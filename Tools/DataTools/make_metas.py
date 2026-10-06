import argparse
import hashlib
import os

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
ASSETS = os.path.join(REPO, "Assets")

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
ASMDEF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
SCRIPT = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
BY_EXTENSION = {".asmdef": ASMDEF, ".asmref": ASMDEF, ".json": TEXT, ".txt": TEXT, ".csv": TEXT, ".md": TEXT, ".cs": SCRIPT}


def guid_for(rel):
    return hashlib.md5(("wreckabulary:" + rel).encode("utf-8")).hexdigest()


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args()
    written, skipped = [], []
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = sorted(d for d in dirs if not d.startswith(".") and not d.endswith("~"))
        entries = [(d, True) for d in dirs] + [(f, False) for f in sorted(files)]
        for name, is_dir in entries:
            if name.startswith(".") or name.endswith(".meta"):
                continue
            path = os.path.join(root, name)
            meta = path + ".meta"
            if os.path.exists(meta):
                continue
            rel = os.path.relpath(path, REPO).replace(os.sep, "/")
            template = FOLDER if is_dir else BY_EXTENSION.get(os.path.splitext(name)[1].lower())
            if template is None:
                skipped.append(rel)
                continue
            if not args.dry_run:
                with open(meta, "w", encoding="utf-8", newline="\n") as f:
                    f.write(template.format(guid=guid_for(rel)))
            written.append(rel)
    for rel in written:
        print(("would write " if args.dry_run else "wrote ") + rel + ".meta")
    for rel in skipped:
        print("left for Unity to import: " + rel)
    print(f"METAS_RESULT {len(written)} {'missing' if args.dry_run else 'written'}, {len(skipped)} left for Unity")


main()
