using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CandyCutter : MonoBehaviour
{
    [Header("Points")]
    public List<ContourPoint> allowedPoints = new List<ContourPoint>();
    public List<ContourPoint> forbiddenPoints = new List<ContourPoint>();

    [Header("Needle")]
    public Transform needleTip;
    public float checkRadius = 0.01f;

    [Header("Fail")]
    public int maxForbiddenTouches = 3;

    [Header("Win / Lose")]
    public float sceneDelay = 1.2f;
    public AudioClip winSound;
    public AudioClip loseSound;

    private int forbiddenTouches = 0;
    private HashSet<ContourPoint> cutAllowed = new HashSet<ContourPoint>();
    private bool finished = false;

    void Update()
    {
        if (finished) return;
        if (needleTip == null) return;

        CheckPoints();
    }

    void CheckPoints()
    {
        for (int i = 0; i < allowedPoints.Count; i++)
        {
            ContourPoint p = allowedPoints[i];
            if (p == null) continue;
            if (cutAllowed.Contains(p)) continue;

            if (Vector3.Distance(needleTip.position, p.transform.position) <= checkRadius)
            {
                p.MarkAllowed();
                cutAllowed.Add(p);
                CheckWin();
                return;
            }
        }

        for (int i = 0; i < forbiddenPoints.Count; i++)
        {
            ContourPoint p = forbiddenPoints[i];
            if (p == null) continue;

            if (Vector3.Distance(needleTip.position, p.transform.position) <= checkRadius)
            {
                p.MarkForbidden();
                forbiddenTouches++;

                Debug.Log("Forbidden touched: " + forbiddenTouches);

                if (forbiddenTouches >= maxForbiddenTouches)
                {
                    Lose();
                }
                return;
            }
        }
    }

    void CheckWin()
    {
        if (cutAllowed.Count == allowedPoints.Count)
        {
            Win();
        }
    }

    void Win()
    {
        finished = true;
        Debug.Log("CANDY WIN");

        if (winSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(winSound, Camera.main.transform.position);

        Invoke("LoadNextScene", sceneDelay);
    }

    void Lose()
    {
        finished = true;
        Debug.Log("CANDY LOSE");

        if (loseSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(loseSound, Camera.main.transform.position);

        Invoke("RestartScene", sceneDelay);
    }

    void LoadNextScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
