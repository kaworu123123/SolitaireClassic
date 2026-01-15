using UnityEngine;
using TMPro;

public class ScoreTimerManager : MonoBehaviour
{
    public static ScoreTimerManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TMP_Text inGameTimerText;   // ゲーム画面のタイマー表示
    [SerializeField] private TMP_Text victoryTimerText;  // 勝利パネル用（あれば）

    [SerializeField] private string timerPrefix = "タイム:";
    [SerializeField] private bool prefixOnNewLine = true;

    // Header("UI") 付近に追加
    [SerializeField] private string victoryTimerPrefix = "タイム:";
    [SerializeField] private bool victoryPrefixOnNewLine = false;

    [Header("State")]
    [SerializeField] private bool timerRunning = false;
    [SerializeField] private float currentTime = 0f; // 秒
    [SerializeField] private int moves = 0;

    [Header("Config")]
    [SerializeField] private bool autoFindTimerTexts = true; // 名前で自動検索の保険

    [SerializeField] private int finalTimeSec = -1;  // -1=未確定
    public bool HasFinalTime => finalTimeSec >= 0;
    public int FinalTimeSec => finalTimeSec;


    public float CurrentTime => currentTime;
    public int Moves => moves;

    public float ElapsedSeconds => currentTime;   // 経過秒数は currentTime
    public int CurrentMoves => moves;             // ムーブ数は moves

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else if (Instance != this) { Destroy(gameObject); return; }

        // 念のため起動ログ
        Log.D("[STM] Awake. Instance set.");
    }

    void Start()
    {
        // UI自動補完（任意）
        if (autoFindTimerTexts)
        {
            if (inGameTimerText == null)
            {
                var go = GameObject.Find("InGameTimerText");
                if (go) inGameTimerText = go.GetComponent<TMP_Text>();
            }
            if (victoryTimerText == null)
            {
                var go = GameObject.Find("VictoryTimerText");
                if (go) victoryTimerText = go.GetComponent<TMP_Text>();
            }
        }

        UpdateTimerUI(); // 初期表示
    }

    void Update()
    {
        if (!timerRunning) return;

        // ポーズ時は Time.timeScale=0 になる想定 → deltaTimeは0なので止まる
        currentTime += Time.deltaTime;

        // 1秒ごとくらいにUI更新（毎フレ更新でもOK）
        UpdateTimerUI();
    }

    // ========= Public API =========
    public void ResetAll()
    {
        timerRunning = false;
        currentTime = 0f;
        moves = 0;
        finalTimeSec = -1;
        ClearSnapshot();
        UpdateTimerUI();
        Log.D("[STM] ResetAll");
    }

    public void StartTimer()
    {
        timerRunning = true;
        finalTimeSec = -1;
        Log.D("[STM] StartTimer");
    }

    public void StopTimer()
    {
        if (!timerRunning)
        {
            Log.D("[STM] StopTimer called but timerRunning was already false");
            return;
        }

        timerRunning = false;
        finalTimeSec = Mathf.RoundToInt(currentTime);
        UpdateTimerUI();
        Log.D("[STM] StopTimer final=" + finalTimeSec);
    }

    public void PauseTimer()
    {
        timerRunning = false;
        Log.D("[STM] PauseTimer");
    }

    public void ResumeTimer()
    {
        timerRunning = true;
        Log.D("[STM] ResumeTimer");
    }

    public void AddMove()
    {
        moves++;
        // ★ここを追加：ムーブ増加直後にUIを更新
        ScoreManager.Instance?.UpdateScoreUI();
    }

    public void SetVictoryPanelTexts(TMP_Text scoreText, TMP_Text bestText, TMP_Text timeText)
    {
        // VictoryUIControllerから渡してくる場合用の補助。必要なければ無視OK。
        victoryTimerText = timeText;
        UpdateTimerUI();
    }

    // ========= Helpers =========
    private string BuildTimerText(string label, int seconds, bool labelOnNewLine)
    {
        string t = FormatTime(seconds);
        if (string.IsNullOrEmpty(label)) return t;
        return labelOnNewLine ? $"{label}\n{t}" : $"{label} {t}";
    }

    private void UpdateTimerUI()
    {
        int src = HasFinalTime ? finalTimeSec : Mathf.RoundToInt(currentTime);

        // ゲーム中のタイマーは「ラベル＋改行 or 同一行」の形式にする
        if (inGameTimerText != null)
            inGameTimerText.text = BuildTimerText(timerPrefix, src, prefixOnNewLine);

        // 勝利パネルの方は時刻のみ（デザインが別なので余計なラベルは付けない）
        if (victoryTimerText != null)
            victoryTimerText.text = BuildTimerText(victoryTimerPrefix, src, victoryPrefixOnNewLine);

        // ★Best系はここで触らない
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.FloorToInt(seconds);
        int mm = total / 60;
        int ss = total % 60;
        return $"{mm:00}:{ss:00}";
    }

    // ▼ 追記: ムーブを減らす（Undo 用）
    public void ReduceMove(int amount = 1)
    {
        if (amount < 0) amount = -amount;
        moves = Mathf.Max(0, moves - amount);
        // ムーブ数をUI表示しているならここで更新処理を呼ぶ（必要なら）
        // UpdateMovesUI();  // 未実装ならコメントのままでOK
    }

    public void SnapshotFinalTime()
    {
        finalTimeSec = Mathf.Max(0, Mathf.RoundToInt(currentTime));
    }

    public void ClearSnapshot()
    {
        finalTimeSec = -1;
    }
}
