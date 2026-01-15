using UnityEngine;

[ExecuteAlways]
public class SlotBackgroundFitter : MonoBehaviour
{
    public SpriteRenderer target;      // AのSpriteRenderer（このオブジェクト）
    public SpriteRenderer reference;   // カード基準のSpriteRenderer（RefCard）
    public int orderOffset = -2;       // カードの下に置くならマイナス
    [Range(0.8f, 1.2f)]
    public float scaleMultiplier = 1.0f;
    [Range(-0.1f, 0.1f)]
    public float inset = 0.0f;         // 負で少しだけ大きく

    Vector3 baseScale = Vector3.one;

    void OnEnable() { Fit(); }
#if UNITY_EDITOR
    void OnValidate(){ if (isActiveAndEnabled) Fit(); }
#endif

    [ContextMenu("Fit Now")]
    public void Fit()
    {
        if (!target) target = GetComponent<SpriteRenderer>();
        if (!target || !target.sprite || !reference || !reference.sprite) return;

        // 1) 参照カードの「幅（ワールド）」を基準に
        float refWidth = reference.bounds.size.x;
        float targetWidth = refWidth * (1f - inset) * Mathf.Max(0.01f, scaleMultiplier);

        // 2) target を localScale=1 で出したときの「幅（ワールド）」
        float ppu = target.sprite.pixelsPerUnit <= 0 ? 100f : target.sprite.pixelsPerUnit;
        float unitWidthLocal = target.sprite.rect.width / ppu;              // localScale=1 の幅
        // 親チェーンのスケールだけ掛ける（= localScale をこれから決める）
        Vector3 parentLossy = transform.lossyScale;
        Vector3 selfLocal = transform.localScale;
        float parentOnlyX = Mathf.Abs(selfLocal.x) > 1e-6f ? Mathf.Abs(parentLossy.x / selfLocal.x) : Mathf.Abs(parentLossy.x);
        float unitWidthWorld = unitWidthLocal * parentOnlyX;

        // 3) 等倍率で縮尺
        float k = targetWidth / Mathf.Max(1e-4f, unitWidthWorld);
        baseScale = new Vector3(k, k, 1f);
        transform.localScale = baseScale;

        // 4) 並び順をカードに合わせて少し下へ
        target.sortingLayerID = reference.sortingLayerID;
        target.sortingOrder = reference.sortingOrder + orderOffset;

        // 5) 位置はスロット座標系で合わせる（同じ親なら原点でOK）
        if (transform.parent != reference.transform.parent)
            transform.SetParent(reference.transform.parent, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
    }

    // 変更前: private void ApplySortingOrder()
    public void ApplySortingOrder()
    {
        if (!target) target = GetComponent<SpriteRenderer>();
        if (!target || !reference) return;

        // 参照(RefCard)と同じレイヤーで、offset 分だけ下(負) or 上(正)
        target.sortingLayerID = reference.sortingLayerID;
        target.sortingOrder = reference.sortingOrder + orderOffset;
    }

}
