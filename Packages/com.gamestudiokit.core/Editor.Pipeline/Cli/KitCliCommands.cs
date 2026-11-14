// Game Studio Kit: the third exec door, a Pipeline command (`unity command kit_exec -- --method X --args '{}' --out P`).
// Compiled only when com.unity.pipeline is present: the GameStudioKit.Editor.Pipeline asmdef defines GSK_PIPELINE through
// versionDefines and requires it in defineConstraints, and this file is guarded too, so removing the experimental
// package never breaks the project's compile (research_unity_mcp.md §5.2). The gate itself calls KitCli.Run through
// `unity command eval` and does not need this command.
#if GSK_PIPELINE
using Unity.Pipeline.Commands;

namespace GameStudioKit.Editor.Pipeline
{
    static class KitCliCommands
    {
        [CliCommand("kit_exec", "Game Studio Kit: run a KitCli entry point (Namespace.Class.Method) with JSON args and write its result envelope", MainThreadRequired = true, Tags = new[] { "gsk" })]
        public static string KitExec(
            [CliArg("method", "Fully qualified static entry point, e.g. GameStudioKit.Editor.Anim.AvatarProbe.Run", Required = true)] string method,
            [CliArg("args", "JSON arguments (default {})")] string args = "{}",
            [CliArg("out", "Result file path (default Logs/exec/<utc>.json)")] string @out = "")
        {
            return global::GameStudioKit.Editor.KitCli.Run(method, args, @out);
        }
    }
}
#endif
