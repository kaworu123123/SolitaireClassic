using UnityEngine;

/// Stock の見た目サイズに追従して、ホットスポットの当たり判定を広げる
[RequireComponent(typeof(BoxCollider2D))]
public class StockHotspotSizer : MonoBehaviour
{
    public SpriteRenderer stockRenderer;   // 親 Stock の SR をドラッグ
    public Transform wasteTransform;       // Waste の Transform（右隣）
    [Range(0f, 1.5f)] public float widthPadding = 0.4f;   // 横をどれだけ広げるか（カード幅比）
    [Range(0f, 1.0f)] public float heightPadding = 0.2f;  // 縦の余白
    [Range(0f, 0.5f)] public float keepRightClear = 0.15f; // 右側に空ける比率（Waste へはみ出さない）

    BoxCollider2D bc;

    void Awake() => bc = GetComponent<BoxCollider2D>();

    void LateUpdate()
    {
        if (!stockRenderer || !bc) return;

        // 参照カードの“見かけサイズ”（ワールド）
        var refSize = stockRenderer.bounds.size;

        // 目標サイズ（ワールド）
        float wWorld = refSize.x * (1f + widthPadding);
        float hWorld = refSize.y * (1f + heightPadding);

        // Hotspot のローカルスケールに変換
        var loss = transform.lossyScale;
        bc.size = new Vector2(
            wWorld / Mathf.Max(1e-6f, loss.x),
            hWorld / Mathf.Max(1e-6f, loss.y)
        );

        // 右端が Waste 側に出過ぎないよう左へオフセット
        float rightClear = refSize.x * keepRightClear;
        bc.offset = new Vector2(-rightClear / Mathf.Max(1e-6f, loss.x), 0f);
    }
}
