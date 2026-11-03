using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    internal static class WordboundTestItems
    {
        public static ItemCatalogue Raw()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Assert.IsTrue(data && data.items, "The installed Wordbound item catalogue must exist.");
            return ItemCatalogue.FromJson(data.items.text, data.items.name);
        }
    }
}
