using UnityEngine;

namespace HorrorLand.MenuSystem
{
    public class TitleScreenBootstrap : MonoBehaviour
    {
        [SerializeField] private ConsentController consentController;
        [SerializeField] private TitleScreenController titleScreenController;
        [SerializeField] private OptionsMenuController optionsMenuController;
        [SerializeField] private UnlocksMenuController unlocksMenuController;
        [SerializeField] private RandomScareController randomScareController;

        private void Start()
        {
            optionsMenuController?.Close();
            unlocksMenuController?.Close();
            titleScreenController?.SetVisible(false);
            randomScareController?.SetEnabledState(false);

            if (consentController != null)
            {
                consentController.Initialize(OnMenuFlowReady);
                return;
            }

            OnMenuFlowReady(false);
        }

        private void OnMenuFlowReady(bool randomScaresEnabled)
        {
            randomScareController?.SetEnabledState(randomScaresEnabled);
            titleScreenController?.Initialize(randomScaresEnabled);
        }
    }
}
