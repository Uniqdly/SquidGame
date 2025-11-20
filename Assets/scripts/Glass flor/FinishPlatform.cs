using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class FinishPlatform : MonoBehaviour
{
    [Tooltip("Можно назначить эффект/звук при достижении финиша")]
    public ParticleSystem finishVfx;
    public AudioClip finishSfx;

    [Tooltip("Если true — сразу загружать следующую сцену в билде. Иначе — просто лог/UI.")]
    public bool loadNextScene = true;

    [Tooltip("Задержка перед загрузкой/вызовом (сек)")]
    public float onArriveDelay = 0.6f;

    [Tooltip("Цвет подсветки при наведении")]
    public Color highlightColor = Color.green;

    // internal
    Renderer rend;
    Material originalMat;
    bool triggered = false;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            originalMat = rend.sharedMaterial;
        }
    }

    public void Highlight(bool on)
    {
        if (rend == null) return;
        if (on)
        {
            var mats = rend.materials; // creates instances
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;
                if (mat.HasProperty("_Color"))
                    mat.color = highlightColor;
                else if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", highlightColor);

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", highlightColor * 0.5f);
                }
            }
            rend.materials = mats;
        }
        else
        {
            if (originalMat != null)
            {
                rend.sharedMaterial = originalMat;
            }
        }
    }

    public void OnPlayerArrive()
    {
        if (triggered) return;
        triggered = true;

        if (finishVfx != null) Instantiate(finishVfx, transform.position, transform.rotation).Play();
        if (finishSfx != null) AudioSource.PlayClipAtPoint(finishSfx, Camera.main != null ? Camera.main.transform.position : transform.position);

        if (loadNextScene)
        {
            Invoke(nameof(LoadNextScene), onArriveDelay);
        }
        else
        {
            Debug.Log("[FinishPlatform] Player arrived at finish (no scene load).");
        }
    }

    void LoadNextScene()
    {
        int idx = SceneManager.GetActiveScene().buildIndex;
        int next = idx + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            SceneManager.LoadScene(idx);
    }
}
