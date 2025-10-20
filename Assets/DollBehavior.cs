using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DollBehavior : MonoBehaviour
{
    [Header("Audio (optional)")]
    public AudioClip redClip;   // ���� ��� ������������ �� RED (����� �������������� "�����")
    public AudioClip greenClip; // ���� ��� ������������ �� GREEN (����� �������������� "�����")

    [Header("Rotation")]
    public float turnSpeed = 6f; // �������� �������� (��� ������ � ��� �������)

    AudioSource audioSource;
    Quaternion forwardRot; // ��������� ���������� (����� � ����)
    Quaternion backRot;    // forwardRot rotated by 180� around Y
    bool isBack = false;   // ������� ���������: true = ��������� "�����" (� ������)

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
    /// �� ������� ����: SetWatching(true) ������ �������� "����� ������� �� ������".
    /// � ������� ���������� SetWatching(true) = ����� �������������� "�����" (� ������),
    /// SetWatching(false) = ����� �������������� ����� � ���� (forward).
    /// </summary>
    public void SetWatching(bool watch)
    {
        if (isBack == watch) return;
        isBack = watch;

        // ����������� ���� ��� ����� ���������
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

    // ���� �� ������ � ��������� ������ ��������� backRot ��� �������������� ��������� ����������:
    private void OnValidate()
    {
        forwardRot = transform.rotation;
        backRot = forwardRot * Quaternion.Euler(0f, 180f, 0f);
    }
}
