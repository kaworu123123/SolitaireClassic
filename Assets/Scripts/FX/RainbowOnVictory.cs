using UnityEngine;
using TMPro;

public class RainbowOnVictory : MonoBehaviour
{
    [Header("Targets")]
    public GameObject panelRoot;
    public TextMeshProUGUI bestScoreText;
    public TextMeshProUGUI bestMovesText;
    public TextMeshProUGUI bestTimeText;

    [Header("Rainbow Defaults")]
    public float speed = 0.6f;
    public float gradientWidth = 10f;
    public float saturation = 1f;
    public float value = 1f;

    // ランタイム更新フラグ
    bool _uScore, _uMoves, _uTime;

    public void SetFlags(bool scoreUpdated, bool movesUpdated, bool timeUpdated)
    {
        _uScore = scoreUpdated;
        _uMoves = movesUpdated;
        _uTime = timeUpdated;
    }

    public void ApplyAndPlay()
    {
        if (panelRoot && !panelRoot.activeInHierarchy) return;

        ApplyOne(bestScoreText, _uScore);
        ApplyOne(bestMovesText, _uMoves);
        ApplyOne(bestTimeText, _uTime);
    }

    public void StopAndReset()
    {
        StopOne(bestScoreText);
        StopOne(bestMovesText);
        StopOne(bestTimeText);
        _uScore = _uMoves = _uTime = false;
    }

    void ApplyOne(TextMeshProUGUI t, bool on)
    {
        if (!t) return;
        var r = t.GetComponent<RainbowTMP_v2>();
        if (on)
        {
            if (!r) r = t.gameObject.AddComponent<RainbowTMP_v2>();
            r.speed = speed;
            r.gradientWidth = gradientWidth;
            r.saturation = saturation;
            r.value = value;
            r.playOnAwake = false;
            r.baseColor = t.color;   // 閉じたらこの色に戻す
            r.enabled = true;
            r.Play();                // ★ これが必須
        }
        else
        {
            if (r) r.enabled = false;
        }
    }

    void StopOne(TextMeshProUGUI t)
    {
        if (!t) return;
        var r = t.GetComponent<RainbowTMP_v2>();
        if (r) r.enabled = false;
    }
}
