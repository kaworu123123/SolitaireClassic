using UnityEngine;

public class InputGate : MonoBehaviour
{
    public static bool Busy { get; private set; }
    static int _opToken = 0;
    static int _currentToken = 0;

    // ★ 開始時刻を保持して、ハング防止のタイムアウトを設ける
    static float _busySince = 0f;
    // 実行中フレームでの終了二重呼び出しを避けるためのフラグ
    static int _endFrame = -1;

    // タイムアウト秒（好みで 2〜5秒程度）
    public static float TimeoutSeconds = 3f;

    public static int Begin()
    {
        // タイムアウトの自己回復
        if (Busy && (Time.unscaledTime - _busySince) > TimeoutSeconds)
        {
            Debug.LogWarning("[InputGate] Timeout. Force clearing busy state.");
            ForceClear();
        }

        if (Busy) return -1;
        Busy = true;
        _busySince = Time.unscaledTime;
        _currentToken = ++_opToken;
        return _currentToken;
    }

    public static bool End(int token)
    {
        // 念のためトークン整合を見る（一致しなければ何もしない）
        if (!Busy) return false;
        if (token != _currentToken)
        {
            // 別オペレーションのEndは無視（ログのみ）
            Debug.LogWarning($"[InputGate] End called with stale token {token} (current={_currentToken}). Ignored.");
            return false;
        }

        // 同一フレームで多重Endしないように（保険）
        if (Time.frameCount == _endFrame) return false;
        _endFrame = Time.frameCount;

        Busy = false;
        _currentToken = 0;
        _busySince = 0f;
        return true;
    }

    public static void ForceClear()
    {
        Busy = false;
        _currentToken = 0;
        _busySince = 0f;
        _endFrame = -1;
    }
}
