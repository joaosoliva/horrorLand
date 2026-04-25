using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorLand.MenuSystem
{
    public class MenuSceneLoader : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "SampleScene";

        public void LoadConfiguredScene()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogWarning("Gameplay scene name is empty.");
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
