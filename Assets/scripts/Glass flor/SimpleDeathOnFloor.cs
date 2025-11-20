using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SimpleDeathOnFloor : MonoBehaviour
{
    [Tooltip("Тег пола. Объект с этим тегом перезапустит игру при касании.")]
    public string floorTag = "Floor";

    [Tooltip("Задержка перед рестартом (в секундах)")]
    public float restartDelay = 0.5f;

    bool isRestarting = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (isRestarting) return;

        if (collision.collider.CompareTag(floorTag))
        {
            isRestarting = true;
            StartCoroutine(RestartScene());
        }
    }

    IEnumerator RestartScene()
    {
        yield return new WaitForSeconds(restartDelay);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
