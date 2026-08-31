using UnityEngine;

public static class UiInputGuard
{
    private const float CooldownSeconds = 0.25f;

    private static float blockedUntil;

    public static void Block()
    {
        blockedUntil = Time.unscaledTime + CooldownSeconds;
    }

    public static bool Allows =>
        !Application.isPlaying || Time.unscaledTime >= blockedUntil;
}
