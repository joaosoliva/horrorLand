using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class AudioSettingsController : MonoBehaviour
    {
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider uiVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;

        public float UiVolume => GameSettingsStore.GetFloat(MenuPrefsKeys.UiVolume, 1f);
        public float SfxVolume => GameSettingsStore.GetFloat(MenuPrefsKeys.SfxVolume, 1f);
        public float MusicVolume => GameSettingsStore.GetFloat(MenuPrefsKeys.MusicVolume, 1f);

        private void Awake()
        {
            masterVolumeSlider.onValueChanged.AddListener(ApplyMasterVolume);
            uiVolumeSlider.onValueChanged.AddListener(ApplyUiVolume);
            sfxVolumeSlider.onValueChanged.AddListener(ApplySfxVolume);
            musicVolumeSlider.onValueChanged.AddListener(ApplyMusicVolume);
        }

        private void OnDestroy()
        {
            masterVolumeSlider.onValueChanged.RemoveListener(ApplyMasterVolume);
            uiVolumeSlider.onValueChanged.RemoveListener(ApplyUiVolume);
            sfxVolumeSlider.onValueChanged.RemoveListener(ApplySfxVolume);
            musicVolumeSlider.onValueChanged.RemoveListener(ApplyMusicVolume);
        }

        public void RefreshFromSavedSettings()
        {
            GameSettingsStore.EnsureDefaults();

            float master = GameSettingsStore.GetFloat(MenuPrefsKeys.MasterVolume, 1f);
            float ui = GameSettingsStore.GetFloat(MenuPrefsKeys.UiVolume, 1f);
            float sfx = GameSettingsStore.GetFloat(MenuPrefsKeys.SfxVolume, 1f);
            float music = GameSettingsStore.GetFloat(MenuPrefsKeys.MusicVolume, 1f);

            masterVolumeSlider.SetValueWithoutNotify(master);
            uiVolumeSlider.SetValueWithoutNotify(ui);
            sfxVolumeSlider.SetValueWithoutNotify(sfx);
            musicVolumeSlider.SetValueWithoutNotify(music);

            ApplyMasterVolume(master);
        }

        private void ApplyMasterVolume(float value)
        {
            AudioListener.volume = value;
            GameSettingsStore.SetFloat(MenuPrefsKeys.MasterVolume, value);
        }

        private void ApplyUiVolume(float value)
        {
            GameSettingsStore.SetFloat(MenuPrefsKeys.UiVolume, value);
        }

        private void ApplySfxVolume(float value)
        {
            GameSettingsStore.SetFloat(MenuPrefsKeys.SfxVolume, value);
        }

        private void ApplyMusicVolume(float value)
        {
            GameSettingsStore.SetFloat(MenuPrefsKeys.MusicVolume, value);
        }
    }
}
