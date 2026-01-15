#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MissingScriptTools
{
    [MenuItem("Tools/Missing Scripts/Find In Active Scene")]
    static void FindInActiveScene()
    {
        int count = 0;
        foreach (var go in GameObject.FindObjectsOfType<GameObject>(true))
        {
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    Debug.LogWarning($"[Missing] {GetPath(go)} (Component index {i})", go);
                    count++;
                }
            }
        }
        Debug.Log($"[Missing] Found: {count}");
    }

    [MenuItem("Tools/Missing Scripts/Remove On Selection")]
    static void RemoveOnSelection()
    {
        foreach (var t in Selection.transforms)
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        Debug.Log("Removed missing scripts on selection (and children).");
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
#endif
