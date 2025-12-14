using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GlassPiece : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("Если true — эта плитка помечена как ломкая (GlassController или ручная настройка)")]
    public bool isBreakable = false;

    [Tooltip("Задержка перед тем, как плитка 'упадёт' после Break() (сек)")]
    public float fallDelay = 0.2f;

    // parent controller (устанавливается из GlassController.Awake или вручную)
    [HideInInspector] public GlassController parentController;

    [Header("Visual / Effects")]
    [Tooltip("Префаб заменяющий целую плитку после ломки (опционально)")]
    public GameObject brokenPrefab;

    [Tooltip("Particle system to spawn when broken (optional)")]
    public ParticleSystem breakParticles;

    [Tooltip("Sound to play on break (optional)")]
    public AudioClip breakSound;

    [Tooltip("Цвет подсветки при наведении/выборе")]
    public Color highlightColor = Color.cyan;

    // internal state
    bool touchedByPlayer = false; // был ли контакт с игроком (чтобы уведомить контроллер 1 раз)
    bool broken = false;          // действительно ли плитка сломана (чтобы не ломать повторно)

    Collider col;
    Renderer[] renderers;
    // сохраняем оригинальные материалы для восстановления
    List<Material[]> originalMaterials = new List<Material[]>();

    // NEW: если этот объект имеет тег "Finish", то он считается финишной платформой
    // и никогда не будет ломаться/проваливаться.
    private bool isFinish = false;

    void Awake()
    {
        col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();

        // берем все renderers в дочерних объектах (если у плитки несколько мешей)
        renderers = GetComponentsInChildren<Renderer>();

        originalMaterials.Clear();
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            Material[] copy = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) copy[i] = mats[i];
            originalMaterials.Add(copy);
        }

        // определяем, является ли объект финишем (тег "Finish")
        isFinish = CompareTag("Finish");
        // Если пометил как Finish, гарантируем, что он не будет ломаться даже если isBreakable=true
        if (isFinish)
        {
            // лог для отладки
            Debug.Log($"GlassPiece '{name}' detected as FINISH (tag). It will not break.");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (broken) return;

        // === ТЕСТЕР (камень / шарик) ===
        if (collision.collider.CompareTag("GlassTester"))
        {
            Debug.Log($"{name} hit by GlassTester");

            // Финиш не ломаем
            if (isFinish) return;

            if (isBreakable)
            {
                Break();
            }

            return; // важно: не идём дальше
        }

        // === ИГРОК ===
        if (collision.collider.CompareTag("Player"))
        {
            if (!touchedByPlayer)
            {
                parentController?.OnGlassTouched(this);
                touchedByPlayer = true;
            }

            if (isFinish)
            {
                Debug.Log($"{name} is FINISH — player landed, no break.");
                return;
            }

            if (isBreakable)
            {
                Break();
            }
            else
            {
                Debug.Log($"{name} is SAFE (player stepped on it).");
            }
        }
    }


    // публичная обёртка для программного вызова ломки
    public void Break()
    {
        // Если это финиш — игнорируем вызов Break
        if (isFinish)
        {
            Debug.Log($"{name}.Break() called but ignored because this is a Finish platform.");
            // всё равно отметим касание/уведомим контроллер, если ещё не уведомляли
            if (!touchedByPlayer)
            {
                parentController?.OnGlassTouched(this);
                touchedByPlayer = true;
            }
            return;
        }

        if (broken) return;
        broken = true;

        if (!touchedByPlayer)
        {
            parentController?.OnGlassTouched(this);
            touchedByPlayer = true;
        }

        if (fallDelay <= 0f)
            DoFallImmediate();
        else
            Invoke(nameof(DoFallImmediate), fallDelay);
    }

    void DoFallImmediate()
    {
        // если это финиш — страховка (не ломаем)
        if (isFinish)
        {
            Debug.Log($"{name}.DoFallImmediate() skipped because this is a Finish platform.");
            return;
        }

        if (breakParticles != null)
        {
            var p = Instantiate(breakParticles, transform.position, transform.rotation);
            p.Play();
        }
        if (breakSound != null)
        {
            AudioSource.PlayClipAtPoint(breakSound, Camera.main != null ? Camera.main.transform.position : transform.position);
        }

        if (brokenPrefab != null)
        {
            Instantiate(brokenPrefab, transform.position, transform.rotation, transform.parent);
            Destroy(gameObject);
            return;
        }

        if (renderers != null)
        {
            foreach (var r in renderers) r.enabled = false;
        }
        if (col != null) col.enabled = false;

        if (gameObject.activeInHierarchy)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 2f;
        }
    }

    // Подсветка плитки (вкл/выкл)
    public void Highlight(bool on)
    {
        if (renderers == null || renderers.Length == 0) return;
        if (on)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var mats = r.materials; // создает экземпляры материалов
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat != null)
                    {
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
                }
                r.materials = mats;
            }
        }
        else
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (i < originalMaterials.Count)
                {
                    var orig = originalMaterials[i];
                    r.materials = orig;
                }
            }
        }
    }

    public bool IsBroken() => broken;
    public bool WasTouchedByPlayer() => touchedByPlayer;
    public bool IsFinishPlatform() => isFinish;
}
