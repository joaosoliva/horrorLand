using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class ConsentController : MonoBehaviour
    {
        private const string ConsentAnsweredKey = "Menu.ConsentAnswered";
        private const string RandomScaresEnabledKey = "Menu.RandomScaresEnabled";

        [Header("UI")]
        [SerializeField] private GameObject consentPanel;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action<bool> onFlowResolved;

        public bool HasAnsweredConsent => PlayerPrefs.GetInt(ConsentAnsweredKey, 0) == 1;
        public bool AreRandomScaresEnabled => PlayerPrefs.GetInt(RandomScaresEnabledKey, 0) == 1;

        private void Awake()
        {
            yesButton.onClick.AddListener(OnYesClicked);
            noButton.onClick.AddListener(OnNoClicked);
        }

        private void OnDestroy()
        {
            yesButton.onClick.RemoveListener(OnYesClicked);
            noButton.onClick.RemoveListener(OnNoClicked);
        }

        public void Initialize(Action<bool> flowResolvedCallback)
        {
            onFlowResolved = flowResolvedCallback;

            if (HasAnsweredConsent)
            {
                consentPanel.SetActive(false);
                onFlowResolved?.Invoke(AreRandomScaresEnabled);
                return;
            }

            consentPanel.SetActive(true);
        }

        private void OnYesClicked()
        {
            SaveConsent(true);
        }

        private void OnNoClicked()
        {
            SaveConsent(false);
        }

        private void SaveConsent(bool randomScaresEnabled)
        {
            PlayerPrefs.SetInt(ConsentAnsweredKey, 1);
            PlayerPrefs.SetInt(RandomScaresEnabledKey, randomScaresEnabled ? 1 : 0);
            PlayerPrefs.Save();

            consentPanel.SetActive(false);
            onFlowResolved?.Invoke(randomScaresEnabled);
        }
    }
}
