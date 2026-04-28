using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class TitleScreenController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject titleScreenPanel;

        [Header("Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button unlocksButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button recoveredTapeButton;

        [Header("Dependencies")]
        [SerializeField] private MenuSceneLoader sceneLoader;
        [SerializeField] private RandomScareController randomScareController;
        [SerializeField] private UnlocksMenuController unlocksMenuController;
        [SerializeField] private OptionsMenuController optionsMenuController;

        private void Awake()
        {
            startGameButton.onClick.AddListener(HandleStartGameClicked);
            unlocksButton.onClick.AddListener(HandleUnlocksClicked);
            optionsButton.onClick.AddListener(HandleOptionsClicked);
            recoveredTapeButton?.onClick.AddListener(HandleRecoveredTapeClicked);
        }

        private void OnDestroy()
        {
            startGameButton.onClick.RemoveListener(HandleStartGameClicked);
            unlocksButton.onClick.RemoveListener(HandleUnlocksClicked);
            optionsButton.onClick.RemoveListener(HandleOptionsClicked);
            recoveredTapeButton?.onClick.RemoveListener(HandleRecoveredTapeClicked);
        }

        public void Initialize(bool randomScaresEnabled)
        {
            randomScareController?.SetEnabledState(randomScaresEnabled);
            optionsMenuController?.Close();
            unlocksMenuController?.Close();
            SetVisible(true);
        }

        public void SetVisible(bool isVisible)
        {
            if (titleScreenPanel != null)
            {
                titleScreenPanel.SetActive(isVisible);
            }
        }

        private void HandleStartGameClicked()
        {
            randomScareController?.TryTriggerScareFromInteraction();
            sceneLoader.LoadConfiguredScene();
        }

        private void HandleUnlocksClicked()
        {
            randomScareController?.TryTriggerScareFromInteraction();
            unlocksMenuController.Open();
        }

        private void HandleOptionsClicked()
        {
            randomScareController?.TryTriggerScareFromInteraction();
            optionsMenuController.Open();
        }

        private void HandleRecoveredTapeClicked()
        {
            randomScareController?.TryTriggerScareFromInteraction();
            sceneLoader.LoadTutorialReplay();
        }
    }
}
