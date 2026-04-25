using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class TitleScreenController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button unlocksButton;
        [SerializeField] private Button optionsButton;

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
        }

        private void OnDestroy()
        {
            startGameButton.onClick.RemoveListener(HandleStartGameClicked);
            unlocksButton.onClick.RemoveListener(HandleUnlocksClicked);
            optionsButton.onClick.RemoveListener(HandleOptionsClicked);
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
    }
}
