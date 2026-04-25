using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button unlocksButton;
        [SerializeField] private Button configButton;

        [Header("Dependencies")]
        [SerializeField] private MenuSceneLoader sceneLoader;
        [SerializeField] private RandomScareController randomScareController;
        [SerializeField] private MenuPanelController unlocksPanel;
        [SerializeField] private MenuPanelController configPanel;

        private void Awake()
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
            unlocksButton.onClick.AddListener(OnUnlocksClicked);
            configButton.onClick.AddListener(OnConfigClicked);
        }

        private void OnDestroy()
        {
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
            unlocksButton.onClick.RemoveListener(OnUnlocksClicked);
            configButton.onClick.RemoveListener(OnConfigClicked);
        }

        public void Initialize(bool randomScaresEnabled)
        {
            randomScareController.SetEnabledState(randomScaresEnabled);

            unlocksPanel?.SetVisible(false);
            configPanel?.SetVisible(false);
            SetVisible(true);
        }

        public void SetVisible(bool isVisible)
        {
            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(isVisible);
            }
        }

        private void OnStartGameClicked()
        {
            randomScareController.TryTriggerScareFromInteraction();
            sceneLoader.LoadConfiguredScene();
        }

        private void OnUnlocksClicked()
        {
            randomScareController.TryTriggerScareFromInteraction();

            if (unlocksPanel != null)
            {
                unlocksPanel.Toggle();
                return;
            }

            Debug.Log("Unlocks placeholder clicked. Assign an Unlocks panel when ready.");
        }

        private void OnConfigClicked()
        {
            randomScareController.TryTriggerScareFromInteraction();

            if (configPanel != null)
            {
                configPanel.Toggle();
                return;
            }

            Debug.Log("Config placeholder clicked. Assign a Config panel when ready.");
        }
    }
}
