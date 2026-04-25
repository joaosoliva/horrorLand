using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public class MainMenuBootstrap : MonoBehaviour
    {
        [SerializeField] private ConsentController consentController;
        [SerializeField] private MainMenuController mainMenuController;

        private void Start()
        {
            mainMenuController.SetVisible(false);
            consentController.Initialize(OnMenuFlowReady);
        }

        private void OnMenuFlowReady(bool randomScaresEnabled)
        {
            mainMenuController.Initialize(randomScaresEnabled);
        }
    }
}
