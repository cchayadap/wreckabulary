// Game Studio Kit (MIT). Writes <run dir>/checks.json atomically (tmp file + rename, CONTRACTS §3.6), so the gate and
// probe_report.mjs never read a half-written document. Engine-free; ChecksDocument.WriteTo does the atomic write.
using System.IO;

namespace GameStudioKit.Qa
{
    public static class ChecksWriter
    {
        public const string FileName = "checks.json";

        /// <summary>Writes <paramref name="doc"/> to &lt;runDir&gt;/checks.json and returns the path.</summary>
        public static string Write(string runDir, ChecksDocument doc)
        {
            if (string.IsNullOrEmpty(runDir)) throw new IOException("checks.json needs a run folder (-qa-out)");
            if (doc == null) throw new System.ArgumentNullException(nameof(doc));
            Directory.CreateDirectory(runDir);
            string path = Path.Combine(runDir, FileName);
            doc.WriteTo(path);
            return path;
        }
    }
}
