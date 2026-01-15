using UnityEngine;

public static class BestScoreUtil
{
    public static int GetUnifiedBest()
    {
        int best = 0;
        if (PlayerPrefs.HasKey("AllBestScore"))
            best = Mathf.Max(best, PlayerPrefs.GetInt("AllBestScore", 0));
        if (PlayerPrefs.HasKey(CardFactory.HighScoreKey))
            best = Mathf.Max(best, PlayerPrefs.GetInt(CardFactory.HighScoreKey, 0));
        if (PlayerPrefs.HasKey("HighScore"))
            best = Mathf.Max(best, PlayerPrefs.GetInt("HighScore", 0));
        return best;
    }

    public static void MigrateAll()
    {
        int best = GetUnifiedBest();
        PlayerPrefs.SetInt(CardFactory.HighScoreKey, best); // 現行キー
        PlayerPrefs.SetInt("AllBestScore", best);           // 勝利画面の累計と揃える
        PlayerPrefs.Save();
    }
}