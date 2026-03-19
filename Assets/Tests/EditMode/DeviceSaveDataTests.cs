using NUnit.Framework;
using UnityEngine;
using VampireSurvivors.Menu;

namespace Tests.EditMode
{
    public class DeviceSaveDataTests
    {
        [Test]
        public void Key_UsesSerial_WhenNonEmpty()
        {
            var key = DeviceSaveData.BuildKey("Xbox Controller", "Microsoft", "SN-1234");
            Assert.AreEqual("SN-1234", key);
        }

        [Test]
        public void Key_UsesCombined_WhenSerialEmpty()
        {
            var key = DeviceSaveData.BuildKey("DualSense", "Sony", "");
            Assert.AreEqual("DualSense|Sony", key);
        }

        [Test]
        public void Key_UsesSlotFallback_WhenAllEmpty()
        {
            var key = DeviceSaveData.BuildKey("", "", "", slotFallback: 2);
            Assert.AreEqual("slot_2", key);
        }

        [Test]
        public void RoundTrip_SaveAndLoad()
        {
            const string key = "test_device_key";
            DeviceSaveData.Save(key, "antonio", 3);
            DeviceSaveData.Load(key, out var charId, out var customIdx);
            Assert.AreEqual("antonio", charId);
            Assert.AreEqual(3, customIdx);
            PlayerPrefs.DeleteKey(key); // cleanup
        }

        [Test]
        public void Load_ReturnsDefaults_WhenKeyMissing()
        {
            DeviceSaveData.Load("__nonexistent_key__", out var charId, out var customIdx);
            Assert.AreEqual("antonio", charId);
            Assert.AreEqual(0, customIdx);
        }
    }
}
