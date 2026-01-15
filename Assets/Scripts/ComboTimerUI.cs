using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComboTimerUI : MonoBehaviour
{
    public Image fillImage;
    public TMP_Text label;

    bool _wasPaused = false;
    float _pausedRemain = 0f;  // Pause突入時の残り秒を保存

    void Update()
    {
        var sm = ScoreManager.Instance;
        if (sm == null || sm.comboWindowSec <= 0f)
        {
            SetFill(0f, "");
            return;
        }

        // ★ Pause中は残り時間を凍結表示
        if (PauseController.IsPaused) // ← 追加
        {
            if (!_wasPaused)
            {
                // Pauseに入った瞬間に一度だけ残り秒をキャプチャ
                float elapsedP = Time.unscaledTime - sm.lastManualActionTime;
                _pausedRemain = Mathf.Clamp(sm.comboWindowSec - elapsedP, 0f, sm.comboWindowSec);
                _wasPaused = true;
            }
            float rP = sm.comboWindowSec > 0f ? _pausedRemain / sm.comboWindowSec : 0f;
            SetFill(rP, (_pausedRemain > 0f) ? $"{_pausedRemain:0.0}s" : "");
            return;
        }
        else if (_wasPaused)
        {
            // 再開時にフラグだけ戻す（再び通常計算へ）
            _wasPaused = false;
        }

        // 通常計算（既存）
        float elapsed = Time.unscaledTime - sm.lastManualActionTime;
        float remain = Mathf.Clamp(sm.comboWindowSec - elapsed, 0f, sm.comboWindowSec);
        float ratio = sm.comboWindowSec > 0f ? remain / sm.comboWindowSec : 0f;
        SetFill(ratio, (remain > 0f) ? $"{remain:0.0}s" : "");
    }

    void SetFill(float r, string text)
    {
        if (fillImage) fillImage.fillAmount = r;
        if (label) label.text = text;
    }
}
