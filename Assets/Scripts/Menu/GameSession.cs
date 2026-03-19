using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// DontDestroyOnLoad singleton that carries player slot assignments
    /// from LobbyScene into GameScene.
    ///
    /// Lifecycle: created by LobbyManager before loading GameScene.
    /// Destroyed by LobbyManager.Awake on re-entry to LobbyScene.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [System.Serializable]
        public struct SlotData
        {
            public bool   Filled;
            public int    DeviceId;           // InputDevice.deviceId (0 = unassigned)
            public string CharacterId;
            public int    CustomizationIndex;
        }

        public SlotData[] Slots { get; } = new SlotData[4];
        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (var s in Slots) if (s.Filled) n++;
                return n;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void DestroySelf() => Destroy(gameObject);
    }
}
