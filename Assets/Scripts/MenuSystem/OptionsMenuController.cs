using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class OptionsMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private DisplaySettingsController displaySettings;
        [SerializeField] private AudioSettingsController audioSettings;
        [SerializeField] private InputSettingsController inputSettings;

        private void Awake()
        {
            GameSettingsStore.EnsureDefaults();
            closeButton.onClick.AddListener(Close);
            rootPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            rootPanel.SetActive(true);
            displaySettings.RefreshFromSavedSettings();
            audioSettings.RefreshFromSavedSettings();
            inputSettings.RefreshFromSavedSettings();
        }

        public void Close()
        {
            rootPanel.SetActive(false);
        }
    }
}
