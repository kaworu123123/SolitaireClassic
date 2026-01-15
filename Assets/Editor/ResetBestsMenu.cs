#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ResetBestsMenu
{
    [MenuItem("Tools/Reset Bests (Score/Time/Moves)")]
    public static void ResetBests()
    {
        PlayerPrefs.DeleteKey("BestScore");
        PlayerPrefs.DeleteKey("BestMoves");
        PlayerPrefs.DeleteKey("BestTime");
        PlayerPrefs.Save();
        Debug.Log("[ResetBests] Done (BestScore/BestMoves/BestTime)");
    }
}
#endif
