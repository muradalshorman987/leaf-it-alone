using FallenYard.UI;
using NUnit.Framework;

namespace FallenYard.Tests.UI
{
    public class HudFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1234, "1,234")]
        [TestCase(-5, "0")]
        public void Coins(int coins, string expected) => Assert.AreEqual(expected, HudFormat.Coins(coins));

        [TestCase(0f, "0%")]
        [TestCase(0.29f, "29%")]
        [TestCase(0.5f, "50%")]
        [TestCase(0.999f, "99%")]
        [TestCase(1f, "100%")]
        [TestCase(1.4f, "100%")]
        [TestCase(-0.2f, "0%")]
        public void CleanPercent_RoundsDownAndClamps(float fraction, string expected) =>
            Assert.AreEqual(expected, HudFormat.CleanPercent(fraction));

        [Test]
        public void CleanPercent_NaN_IsZero() => Assert.AreEqual("0%", HudFormat.CleanPercent(float.NaN));

        [TestCase("rake", "Rake")]
        [TestCase("leaf_blower", "Leaf Blower")]
        [TestCase("leaf-vacuum", "Leaf Vacuum")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void ToolName_PrettifiesId(string id, string expected) =>
            Assert.AreEqual(expected, HudFormat.ToolName(id));

        [Test]
        public void ToolName_PrefersLookup()
        {
            Assert.AreEqual("Bigger Bags", HudFormat.ToolName("bag", id => id == "bag" ? "Bigger Bags" : null));
            Assert.AreEqual("Rake", HudFormat.ToolName("rake", _ => null));
        }

        [Test]
        public void Level()
        {
            Assert.AreEqual("Lv 1 / 3", HudFormat.Level(1, 3));
            Assert.AreEqual("Lv 0", HudFormat.Level(0, 0));
        }

        [Test]
        public void PileBagged()
        {
            Assert.AreEqual("Bagged 1,200 leaves  +600 coins", HudFormat.PileBagged(1200, 600));
            Assert.AreEqual("Bagged 1 leaf", HudFormat.PileBagged(1, 0));
        }
    }
}
