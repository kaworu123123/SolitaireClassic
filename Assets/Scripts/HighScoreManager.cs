using UnityEngine;

/// <summary>
/// 単純なベストスコア保存/読込マネージャ
/// </summary>
public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    private const string BestScoreKey = "BestScore";
    public int BestScore { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 必要ならシーン跨ぎ保持
        // DontDestroyOnLoad(gameObject);

        Load();
    }

    public void TryUpdateBestScore(int currentScore)
    {
        if (currentScore > BestScore)
        {
            BestScore = currentScore;
            Save();
            Log.D($"[HighScore] New best: {BestScore}");
        }
    }

    public void ResetBestScore()
    {
        BestScore = 0;
        PlayerPrefs.DeleteKey(BestScoreKey);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(BestScoreKey, BestScore);
        PlayerPrefs.Save();
    }
}
