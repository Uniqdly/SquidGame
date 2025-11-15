using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GlassFragile : MonoBehaviour
{
    [Tooltip("Минимальная вертикальная скорость при контакте (по players rb) для поломки")]
    public float breakVelocityThreshold = 2.0f;
    public GameObject brokenPrefab; // prefab разбитого стекла
    public ParticleSystem breakParticles;
    public AudioClip breakSound;
    public bool isFragile = false; // пометка, ломается ли эта панель (назначает GlassManager)

    Collider col;
    MeshRenderer rend;
    bool broken = false;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = false;
        rend = GetComponentInChildren<MeshRenderer>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (broken) return;
        if (!isFragile) return;

        // ищем rigidbody игрока
        Rigidbody otherRb = collision.rigidbody;
        if (otherRb == null) return;

        // находим контактную нормаль вниз и скорость игрока относительно земли
        // используем вертикальную составляющую скорости (в локальной world)
        float vy = -otherRb.velocity.y; // скорость вниз положительная

        // Опционально: убедиться, что игрок контактировал сверху (нормаль примерно вверх)
        bool topContact = false;
        foreach (var c in collision.contacts)
        {
            if (Vector3.Dot(c.normal, Vector3.up) > 0.5f)
            {
                topContact = true;
                break;
            }
        }
        if (!topContact) return;

        if (vy >= breakVelocityThreshold)
        {
            Break();
        }
    }

    public void Break()
    {
        if (broken) return;
        broken = true;

        // VFX и звук
        if (breakParticles != null) Instantiate(breakParticles, transform.position, Quaternion.identity);
        if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, transform.position);

        // instantiate broken prefab
        if (brokenPrefab != null)
        {
            Instantiate(brokenPrefab, transform.position, transform.rotation, transform.parent);
        }

        // disable original visual and collider
        if (rend != null) rend.enabled = false;
        if (col != null) col.enabled = false;
    }

    // утилита: пометить как ломаемую или нет
    public void SetFragile(bool val)
    {
        isFragile = val;
        // можно поменять цвет/материал чтобы визуально показать игроку (optional)
        if (rend != null)
        {
            rend.material.color = val ? Color.red : Color.white; // временно
        }
    }
}
