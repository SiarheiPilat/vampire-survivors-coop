using UnityEngine;
using UnityEngine.UI;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Settings panel: master/music/sfx volume sliders + Reset Progress button.
    /// Persisted in PlayerPrefs. Back button calls LobbyManager.OnSettingsClose.
    /// Focus order (tvOS): MasterVolume → MusicVolume → SFXVolume → ResetProgress → Back.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;

        [Header("Dev / Debug")]
        [SerializeField] Button resetProgressButton;  // optional; wire in Inspector

        const string KeyMaster = "vol_master";
        const string KeyMusic  = "vol_music";
        const string KeySFX    = "vol_sfx";

        void OnEnable()
        {
            if (masterSlider == null || musicSlider == null || sfxSlider == null)
            {
                Debug.LogError("[SettingsPanel] One or more slider fields not assigned in Inspector.");
                return;
            }

            masterSlider.value = PlayerPrefs.GetFloat(KeyMaster, 100f);
            musicSlider.value  = PlayerPrefs.GetFloat(KeyMusic,  100f);
            sfxSlider.value    = PlayerPrefs.GetFloat(KeySFX,    100f);

            masterSlider.onValueChanged.AddListener(OnMasterChanged);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);

            if (resetProgressButton != null)
                resetProgressButton.onClick.AddListener(OnResetProgress);
        }

        void OnDisable()
        {
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);

            if (resetProgressButton != null)
                resetProgressButton.onClick.RemoveListener(OnResetProgress);
        }

        void OnMasterChanged(float v) { PlayerPrefs.SetFloat(KeyMaster, v); PlayerPrefs.Save(); }
        void OnMusicChanged(float v)  { PlayerPrefs.SetFloat(KeyMusic,  v); PlayerPrefs.Save(); }
        void OnSFXChanged(float v)    { PlayerPrefs.SetFloat(KeySFX,    v); PlayerPrefs.Save(); }

        void OnResetProgress()
        {
            PersistentProgress.ResetAll();
            // Also clear the unlock toast tracking so characters show as re-locked
            PlayerPrefs.DeleteKey("unlocked_chars_v1");
            PlayerPrefs.Save();
            Debug.Log("[SettingsPanel] All persistent progress reset.");
        }
    }
}
