using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundRulesFingerprintTests
    {
        string items, source;
        WordboundPuzzleProof bound;

        [OneTimeSetUp]
        public void LoadActualContent()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string path = Path.Combine(dir.FullName, "Assets/_Project/Data/Config");
                    if (!File.Exists(Path.Combine(path, "items.json"))) continue;
                    items = File.ReadAllText(Path.Combine(path, "items.json"));
                    source = File.ReadAllText(Path.Combine(path, "rules.json"));
                    bound = WordboundPuzzleProof.Create(items, ruleBookJson: source);
                    return;
                }
            Assert.Fail("Actual repository items and rules are required.");
        }

        MatchBinding Binding(WordboundPuzzleProof proof) => new MatchBinding("rules-proof", "wordbound-v2",
            proof.Mode.Hash, proof.Course.SourceHash, proof.Course.Hash, new string('a', 64),
            proof.CatalogueHash, new[] { new WordboundSeat("a", 0) });

        [Test]
        public void BoundProofUsesTheExactDibsDerivedLauncherRulesAndDefensiveClones()
        {
            var expected = bound.Mode.CreateGameplayRules(RuleBook.FromJson(source).For("Dibs"));
            Assert.IsTrue(bound.BindsRules);
            Assert.AreEqual(WordboundRulesFingerprint.Compute(expected), bound.EffectiveRulesHash);
            Assert.AreEqual(WordboundIdentity.Sha256(source), bound.RuleSourceHash);
            Assert.DoesNotThrow(() => bound.Validate(Binding(bound), expected));
            var mutable = bound.Rules;
            mutable.Unarmed.Damage += 1;
            mutable.BleedOutSeconds[0] += 1;
            Assert.AreEqual(bound.EffectiveRulesHash, WordboundRulesFingerprint.Compute(bound.Rules));
            Assert.Throws<ArgumentException>(() => bound.Validate(Binding(bound), mutable));
        }

        [Test]
        public void SourceAndEffectiveChangesBothBindWithoutChangingTheCourse()
        {
            var whitespace = WordboundPuzzleProof.Create(items, ruleBookJson: source + "\n");
            Assert.AreEqual(bound.EffectiveRulesHash, whitespace.EffectiveRulesHash);
            Assert.AreNotEqual(bound.CatalogueHash, whitespace.CatalogueHash);
            var changed = WordboundPuzzleProof.Create(items,
                ruleBookJson: source.Replace("\"groundAccel\": 36", "\"groundAccel\": 35"));
            Assert.AreNotEqual(bound.EffectiveRulesHash, changed.EffectiveRulesHash);
            Assert.AreNotEqual(bound.CatalogueHash, changed.CatalogueHash);
            Assert.AreEqual(bound.Course.Hash, changed.Course.Hash);
            Assert.Throws<ArgumentException>(() => bound.Validate(Binding(changed), bound.Rules));
        }

        [Test]
        public void DibsSpecificOverridesAreAppliedBeforeWordboundModePolicy()
        {
            var changed = WordboundPuzzleProof.Create(items,
                ruleBookJson: source.Replace("\"Dibs\": {", "\"Dibs\": {\"groundAccel\":31,"));
            Assert.AreEqual(31, changed.Rules.GroundAccel);
            Assert.AreEqual("TimeRun", changed.Rules.Mode);
            Assert.IsFalse(changed.Rules.ClearOutEnabled);
            Assert.AreNotEqual(bound.EffectiveRulesHash, changed.EffectiveRulesHash);
        }

        [Test]
        public void HistoricalUnboundFactoryIsExplicitAndCannotReadImplicitRules()
        {
            var historical = WordboundPuzzleProof.Create(items);
            Assert.IsFalse(historical.BindsRules);
            Assert.AreEqual("", historical.RuleSourceHash);
            Assert.AreEqual("", historical.EffectiveRulesHash);
            Assert.AreNotEqual(historical.CatalogueHash, bound.CatalogueHash);
            Assert.Throws<InvalidOperationException>(() => { var ignored = historical.Rules; });
        }

        [Test]
        public void EveryPublicGameplayRuleAndNestedMeleeFieldAffectsTheFingerprint()
        {
            var rules = bound.Rules;
            string baseline = WordboundRulesFingerprint.Compute(rules);
            foreach (var field in typeof(GameRules).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(MeleeStats))
                {
                    foreach (var nested in typeof(MeleeStats).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var changed = rules.Clone();
                        nested.SetValue(changed.Unarmed, (float)nested.GetValue(changed.Unarmed) + .125f);
                        Assert.AreNotEqual(baseline, WordboundRulesFingerprint.Compute(changed), "Unarmed." + nested.Name);
                    }
                    continue;
                }
                var copy = rules.Clone();
                object value = field.GetValue(copy);
                if (value is float number) field.SetValue(copy, number + .125f);
                else if (value is int integer) field.SetValue(copy, integer + 1);
                else if (value is bool boolean) field.SetValue(copy, !boolean);
                else if (value is string text) field.SetValue(copy, text + "X");
                else if (value is float[] array)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        var changed = rules.Clone();
                        changed.BleedOutSeconds[i] += .125f;
                        Assert.AreNotEqual(baseline, WordboundRulesFingerprint.Compute(changed), field.Name + "." + i);
                    }
                    field.SetValue(copy, array.Concat(new[] { 1f }).ToArray());
                }
                else Assert.Fail("Add canonical coverage for new rule type: " + field.Name);
                Assert.AreNotEqual(baseline, WordboundRulesFingerprint.Compute(copy), field.Name);
            }
        }

        [Test]
        public void FingerprintIsCultureIndependentAndPreservesExactFloatBits()
        {
            var rules = bound.Rules;
            string baseline = WordboundRulesFingerprint.Compute(rules);
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.AreEqual(baseline, WordboundRulesFingerprint.Compute(rules));
                rules.ClearOutFirstAt = 0;
                string zero = WordboundRulesFingerprint.Compute(rules);
                rules.ClearOutFirstAt = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
                Assert.AreNotEqual(zero, WordboundRulesFingerprint.Compute(rules));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void InvalidUnboundedRulesAndDeepRuleSourcesAreRejected()
        {
            var rules = bound.Rules;
            rules.GroundAccel = float.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => WordboundRulesFingerprint.Compute(rules));
            rules = bound.Rules; rules.BleedOutSeconds = new float[65];
            Assert.Throws<ArgumentException>(() => WordboundRulesFingerprint.Compute(rules));
            rules = bound.Rules; rules.Unarmed = null;
            Assert.Throws<ArgumentException>(() => WordboundRulesFingerprint.Compute(rules));
            rules = bound.Rules; rules.Mode = new string('x', 257);
            Assert.Throws<ArgumentException>(() => WordboundRulesFingerprint.Compute(rules));
            Assert.Throws<ArgumentException>(() => WordboundPuzzleProof.Create(items,
                ruleBookJson: new string('[', 17) + new string(']', 17)));
            Assert.Throws<ArgumentException>(() => WordboundPuzzleProof.Create(items,
                ruleBookJson: new string(' ', CourseLimits.DocumentBytes + 1)));
            Assert.Throws<FormatException>(() => WordboundPuzzleProof.Create(items, ruleBookJson: "{\"defaults\":"));
        }
    }
}
