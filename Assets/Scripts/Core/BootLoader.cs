using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChromaBlast
{
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] private string menuSceneName = "Menu";
        [SerializeField] private float minimumBootSeconds = 0.15f;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(minimumBootSeconds);
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
