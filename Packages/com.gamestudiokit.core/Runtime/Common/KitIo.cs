// Game Studio Kit (MIT). Engine-free file, time and path helpers that follow CONTRACTS.md §1.1–§1.3:
// UTF-8 without BOM, LF, atomic tmp+rename writes, UTC second-precision timestamps, POSIX relative paths.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GameStudioKit.Common
{
    public static class KitIo
    {
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>Attempts at replacing the destination before the rename fallback, and the base pause between them
        /// (the pause grows linearly: 50, 100, 150, 200 ms).</summary>
        public const int AtomicWriteAttempts = 5;
        public const int AtomicWriteBackoffMs = 50;

        /// <summary>
        /// Writes text atomically: a sibling temp file, then a replace. LF line endings are the caller's job.
        /// A replace that fails with a sharing violation (antivirus, indexer, a viewer holding the file) is retried with
        /// backoff. The fallback renames the old file aside, moves the new one in, and only then deletes the old one, so a
        /// failure never loses both versions: on failure the old file is still in place (or restored) and the new
        /// content stays in the temp file, whose path the exception names.
        /// </summary>
        public static void WriteAllTextAtomic(string path, string text) =>
            WriteAllTextAtomic(path, text, AtomicWriteAttempts, AtomicWriteBackoffMs);

        /// <summary><see cref="WriteAllTextAtomic(string, string)"/> with explicit retry settings.</summary>
        public static void WriteAllTextAtomic(string path, string text, int attempts, int backoffMs)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string pid = System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
            string tmp = full + ".tmp-" + pid + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                File.WriteAllText(tmp, text, Utf8NoBom);
            }
            catch
            {
                TryDelete(tmp);
                throw;
            }

            Exception last = null;
            attempts = Math.Max(1, attempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    if (File.Exists(full)) File.Replace(tmp, full, null, true);
                    else File.Move(tmp, full);
                    return;
                }
                catch (IOException e) { last = e; }
                catch (UnauthorizedAccessException e) { last = e; }
                if (attempt + 1 < attempts) System.Threading.Thread.Sleep(Math.Max(0, backoffMs) * (attempt + 1));
            }

            // Fallback for file systems where File.Replace does not work (network shares, FAT): old aside, new in, old gone.
            string bak = full + ".bak-" + pid;
            bool hadOld = File.Exists(full);
            try
            {
                if (hadOld)
                {
                    TryDelete(bak);
                    File.Move(full, bak);
                }
                try
                {
                    File.Move(tmp, full);
                }
                catch
                {
                    if (hadOld && !File.Exists(full) && File.Exists(bak)) File.Move(bak, full);
                    throw;
                }
                if (hadOld) TryDelete(bak);
                return;
            }
            catch (IOException e) { last = e; }
            catch (UnauthorizedAccessException e) { last = e; }
            throw new IOException("could not replace " + full + " (" + (last?.Message ?? "unknown error") + "); the new content is kept in " + tmp, last);
        }

        static void TryDelete(string file)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Kind and id from a Builds/&lt;folder&gt; name (CONTRACTS §7.8): &lt;id&gt; is a release build, qa-&lt;id&gt; a
        /// Release-QA build, dev-&lt;id&gt; a development build. A development build is always "dev". Shared by the
        /// player (PerfProbe.BuildId) and the editor (packed_assets.json) so their build_id values join.
        /// </summary>
        public static void ParseBuildFolder(string folderName, bool development, out string kind, out string buildId)
        {
            string name = folderName ?? "";
            if (name.StartsWith("dev-", StringComparison.Ordinal)) { kind = "dev"; buildId = name.Substring(4); }
            else if (name.StartsWith("qa-", StringComparison.Ordinal)) { kind = "release-qa"; buildId = name.Substring(3); }
            else { kind = "release"; buildId = name; }
            if (development) kind = "dev";
            if (buildId.Length == 0) buildId = name.Length > 0 ? name : "unknown";
        }

        /// <summary>Atomic JSON write of any KitJson-serialisable value, ending with one LF.</summary>
        public static void WriteJsonAtomic(string path, object value)
        {
            WriteAllTextAtomic(path, KitJson.Serialize(value, true) + "\n");
        }

        /// <summary>Reads UTF-8 text with or without a BOM (File.ReadAllText detects BOMs).</summary>
        public static string ReadAllText(string path) => File.ReadAllText(path, Utf8NoBom);

        /// <summary>UTC, second precision: yyyy-MM-ddTHH:mm:ssZ (CONTRACTS §1.2).</summary>
        public static string UtcIso(DateTime utc) =>
            utc.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);

        public static string UtcNowIso() => UtcIso(DateTime.UtcNow);

        /// <summary>File-name stamp: yyyyMMddTHHmmssZ.</summary>
        public static string FileStamp(DateTime utc) =>
            utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        public static string ToPosix(string p) => p?.Replace('\\', '/');

        /// <summary>POSIX path of <paramref name="path"/> relative to <paramref name="root"/>, or null when it is outside.</summary>
        public static string RelativePosix(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path)) return null;
            string r = ToPosix(Path.GetFullPath(root)).TrimEnd('/') + "/";
            string p = ToPosix(Path.GetFullPath(path));
            if (!p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return null;
            return p.Substring(r.Length);
        }

        /// <summary>
        /// Finds the workspace root W from a QA output folder of the form W/Docs/qa/runs/&lt;run_id&gt; (the path the
        /// gate passes as -qa-out). Returns null when the folder is not inside a Docs/qa/runs tree.
        /// </summary>
        public static string WorkspaceFromRunDir(string runDir)
        {
            if (string.IsNullOrEmpty(runDir)) return null;
            string p = ToPosix(Path.GetFullPath(runDir));
            int i = p.LastIndexOf("/Docs/qa/runs/", StringComparison.OrdinalIgnoreCase);
            if (i <= 0) return null;
            return p.Substring(0, i);
        }

        /// <summary>Walks up from <paramref name="start"/> to find a folder that contains <paramref name="relFile"/>.</summary>
        public static string FindUpwards(string start, string relFile, int maxLevels = 6)
        {
            if (string.IsNullOrEmpty(start)) return null;
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            for (int i = 0; dir != null && i <= maxLevels; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, relFile);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        static readonly Regex NonSlug = new Regex("[^a-z0-9]+", RegexOptions.CultureInvariant);

        /// <summary>Lower-case a-z0-9 words joined by '-', at most <paramref name="max"/> characters.</summary>
        public static string Slug(string s, int max = 40)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            string slug = NonSlug.Replace(s.ToLowerInvariant(), "-").Trim('-');
            if (slug.Length > max) slug = slug.Substring(0, max).TrimEnd('-');
            return slug.Length == 0 ? "unknown" : slug;
        }

        static readonly Regex SafeName = new Regex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant);
        static readonly Regex RunIdPattern = new Regex("^[0-9]{8}T[0-9]{6}Z-[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant);

        /// <summary>True for names made only of A-Z a-z 0-9 _ . - (scenario names, checkpoint names, body ids).</summary>
        public static bool IsSafeName(string s) => !string.IsNullOrEmpty(s) && SafeName.IsMatch(s);

        /// <summary>checks.json run_id format: &lt;file stamp&gt;-&lt;scenario&gt;.</summary>
        public static bool IsRunId(string s) => !string.IsNullOrEmpty(s) && RunIdPattern.IsMatch(s);

        /// <summary>Replaces characters that are not safe in a file or run name with '_'.</summary>
        public static string SafeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unnamed";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' ? c : '_');
            return sb.ToString();
        }

        /// <summary>Lower-case hex SHA-256 of UTF-8 text.</summary>
        public static string Sha256Hex(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Utf8NoBom.GetBytes(text ?? string.Empty));
                var sb = new StringBuilder(64);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>16 lower-case hex digits: the checks.json state_hashes format.</summary>
        public static string Hex16(ulong v) => v.ToString("x16", CultureInfo.InvariantCulture);
    }
}
