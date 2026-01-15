using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public class FoundationSuitIconFitter : MonoBehaviour
{
    [Tooltip("RefCard の SpriteRenderer を指定")]
    public SpriteRenderer reference;     // ← RefCard の SR
    [Tooltip("このオブジェクトの SpriteRenderer（未設定なら自動）")]
    public SpriteRenderer target;        // ← FoundationMark の SR
    [Range(0.05f, 1f)]
    public float coverage = 0.30f;       // アイコンをカード幅の何割にするか
    public float minScale = 0.05f, maxScale = 1.0f;
    public bool applyEveryFrame = true;  // false なら OnEnable/OnValidate だけで適用

    void Reset()
    {
        target = GetComponent<SpriteRenderer>();
        if (transform.parent)
            reference = transform.parent.GetComponentInChildren<SpriteRenderer>(true); // RefCard を拾う
    }

    void OnEnable() { Fit(true); }
    void OnValidate() { Fit(true); }
    void Update() { if (applyEveryFrame) Fit(false); }

    void Fit(bool immediate)
    {
        if (!target || !target.sprite || !reference || !reference.sprite) return;

        // RefCard を scale=1 で出した時の幅 → 親スケールでワールド換算
        float refPPU = reference.sprite.pixelsPerUnit <= 0 ? 100f : reference.sprite.pixelsPerUnit;
        float refNativeW = reference.sprite.rect.width / refPPU;
        float refWorldW = refNativeW * Mathf.Abs(reference.transform.lossyScale.x);

        // 目標幅：カード幅 * coverage
        float goalW = refWorldW * Mathf.Clamp01(coverage);

        // 自分の sprite を scale=1 で出した時のワールド幅（親スケールも考慮）
        float tgtPPU = target.sprite.pixelsPerUnit <= 0 ? 100f : target.sprite.pixelsPerUnit;
        float tgtNativeW = target.sprite.rect.width / tgtPPU;
        float parentScaleX = transform.parent ? Mathf.Abs(transform.parent.lossyScale.x) : 1f;
        float unitW = Mathf.Max(1e-4f, tgtNativeW * parentScaleX);

        float k = Mathf.Clamp(goalW / unitW, minScale, maxScale);
        transform.localScale = new Vector3(k, k, 1f);
    }
}
