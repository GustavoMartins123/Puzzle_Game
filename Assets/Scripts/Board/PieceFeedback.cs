using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PieceFeedback : MonoBehaviour
{
    [Header("Reject")]
    [SerializeField] private Color rejectColor = new Color(1f, 0.42f, 0.38f, 1f);
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 34f;
    [SerializeField] private int shakeVibrato = 14;
    [SerializeField] private float shakeTilt = 14f;
    [SerializeField] private float rejectedReturnDuration = 0.55f;

    [Header("Drop")]
    [SerializeField] private float dropScale = 1.14f;
    [SerializeField] private float dropDuration = 0.28f;

    [Header("Hint")]
    [SerializeField] private Color hintColor = new Color(0.3f, 1f, 0.82f, 1f);
    [SerializeField] private float hintDuration = 0.75f;
    [SerializeField] private float hintScale = 1.18f;

    [Header("Selection")]
    [SerializeField] private float selectedScale = 1.08f;
    [SerializeField] private float selectedDuration = 0.12f;

    [Header("Rotation")]
    [SerializeField] private float rotationPulse = 0.1f;
    [SerializeField] private float rotationDuration = 0.2f;

    private RectTransform rectTransform;
    private Image image;
    private Color baseColor;
    private Quaternion restRotation;
    private Vector3 restScale = Vector3.one;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        image = GetComponent<Image>();
        baseColor = image.color;
    }

    public void Stop()
    {
        DOTween.Kill(rectTransform);
        DOTween.Kill(image);
        image.color = baseColor;
    }

    public void PlayDrop()
    {
        Stop();

        restRotation = rectTransform.localRotation;
        restScale = rectTransform.localScale;
        rectTransform.localScale = restScale * dropScale;

        DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(rectTransform.DOScale(restScale, dropDuration).SetEase(Ease.OutBack, 2.6f))
            .OnComplete(Settle);
    }

    public void PlaySelected()
    {
        Stop();
        restRotation = rectTransform.localRotation;
        restScale = rectTransform.localScale;
        rectTransform.DOScale(restScale * selectedScale, selectedDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    public void PlayReturn(Vector3 worldOrigin)
    {
        Stop();

        restRotation = rectTransform.localRotation;
        restScale = rectTransform.localScale;
        rectTransform.position = worldOrigin;
        rectTransform.localRotation = Quaternion.identity;

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        sequence.Insert(0f, rectTransform
            .DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato, 90f, false, true));
        sequence.Insert(0f, rectTransform
            .DOPunchRotation(new Vector3(0f, 0f, shakeTilt), shakeDuration, shakeVibrato, 0.6f));
        sequence.Insert(0f, image
            .DOColor(rejectColor, shakeDuration * 0.3f).SetLoops(2, LoopType.Yoyo));
        sequence.Insert(shakeDuration, rectTransform
            .DOAnchorPos(Vector2.zero, rejectedReturnDuration).SetEase(Ease.OutQuint));
        sequence.Insert(shakeDuration, rectTransform
            .DOLocalRotateQuaternion(restRotation, rejectedReturnDuration).SetEase(Ease.OutQuint));
        sequence.OnComplete(Settle);
    }

    public void PlayHint()
    {
        Stop();
        restRotation = rectTransform.localRotation;
        restScale = rectTransform.localScale;

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        sequence.Insert(0f, image.DOColor(hintColor, hintDuration * 0.5f)
            .SetLoops(2, LoopType.Yoyo));
        sequence.Insert(0f, rectTransform.DOPunchScale(
            restScale * (hintScale - 1f),
            hintDuration,
            6,
            0.6f));
        sequence.OnComplete(Settle);
    }

    public void PlayRotation()
    {
        DOTween.Kill(rectTransform);
        restRotation = rectTransform.localRotation;
        restScale = rectTransform.localScale;
        rectTransform.DOPunchScale(
                restScale * rotationPulse,
                rotationDuration,
                4,
                0.5f)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(Settle);
    }

    private void Settle()
    {
        image.color = baseColor;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = restRotation;
        rectTransform.localScale = restScale;
    }
}
