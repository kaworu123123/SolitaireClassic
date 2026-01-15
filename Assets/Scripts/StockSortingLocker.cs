using UnityEngine;

/// 山札の裏面SpriteRendererの表示順を毎フレーム固定する
[RequireComponent(typeof(SpriteRenderer))]
public class StockSortingLocker : MonoBehaviour
{
    [Tooltip("カードと同じSorting Layer名（例: Cards / Default）")]
    public string sortingLayerName = "Cards";
    [Tooltip("背景より手前、Waste(500+)より十分小さい値")]
    public int sortingOrder = 50;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        Apply();
    }

    void LateUpdate()
    {
        // 毎フレーム固定（他のスクリプトが上書きしても直後に戻す）
        Apply();
    }

    void Apply()
    {
        if (sr == null) return;
        int id = SortingLayer.NameToID(sortingLayerName);
        if (sr.sortingLayerID != id) sr.sortingLayerID = id;
        if (sr.sortingOrder != sortingOrder) sr.sortingOrder = sortingOrder;
    }
}
