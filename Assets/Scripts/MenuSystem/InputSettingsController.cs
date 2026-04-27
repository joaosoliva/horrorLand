using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class InputSettingsController : MonoBehaviour
    {
        public static event Action<float, bool, bool> LookSettingsChanged;

        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Toggle invertMouseToggle;
        [SerializeField] private Toggle mouseSmoothingToggle;
        [SerializeField] private Button resetScareChoiceButton;

        private void Awake()
        {
            mouseSensitivitySlider.onValueChanged.AddListener(ApplySensitivity);
            invertMouseToggle.onValueChanged.AddListener(ApplyInvertMouse);
            mouseSmoothingToggle.onValueChanged.AddListener(ApplyMouseSmoothing);
            resetScareChoiceButton.onClick.AddListener(ResetScareChoice);
        }

        private void OnDestroy()
        {
            mouseSensitivitySlider.onValueChanged.RemoveListener(ApplySensitivity);
            invertMouseToggle.onValueChanged.RemoveListener(ApplyInvertMouse);
            mouseSmoothingToggle.onValueChanged.RemoveListener(ApplyMouseSmoothing);
            resetScareChoiceButton.onClick.RemoveListener(ResetScareChoice);
        }

        public void RefreshFromSavedSettings()
        {
            GameSettingsStore.EnsureDefaults();

            float sensitivity = GameSettingsStore.GetFloat(MenuPrefsKeys.MouseSensitivity, 2f);
            bool invertMouse = GameSettingsStore.GetInt(MenuPrefsKeys.InvertMouse, 0) == 1;
            bool smoothing = GameSettingsStore.GetInt(MenuPrefsKeys.MouseSmoothing, 0) == 1;

            mouseSensitivitySlider.SetValueWithoutNotify(sensitivity);
            invertMouseToggle.SetIsOnWithoutNotify(invertMouse);
            mouseSmoothingToggle.SetIsOnWithoutNotify(smoothing);

            NotifyLookSettingsChanged();
        }

        private void ApplySensitivity(float value)
        {
            GameSettingsStore.SetFloat(MenuPrefsKeys.MouseSensitivity, value);
            NotifyLookSettingsChanged();
        }

        private void ApplyInvertMouse(bool isEnabled)
        {
            GameSettingsStore.SetInt(MenuPrefsKeys.InvertMouse, isEnabled ? 1 : 0);
            NotifyLookSettingsChanged();
        }

        private void ApplyMouseSmoothing(bool isEnabled)
        {
            GameSettingsStore.SetInt(MenuPrefsKeys.MouseSmoothing, isEnabled ? 1 : 0);
            NotifyLookSettingsChanged();
        }

        private void ResetScareChoice()
        {
            PlayerPrefs.DeleteKey(MenuPrefsKeys.ConsentAnswered);
            PlayerPrefs.DeleteKey(MenuPrefsKeys.RandomScaresEnabled);
            PlayerPrefs.Save();
            Debug.Log("Scare consent reset. The prompt will appear on next launch.");
        }

        private void NotifyLookSettingsChanged()
        {
            LookSettingsChanged?.Invoke(
                GameSettingsStore.GetFloat(MenuPrefsKeys.MouseSensitivity, 2f),
                GameSettingsStore.GetInt(MenuPrefsKeys.InvertMouse, 0) == 1,
                GameSettingsStore.GetInt(MenuPrefsKeys.MouseSmoothing, 0) == 1);
        }
    }
}
