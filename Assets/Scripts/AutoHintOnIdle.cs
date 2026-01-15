using UnityEngine;

/// <summary>
/// 一定時間（idleSeconds）プレイヤー操作が無いと自動でヒントを点滅表示する。
/// ScoreManager.MarkManualComboTick() が呼ばれたタイミングを「操作あり」としてリセット。
/// </summary>
public class AutoHintOnIdle : MonoBehaviour
{
    [Header("Settings")]
    public float idleSeconds = 20f;      // 何秒無操作で点滅開始するか
    public float repeatEvery = 6f;       // 2回目以降の点滅間隔
    public bool onlyWhenMovesExist = true;        // 合法手があるときだけ
    public bool ignoreWhileAutoCompleting = true; // オート補完中は無効

    float _nextAt = -1f;
    float _lastSeenManualTick = float.NaN;

    void OnEnable()
    {
        // ゲーム開始直後はすぐは点滅させない
        _nextAt = Time.unscaledTime + idleSeconds;
        CacheManualTick();
    }

    void Update()
    {
        var fac = CardFactory.Instance;
        if (ignoreWhileAutoCompleting && fac != null && fac.isAutoCompleting)
        {
            // オート中は監視リセット
            _nextAt = Time.unscaledTime + idleSeconds;
            CacheManualTick();
            return;
        }

        // ScoreManager 側で手動操作が記録されたらリセット
        var sm = ScoreManager.Instance;
        if (sm != null && !Mathf.Approximately(_lastSeenManualTick, sm.lastManualActionTime))
        {
            CacheManualTick(); // ← 手動操作があった
            _nextAt = Time.unscaledTime + idleSeconds;
        }

        if (Time.unscaledTime < _nextAt) return;

        var hs = FindObjectOfType<HintSystem>();
        if (hs != null)
        {
            if (!onlyWhenMovesExist || hs.HasAnyMove())
                hs.ShowHint(); // 点滅開始（HintSystem側のBlinkで0.8秒ほど光ります）
        }

        // 次の点滅予定をセット
        _nextAt = Time.unscaledTime + repeatEvery;
    }

    void CacheManualTick()
    {
        var sm = ScoreManager.Instance;
        _lastSeenManualTick = (sm != null) ? sm.lastManualActionTime : Time.unscaledTime;
    }

    /// <summary>
    /// UIボタン押下など「手動操作があったら外部から明示的にリセット」したい場合用（任意）
    /// </summary>
    public void ResetIdleTimer()
    {
        _nextAt = Time.unscaledTime + idleSeconds;
        CacheManualTick();
    }
}
