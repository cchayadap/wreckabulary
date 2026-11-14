using System;
using System.Globalization;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Wordbound
{
    public static class CourseRandom
    {
        public const string Version = "gsk-pcg32-1";
        public static bool ValidSeed(string seed) => seed != null && seed.Length == 16 &&
            ulong.TryParse(seed, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _) &&
            seed == seed.ToLowerInvariant();
        public static Pcg32 Stream(string seed, string purpose, string scopeId, int lap = 0, int attempt = 0)
        {
            if (!ValidSeed(seed)) throw new ArgumentException("Use sixteen lowercase hexadecimal seed digits.", nameof(seed));
            if (!CourseValidator.ValidId(scopeId) || !CourseValidator.ValidId(purpose) || lap < 0 || lap > 3 ||
                attempt < 0 || attempt >= CourseLimits.Attempts) throw new ArgumentException("Invalid stream scope.");
            ulong runSeed = ulong.Parse(seed, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            ulong keyed = Hash64.Combine(runSeed, Hash64.Of("wreckabulary-course-generator-1"));
            return Pcg32.ForStream(keyed, purpose + "/" + scopeId + "/" + lap.ToString(CultureInfo.InvariantCulture), attempt);
        }
    }
}
