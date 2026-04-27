using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class DisplaySettingsController : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown fullscreenDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private TMP_Dropdown qualityDropdown;

        private readonly List<Resolution> uniqueResolutions = new List<Resolution>();
        private readonly List<string> fullscreenOptions = new List<string>
        {
            "Exclusive Fullscreen",
            "Fullscreen Window",
            "Maximized Window",
            "Windowed"
        };

        private void Awake()
        {
            PopulateResolutionDropdown();
            PopulateFullscreenDropdown();
            PopulateQualityDropdown();

            resolutionDropdown.onValueChanged.AddListener(ApplyResolutionByIndex);
            fullscreenDropdown.onValueChanged.AddListener(ApplyFullscreenModeByIndex);
            vSyncToggle.onValueChanged.AddListener(ApplyVSync);
            qualityDropdown.onValueChanged.AddListener(ApplyQuality);
        }

        private void OnDestroy()
        {
            resolutionDropdown.onValueChanged.RemoveListener(ApplyResolutionByIndex);
            fullscreenDropdown.onValueChanged.RemoveListener(ApplyFullscreenModeByIndex);
            vSyncToggle.onValueChanged.RemoveListener(ApplyVSync);
            qualityDropdown.onValueChanged.RemoveListener(ApplyQuality);
        }

        public void RefreshFromSavedSettings()
        {
            GameSettingsStore.EnsureDefaults();

            int resolutionIndex = Mathf.Clamp(GameSettingsStore.GetInt(MenuPrefsKeys.ResolutionIndex, uniqueResolutions.Count - 1), 0, Mathf.Max(0, uniqueResolutions.Count - 1));
            int fullscreenIndex = Mathf.Clamp(GameSettingsStore.GetInt(MenuPrefsKeys.FullscreenMode, (int)Screen.fullScreenMode), 0, fullscreenOptions.Count - 1);
            int vSync = GameSettingsStore.GetInt(MenuPrefsKeys.VSync, 0);
            int quality = Mathf.Clamp(GameSettingsStore.GetInt(MenuPrefsKeys.Graphics, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);

            resolutionDropdown.SetValueWithoutNotify(resolutionIndex);
            fullscreenDropdown.SetValueWithoutNotify(fullscreenIndex);
            vSyncToggle.SetIsOnWithoutNotify(vSync == 1);
            qualityDropdown.SetValueWithoutNotify(quality);

            ApplyResolutionByIndex(resolutionIndex);
            ApplyFullscreenModeByIndex(fullscreenIndex);
            ApplyVSync(vSync == 1);
            ApplyQuality(quality);
        }

        private void PopulateResolutionDropdown()
        {
            uniqueResolutions.Clear();
            resolutionDropdown.ClearOptions();

            Resolution[] resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution candidate = resolutions[i];
                bool alreadyAdded = false;
                for (int j = 0; j < uniqueResolutions.Count; j++)
                {
                    Resolution existing = uniqueResolutions[j];
                    if (existing.width == candidate.width && existing.height == candidate.height)
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    uniqueResolutions.Add(candidate);
                }
            }

            if (uniqueResolutions.Count == 0)
            {
                uniqueResolutions.Add(new Resolution { width = Screen.currentResolution.width, height = Screen.currentResolution.height, refreshRateRatio = Screen.currentResolution.refreshRateRatio });
            }

            var options = new List<string>(uniqueResolutions.Count);
            for (int i = 0; i < uniqueResolutions.Count; i++)
            {
                Resolution resolution = uniqueResolutions[i];
                options.Add($"{resolution.width} x {resolution.height}");
            }

            resolutionDropdown.AddOptions(options);
        }

        private void PopulateFullscreenDropdown()
        {
            fullscreenDropdown.ClearOptions();
            fullscreenDropdown.AddOptions(fullscreenOptions);
        }

        private void PopulateQualityDropdown()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        }

        private void ApplyResolutionByIndex(int index)
        {
            if (uniqueResolutions.Count == 0)
            {
                return;
            }

            int clamped = Mathf.Clamp(index, 0, uniqueResolutions.Count - 1);
            Resolution resolution = uniqueResolutions[clamped];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
            GameSettingsStore.SetInt(MenuPrefsKeys.ResolutionIndex, clamped);
        }

        private void ApplyFullscreenModeByIndex(int index)
        {
            int clamped = Mathf.Clamp(index, 0, fullscreenOptions.Count - 1);
            Screen.fullScreenMode = (FullScreenMode)clamped;
            GameSettingsStore.SetInt(MenuPrefsKeys.FullscreenMode, clamped);
        }

        private void ApplyVSync(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            GameSettingsStore.SetInt(MenuPrefsKeys.VSync, enabled ? 1 : 0);
        }

        private void ApplyQuality(int qualityIndex)
        {
            int clamped = Mathf.Clamp(qualityIndex, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(clamped);
            GameSettingsStore.SetInt(MenuPrefsKeys.Graphics, clamped);
        }
    }
}
