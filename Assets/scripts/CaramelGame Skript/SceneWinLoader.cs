using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneWinLoader : MonoBehaviour
{
    private static SceneWinLoader instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void LoadNext(float delay)
    {
        if (instance == null)
        {
            var go = new GameObject("SceneWinLoader");
            instance = go.AddComponent<SceneWinLoader>();
            DontDestroyOnLoad(go);
        }

        instance.Invoke(nameof(Load), delay);
    }

    void Load()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        int next = current + 1;

        if (next < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(next);
        }
        else
        {
            Debug.Log("[SceneWinLoader] Last level completed");
        }
    }
}
