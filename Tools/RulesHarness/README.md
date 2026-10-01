# Rules harness

Runs the engine-free rules tests without Unity. The rules code
(`Assets/_Project/Scripts/Rules`) has no Unity references, so plain .NET can build it the same way
Unity does: netstandard2.1 and C# 9.

```
dotnet run --project Tools/RulesHarness
dotnet run --project Tools/RulesHarness -- Economy
```

The optional argument runs only the tests whose name contains it. The last line reads
`RULES_HARNESS passed=N failed=M`, and the exit code is 0 only when every test passed.

- `Rules/Rules.csproj` compiles the rules sources in place. Warnings are errors.
- `RulesHarness.csproj` compiles the tests in `Assets/_Project/Tests/Rules` against
  `NUnitShim.cs`, a stand-in for the part of NUnit they use, so no NuGet packages are needed.
- In Unity, the same test files run in the Test Runner as EditMode tests
  (`Wreckabulary.Rules.Tests`) with the real NUnit.

Keep the tests to the NUnit subset in `NUnitShim.cs` (classic `Assert.AreEqual`-style asserts,
`[Test]`, `[TestCase]`, `[SetUp]`), or add what you need to the shim.
