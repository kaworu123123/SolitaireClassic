using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Events;

public class TableauClearWatcher : MonoBehaviour
{
    [Header("監視対象（未指定なら Tag=TableauColumn 検出）")]
    public Transform tableauRoot;

    [Header("Foundation 親（未指定なら Tag=FoundationSlot 全取得）")]
    public Transform foundationRoot;

    [Header("演出パラメータ")]
    public float checkInterval = 0.25f;
    public float moveDuration = 0.25f;       // 1枚を飛ばす秒数
    public float perCardDelay = 0.05f;       // 1枚毎のウェイト
    public Vector3 foundationStackOffset = new Vector3(0f, 0.02f, 0f); // 積む時の微オフセット

    [Header("完了時コールバック（任意）")]
    public UnityEvent onAutoCompleteFinished;  // VictoryUI表示などを繋いでOK
    public GameObject victoryPanelRoot;        // 直接表示したい場合

    [Header("スロット未割り当て時のフォールバック")]
    public Transform fallback;

    bool watching = true;
    bool autoCompleting = false;

    bool _endgameLock = false;   // 終了処理中は外部からのVictoryオープンをブロック

    List<Transform> tableauColumns = new List<Transform>();
    List<Transform> foundationSlots = new List<Transform>(); // Tag=FoundationSlot を想定

    [SerializeField] private Transform stockRoot;
    [SerializeField] private Transform wasteRoot;

    [Header("AutoComplete Safety Guards")]
    [SerializeField] private int maxDrawsWithoutProgress = 120;   // 進捗なしでめくれる最大回数
    [SerializeField] private int maxFramesWithoutProgress = 600;  // 進捗なしで待てる最大フレーム

    [Header("Visual Order (Tableau)")]
    [SerializeField] private string cardsSortingLayer = "Cards";
    [SerializeField] private int tableauColumnStep = 1000; // 列ごとの帯域幅（重ならないよう十分大きめ）

    IEnumerator Start()
    {
        // カード配布など初期化が終わるまで少し待つ
        yield return new WaitForSeconds(2f);

        // 念のため最新の参照を取り直す
        RefreshColumns();
        RefreshFoundations();

        // ここで初めて監視開始
        StartCoroutine(WatchLoop());
    }

    void Awake()
    {
        RefreshColumns();
        RefreshFoundations();
    }

    void OnEnable()
    {
        // Start()内で遅延してから開始するので、ここでは始めない
        // StartCoroutine(WatchLoop());
    }

    void OnDisable()
    {
        watching = false;
    }

    IEnumerator WatchLoop()
    {
        watching = true;
        while (watching && !autoCompleting)
        {
            if (AllTableauCardsFaceUp())
            {
                // ★ 自前の AutoCompleteToFoundations() は起動しない
                // ★ CardFactory に一本化（TryAutoComplete は既存）
                var fac = CardFactory.Instance;
                if (fac != null && !fac.isAutoCompleting)
                    fac.TryAutoComplete();

                yield break; // 自分の監視は終わり
            }
            yield return new WaitForSeconds(checkInterval);
        }
    }

    void LateUpdate()
    {
        if (_endgameLock && victoryPanelRoot != null && victoryPanelRoot.activeSelf)
            victoryPanelRoot.SetActive(false); // 何かに開かれても即閉じる
    }

    int GetTopRankInSlot(Transform slot)
    {
        if (!slot) return 0;
        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            var cb = slot.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null) return cb.Rank;
        }
        return 0;
    }


    int CountFoundationCards(Transform slot)
    {
        if (!slot) return 0;
        int count = 0;
        for (int i = 0; i < slot.childCount; i++)
            if (slot.GetChild(i).GetComponent<CardBehaviour>() != null)
                count++;
        return count;
    }

    private bool StockAndWasteEmpty()
    {
        var fac = CardFactory.Instance;
        if (fac != null)
        {
            return fac.IsDeckEmpty() && !fac.HasWaste();
        }
        // フォールバック（参照が無い環境）
        bool stockEmpty = (stockRoot == null) || stockRoot.childCount == 0;
        bool wasteEmpty = (wasteRoot == null) || wasteRoot.childCount == 0;
        return stockEmpty && wasteEmpty;
    }

    void RefreshColumns()
    {
        tableauColumns.Clear();
        if (tableauRoot != null)
        {
            foreach (var t in tableauRoot.GetComponentsInChildren<Transform>(true))
                if (t.CompareTag("TableauColumn")) tableauColumns.Add(t);

            if (tableauColumns.Count == 0)
            {
                // タグ未使用なら、直下を列とみなす
                foreach (Transform child in tableauRoot) tableauColumns.Add(child);
            }
        }
        else
        {
            var tagged = GameObject.FindGameObjectsWithTag("TableauColumn");
            foreach (var go in tagged) tableauColumns.Add(go.transform);
        }
    }

    void RefreshFoundations()
    {
        foundationSlots.Clear();
        if (foundationRoot != null)
        {
            foreach (var t in foundationRoot.GetComponentsInChildren<Transform>(true))
                if (t.CompareTag("FoundationSlot")) foundationSlots.Add(t);
            if (foundationSlots.Count == 0)
            {
                // タグ未使用でも使えるように直下を候補に
                foreach (Transform child in foundationRoot) foundationSlots.Add(child);
            }
        }
        else
        {
            var tagged = GameObject.FindGameObjectsWithTag("FoundationSlot");
            foreach (var go in tagged) foundationSlots.Add(go.transform);
        }
    }

    bool AllTableauCardsFaceUp()
    {
        // 念のため最新化（tableauRoot 変更や列増減に追従）
        RefreshColumns();

        bool any = false;
        foreach (var col in tableauColumns)
        {
            if (!col) continue;

            for (int i = 0; i < col.childCount; i++)
            {
                var cb = col.GetChild(i).GetComponent<CardBehaviour>();
                if (cb == null) continue;

                any = true;                    // Tableau に1枚はある
                if (!cb.IsFaceUp) return false; // 1枚でも裏なら未達
            }
        }
        return any; // 1枚以上あって全て表 → true
    }

    // スロット内で s の“最上位ランク”を返す（A=1..K=13、無ければ0）
    private int GetTopRankForSuitInSlot(Transform slot, Suit tagSuit)
    {
        int top = 0;
        for (int i = 0; i < slot.childCount; i++)
        {
            var cb = slot.GetChild(i).GetComponent<CardBehaviour>();
            if (cb == null) continue;
            if (cb.Suit != tagSuit) continue;   // ← 他スートは無視
            if (cb.Rank > top) top = cb.Rank;
        }
        return top;
    }
    // スロット内で s のカードが何枚あるか
    private int CountCardsOfSuitInSlot(Transform slot, Suit tagSuit)
    {
        int count = 0;
        for (int i = 0; i < slot.childCount; i++)
        {
            var cb = slot.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null && cb.Suit == tagSuit) count++;
        }
        return count;
    }

    // 4スートすべて K(13) まで積めているか？（厳密版）
    private bool AllFoundationsComplete()
    {
        // 4スート固定で回す（不足/重複スロットは即 false）
        Suit[] suits = { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades };

        foreach (var s in suits)
        {
            // 該当スートの FoundationSlotTag を持つスロットを見つける
            Transform slot = null;
            foreach (var t in foundationSlots)
            {
                var tag = t.GetComponent<FoundationSlotTag>();
                if (tag != null && tag.suit == s)
                {
                    slot = t;
                    break;
                }
            }

            if (slot == null)
            {
                // そのスートの置き場が無い ⇒ 未完成
                Log.W($"[CompleteCheck] slot for {s} not found");
                return false;
            }

            // 同スートだけ見てトップランクを取得（0=空, 1..13）
            int top = GetTopRankForSlotFilteringBySuit(slot, s); // 既存ヘルパー
            if (top != 13)
            {
                // 途中なら未完成
                Log.D($"[CompleteCheck] {s} top={top} (need 13)");
                return false;
            }
        }

        return true;
    }

    // Foundation(=タグ付き)の子かどうかを判定する小ヘルパー
    private static bool IsInFoundationTransform(Transform t)
    {
        if (t == null) return false;
        // 自身 or 親に FoundationSlotTag が付いていれば Foundation 扱い
        return t.GetComponent<FoundationSlotTag>() != null ||
               t.GetComponentInParent<FoundationSlotTag>() != null;
    }

    // --- 全自動：Foundation へ吸い込み ---
    // --- 全自動：Foundation へ吸い込み ---
    private IEnumerator AutoCompleteToFoundations()
    {
        autoCompleting = true;
        var fac = CardFactory.Instance;
        if (fac != null) fac.isAutoCompleting = true;

        RefreshFoundations();   // ← 正式名で呼び出し
        RefreshColumns();
        var suitToSlot = BuildSuitToSlotStrict();

        // Foundation 合計枚数を取得
        int GetFoundationTotal()
        {
            int total = 0;
            foreach (var slot in foundationSlots)
                if (slot) total += slot.childCount;
            return total;
        }

        int lastFoundationTotal = GetFoundationTotal();
        int drawsWithoutProgress = 0;
        int framesWithoutProgress = 0;

        int guard = 0;
        while (guard++ < 3000)
        {
            bool moved = false;

            // 各スートの必要ランクを Foundation へ置けるだけ置く
            foreach (var kv in suitToSlot)
            {
                var suit = kv.Key;
                var slot = kv.Value;
                if (slot == null) continue;

                int top = GetTopRankForSuitInSlot(slot, suit); // 0..13
                int need = top + 1;                             // 1..13

                if (need >= 1 && need <= 13)
                {
                    if (TryPlaceNextFor(suit, need, slot))
                    {
                        moved = true;
                        // 1枚動かしたら少し待つ＋Tween完了待ち
                        yield return new WaitForSeconds(perCardDelay);
                        yield return null;
                        if (DG.Tweening.DOTween.TotalPlayingTweens() > 0)
                            yield return new WaitWhile(() => DG.Tweening.DOTween.TotalPlayingTweens() > 0);
                    }
                }
            }

            if (!moved)
            {
                // Waste→Tableau があれば先に実施（これもTween完了待ち）
                if (TryWasteToAnyTableau())
                {
                    yield return new WaitForSeconds(perCardDelay);
                    yield return null;
                    if (DG.Tweening.DOTween.TotalPlayingTweens() > 0)
                        yield return new WaitWhile(() => DG.Tweening.DOTween.TotalPlayingTweens() > 0);
                    continue;
                }

                // 進捗が無いので 1 枚だけ stock→waste をめくる
                if (fac != null)
                {
                    fac.DrawToWaste(false);
                    drawsWithoutProgress++;
                    framesWithoutProgress++;
                    yield return null;
                }

                // 進捗なしが一定回数/フレーム続いたら安全のため中断
                if (drawsWithoutProgress >= maxDrawsWithoutProgress ||
                    framesWithoutProgress >= maxFramesWithoutProgress)
                {
                    Log.W("[AutoComplete] Aborted to avoid infinite loop (no progress).");
                    break;
                }

                continue; // 次評価へ
            }

            // 何かしら動いたフレーム。Foundation 合計が増えていれば進捗としてカウンタをリセット
            yield return null;
            int nowTotal = GetFoundationTotal();
            if (nowTotal > lastFoundationTotal)
            {
                lastFoundationTotal = nowTotal;
                drawsWithoutProgress = 0;
                framesWithoutProgress = 0;
            }
        }

        // 最後に少しだけ収束待ち
        int guardWait = 0;
        while ((!AllFoundationsComplete() || !StockAndWasteEmpty()) && guardWait++ < 600)
            yield return null;

        if (AllFoundationsComplete() && StockAndWasteEmpty())
        {
            onAutoCompleteFinished?.Invoke();
            if (victoryPanelRoot != null) victoryPanelRoot.SetActive(true);
        }

        autoCompleting = false;
        if (fac != null) fac.isAutoCompleting = false;

        CardFactory.Instance?.CheckVictory();
    }



    #region AutoComplete helpers

    // スロット内の「同スートだけ」を見て、いまの頂上ランクを返す（0=空, 1..13=A..K）
    private int GetTopRankForSlotFilteringBySuit(Transform slot, Suit suit)
    {
        int top = 0;
        for (int i = 0; i < slot.childCount; i++)
        {
            var cb = slot.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null && cb.Suit == suit)
                top = Mathf.Max(top, cb.Rank);
        }
        return top;
    }

    // 指定スートの「rankNeeded」カードを場から拾って Foundation に置く（置けたら true）
    private bool TryPlaceNextFor(Suit suit, int rankNeeded, Transform targetSlot)
    {
        if (rankNeeded < 1 || rankNeeded > 13) return false;

        var all = GameObject.FindObjectsOfType<CardBehaviour>();
        foreach (var cb in all)
        {
            if (!cb || !cb.IsFaceUp) continue;
            if (cb.Suit != suit || cb.Rank != rankNeeded) continue;

            // Tableau の最上段チェック
            var p = cb.transform.parent;
            if (p != null && p.CompareTag("TableauColumn"))
            {
                if (cb.transform.GetSiblingIndex() != p.childCount - 1)
                    continue;
            }

            // 追加チェック: Foundation スロットのトップが rankNeeded - 1 か？
            int top = GetTopRankForSlotFilteringBySuit(targetSlot, suit);
            if (top + 1 != rankNeeded) continue;

            // 移動
            StartCoroutine(cb.AnimateMoveToSlot(targetSlot, moveDuration));
            Log.D($"[AutoComplete] placed {suit} {rankNeeded}");
            return true;
        }
        return false;
    }



    // 山札から1枚めくる。成功したら true
    private bool TryDrawOneFromStock()
    {
        // 1) CardFactory に直呼びできるAPIがある場合
        var factory = GameObject.FindObjectOfType<CardFactory>();
        if (factory != null)
        {
            // 下のどれか“実在する方”だけ残してください（無ければ SendMessage を使います）
            // if (factory.TryDrawFromStock()) return true;
            // factory.DrawFromStockOne(); return true;

            // API名が不明な場合のフォールバック（存在すれば呼ばれる／無ければ無視）
            //factory.gameObject.SendMessage("DrawFromStockOne", SendMessageOptions.DontRequireReceiver);
            //factory.gameObject.SendMessage("DrawOne", SendMessageOptions.DontRequireReceiver);
            //factory.gameObject.SendMessage("FlipStock", SendMessageOptions.DontRequireReceiver);

            factory.DrawFromStockOne();

            // 成否が取れない実装の場合は“とりあえず試した”扱いで true を返すと、
            // 次フレームでまた評価→進めるカードが出ていれば置きに行きます。
            return true;
        }
        return false;
    }

    #endregion


    Transform PickFoundationSlotFor(CardBehaviour card, ref int rrIndex)
    {
        // 1) スート名で一致するスロットを優先（スロット名に "Clubs/Spades/Hearts/Diamonds" が含まれる、等）
        var acc = card.GetComponent<CardDataAccessor>();
        if (acc != null)
        {
            foreach (var slot in foundationSlots)
            {
                string n = slot.name.ToLower();
                if ((acc.suit.ToString().ToLower().Contains("club") && n.Contains("club")) ||
                    (acc.suit.ToString().ToLower().Contains("spade") && n.Contains("spade")) ||
                    (acc.suit.ToString().ToLower().Contains("heart") && n.Contains("heart")) ||
                    (acc.suit.ToString().ToLower().Contains("diamond") && n.Contains("diamond")))
                {
                    return slot;
                }
            }
        }

        // 2) 見つからなければラウンドロビンで分配
        if (foundationSlots.Count == 0) return null;
        var s = foundationSlots[rrIndex % foundationSlots.Count];
        rrIndex++;
        return s;
    }

    IEnumerator MoveLerp(Transform t, Vector3 end, float duration)
    {
        Vector3 start = t.position;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / duration);
            t.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, p));
            yield return null;
        }
        t.position = end;
    }

    // Foundation の見た目順序を完全に固定する（SortingGroup で強制）
    void ApplyFoundationVisualOrder(CardBehaviour card, int slotIndex, int orderInSlot)
    {
        if (!card) return;

        // 親の最前面へ
        card.transform.SetAsLastSibling();

        // 他の上書きに絶対負けない帯域
        int forcedOrder = 200000 + slotIndex * 10000 + orderInSlot;

        // 1) SortingGroup で一括制御
        var sg = card.GetComponent<SortingGroup>();
        if (!sg) sg = card.gameObject.AddComponent<SortingGroup>();
        sg.sortingLayerName = "Cards";          // 無ければ Project Settings > Tags and Layers で作成
        sg.sortingOrder = forcedOrder;

        // 2) 子のSpriteRendererも合わせる（保険）
        var srs = card.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in srs)
        {
            r.sortingLayerName = sg.sortingLayerName;
            r.sortingOrder = forcedOrder;
        }

        // 3) Z を微調整（0.001f/枚）
        var lp = card.transform.localPosition;
        card.transform.localPosition = new Vector3(lp.x, lp.y, -0.001f * orderInSlot);

        // 4) 次フレームでもう一度“二度掛け”（他スクリプトの上書きに勝つ）
        StartCoroutine(ReapplyOrderNextFrame(card, sg.sortingLayerName, forcedOrder, orderInSlot));
    }

    IEnumerator ReapplyOrderNextFrame(CardBehaviour card, string layer, int order, int orderInSlot)
    {
        yield return null; // 1フレーム待つ

        if (!card) yield break;

        card.transform.SetAsLastSibling();

        var sg = card.GetComponent<SortingGroup>();
        if (!sg) sg = card.gameObject.AddComponent<SortingGroup>();
        sg.sortingLayerName = layer;
        sg.sortingOrder = order;

        var srs = card.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in srs)
        {
            r.sortingLayerName = layer;
            r.sortingOrder = order;
        }

        var lp = card.transform.localPosition;
        card.transform.localPosition = new Vector3(lp.x, lp.y, -0.001f * orderInSlot);
    }

    // FoundationSlotTag を走査して suit→slot の対応表を作る
    private Dictionary<Suit, Transform> BuildSuitToSlot()
    {
        var map = new Dictionary<Suit, Transform>();
        foreach (var slot in foundationSlots)
        {
            var tag = slot.GetComponent<FoundationSlotTag>();
            if (tag != null && !map.ContainsKey(tag.suit))
                map.Add(tag.suit, slot);
        }
        return map;
    }

    /// <summary>
    /// CardData の suit / rank を安全に参照する小さなヘルパ
    /// （CardBehaviourから直接dataに触らないで済むように）
    /// </summary>
    public class CardDataAccessor : MonoBehaviour
    {
        public Suit suit => _data != null ? _data.suit : Suit.Clubs;
        public int rank => _data != null ? _data.rank : 1;

        CardData _data;
        void Awake()
        {
            // CardBehaviour と同じオブジェクトに CardData を持っている前提
            // 無ければ CardBehaviour 側の公開APIに差し替えてOK
            _data = GetComponent<CardBehaviour>() != null
                  ? typeof(CardBehaviour).GetField("data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(GetComponent<CardBehaviour>()) as CardData
                  : null;
        }
    }

    // Tableau の先頭に置けるか？（色交互＆1ランク下／空列はKのみ）
    bool CanPlaceOnTableau(CardBehaviour card, Transform column)
    {
        if (!column) return false;
        CardBehaviour last = column.childCount > 0
            ? column.GetChild(column.childCount - 1).GetComponent<CardBehaviour>()
            : null;
        if (!last) return card.Rank == 13; // 空列は K
        bool opposite = (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds)
                     != (last.Suit == Suit.Hearts || last.Suit == Suit.Diamonds);
        bool oneLower = card.Rank == last.Rank - 1;
        return opposite && oneLower;
    }

    // Waste のトップを、置ける Tableau に1回だけ落とす
    bool TryWasteToAnyTableau()
    {
        if (wasteRoot == null || wasteRoot.childCount == 0) return false;
        var top = wasteRoot.GetChild(wasteRoot.childCount - 1).GetComponent<CardBehaviour>();
        if (!top || !top.IsFaceUp) return false;

        foreach (var col in tableauColumns)
        {
            if (CanPlaceOnTableau(top, col))
            {
                StartCoroutine(top.AnimateMoveToSlot(col, moveDuration));
                return true;
            }
        }
        return false;
    }

    private Dictionary<Suit, Transform> BuildSuitToSlotStrict()
    {
        var map = new Dictionary<Suit, Transform>();

        foreach (var slot in foundationSlots)
        {
            var tag = slot.GetComponent<FoundationSlotTag>();
            if (tag == null) continue;
            if (!map.ContainsKey(tag.suit))
                map[tag.suit] = slot;      // 同じスートが複数あっても最初のを採用
        }

        if (map.Count != 4)
            Log.W($"[AutoComplete] FoundationSlotTag 不足/重複: {map.Count}/4");

        return map;
    }

    void SetCardVisualOrder(CardBehaviour card, int sortingOrder, int orderInStack)
    {
        if (!card) return;

        // 上に積む
        card.transform.SetAsLastSibling();

        // Tableau 配下なら SortingGroup は使わない（裏表の切替と競合するため）
        if (card.transform.parent != null && card.transform.parent.CompareTag("TableauColumn"))
        {
            var sg = card.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sg) DestroyImmediate(sg); // 削除
                                          // 親の SpriteRenderer のみを触る
            var sr = card.GetComponent<SpriteRenderer>();
            if (sr)
            {
                sr.sortingLayerName = cardsSortingLayer;
                sr.sortingOrder = sortingOrder;
            }
            // Zは触らない（列の縦オフセットはRefreshTableauColumnに任せる）
            var lp = card.transform.localPosition;
            card.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
            return;
        }

        // ← FoundationなどTableau以外は従来どおりでOK（必要ならSG維持）
        var sg2 = card.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (!sg2) sg2 = card.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        sg2.sortingLayerName = cardsSortingLayer;
        sg2.sortingOrder = sortingOrder;
    }

    public void RebuildTableauVisualOrders()
    {
        RefreshColumns();

        for (int colIdx = 0; colIdx < tableauColumns.Count; colIdx++)
        {
            var col = tableauColumns[colIdx];
            if (!col) continue;

            int baseOrder = colIdx * tableauColumnStep;

            for (int i = 0; i < col.childCount; i++)
            {
                var cb = col.GetChild(i).GetComponent<CardBehaviour>();
                if (!cb) continue;

                int order = baseOrder + i;
                SetCardVisualOrder(cb, order, i);
            }

            // ★ 表/裏の縦オフセットなど公式整列を再適用
            CardBehaviour.RefreshTableauColumn(col);
        }
    }

    private System.Collections.IEnumerator WaitForTweens()
    {
        yield return null; // 1フレーム分は必ず待つ
#if DOTWEEN || DOTWEEN_PRESENT
    if (DG.Tweening.DOTween.TotalPlayingTweens() > 0)
        yield return new WaitWhile(() => DG.Tweening.DOTween.TotalPlayingTweens() > 0);
#endif
    }
}