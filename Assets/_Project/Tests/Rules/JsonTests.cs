using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class JsonTests
    {
        [Test]
        public void ReadsEveryKindOfValue()
        {
            var n = Json.Parse("{\"s\": \"a\\\"b\\u00e9\\n\", \"i\": -12, \"f\": 1.5e2, \"t\": true, \"no\": false, \"z\": null, \"list\": [1, 2, 3], \"o\": {\"k\": \"v\"}}");
            Assert.AreEqual("a\"bé\n", n["s"].String());
            Assert.AreEqual(-12, n["i"].Int());
            Assert.AreEqual(150f, n["f"].Float());
            Assert.IsTrue(n["t"].Bool());
            Assert.IsFalse(n["no"].Bool());
            Assert.IsTrue(n["z"].IsNull);
            Assert.IsFalse(n.Has("z"), "a null value counts as missing");
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f }, n["list"].Floats(3));
            Assert.AreEqual("v", n["o"]["k"].String());
            CollectionAssert.AreEqual(new[] { "s", "i", "f", "t", "no", "z", "list", "o" }, n.Keys.ToList(), "keys keep file order");
        }

        [Test]
        public void MissingValuesUseTheFallback()
        {
            var n = Json.Parse("{}");
            Assert.AreEqual(7, n["x"].Int(7));
            Assert.AreEqual("d", n["x"].String("d"));
            Assert.AreEqual(0, n["x"].Items.Count);
            Assert.Throws<FormatException>(() => n["x"].Int());
        }

        [TestCase("{\"a\": 1,}")]
        [TestCase("{\"a\": 1 \"b\": 2}")]
        [TestCase("{\"a\": 1, \"a\": 2}")]
        [TestCase("[1, 2")]
        [TestCase("{\"a\": tru}")]
        [TestCase("{} extra")]
        [TestCase("\"unterminated")]
        public void MalformedJsonIsAnError(string text)
        {
            Assert.Throws<FormatException>(() => Json.Parse(text, "bad.json"));
        }

        [Test]
        public void ErrorsGiveFileLineAndColumn()
        {
            var e = Assert.Throws<FormatException>(() => Json.Parse("{\n  \"a\": 1,\n  \"b\": ?\n}", "cfg.json"));
            Assert.IsTrue(e.Message.StartsWith("cfg.json:3:8:"), e.Message);
        }

        [Test]
        public void WrongTypesNameThePath()
        {
            var n = Json.Parse("{\"items\": [{\"id\": 5}]}", "items.json");
            var e = Assert.Throws<FormatException>(() => n["items"].Items[0]["id"].String());
            Assert.IsTrue(e.Message.Contains("items.json") && e.Message.Contains("$.items[0].id"), e.Message);
            Assert.Throws<FormatException>(() => Json.Parse("{\"x\": 1.5}")["x"].Int());
        }

        [Test]
        public void NumbersIgnoreTheMachinesCulture()
        {
            var saved = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.AreEqual(0.25f, Json.Parse("{\"x\": 0.25}")["x"].Float());
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = saved;
            }
        }
    }
}
