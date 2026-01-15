using UnityEngine;
using TMPro;
using System.Collections.Generic;

public enum ScoreAction
{
    FoundationMove,
    FlipCard,
    StockRecycle,
    Undo,
    InvalidMove,
    ComboBonus,
    SuitStreakBonus,
    QuickFoundation,
    ClearTimeBonus
}

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    private int score = 0;

    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;   // TMP対応
    [SerializeField] private TMP_Text movesText;   // TMP対応

    [Header("Combo SFX (2,3,4,5+ )")]
    public AudioSource comboSfxSource;   // 再生に使うAudioSource（BGM以外のSFX用を割当）
    public AudioClip combo1Clip;         // ★ 追加：ストリーク=1（1手目）
    public AudioClip combo2Clip;         // ストリーク=2
    public AudioClip combo3Clip;         // ストリーク=3
    public AudioClip combo4Clip;         // ストリーク=4
    public AudioClip combo5PlusClip;     // ストリーク>=5
    public bool comboSfxEnabled = true;  // 必要なら一時的に無効化できる

    [Header("Combo Settings")]
    public bool useUnifiedCombo = true;  // ← これを true にすると、全アクション共通のコンボ連鎖になる

    [Header("時間ボーナス設定")]
    public float maxTimeSeconds = 300f;
    public int timeBonusPerSecond = 5;

    public int CurrentScore => score;

    private readonly List<string> scoreLog = new List<string>(256);

    [Header("ゲームオプション")]
    public bool timedGame = false;

    public int CurrentComboCount { get; private set; } = 0;

    // 手動操作の最終時刻（unscaled）
    private float _lastManualActionTime = -999f;
    public float lastManualActionTime => _lastManualActionTime;  // ← ComboTimerUIが参照

    /// <summary>手動操作が成立したタイミングで呼ぶ</summary>
    public void MarkManualComboTick()
    {
        CurrentComboCount = Mathf.Max(1, CurrentComboCount + 1);
        _lastManualActionTime = Time.unscaledTime;
    }

    /// <summary>残り秒数（0〜comboWindowSec）</summary>
    public float GetComboRemaining()
    {
        if (comboWindowSec <= 0f || _lastManualActionTime < -900f) return 0f;
        float elapsed = Time.unscaledTime - _lastManualActionTime;
        return Mathf.Clamp(comboWindowSec - elapsed, 0f, comboWindowSec);
    }

    /// <summary>残り割合（0〜1）</summary>
    public float GetComboRatio()
    {
        float remain = GetComboRemaining();
        return comboWindowSec > 0f ? remain / comboWindowSec : 0f;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        UpdateScoreUI();
    }

    // ===== 点数表 & 色 =====
    public static int GetPoints(ScoreAction action)
    {
        return action switch
        {
            ScoreAction.FoundationMove => 20,   // 上の完成エリアに置く
            ScoreAction.FlipCard => 15,   // 裏カードをめくる
            ScoreAction.QuickFoundation => 5,    // 「場に並べ替え」用に流用
            ScoreAction.ComboBonus => 50,   // 1組(A〜K)そろえる
            ScoreAction.StockRecycle => -50,  // そのまま（要件次第で0に可）
            ScoreAction.Undo => 0,    // ペナルティ無し
            ScoreAction.InvalidMove => 0,    // 点数なし
            ScoreAction.ClearTimeBonus => 500,  // 現状未使用だが表は残す
            ScoreAction.SuitStreakBonus => 30,   // 未使用なら据え置き
            _ => 0
        };
    }

    public static Color GetColor(ScoreAction action)
    {
        switch (action)
        {
            case ScoreAction.FoundationMove: return new Color(1.00f, 0.84f, 0.20f);
            case ScoreAction.FlipCard: return new Color(0.30f, 1.00f, 0.30f);
            case ScoreAction.StockRecycle: return new Color(1.00f, 0.25f, 0.25f);
            case ScoreAction.Undo: return new Color(1.00f, 0.60f, 0.00f);
            case ScoreAction.InvalidMove: return new Color(0.70f, 0.70f, 0.70f);
            case ScoreAction.ComboBonus: return new Color(0.40f, 0.60f, 1.00f);
            case ScoreAction.SuitStreakBonus: return new Color(0.80f, 0.40f, 1.00f);
            case ScoreAction.QuickFoundation: return new Color(0.30f, 1.00f, 1.00f);
            case ScoreAction.ClearTimeBonus: return new Color(1.00f, 0.95f, 0.40f);
            default: return Color.yellow;
        }
    }

    // ===== 既存の加点API（演出保持） =====
    public void AddScore(ScoreAction action)
    {
        int amount = GetPoints(action);
        AddScore(amount, action.ToString());
    }

    public void AddScoreAt(ScoreAction action, Vector3 worldPos)
    {
        int amount = GetPoints(action);
        AddScore(amount, action.ToString());
        var col = GetColor(action);
        ScorePopupManager.Instance?.Show(amount, worldPos, col);
    }

    public void AddRawAt(int amount, string reason, Vector3 worldPos)
    {
        AddScore(amount, reason);
        ScorePopupManager.Instance?.Show(amount, worldPos);
    }

    public void AddScore(int amount) => AddScore(amount, null);

    public void AddScore(int amount, string reason)
    {
        score += amount;
        string msg = $"[Score] {(amount >= 0 ? "+" : "")}{amount}"
                   + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})")
                   + $" -> total {score}";
        AppendScoreLog(msg);
        UpdateScoreUI();
    }

    // ======= ★ 追加：CardBehaviour / CardFactory から呼ばれるAPI =======

    // ▼ Foundation 置き：戻り値で“実付与点”を返す（Undo用）
    public int OnFoundationPlaced(Vector3 worldPos, bool isAuto)
    {
        int basePts = GetPoints(ScoreAction.FoundationMove);

        if (isAuto)
        {
            // 自動はコンボ外（スコアのみ）
            AddRawAt(basePts, "Auto +20", worldPos);
            return basePts;
        }

        MarkManualComboTick();
        int total = ApplyCombo("foundation", basePts, foundationComboStep, isAuto: false);
        AddRawAt(total, $"Foundation x{comboStreak}", worldPos);

        PlayComboSfxIfNeeded();

        // ★ スパーク: 2手目以降のみ
        PlayComboSparkIfNeeded(worldPos);

        return total;
    }

    // ▼ スパーク統一呼び出し（2連目以降）
    private void PlayComboSparkIfNeeded(Vector3 worldPos)
    {
        if (comboStreak >= 2)
            ComboVfx.Instance?.PlayCardSparkAt(worldPos, comboStreak);
    }

    // 互換呼び出し
    public void OnFoundationPlaced(bool isAuto) => OnFoundationPlaced(Vector3.zero, isAuto);
    public void OnFoundationPlaced() => OnFoundationPlaced(Vector3.zero, false);

    // ▼ Tableau 並べ替え：+5（コンボ適用、手動のみ）
    public int AwardTableauReorder(Vector3 worldPos, bool isAuto = false)
    {
        if (isAuto) return GetPoints(ScoreAction.QuickFoundation);

        MarkManualComboTick();

        int basePts = GetPoints(ScoreAction.QuickFoundation);
        int total = ApplyCombo("reorder", basePts, tableauComboStep, isAuto: false);

        AddRawAt(total, $"Reorder x{comboStreak}", worldPos);
        PlayComboSfxIfNeeded();

        // ★ スパーク: 2手目以降のみ
        PlayComboSparkIfNeeded(worldPos);

        return total;
    }

    /// <summary>
    /// そのスートが K まで揃った時のボーナス（+50）。演出つき。
    /// </summary>
    public void AwardSuitComplete(Vector3 worldPos)
    {
        AddScoreAt(ScoreAction.ComboBonus, worldPos); // 既定では +50
        // もし「完成ボーナス=+50」を厳密に分けたいなら ScoreAction を増やしてもOK
    }

    // 位置不要の互換（保険）
    public void AwardSuitComplete() => AwardSuitComplete(Vector3.zero);

    // ======= その他ユーティリティ =======
    public void AddTimeBonus(float elapsedTime)
    {
        if (!timedGame)
        {
            AddScore(0, "Time Bonus skipped (timedGame=false)");
            return;
        }
        float remaining = Mathf.Max(0f, maxTimeSeconds - elapsedTime);
        int bonus = Mathf.RoundToInt(remaining * timeBonusPerSecond);
        AddScore(bonus, $"Time Bonus (elapsed {elapsedTime:F1}s, remaining {remaining:F1}s)");
    }

    public void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"スコア {score}";

        if (movesText != null)
        {
            var timer = FindObjectOfType<ScoreTimerManager>();
            int moves = timer != null ? timer.CurrentMoves : 0;
            movesText.text = $"ムーブ {moves}";
        }
    }

    public int GetScore() => score;

    public void ResetScore()
    {
        AppendScoreLog($"[Score] Reset (was {score})");
        score = 0;
        scoreLog.Clear();
        UpdateScoreUI();
    }

    public void DumpScoreSummary(string tag = "Victory")
    {
        Log.D($"[Score.{tag}] total={score}, events={scoreLog.Count}");
        foreach (var line in scoreLog) Log.D(line);
    }

    private void AppendScoreLog(string line)
    {
        scoreLog.Add($"{Time.time:0.00}s {line}");
        Log.D(line);
    }

    // ▼▼ コンボ用フィールド＆ロジックを ScoreManager クラス内に追加 ▼▼
    [Header("Combo Settings")]
    public float comboWindowSec = 10f;      // 連続と認める時間
    public int foundationComboStep = 5;       // Foundation 連続時の増分（20,25,30,…）
    public int tableauComboStep = 2;       // 並べ替え連続時の増分（5,7,9,…）

    private string lastComboKey = "";
    private float lastActionTime = -999f;
    private int comboStreak = 0;

    private int ApplyCombo(string key, int basePts, int step, bool isAuto)
    {
        // オート処理はコンボ無効：基礎点のみ
        if (isAuto) return basePts;

        // ★ コンボキーの統一：true の場合は全アクションを同一キーで扱う
        string effectiveKey = useUnifiedCombo ? "__ANY_MANUAL__" : key;

        float now = Time.unscaledTime;

        // 直前と“同じキー”かつ時間窓内 → ストリーク加算
        if (lastComboKey == effectiveKey && (now - lastActionTime) <= comboWindowSec)
        {
            comboStreak++;
        }
        else
        {
            lastComboKey = effectiveKey;  // 別キー or 窓切れ → 新しく開始
            comboStreak = 1;
        }
        lastActionTime = now;

        // 増分：各アクションごとの step を使用（←ここが“スロットごとに増え方は変えない”の肝）
        int bonus = (comboStreak - 1) * step;

        // 連鎖SFX（任意）：2連目から
        PlayComboSfxIfNeeded();

        return basePts + bonus;
    }

    // === コンボ状態の保存/復元用 ===
    [System.Serializable]
    public struct ComboSnapshot
    {
        public string key;
        public float lastTime;
        public int streak;
    }

    public ComboSnapshot GetComboSnapshot()
    {
        return new ComboSnapshot
        {
            key = lastComboKey,
            lastTime = lastActionTime,
            streak = comboStreak
        };
    }

    public void RestoreCombo(ComboSnapshot s)
    {
        lastComboKey = s.key;
        lastActionTime = s.lastTime;
        comboStreak = s.streak;
    }

    public void BreakCombo()
    {
        CurrentComboCount = 0;
        lastComboKey = "";
        lastActionTime = -999f;
        comboStreak = 0;
    }

    void PlayComboSfxIfNeeded()
    {
        if (!comboSfxEnabled || comboSfxSource == null) return;

        // ★ 1手目（streak=1）から鳴らす
        if (comboStreak < 1) return;

        AudioClip clip = null;

        // 1手目：combo1Clip を優先。未設定なら combo2Clip → 3 → 4 → 5+ の順にフォールバック
        if (comboStreak == 1)
        {
            clip = combo1Clip ?? combo2Clip ?? combo3Clip ?? combo4Clip ?? combo5PlusClip;
        }
        else
        {
            // 2,3,4,5+(=5以降は同じ) は従来通り
            int lvl = Mathf.Min(comboStreak, 5);
            switch (lvl)
            {
                case 2: clip = combo2Clip; break;
                case 3: clip = combo3Clip; break;
                case 4: clip = combo4Clip; break;
                default: clip = combo5PlusClip; break;
            }
        }

        if (clip != null) comboSfxSource.PlayOneShot(clip);
    }


    [Header("Anti Ping-Pong")]
    public float antiPingPongWindow = 2.0f; // 2秒以内の逆戻りは無効

    int lastCardId, lastFromId, lastToId;
    float lastMoveTime = -999f;

    public bool IsReverseOfLastMove(CardBehaviour card, Transform from, Transform to)
    {
        if (Time.unscaledTime - lastMoveTime > antiPingPongWindow) return false;
        int cid = card ? card.GetInstanceID() : 0;
        int fid = from ? from.GetInstanceID() : 0;
        int tid = to ? to.GetInstanceID() : 0;
        return (cid == lastCardId && fid == lastToId && tid == lastFromId);
    }

    // Undo直後などで“直前の手”を無効化したい時に呼ぶ
    public void ClearLastManualMove()
    {
        lastCardId = lastFromId = lastToId = 0;
        lastMoveTime = -999f;
    }

    // === 追加：列ごとの最終滞在時刻でピンポン抑止 ===
    readonly Dictionary<CardBehaviour, Dictionary<Transform, float>> _lastVisit
        = new Dictionary<CardBehaviour, Dictionary<Transform, float>>();

    public bool ShouldSuppressForPingPong(CardBehaviour card, Transform targetParent)
    {
        if (antiPingPongWindow <= 0f || card == null || targetParent == null) return false;
        if (_lastVisit.TryGetValue(card, out var map) && map != null &&
            map.TryGetValue(targetParent, out var last) &&
            (Time.unscaledTime - last) <= antiPingPongWindow)
        {
            return true; // 直近に同じ列へ戻っている
        }
        return false;
    }

    // 既存の NoteManualMove の末尾に「滞在記録」を追加
    public void NoteManualMove(CardBehaviour card, Transform from, Transform to)
    {
        // 既存の記録はそのまま…
        lastCardId = card ? card.GetInstanceID() : 0;
        lastFromId = from ? from.GetInstanceID() : 0;
        lastToId = to ? to.GetInstanceID() : 0;
        lastMoveTime = Time.unscaledTime;

        // ★追加：行き先列の最終滞在時刻を刻む
        if (card != null && to != null)
        {
            if (!_lastVisit.TryGetValue(card, out var map) || map == null)
            {
                map = new Dictionary<Transform, float>();
                _lastVisit[card] = map;
            }
            map[to] = Time.unscaledTime;
        }
    }

    // ▼ Flip（めくり）：+15 をコンボ適用で付与（手動のみ）
    public int AwardFlipReveal(Vector3 worldPos)
    {
        MarkManualComboTick();

        int basePts = GetPoints(ScoreAction.FlipCard);
        int total = ApplyCombo("flip", basePts, tableauComboStep, isAuto: false);
        AddRawAt(total, $"Flip x{comboStreak}", worldPos);

        PlayComboSfxIfNeeded();

        // ★ スパーク: 2手目以降のみ
        PlayComboSparkIfNeeded(worldPos);

        return total;
    }

    // 位置不要の互換（保険）
    public int AwardFlipReveal() => AwardFlipReveal(Vector3.zero);

    // ScoreManager 例
    public bool FinalizeAndUpdateHighScore()
    {
        // まだ反映してない加点やボーナスがあればここで確定（必要なら追記）

        int final = CurrentScore;
        const string key = CardFactory.HighScoreKey;

        int best = PlayerPrefs.GetInt(key, int.MinValue);
        bool isNew = final > best;
        if (isNew)
        {
            PlayerPrefs.SetInt(key, final);
            PlayerPrefs.Save();
        }
        return isNew;
    }
}
