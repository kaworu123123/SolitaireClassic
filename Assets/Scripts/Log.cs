using UnityEngine;

public static class Log
{
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void D(object msg) => UnityEngine.Debug.Log(msg);

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void I(object msg) => UnityEngine.Debug.Log($"[INFO] {msg}");

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void W(object msg) => UnityEngine.Debug.LogWarning(msg);

    // d‘å‚ÍíŽžo‚·
    public static void E(object msg) => UnityEngine.Debug.LogError(msg);

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Assert(bool condition, object message = null)
    {
        if (!condition) UnityEngine.Debug.LogError(message ?? "Assertion failed.");
    }
}
