using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorLand.MenuSystem
{
    public class MenuSceneLoader : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "SampleScene";
        [SerializeField] private string introSceneName = "IntroScene";

        public void LoadConfiguredScene()
        {
            bool tutorialCompleted = PlayerPrefs.GetInt(MenuPrefsKeys.TutorialCompleted, 0) == 1;
            bool forcedReplay = PlayerPrefs.GetInt(MenuPrefsKeys.ForceTutorialReplay, 0) == 1;

            string target = (tutorialCompleted && !forcedReplay) ? gameplaySceneName : introSceneName;
            if (forcedReplay)
            {
                PlayerPrefs.SetInt(MenuPrefsKeys.ForceTutorialReplay, 0);
                PlayerPrefs.Save();
            }

            LoadSceneByName(target);
        }

        public void LoadTutorialReplay()
        {
            PlayerPrefs.SetInt(MenuPrefsKeys.ForceTutorialReplay, 1);
            PlayerPrefs.Save();
            LoadSceneByName(introSceneName);
        }

        private void LoadSceneByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("Scene name is empty.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
