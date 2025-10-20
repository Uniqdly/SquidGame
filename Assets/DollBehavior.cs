using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DollBehavior : MonoBehaviour
{
    [Header("Audio (optional)")]
    public AudioClip redClip;   // звук при переключении на RED (кукла поворачивается "назад")
    public AudioClip greenClip; // звук при переключении на GREEN (кукла поворачивается "вперёд")

    [Header("Rotation")]
    public float turnSpeed = 6f; // скорость поворота (чем больше — тем быстрее)

    AudioSource audioSource;
    Quaternion forwardRot; // начальная ориентация (лицом к полю)
    Quaternion backRot;    // forwardRot rotated by 180° around Y
    bool isBack = false;   // текущее состояние: true = повернута "назад" (к дереву)

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        forwardRot = transform.rotation;
        backRot = forwardRot * Quaternion.Euler(0f, 180f, 0f);
    }

    void Update()
    {
        Quaternion target = isBack ? backRot : forwardRot;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * turnSpeed);
    }

    /// <summary>
    /// По истории кода: SetWatching(true) раньше означало "кукла смотрит на игрока".
    /// В текущей реализации SetWatching(true) = кукла поворачивается "назад" (к дереву),
    /// SetWatching(false) = кукла поворачивается лицом к полю (forward).
    /// </summary>
    public void SetWatching(bool watch)
    {
        if (isBack == watch) return;
        isBack = watch;

        // проигрываем звук при смене состояния
        if (audioSource != null)
        {
            AudioClip c = isBack ? redClip : greenClip;
            if (c != null)
            {
                audioSource.clip = c;
                audioSource.Play();
            }
        }
    }

    // Если вы хотите в редакторе быстро обновлять backRot при редактировании начальной ориентации:
    private void OnValidate()
    {
        forwardRot = transform.rotation;
        backRot = forwardRot * Quaternion.Euler(0f, 180f, 0f);
    }
}
