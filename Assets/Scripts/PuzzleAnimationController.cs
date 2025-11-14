using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for programmatic animations in the puzzle game.
/// Handles piece placement animations, completion celebrations, and UI transitions.
/// </summary>
public class PuzzleAnimationController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float piecePlacementDuration = 0.3f;
    [SerializeField] private float pieceScaleBounce = 1.2f;
    [SerializeField] private float rotationSnapSpeed = 5f;

    [Header("Completion Animation")]
    [SerializeField] private float completionCelebrationDuration = 1f;
    [SerializeField] private float pieceWaveDelay = 0.05f;

    /// <summary>
    /// Animates a piece being placed in a slot with scale and rotation
    /// </summary>
    public void AnimatePiecePlacement(Transform piece, Transform targetSlot, System.Action onComplete = null)
    {
        StartCoroutine(PiecePlacementAnimation(piece, targetSlot, onComplete));
    }

    private IEnumerator PiecePlacementAnimation(Transform piece, Transform targetSlot, System.Action onComplete)
    {
        piece.SetParent(targetSlot);
        
        Vector3 startScale = piece.localScale;
        Quaternion startRotation = piece.localRotation;
        Vector3 startPosition = piece.localPosition;

        float elapsed = 0f;

        // Animate to position with bounce effect
        while (elapsed < piecePlacementDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / piecePlacementDuration;

            // Ease out back for bounce effect
            float bounce = EaseOutBack(t);

            // Smooth scale animation with bounce
            float scaleProgress = t < 0.5f ? pieceScaleBounce * (t * 2f) : pieceScaleBounce - (pieceScaleBounce - 1f) * ((t - 0.5f) * 2f);
            piece.localScale = startScale * scaleProgress;

            // Smooth rotation to identity
            piece.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, t * rotationSnapSpeed);

            // Smooth position to center
            piece.localPosition = Vector3.Lerp(startPosition, Vector3.zero, bounce);

            yield return null;
        }

        // Ensure final state
        piece.localPosition = Vector3.zero;
        piece.localRotation = Quaternion.identity;
        piece.localScale = startScale;

        onComplete?.Invoke();
    }

    /// <summary>
    /// Plays a celebration animation when the puzzle is completed
    /// </summary>
    public void AnimatePuzzleCompletion(Transform[] pieces)
    {
        StartCoroutine(CompletionCelebrationAnimation(pieces));
    }

    private IEnumerator CompletionCelebrationAnimation(Transform[] pieces)
    {
        // Wave effect - pieces scale up one by one
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null)
            {
                StartCoroutine(PieceCelebrationBounce(pieces[i]));
            }
            yield return new WaitForSeconds(pieceWaveDelay);
        }
    }

    private IEnumerator PieceCelebrationBounce(Transform piece)
    {
        Vector3 originalScale = piece.localScale;
        float elapsed = 0f;
        float duration = 0.4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Bounce scale animation
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            piece.localScale = originalScale * scale;

            yield return null;
        }

        piece.localScale = originalScale;
    }

    /// <summary>
    /// Animates piece spawn with scale effect
    /// </summary>
    public void AnimatePieceSpawn(Transform piece, float delay = 0f)
    {
        StartCoroutine(PieceSpawnAnimation(piece, delay));
    }

    private IEnumerator PieceSpawnAnimation(Transform piece, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        Vector3 targetScale = piece.localScale;
        piece.localScale = Vector3.zero;

        float elapsed = 0f;
        float duration = 0.4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease out back for spawn effect
            float scale = EaseOutBack(t);
            piece.localScale = targetScale * scale;

            yield return null;
        }

        piece.localScale = targetScale;
    }

    /// <summary>
    /// Animates UI panel fade in
    /// </summary>
    public void AnimatePanelFadeIn(CanvasGroup panel, float duration = 0.3f)
    {
        StartCoroutine(FadePanel(panel, 0f, 1f, duration));
    }

    /// <summary>
    /// Animates UI panel fade out
    /// </summary>
    public void AnimatePanelFadeOut(CanvasGroup panel, float duration = 0.3f)
    {
        StartCoroutine(FadePanel(panel, 1f, 0f, duration));
    }

    private IEnumerator FadePanel(CanvasGroup panel, float from, float to, float duration)
    {
        float elapsed = 0f;
        panel.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            panel.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        panel.alpha = to;
    }

    /// <summary>
    /// Ease out back function for bounce effect
    /// </summary>
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
