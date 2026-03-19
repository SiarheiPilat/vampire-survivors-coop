using NUnit.Framework;
using VampireSurvivors.Menu;

namespace Tests.EditMode
{
    public class GameSessionTests
    {
        [Test]
        public void SlotCount_StartsAtZero()
        {
            var slots = new GameSession.SlotData[4];
            int count = 0;
            foreach (var s in slots)
                if (s.Filled) count++;
            Assert.AreEqual(0, count);
        }

        [Test]
        public void SlotData_StoresCharacterAndCustomization()
        {
            var slot = new GameSession.SlotData
            {
                Filled             = true,
                CharacterId        = "imelda",
                CustomizationIndex = 2,
                DeviceId           = 42,
            };
            Assert.IsTrue(slot.Filled);
            Assert.AreEqual("imelda", slot.CharacterId);
            Assert.AreEqual(2, slot.CustomizationIndex);
            Assert.AreEqual(42, slot.DeviceId);
        }
    }
}
