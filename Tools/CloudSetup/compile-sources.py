#!/usr/bin/env python3
"""Compile separate Unity assemblies without executing the Unity editor.

This checks C# and assembly boundaries only. Template-cache package DLLs may differ
from the project's pinned packages; real Unity import/tests/builds are still required.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET


def run(repo, editor, dotnet, output):
    data = editor.parent / "Data"
    engine = list((data / "Managed/UnityEngine").glob("UnityEngine*.dll"))
    editor_refs = list((data / "Managed/UnityEngine").glob("UnityEditor*.dll"))
    templates = sorted((data / "Resources/PackageManager/ProjectTemplates/libcache").glob("*/ScriptAssemblies"))
    nunit = data / "Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll"
    if not engine or not nunit.is_file():
        raise RuntimeError("An installed official Unity editor and its NUnit package are required")
    definitions = {json.loads(p.read_text())["name"]: (p, json.loads(p.read_text()))
                   for p in (repo / "Assets/_Project").rglob("*.asmdef")}
    order = ["Wreckabulary.Rules", "Wreckabulary", "Wreckabulary.Editor",
             "Wreckabulary.Rules.Tests", "Wreckabulary.Tests", "Wreckabulary.Editor.Tests"]
    output.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="csharp-", dir=output))
    packages = {}
    for name in order:
        path, spec = definitions[name]
        folder = work / name
        folder.mkdir()
        project = ET.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
        properties = ET.SubElement(project, "PropertyGroup")
        for key, value in {"TargetFramework": "netstandard2.1", "EnableDefaultCompileItems": "false",
                           "Nullable": "disable", "LangVersion": "9.0", "NoWarn": "1701;1702",
                           "AssemblyName": name, "DefineConstants": "UNITY_INCLUDE_TESTS;UNITY_EDITOR"}.items():
            ET.SubElement(properties, key).text = value
        group = ET.SubElement(project, "ItemGroup")
        for source in sorted(path.parent.rglob("*.cs")):
            closest = max((p for p, _ in definitions.values() if p.parent in source.parents),
                          key=lambda p: len(p.parts))
            if closest == path:
                ET.SubElement(group, "Compile", {"Include": str(source)})

        def reference(dll):
            node = ET.SubElement(group, "Reference", {"Include": dll.stem})
            ET.SubElement(node, "HintPath").text = str(dll)

        if not spec.get("noEngineReferences"):
            for dll in engine + editor_refs:
                reference(dll)
        for assembly in spec.get("references", []):
            if assembly in definitions:
                ET.SubElement(group, "ProjectReference", {"Include": str(work / assembly / (assembly + ".csproj"))})
            else:
                candidates = [repo / "Library/ScriptAssemblies" / (assembly + ".dll")]
                candidates.extend(p / (assembly + ".dll") for p in templates)
                dll = next((p for p in candidates if p.is_file()), None)
                if not dll:
                    raise RuntimeError("Missing actual package assembly: " + assembly)
                packages[assembly] = str(dll)
                reference(dll)
        for precompiled in spec.get("precompiledReferences", []):
            if precompiled != "nunit.framework.dll":
                raise RuntimeError("Unsupported precompiled reference: " + precompiled)
            reference(nunit)
        ET.indent(project)
        ET.ElementTree(project).write(folder / (name + ".csproj"), encoding="unicode")
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(dotnet.parent), DOTNET_NOLOGO="1", DOTNET_CLI_TELEMETRY_OPTOUT="1")
    results = []
    for name in order:
        command = [str(dotnet), "build", str(work / name / (name + ".csproj")), "--nologo"]
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, env=env, text=True)
        (work / (name + ".log")).write_text(result.stdout)
        entry = {"assembly": name, "exitCode": result.returncode, "summary": result.stdout.splitlines()[-5:]}
        results.append(entry)
        print(json.dumps(entry), flush=True)
        if result.returncode:
            break
    (work / "result.json").write_text(json.dumps({"kind": "source compilation only", "results": results,
        "packageAssemblies": packages, "unityExecution": "NOT RUN"}, indent=2) + "\n")
    print("Current compilation evidence: " + str(work))
    return results[-1]["exitCode"]


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--editor", type=Path, default=Path("/workspace/.cloud-setup/Unity6000.6.3f1/Editor/Unity"))
    parser.add_argument("--dotnet", type=Path, default=Path("/workspace/.cloud-setup/dotnet-root/usr/share/dotnet/dotnet"))
    parser.add_argument("--output", type=Path, default=Path("/tmp/wreckabulary-source-checks"))
    args = parser.parse_args()
    raise SystemExit(run(args.repo.resolve(), args.editor.resolve(), args.dotnet.resolve(), args.output.resolve()))
