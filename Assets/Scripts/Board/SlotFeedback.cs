using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SlotFeedback : MonoBehaviour
{
    [Header("Snap")]
    [SerializeField] private float snapDuration = 0.45f;
    [SerializeField] private float glideOvershoot = 1.4f;
    [SerializeField] private float scaleOvershoot = 3.2f;
    [SerializeField] private float entryScale = 1.35f;
    [SerializeField] private float entryTilt = 20f;

    [Header("Punch")]
    [SerializeField] private float punchStrength = 0.24f;
    [SerializeField] private int punchVibrato = 7;

    [Header("Flash")]
    [SerializeField] private Color flashColor = new Color(1f, 0.98f, 0.78f, 1f);

    [Header("Reject")]
    [SerializeField] private Color rejectColor = new Color(1f, 0.35f, 0.32f, 1f);
    [SerializeField] private float rejectDuration = 0.35f;
    [SerializeField] private float rejectTilt = 9f;

    [Header("Completion Wave")]
    [SerializeField] private float waveDuration = 0.55f;
    [SerializeField] private float wavePunch = 0.3f;

    private RectTransform rectTransform;
    private PuzzlePiece placed;
    private Color placedColor;

    public float WaveDuration => waveDuration;

    private void Awake() => rectTransform = (RectTransform)transform;

    public void PlayFilled(PuzzlePiece piece, Vector3 worldOrigin)
    {
        placed = piece;
        placedColor = piece.Image.color;

        RectTransform pieceRect = piece.RectTransform;
        pieceRect.position = worldOrigin;
        pieceRect.localScale = Vector3.one * entryScale;
        pieceRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-entryTilt, entryTilt));

        DOTween.Kill(pieceRect);
        DOTween.Kill(piece.Image);
        DOTween.Kill(rectTransform);

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        sequence.Insert(0f, pieceRect.DOAnchorPos(Vector2.zero, snapDuration)
            .SetEase(Ease.OutBack, glideOvershoot));
        sequence.Insert(0f, pieceRect.DOScale(Vector3.one, snapDuration)
            .SetEase(Ease.OutBack, scaleOvershoot));
        sequence.Insert(0f, pieceRect.DOLocalRotate(Vector3.zero, snapDuration)
            .SetEase(Ease.OutBack, scaleOvershoot));
        sequence.Insert(0f, piece.Image.DOColor(flashColor, snapDuration * 0.22f)
            .SetLoops(2, LoopType.Yoyo));
        sequence.Insert(snapDuration * 0.3f, rectTransform
            .DOPunchScale(Vector3.one * punchStrength, snapDuration * 0.7f, punchVibrato, 0.7f));
        sequence.OnComplete(Settle);
    }

    public void PlayRejected(Image slotImage)
    {
        DOTween.Kill(rectTransform);
        DOTween.Kill(slotImage);

        Color slotColor = slotImage.color;

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        sequence.Insert(0f, slotImage.DOColor(rejectColor, rejectDuration * 0.3f)
            .SetLoops(2, LoopType.Yoyo));
        sequence.Insert(0f, rectTransform
            .DOPunchRotation(new Vector3(0f, 0f, rejectTilt), rejectDuration, punchVibrato, 0.5f));
        sequence.OnComplete(() =>
        {
            slotImage.color = slotColor;
            rectTransform.localRotation = Quaternion.identity;
        });
    }

    public void PlayWave(float delay)
    {
        if (placed == null) return;

        DOTween.Kill(rectTransform);
        DOTween.Kill(placed.Image);

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        sequence.AppendInterval(delay);
        sequence.Append(rectTransform
            .DOPunchScale(Vector3.one * wavePunch, waveDuration, punchVibrato, 0.6f));
        sequence.Insert(delay, placed.Image.DOColor(flashColor, waveDuration * 0.25f)
            .SetLoops(2, LoopType.Yoyo));
        sequence.OnComplete(Settle);
    }

    private void Settle()
    {
        rectTransform.localScale = Vector3.one;

        if (placed == null) return;
        placed.Image.color = placedColor;
        placed.RectTransform.localScale = Vector3.one;
        placed.RectTransform.localRotation = Quaternion.identity;
        placed.RectTransform.anchoredPosition = Vector2.zero;
    }
}
