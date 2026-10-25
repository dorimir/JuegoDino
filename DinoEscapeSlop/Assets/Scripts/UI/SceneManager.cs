using UnityEngine;
using UnityEngine.SceneManagement;

namespace StealthGame
{
    public class SceneManagerScript : MonoBehaviour
    {
        [SerializeField] private string sceneName;

        public void LoadScene()
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            }
        }

    public class SceneManager : SceneManagerScript
    {
    }
        public void ExitGame()
        {
            Application.Quit();
        }
    }
}
