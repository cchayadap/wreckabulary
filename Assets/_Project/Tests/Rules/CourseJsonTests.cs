using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public class CourseJsonTests
    {
        [TestCase("unknown-field"), TestCase("duplicate-field"), TestCase("missing-field"), TestCase("null")]
        [TestCase("integer-overflow"), TestCase("decimal"), TestCase("rounded-decimal"), TestCase("exponent")]
        [TestCase("nan"), TestCase("infinity"), TestCase("invalid-escape"), TestCase("surrogate")]
        [TestCase("url-id"), TestCase("unknown-version"), TestCase("trailing-comma"), TestCase("second-document")]
        public void StrictDocumentBoundaryRejectsMalformedData(string mutation)
        {
            string json = CourseJson.ExportDocument(CourseTemplates.FreestyleWorld());
            switch (mutation)
            {
                case "unknown-field": json = "{\"script\":\"run\"," + json.Substring(1); break;
                case "duplicate-field": json = "{\"schema\":1," + json.Substring(1); break;
                case "missing-field": json = json.Replace("\"schema\":1,", ""); break;
                case "null": json = json.Replace("\"schema\":1", "\"schema\":null"); break;
                case "integer-overflow": json = json.Replace("\"schema\":1", "\"schema\":2147483648"); break;
                case "decimal": json = json.Replace("\"schema\":1", "\"schema\":1.1"); break;
                case "rounded-decimal": json = json.Replace("\"schema\":1", "\"schema\":1.00000000000000001"); break;
                case "exponent": json = json.Replace("\"schema\":1", "\"schema\":1e0"); break;
                case "nan": json = json.Replace("\"schema\":1", "\"schema\":NaN"); break;
                case "infinity": json = json.Replace("\"schema\":1", "\"schema\":Infinity"); break;
                case "invalid-escape": json = json.Replace("Parcel Playroom", @"Parcel\qPlayroom"); break;
                case "surrogate": json = json.Replace("Parcel Playroom", @"Parcel\ud800"); break;
                case "url-id": json = json.Replace("\"id\":\"freestyle-proof\"", "\"id\":\"https://example.org/model\""); break;
                case "unknown-version": json = json.Replace("\"schema\":1", "\"schema\":2"); break;
                case "trailing-comma": json = json.Substring(0, json.Length - 1) + ",}"; break;
                case "second-document": json += "{}"; break;
            }
            var result = CourseJson.ImportDocument(json);
            Assert.IsFalse(result.Success, mutation); Assert.IsNull(result.Document); Assert.IsNotEmpty(result.Issues);
        }

        [Test]
        public void OversizeDeepAndRawInvalidUnicodeFailBeforeParsing()
        {
            Assert.IsFalse(CourseJson.ImportDocument(new string(' ', CourseLimits.DocumentBytes + 1)).Success);
            Assert.IsFalse(CourseJson.ImportDocument(new string('[', 25) + "0" + new string(']', 25)).Success);
            Assert.IsFalse(CourseJson.ImportDocument("\"\ud800\"").Success);
            Assert.IsFalse(CourseJson.ImportDocument(new string('é', CourseLimits.DocumentBytes / 2 + 1)).Success);
            Assert.IsFalse(CourseJson.ImportDocument(null).Success);
        }

        [TestCase("hash"), TestCase("route"), TestCase("metadata"), TestCase("position"), TestCase("ownership")]
        [TestCase("supply"), TestCase("version"), TestCase("unknown-enum"), TestCase("duplicate-lap"), TestCase("fixed-position")]
        public void EvenRehashedManifestsMustSatisfySemanticConstraints(string mutation)
        {
            bool freestyle = mutation == "fixed-position";
            var cat = CourseTemplates.ProofCatalogue();
            var result = CourseResolver.Resolve(freestyle ? CourseTemplates.FreestyleWorld() : CourseTemplates.RaceCircuit(), cat,
                new CourseResolveRequest("0000000000000000", CourseCoreTests.Play(freestyle ? "Freestyle" : "TeamRace")));
            Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
            string json = CourseJson.ExportManifest(result.Manifest);
            int start = json.IndexOf("\"placements\":[", StringComparison.Ordinal);
            int end = json.IndexOf("\"play\":", start, StringComparison.Ordinal);
            string placements = json.Substring(start, end - start);
            switch (mutation)
            {
                case "hash": json = json.Replace("\"hash\":\"" + result.Manifest.Hash, "\"hash\":\"" + new string('0', 64)); break;
                case "route": json = json.Replace("\"checkpointSteps\":[0,1,3]", "\"checkpointSteps\":[0,0,3]"); break;
                case "metadata": json = json.Substring(0, start) + placements.Replace("\"moduleId\":\"Side\"", "\"moduleId\":\"Missing\"") + json.Substring(end); break;
                case "position": json = json.Substring(0, start) + Regex.Replace(placements, "\"position\":\\[[^]]+\\]", "\"position\":[0,0,0]") + json.Substring(end); break;
                case "ownership": json = json.Substring(0, start) + placements.Replace("\"teamIndex\":1", "\"teamIndex\":0") + json.Substring(end); break;
                case "supply": json = json.Replace("\"renewable\":true", "\"renewable\":false"); break;
                case "version": json = json.Replace("\"generatorVersion\":1", "\"generatorVersion\":2"); break;
                case "unknown-enum": json = json.Replace("\"capability\":\"Circuit\"", "\"capability\":\"0\""); break;
                case "duplicate-lap": json = json.Replace("\"number\":2", "\"number\":1"); break;
                case "fixed-position": json = json.Substring(0, start) + placements.Replace("\"position\":[-5000,0,3000]", "\"position\":[-4500,0,3000]") + json.Substring(end); break;
            }
            Assert.AreNotEqual(CourseJson.ExportManifest(result.Manifest), json, "Mutation must alter the payload.");
            if (mutation != "hash") json = Rehash(json);
            var restored = CourseJson.ImportManifest(json, cat);
            Assert.IsFalse(restored.Success, mutation); Assert.IsNull(restored.Manifest); Assert.IsNotEmpty(restored.Issues);
        }

        [Test]
        public void CanonicalManifestHashAndUnicodeRoundTripAreStable()
        {
            var d = CourseTemplates.FreestyleWorld(); d.Title = "Toy café 🧸";
            var result = CourseResolver.Resolve(d, CourseTemplates.ProofCatalogue(), new CourseResolveRequest("0123456789abcdef", CourseCoreTests.Play("Freestyle")));
            Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
            string json = CourseJson.ExportManifest(result.Manifest);
            Assert.AreEqual(json, Rehash(json));
            Assert.AreEqual(d.Title, CourseJson.ImportDocument(CourseJson.ExportDocument(d)).Document.Title);
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", CourseJson.Sha256("abc"));
        }

        static string Rehash(string json)
        {
            string without = Regex.Replace(json, "\"hash\":\"[0-9a-f]{64}\",", "");
            string hash = CourseJson.Sha256(without);
            return Regex.Replace(json, "\"hash\":\"[0-9a-f]{64}\"", "\"hash\":\"" + hash + "\"");
        }
    }
}
