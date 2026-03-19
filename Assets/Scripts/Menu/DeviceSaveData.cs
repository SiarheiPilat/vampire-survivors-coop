using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Builds PlayerPrefs keys from InputDevice.description fields and
    /// saves/loads character+customization selections per device.
    /// </summary>
    public static class DeviceSaveData
    {
        private static readonly string[] ValidCharacterIds =
            { "antonio", "imelda", "pasqualina", "gennaro" };

        /// <summary>
        /// Build a stable PlayerPrefs key for a device.
        /// Priority: serial > product|manufacturer > slot_N
        /// </summary>
        public static string BuildKey(string product, string manufacturer,
                                      string serial, int slotFallback = 0)
        {
            if (!string.IsNullOrEmpty(serial))
                return serial;
            var combined = $"{product}|{manufacturer}";
            if (combined.Length > 1) // at least one non-empty
                return combined;
            return $"slot_{slotFallback}";
        }

        public static void Save(string key, string characterId, int customizationIndex)
        {
            var json = JsonUtility.ToJson(new SavePayload
            {
                characterId        = characterId,
                customizationIndex = customizationIndex,
            });
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public static void Load(string key, out string characterId, out int customizationIndex)
        {
            characterId        = ValidCharacterIds[0];
            customizationIndex = 0;

            var json = PlayerPrefs.GetString(key, null);
            if (string.IsNullOrEmpty(json)) return;

            var payload = JsonUtility.FromJson<SavePayload>(json);
            if (payload == null) return;

            characterId        = payload.characterId;
            customizationIndex = payload.customizationIndex;
        }

        [System.Serializable]
        private class SavePayload
        {
            public string characterId;
            public int    customizationIndex;
        }
    }
}
