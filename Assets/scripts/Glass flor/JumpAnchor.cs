using UnityEngine;

/// <summary>
/// Точка, на которую может прыгнуть игрок.
/// Должен быть дочерним объектом GlassPiece или FinishPlatform.
/// </summary>
[DisallowMultipleComponent]
public class JumpAnchor : MonoBehaviour
{
    [Header("Appearance")]
    public bool showGizmo = true;
    public Color gizmoColor = new Color(0, 1, 0, 0.5f); // полупрозрачный зелёный
    public float gizmoRadius = 0.15f;

    [Header("Behavior")]
    [Tooltip("Можно ли прыгать на эту точку?")]
    public bool isActive = true;

    [Tooltip("Порядок активации (для последовательных прыжков)")]
    public int orderIndex = 0;

    [Tooltip("Связанная анимация/эффект при приземлении")]
    public AudioClip landSound;
    public ParticleSystem landParticles;

    // Кэшируем родителя один раз
    GlassPiece cachedParent = null;
    public GlassPiece ParentPiece => cachedParent != null ? cachedParent : (cachedParent = GetComponentInParent<GlassPiece>());

    void OnDrawGizmos()
    {
        if (!showGizmo) return;
        Gizmos.color = isActive ? gizmoColor : Color.red * 0.5f;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
    }

    // Публичный метод: активировать/деактивировать (например, по сигналу)
    public void SetActive(bool active)
    {
        isActive = active;
    }

    
    public Vector3 GetLandingPosition(float landingYOffset)
    {
        
        return transform.position + Vector3.up * landingYOffset;

        
    }
}