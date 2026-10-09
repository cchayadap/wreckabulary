#!/usr/bin/env python3
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

HOST = r'''
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Wreckabulary.Rules;
namespace UnityEngine { public static class Application {
    public static string persistentDataPath => throw new InvalidOperationException("Tests must inject a directory; Unity paths are not emulated.");
} }
namespace NUnit.Framework {
    [AttributeUsage(AttributeTargets.Method)] public sealed class TearDownAttribute : Attribute { }
    public static class StringAssert {
        public static void Contains(string expected, string actual) {
            if (actual == null || !actual.Contains(expected)) throw new AssertionException("Expected substring: " + expected);
        }
    }
}
namespace Wreckabulary { public sealed class GameConfig {
    public static GameConfig Current = new GameConfig();
    public readonly ItemCatalogue Items;
    public readonly Dictionary<string,HouseLayout> Houses;
    public static void Use(object ignored) { }
    public HouseLayout HouseFor(string map) => Houses[map];
    GameConfig() {
        var config = Path.Combine(Environment.GetEnvironmentVariable("HOME_TEST_REPO"),"Assets/_Project/Data/Config");
        Items = ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(config,"items.json")));
        Houses = new Dictionary<string,HouseLayout> {
            ["pinwheel"] = HouseLayout.FromJson(File.ReadAllText(Path.Combine(config,"house_pinwheel.json"))),
            ["courtyard"] = HouseLayout.FromJson(File.ReadAllText(Path.Combine(config,"house_courtyard.json"))) };
    }
} }
public static class StorageTestHost {
    public static int Main() {
        int passed = 0, failed = 0;
        var type = typeof(Wreckabulary.Tests.HomeStorageTests);
        foreach (var method in type.GetMethods().Where(m => m.IsDefined(typeof(TestAttribute)) || m.IsDefined(typeof(TestCaseAttribute)))) {
            var cases = method.GetCustomAttributes<TestCaseAttribute>().Select(c => c.Arguments).ToList();
            if (cases.Count == 0) cases.Add(Array.Empty<object>());
            foreach (var arguments in cases) {
                var fixture = Activator.CreateInstance(type);
                try {
                    foreach (var setup in type.GetMethods().Where(m => m.IsDefined(typeof(SetUpAttribute)))) setup.Invoke(fixture,null);
                    method.Invoke(fixture,arguments); passed++;
                    Console.WriteLine("PASS " + method.Name + "(" + string.Join(",",arguments) + ")");
                } catch (Exception e) {
                    failed++; var error = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                    Console.WriteLine("FAIL " + method.Name + ": " + error);
                } finally {
                    foreach (var teardown in type.GetMethods().Where(m => m.IsDefined(typeof(TearDownAttribute)))) teardown.Invoke(fixture,null);
                }
            }
        }
        Console.WriteLine("HOME_STORAGE_TESTS passed="+passed+" failed="+failed+" UnityExecution=NOT-RUN");
        return passed > 0 && failed == 0 ? 0 : 1;
    }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", type=Path, default=Path("/workspace/.cloud-setup/dotnet-root/usr/share/dotnet/dotnet"))
    parser.add_argument("--output", type=Path, default=Path("/tmp/wreckabulary-storage-checks"))
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    args.output.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="storage-", dir=args.output))
    (work / "Host.cs").write_text(HOST)
    root = ET.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
    props = ET.SubElement(root, "PropertyGroup")
    for key, value in {"OutputType": "Exe", "TargetFramework": "net9.0", "LangVersion": "9.0",
                       "EnableDefaultCompileItems": "false", "Nullable": "disable"}.items():
        ET.SubElement(props, key).text = value
    group = ET.SubElement(root, "ItemGroup")
    for path in [work / "Host.cs", repo / "Tools/RulesHarness/NUnitShim.cs",
                 repo / "Assets/_Project/Scripts/Core/HomeStorage.cs",
                 repo / "Assets/_Project/Tests/Editor/HomeStorageTests.cs"]:
        ET.SubElement(group, "Compile", {"Include": str(path)})
    ET.SubElement(group, "ProjectReference", {"Include": str(repo / "Tools/RulesHarness/Rules/Rules.csproj")})
    ET.indent(root)
    ET.ElementTree(root).write(work / "Host.csproj", encoding="unicode")
    variables = os.environ.copy()
    variables.update(DOTNET_ROOT=str(args.dotnet.parent), DOTNET_NOLOGO="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", HOME_TEST_REPO=str(repo))
    result = subprocess.run([str(args.dotnet), "run", "--project", str(work / "Host.csproj")], env=variables,
                            text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (work / "result.log").write_text(result.stdout)
    print(result.stdout, end="")
    print("Current filesystem-only evidence: " + str(work))
    raise SystemExit(result.returncode)


if __name__ == "__main__":
    main()
