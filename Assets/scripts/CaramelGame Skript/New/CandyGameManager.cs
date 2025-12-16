using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CandyGameManager : MonoBehaviour
{
    public static CandyGameManager Instance;

    [Header("Candies on scene")]
    public List<GameObject> candies = new List<GameObject>();

    private GameObject activeCandy = null;
    private bool winTriggered = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Вызывается при первом взятии карамели
    /// </summary>
    public void SelectCandy(GameObject selected)
    {
        if (activeCandy != null) return; // уже выбрана

        activeCandy = selected;

        foreach (var candy in candies)
        {
            if (candy != null && candy != activeCandy)
            {
                Destroy(candy);
            }
        }

        Debug.Log($"[CandyGameManager] Active candy selected: {activeCandy.name}");
    }

    /// <summary>
    /// Победа при вырезании ЛЮБОЙ карамели
    /// </summary>
    public void TriggerWin(float delay)
    {
        if (winTriggered) return;
        winTriggered = true;

        Debug.Log("[CandyGameManager] WIN! Loading next level");

        Invoke(nameof(LoadNextLevel), delay);
    }

    void LoadNextLevel()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        int next = current + 1;

        if (next < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(next);
        }
        else
        {
            Debug.Log("[CandyGameManager] Last level completed");
        }
    }
}
