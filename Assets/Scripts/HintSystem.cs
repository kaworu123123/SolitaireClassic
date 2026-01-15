using System.Collections.Generic;
using UnityEngine;

public class HintSystem : MonoBehaviour
{
    [Header("Optional refs (未指定ならTag探索)")]
    public Transform wasteParent; // CardFactory.Instance?.wasteParent を自動で使う

    [Header("Visual")]
    public Color hintColor = new Color(1f, 0.95f, 0.25f, 1f);
    public float hintBlinkSec = 0.8f;

    private Coroutine _blinkCo;
    private SpriteRenderer _lastBlinkSr;

    private string _lastBoardHash = "";
    private MoveCand? _cachedHint = null;
    private MoveCand? _lastShownHint = null;
    private float _nextRefreshAt = 0f;
    private const float COOL_DOWN = 1.0f; // ヒント固定の最短維持時間

    struct MoveCand
    {
        public Transform card;   // 動かすカード(先頭)
        public Transform target; // 行き先(TableauColumn or FoundationSlot)
        public int priority;     // 大きいほど優先

        // 安定キー（小さいほど先）
        public int tie_cardColIdx;   // カードの属するTableau列のindex（なければ大きめ）
        public int tie_cardRank;     // Rank 1..13 を使う（A=1）
        public int tie_cardIsRed;    // 赤=0, 黒=1
        public int tie_targetIdx;    // 行き先の列/スロットindex
    }

    // ボタン等から呼ぶ
    public void ShowHint()
    {
        // クールダウン中はキャッシュを使う
        if (Time.unscaledTime < _nextRefreshAt && _cachedHint.HasValue)
        {
            ApplyBlink(_cachedHint.Value);
            _lastShownHint = _cachedHint.Value;
            return;
        }

        string hash = ComputeBoardHash();
        if (hash == _lastBoardHash && _cachedHint.HasValue)
        {
            ApplyBlink(_cachedHint.Value);
            _lastShownHint = _cachedHint.Value;
            _nextRefreshAt = Time.unscaledTime + COOL_DOWN;
            return;
        }
        var best = FindBestMove(); // ここで初めて再計算

        if (best.card == null)
        {
            Log.D("No more moves…");
            return;
        }

        _cachedHint = best;
        _lastBoardHash = hash;
        _nextRefreshAt = Time.unscaledTime + COOL_DOWN;
        ApplyBlink(best);
        _lastShownHint = best;
    }

    private void ApplyBlink(MoveCand cand)
    {
        StopLastBlinkIfAny();
        var srcR = cand.card ? cand.card.GetComponent<SpriteRenderer>() : null;
        if (srcR != null) { _lastBlinkSr = srcR; _blinkCo = StartCoroutine(Blink(srcR)); }
    }

    private void StopLastBlinkIfAny()
    {
        if (_blinkCo != null) { StopCoroutine(_blinkCo); _blinkCo = null; }
        if (_lastBlinkSr != null) { _lastBlinkSr.color = Color.white; _lastBlinkSr = null; }
    }

    // 無効化時に色を戻す保険
    void OnDisable() => StopLastBlinkIfAny();

    // 詰み判定用：合法手があるか
    public bool HasAnyMove()
    {
        var c = CollectMoves();
        return c.Count > 0;
    }

    // ========= 内部 =========
    MoveCand FindBestMove()
    {
        var list = CollectMoves();
        if (list.Count == 0) return default;

        list.Sort((a, b) =>
        {
            // 1) 大優先：priority 降順
            int d = b.priority.CompareTo(a.priority);
            if (d != 0) return d;

            // 2) ヒステリシス：前回と同じ move を最優先
            if (_lastShownHint.HasValue)
            {
                bool aSame = (a.card == _lastShownHint.Value.card && a.target == _lastShownHint.Value.target);
                bool bSame = (b.card == _lastShownHint.Value.card && b.target == _lastShownHint.Value.target);
                if (aSame != bSame) return bSame ? 1 : -1; // bが同じなら b を先頭
            }

            // 3) 安定キー（列→ランク→色→行き先）
            d = a.tie_cardColIdx.CompareTo(b.tie_cardColIdx); if (d != 0) return d;
            d = a.tie_cardRank.CompareTo(b.tie_cardRank); if (d != 0) return d;
            d = a.tie_cardIsRed.CompareTo(b.tie_cardIsRed); if (d != 0) return d;
            return a.tie_targetIdx.CompareTo(b.tie_targetIdx);
        });

        return list[0];
    }

    List<MoveCand> CollectMoves()
    {
        var cands = new List<MoveCand>();

        // ★ 安定順で取得
        var tableauCols = GetOrderedWithTag("TableauColumn");
        var foundSlots = GetOrderedWithTag("FoundationSlot");

        // 候補カード：各Tableauの最上段表カード / Wasteトップ
        var topCards = new List<Transform>();
        for (int i = 0; i < tableauCols.Length; i++)
        {
            var col = tableauCols[i].transform;
            if (col.childCount == 0) continue;
            var top = col.GetChild(col.childCount - 1);
            var cb = top.GetComponent<CardBehaviour>();
            if (cb != null && cb.Data != null && cb.Data.isFaceUp) topCards.Add(top);
        }

        var fac = CardFactory.Instance;
        var waste = wasteParent != null ? wasteParent : (fac != null ? fac.wasteParent : null);
        if (waste != null && waste.childCount > 0)
        {
            var top = waste.GetChild(waste.childCount - 1);
            var cb = top.GetComponent<CardBehaviour>();
            if (cb != null && cb.Data != null && cb.Data.isFaceUp) topCards.Add(top);
        }

        // 1) Foundation 優先（priority = 100）
        foreach (var t in topCards)
        {
            var cb = t ? t.GetComponent<CardBehaviour>() : null;
            if (cb == null) continue;

            for (int j = 0; j < foundSlots.Length; j++)
            {
                var slot = foundSlots[j] ? foundSlots[j].transform : null;
                if (slot != null && cb.CanMoveToFoundation(slot))
                {
                    cands.Add(new MoveCand
                    {
                        card = t,
                        target = slot,
                        priority = 100,
                        tie_cardColIdx = ColumnIndexOf(t.parent, tableauCols),
                        tie_cardRank = cb.Data.rank,
                        tie_cardIsRed = IsRedSuit(cb) ? 0 : 1,
                        tie_targetIdx = j
                    });
                }
            }
        }

        // 2) Tableau→Tableau（めくり加点あり：base 50 + 20）
        foreach (var t in topCards)
        {
            var cb = t ? t.GetComponent<CardBehaviour>() : null;
            if (cb == null) continue;

            for (int j = 0; j < tableauCols.Length; j++)
            {
                var col = tableauCols[j] ? tableauCols[j].transform : null;
                if (col == null) continue;

                if (CanMoveToTableau_Hint(cb, col))
                {
                    int pr = 50;
                    if (WouldFlipAfterMove(cb)) pr += 20;

                    cands.Add(new MoveCand
                    {
                        card = t,
                        target = col,
                        priority = pr,
                        tie_cardColIdx = ColumnIndexOf(t.parent, tableauCols),
                        tie_cardRank = cb.Data.rank,
                        tie_cardIsRed = IsRedSuit(cb) ? 0 : 1,
                        tie_targetIdx = j
                    });
                }
            }
        }

        return cands;
    }


    private string ComputeBoardHash()
    {
        unchecked
        {
            int h = 17;

            // Foundation総枚数
            h = h * 31 + CountFoundationCards();

            // Tableau列を安定順（名前 or SiblingIndex）で取得
            var cols = GetOrderedWithTag("TableauColumn");
            foreach (var col in cols)
            {
                var cb = GetTopFaceUp(col.transform);
                int rank = cb ? Mathf.Clamp(cb.Data.rank, 1, 13) : 0;
                int isRed = cb ? (IsRedSuit(cb) ? 1 : 0) : 0;
                h = h * 31 + rank * 3 + isRed;
            }

            // Wasteトップ
            var waste = wasteParent != null ? wasteParent :
                        (CardFactory.Instance ? CardFactory.Instance.wasteParent : null);
            var wt = GetTopCard(waste);
            int wrank = wt ? Mathf.Clamp(wt.Data.rank, 1, 13) : 0;
            h = h * 31 + wrank;

            return h.ToString();
        }
    }

    // ===== 安定取得 & ヘルパ =====
    private GameObject[] GetOrderedWithTag(string tag)
    {
        var arr = GameObject.FindGameObjectsWithTag(tag) ?? new GameObject[0];
        System.Array.Sort(arr, (x, y) => {
            if (x == null || y == null) return 0;
            int ix = x.transform.GetSiblingIndex();
            int iy = y.transform.GetSiblingIndex();
            int d = ix.CompareTo(iy);
            return d != 0 ? d : string.Compare(x.name, y.name, System.StringComparison.Ordinal);
        });
        return arr;
    }

    private int ColumnIndexOf(Transform col, GameObject[] orderedCols)
    {
        if (!col) return 9999;
        for (int i = 0; i < orderedCols.Length; i++)
            if (orderedCols[i] && orderedCols[i].transform == col) return i;
        return 9999;
    }

    private static bool IsRedSuit(CardBehaviour cb)
    {
        var s = cb.Data.suit;
        return s == Suit.Hearts || s == Suit.Diamonds;
    }

    private CardBehaviour GetTopCard(Transform parent)
    {
        if (!parent) return null;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var cb = parent.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null) return cb;
        }
        return null;
    }

    private CardBehaviour GetTopFaceUp(Transform col)
    {
        if (!col) return null;
        for (int i = col.childCount - 1; i >= 0; i--)
        {
            var cb = col.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null && cb.Data != null && cb.Data.isFaceUp) return cb;
        }
        return null;
    }

    private int CountFoundationCards()
    {
        int total = 0;
        var fslots = GetOrderedWithTag("FoundationSlot");
        foreach (var go in fslots)
            if (go) for (int i = 0; i < go.transform.childCount; i++)
                    if (go.transform.GetChild(i).GetComponent<CardBehaviour>() != null) total++;
        return total;
    }


    // CardBehaviour.CanMoveToTableau のロジックを、外部用に再実装
    // ・空列ならキングのみOK
    // ・色は交互（赤/黒）かつ、ランクは1つ下
    bool CanMoveToTableau_Hint(CardBehaviour mover, Transform column)
    {
        CardBehaviour lastCard = column.childCount > 0
            ? column.GetChild(column.childCount - 1).GetComponent<CardBehaviour>()
            : null;

        if (lastCard == null)
            return mover.Data.rank == 13; // Kのみ

        bool moverRed = (mover.Data.suit == Suit.Hearts || mover.Data.suit == Suit.Diamonds);
        bool lastRed = (lastCard.Data.suit == Suit.Hearts || lastCard.Data.suit == Suit.Diamonds);
        bool isOppositeColor = (moverRed != lastRed);
        bool isOneLower = mover.Data.rank == lastCard.Data.rank - 1;
        return isOppositeColor && isOneLower;
    }

    // moving.originalParent は private なので、現在の親 transform.parent を使う
    bool WouldFlipAfterMove(CardBehaviour moving)
    {
        var parent = moving ? moving.transform.parent : null;
        if (parent == null || !parent.CompareTag("TableauColumn")) return false;
        // moving が一番上と仮定 → その一つ下を見る
        int idx = moving.transform.GetSiblingIndex();
        int nextIdx = idx - 1;
        if (nextIdx < 0) return false;
        var next = parent.GetChild(nextIdx).GetComponent<CardBehaviour>();
        return next != null && next.Data != null && !next.Data.isFaceUp;
    }

    System.Collections.IEnumerator Blink(SpriteRenderer sr)
    {
        if (!sr) yield break;
        Color baseC = sr.color;
        float freq = 6f;            // 点滅スピード
        float dur = hintBlinkSec;  // 既存のInspector値を使用

        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            // 0..1 を往復させて base ↔ hintColor に補間（スケールには触れません）
            float a = 0.5f * (1f + Mathf.Sin(t * Mathf.PI * 2f * freq));
            sr.color = Color.Lerp(baseC, hintColor, a);
            yield return null;
        }
        sr.color = baseC;
    }
}
