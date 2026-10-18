using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace StealthGame
{
    public class SceneManagerScript : MonoBehaviour
    {
        [SerializeField] private string sceneName;

        public void LoadScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    
            public void ExitGame()
        {
            Debug.Log("Salir del juego");
            Application.Quit();
        }
    }
}