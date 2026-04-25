using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public static class GameSettingsStore
    {
        public static void EnsureDefaults()
        {
            if (PlayerPrefs.GetInt(MenuPrefsKeys.SettingsSaved, 0) == 1)
            {
                return;
            }

            PlayerPrefs.SetInt(MenuPrefsKeys.Graphics, 0);
            PlayerPrefs.SetInt(MenuPrefsKeys.ResolutionIndex, 0);
            PlayerPrefs.SetFloat(MenuPrefsKeys.MasterVolume, 1f);
            PlayerPrefs.SetInt(MenuPrefsKeys.VSync, QualitySettings.vSyncCount > 0 ? 1 : 0);
            PlayerPrefs.SetInt(MenuPrefsKeys.FullscreenMode, (int)Screen.fullScreenMode);
            PlayerPrefs.SetFloat(MenuPrefsKeys.UiVolume, 1f);
            PlayerPrefs.SetFloat(MenuPrefsKeys.SfxVolume, 1f);
            PlayerPrefs.SetFloat(MenuPrefsKeys.MusicVolume, 1f);
            PlayerPrefs.SetFloat(MenuPrefsKeys.MouseSensitivity, 2f);
            PlayerPrefs.SetInt(MenuPrefsKeys.InvertMouse, 0);
            PlayerPrefs.SetInt(MenuPrefsKeys.MouseSmoothing, 0);
            PlayerPrefs.SetInt(MenuPrefsKeys.SettingsSaved, 1);
            PlayerPrefs.Save();
        }

        public static float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
        public static int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
