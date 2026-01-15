using UnityEngine;

public class DebugUndoManagers : MonoBehaviour
{
    void Start()
    {
        var all = FindObjectsOfType<UndoManager>(includeInactive: true);
        Log.D($"[DEBUG] シーン内の UndoManager コンポーネント数 = {all.Length}");
        foreach (var um in all)
            Log.D($" → {um.gameObject.name} (active: {um.gameObject.activeSelf})");
    }
}
