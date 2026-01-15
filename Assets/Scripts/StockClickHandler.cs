using UnityEngine;
using UnityEngine.EventSystems;

public class StockClickHandler : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData e)
    {
        var factory = CardFactory.Instance;

        // ★ 追加：オート中 or ポーズ中は無効
        if ((factory != null && factory.isAutoCompleting) || Time.timeScale == 0f)
            return;

        // クリック位置のワールド座標
        var cam = Camera.main;
        var wp = cam ? (Vector2)cam.ScreenToWorldPoint(e.position) : (Vector2)e.position;

        // クリック地点に重なっている 2D コライダーを走査して、
        // Waste 直下の「表向きトップカード」を優先的に拾う
        var hits = Physics2D.OverlapPointAll(wp);

        CardBehaviour target = null;
        int bestIndex = -1;

        foreach (var h in hits)
        {
            var cb = h.GetComponent<CardBehaviour>();
            if (cb == null || cb.Data == null) continue;

            var p = cb.transform.parent;
            bool parentIsWaste =
                p != null && (
                    p.CompareTag("WasteSlot") ||
                    (factory != null && p == factory.wasteParent)
                );

            if (!parentIsWaste) continue;
            if (!cb.Data.isFaceUp) continue;

            // Waste 内で最後尾（=トップ）を選ぶ
            int idx = cb.transform.GetSiblingIndex();
            if (idx > bestIndex)
            {
                bestIndex = idx;
                target = cb;
            }
        }

        if (target != null)
        {
            // Waste カードのタップ処理へフォワード
            ExecuteEvents.Execute<IPointerDownHandler>(target.gameObject, e, ExecuteEvents.pointerDownHandler);
            return;
        }

        // ここに来たら「Waste 上ではない」→ 通常どおり山札をめくる
        factory?.DrawToWaste();
    }
}
