using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorLand.MenuSystem
{
    public class ConsentController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject consentPanel;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action<bool> onFlowResolved;

        public bool HasAnsweredConsent => PlayerPrefs.GetInt(MenuPrefsKeys.ConsentAnswered, 0) == 1;
        public bool AreRandomScaresEnabled => PlayerPrefs.GetInt(MenuPrefsKeys.RandomScaresEnabled, 0) == 1;

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

        public void ResetConsentChoice()
        {
            PlayerPrefs.DeleteKey(MenuPrefsKeys.ConsentAnswered);
            PlayerPrefs.DeleteKey(MenuPrefsKeys.RandomScaresEnabled);
            PlayerPrefs.Save();
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
            PlayerPrefs.SetInt(MenuPrefsKeys.ConsentAnswered, 1);
            PlayerPrefs.SetInt(MenuPrefsKeys.RandomScaresEnabled, randomScaresEnabled ? 1 : 0);
            PlayerPrefs.Save();

            consentPanel.SetActive(false);
            onFlowResolved?.Invoke(randomScaresEnabled);
        }
    }
}
