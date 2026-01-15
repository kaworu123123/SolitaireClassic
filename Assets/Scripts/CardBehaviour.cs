using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class CardBehaviour : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    [SerializeField] private CardData data;
    // 公開用の getter プロパティを追加
    public CardData Data => data;

    private CardFactory factory;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalPosition;
    private int originalSiblingIndex;
    private Transform originalParent;
    private Vector3 originalLocalScale;
    private Vector3 originalLocalPosition;
    private Vector3 originalLocalPos;
    private int originalSortingOrder;

    public bool allowDrag = false;

    private List<Transform> draggingCards;         // まとめてドラッグ中のカード群
    private Vector3[] draggingCardsStartPos;
    private Vector3 pointerStartWorldPos;  // ドラッグ開始時のポインタ座標
    private Transform dragContainer;         // すべてのカードを一時的に入れる親 (Canvas)
    private int originalIndex;         // ドラッグしたカードの列内インデックス

    private BoxCollider2D boxCollider;
    private PolygonCollider2D polyCollider;
    // お好みの重ね高さ
    private const float TableauSpacing = 0.3f;

    private Vector3 _initialScale;

    private DG.Tweening.Tween currentTween;
    private readonly List<DG.Tweening.Tween> currentTweens = new();

    // Kを空列に置いたときの+5をそのカードで一度だけ許可するためのフラグ
    [SerializeField] private bool kingEmptyOnceAwarded = false;

    [Header("表裏設定")]
    [Tooltip("カード裏面用スプライト")]
    [SerializeField] private Sprite faceDownSprite;


    [Header("Flip アニメーション設定")]
    [Tooltip("ひっくり返しにかけるトータル時間（秒）")]
    [SerializeField] private float flipDuration = 0.4f;

    // 追加: タップ判定用
    private Vector2 _pressScreenPos;
    private float _pressTime;
    [SerializeField] private float tapMaxPx = 10f;   // 画面ピクセルの移動量しきい値
    [SerializeField] private float tapMaxTime = 0.25f; // タップ時間しきい値

    private bool isFlipping = false;
    private Vector3 originalScale;

    public Suit Suit => data != null ? data.suit : Suit.Clubs;
    public int Rank => data != null ? data.rank : 1;

    /// <summary>ドラッグ前の親 Transform を保持</summary>
    private Transform oldParent;
    /// <summary>ドラッグ前のワールド座標を保持</summary>
    private Vector3 oldWorldPos;
    /// <summary>ドラッグ前の描画順を保持</summary>
    private int oldSortingOrder;
    /// <summary>ドラッグ前の列内インデックスを保持</summary>
    private int oldSiblingIndex;

    [Header("Flip Sound")]
    [Tooltip("カードをめくる効果音")]
    public AudioClip flipSound;

    [Header("Tableau に置いたときの SE")]
    [SerializeField] private AudioClip[] placeSounds;    // Inspector で 4つの wav をドラッグ
    private AudioSource audioSource;

    [Header("SE (Foundation)")]
    [SerializeField] private AudioClip foundationSfx;   // Inspector でファイルをセット

    [Header("Invalid Move Feedback")]
    [SerializeField] private AudioClip invalidMoveSfx;

    private AudioSource cardAudioSource;

    public Vector3 DefaultLocalScale { get; set; }

    // 全カード共通で次に鳴らすインデックスを保持
    private static int nextPlaceSoundIndex = 0;

    private Vector2 pointerDownPos;
    private float pointerDownTime;

    public static float FACE_DOWN_OFFSET = 0.18f;  // 裏向きは詰める
    public static float FACE_UP_OFFSET = 0.36f;  // 表向きは広げる

    private bool awardForTapMove = false; // タップ→TryAutoMove の時だけ true

    private bool isAutoMoving = false;
    private void SetAutoMoving(bool v)
    {
        isAutoMoving = v;
        if (boxCollider) boxCollider.enabled = !v; // 触れないように
    }

    private const float FOUNDATION_Y_STEP = 0.00f; // 好みで 0.02〜0.06

    private int _dragGateToken = -1;

    // 追加①: 外から“表向きか”読める
    public bool IsFaceUp => data != null && data.isFaceUp;

    // 追加②: いまFoundation配下にあるか
    public bool IsInFoundation => transform.parent != null && transform.parent.CompareTag("FoundationSlot");

    private bool _autoAwardedThisTween = false;

    // 追加③: 裏なら表にめくる（演出用）。SetFaceUp(bool) は内部にある前提
    public void RevealIfNeeded()
    {
        if (data != null && !data.isFaceUp)
        {
            // クラス内にある SetFaceUp(bool) を呼ぶ（privateでも同クラス内なのでOK）
            SetFaceUp(true);
        }
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        DefaultLocalScale = transform.localScale;   // 生成時のローカルスケールを記憶

        // ② 生成時のスケールをキャッシュ
        _initialScale = transform.localScale;
        boxCollider = GetComponent<BoxCollider2D>();
        originalScale = transform.localScale;

        // 追加②: AudioSource コンポーネントを取得 or 追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // 効果音再生用の設定（必要なら）
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D サウンド


        // --- もし別に分けたいならこう ---
        cardAudioSource = GetComponent<AudioSource>();
        if (cardAudioSource == null)
            cardAudioSource = gameObject.AddComponent<AudioSource>();
        cardAudioSource.playOnAwake = false;
        cardAudioSource.spatialBlend = 0f;

                FitColliderToSprite();

        kingEmptyOnceAwarded = false; // 念のため毎回リセット

    }


    private void FlipWithAnimation(bool faceUp)
    {
        if (isFlipping) return;
        StartCoroutine(FlipCoroutine(faceUp));
    }

    private IEnumerator FlipCoroutine(bool faceUp)
    {
        isFlipping = true;

        // 現在のローカルスケールを保持
        Vector3 baseScale = transform.localScale;
        float half = flipDuration * 0.2f;
        float t = 0f;

        // ① X スケールを１→０ にアニメーション
        while (t < half)
        {
            float s = Mathf.Lerp(1f, 0f, t / half);
            transform.localScale = new Vector3(s * baseScale.x, baseScale.y, baseScale.z);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localScale = new Vector3(0f, baseScale.y, baseScale.z);

        // ② スプライトを切り替えて即時反映
        ApplyFaceUpVisual(faceUp);

        // ③ X スケールを０→１ にアニメーション
        t = 0f;
        while (t < half)
        {
            float s = Mathf.Lerp(0f, 1f, t / half);
            transform.localScale = new Vector3(s * baseScale.x, baseScale.y, baseScale.z);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localScale = baseScale;

        isFlipping = false;
    }

    public void Initialize(CardData data, CardFactory factory)
    {

        this.data = data;
        this.factory = factory;

        // 裏面スプライトを取得してセット
        if (faceDownSprite == null && factory != null)
        {
            faceDownSprite = factory.GetBackSprite();

        }

        UpdateVisual();
        FitColliderToSprite();
    }

    /// <summary>
    /// スプライトの大きさにピッタリ合う BoxCollider2D を設定します
    /// </summary>
    private void FitColliderToSprite()
    {
        if (spriteRenderer.sprite == null) return;

        // スプライトの Bounds（ローカル単位）を取得
        Vector2 size = spriteRenderer.sprite.bounds.size;
        Vector2 center = spriteRenderer.sprite.bounds.center;

        // Collider をスプライト全体にぴったり合わせる
        boxCollider.size = size;
        // ここのオフセットを 0,0 ではなくスプライトの中心に
        boxCollider.offset = center;
    }

    // 既存と置き換え（内容は短い）: 押した瞬間に位置/時刻だけ覚える
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
    {
        Log.D("[CARD DOWN] " + name);

        // ── オート中は無視 ──
        if (CardFactory.Instance != null && CardFactory.Instance.isAutoCompleting) return;

        var p = transform.parent;

        // ── Waste 親判定を強化：Tag一致 もしくは 参照一致（CardFactory.Instance.wasteParent） ──
        bool parentIsWaste =
            p != null && (
                p.CompareTag("WasteSlot") ||
                (CardFactory.Instance != null && p == CardFactory.Instance.wasteParent)
            );

        // ── Tween 中ブロック：Waste だけは即時タップ移動を優先して許可 ──
        if (!parentIsWaste && DG.Tweening.DOTween.IsTweening(transform)) return;

        _pressScreenPos = e.position;
        _pressTime = Time.unscaledTime;

        // ───── ① Waste：表向き＆トップなら移動を最優先 ─────
        if (parentIsWaste && Data.isFaceUp && IsTopCardInPile())
        {
            awardForTapMove = true;       // ムーブ+1
            TryAutoMove();                // Foundation → Tableau の順で内部判定して動く
            return;
        }

        // ───── ② Stock：裏向き＆トップならめくる ─────
        if (p != null && p.CompareTag("StockSlot") && !Data.isFaceUp && IsTopCardInPile())
        {
            // Stockは直接表にせず、必ずWasteへ（ゲームルールの一貫性を保つ）
            var factory = CardFactory.Instance;
            if (factory != null)
            {
                factory.DrawToWaste();    // ← ここでWasteに出す
            }
            return; // 以降のドラッグ開始はしない
        }

        // ───── ③ それ以外（Tableau 上など表向き）→ 自動移動 ─────
        if (Data.isFaceUp)
        {
            awardForTapMove = true;
            TryAutoMove();
        }
    }

    public void SetDragEnabled(bool enabled)
    {
        var all = FindObjectsOfType<CardBehaviour>(true);
        foreach (var cb in all) cb.allowDrag = enabled;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!allowDrag) return;
        // --- 既存の早期リターン条件 ---
        if (isAutoMoving || DG.Tweening.DOTween.IsTweening(transform)) return;
        if (CardFactory.Instance != null && CardFactory.Instance.isAutoCompleting) return;

        // 裏向きはドラッグ不可
        if (!data.isFaceUp) return;

        // 同じ列の最前面だけを許可
        if (!IsFrontMostAtPointer(eventData)) return;

        // ★ ここで操作ロックを取得（重複ドラッグ開始をブロック）
        _dragGateToken = InputGate.Begin();
        if (_dragGateToken < 0) return;

        // --- 以降：既存処理をそのまま ---
        originalParent = transform.parent;
        originalIndex = transform.GetSiblingIndex();
        originalLocalPos = transform.localPosition;
        originalLocalScale = transform.localScale;
        originalSortingOrder = spriteRenderer.sortingOrder;
        originalSiblingIndex = transform.GetSiblingIndex();

        draggingCards = new List<Transform>();
        if (originalParent != null && originalParent.CompareTag("TableauColumn"))
        {
            for (int i = originalIndex; i < originalParent.childCount; i++)
            {
                var child = originalParent.GetChild(i);
                var cb = child.GetComponent<CardBehaviour>();

                // 表向きのカードだけグループに追加し、各カードの元状態も保存
                if (cb != null && cb.Data.isFaceUp)
                {
                    cb.originalParent = child.parent;
                    cb.originalLocalPos = child.localPosition;
                    cb.originalLocalScale = child.localScale;
                    var srChild = child.GetComponent<SpriteRenderer>();
                    cb.originalSortingOrder = srChild ? srChild.sortingOrder : 0;
                    cb.originalSiblingIndex = child.GetSiblingIndex();

                    draggingCards.Add(child);
                }
                else
                {
                    break;
                }
            }
        }
        else
        {
            // 自分だけドラッグのときも念のため再保存
            this.originalParent = transform.parent;
            this.originalLocalPos = transform.localPosition;
            this.originalLocalScale = transform.localScale;
            this.originalSortingOrder = spriteRenderer.sortingOrder;
            this.originalSiblingIndex = transform.GetSiblingIndex();

            draggingCards.Add(transform);
        }

        draggingCardsStartPos = new Vector3[draggingCards.Count];
        for (int i = 0; i < draggingCards.Count; i++)
            draggingCardsStartPos[i] = draggingCards[i].position;

        pointerStartWorldPos = Camera.main.ScreenToWorldPoint(eventData.position);

        int maxOrder = int.MinValue;
        foreach (var tf in draggingCards)
        {
            var sr = tf.GetComponent<SpriteRenderer>();
            if (sr && sr.sortingOrder > maxOrder) maxOrder = sr.sortingOrder;
        }
        for (int i = 0; i < draggingCards.Count; i++)
        {
            var sr = draggingCards[i].GetComponent<SpriteRenderer>();
            if (sr) sr.sortingOrder = maxOrder + 1 + i;
        }
    }


    public void OnDrag(PointerEventData eventData)
    {
        if (CardFactory.Instance != null && CardFactory.Instance.isAutoCompleting)
            return;

        if (draggingCards == null) return;

        // ワールド座標差分を計算
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0;
        Vector3 delta = worldPos - pointerStartWorldPos;

        // 各カードを移動
        for (int i = 0; i < draggingCards.Count; i++)
            draggingCards[i].position = draggingCardsStartPos[i] + delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        try
        {
            // 0) 早期リターン条件
            if (eventData == null) return;
            if (CardFactory.Instance != null && CardFactory.Instance.isAutoCompleting) return;
            if (draggingCards == null || draggingCards.Count == 0) return; // そもそもドラッグしてない
            if (factory == null) { draggingCards = null; return; }

            // ① 物理ヒットでドロップ先を決定
            var cam = Camera.main;
            if (cam == null) { RestoreDragToOriginal(); PostCleanupAfterFail(); return; }

            Vector3 wp = cam.ScreenToWorldPoint(eventData.position);
            Vector2 p2 = new Vector2(wp.x, wp.y);
            Collider2D[] hits;
            try { hits = Physics2D.OverlapPointAll(p2); }
            catch { RestoreDragToOriginal(); PostCleanupAfterFail(); return; }

            Transform target = null;
            bool toFound = false;

            // Foundation 優先（単体のみ）
            if (draggingCards.Count == 1 && hits != null)
            {
                foreach (var h in hits)
                {
                    if (h == null) continue;
                    Transform t = h.transform;
                    while (t != null && !t.CompareTag("TableauColumn") && !t.CompareTag("FoundationSlot")) t = t.parent;
                    if (t != null && t.CompareTag("FoundationSlot") && CanMoveToFoundation(t))
                    {
                        target = t; toFound = true; break;
                    }
                }
            }

            // Tableau
            if (target == null && hits != null)
            {
                foreach (var h in hits)
                {
                    if (h == null) continue;
                    Transform t = h.transform;
                    while (t != null && !t.CompareTag("TableauColumn") && !t.CompareTag("FoundationSlot")) t = t.parent;
                    if (t != null && t.CompareTag("TableauColumn"))
                    {
                        bool ok = CanMoveToTableau(t);
                        if (ok) { target = t; break; }
                    }
                }
            }

            // ③ 成功
            if (target != null)
            {
                var sm = ScoreManager.Instance;
                int awarded = 0;
                var actions = new List<IUndoable>();

                if (toFound)
                {
                    // Foundation（単体）
                    if (foundationSfx != null && audioSource != null) audioSource.PlayOneShot(foundationSfx);

                    var tf = draggingCards[0];
                    if (tf == null) { RestoreDragToOriginal(); PostCleanupAfterFail(); return; }

                    tf.SetParent(target, false);                   // ← ローカル基準
                    factory.AdjustCardSize(tf.gameObject);         // ← ★ サイズ再計算
                    tf.localPosition = Vector3.zero;
                    tf.localRotation = Quaternion.identity;

                    FitColliderToSpriteSafely();
                    RefreshFoundationSlotSafely(target);

                    // +20（手動はコンボあり）
                    awarded = (sm != null)
                        ? sm.OnFoundationPlaced(tf.position, isAuto: false)
                        : ScoreManager.GetPoints(ScoreAction.FoundationMove);

                    // K 置き +50 も合算
                    var placedCb = tf.GetComponent<CardBehaviour>();
                    if (placedCb != null && placedCb.Rank == 13)
                    {
                        sm?.AwardSuitComplete(target.position);
                        awarded += ScoreManager.GetPoints(ScoreAction.ComboBonus);
                    }
                }
                else
                {
                    // =================== Tableau（複数OK） ===================
                    bool targetWasEmptyBefore = IsColumnEmptyOfCards(target); // “カード的に空”で判定

                    // ★ ローカル基準で親替え → 直後にサイズ再計算
                    foreach (var tf in draggingCards)
                    {
                        if (tf == null) continue;
                        tf.SetParent(target, false);
                        factory.AdjustCardSize(tf.gameObject);
                    }

                    RefreshTableauColumnSafely(target);

                    if (placeSounds != null && placeSounds.Length > 0)
                    {
                        if (nextPlaceSoundIndex < 0 || nextPlaceSoundIndex >= placeSounds.Length) nextPlaceSoundIndex = 0;
                        var clip = placeSounds[nextPlaceSoundIndex];
                        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
                        nextPlaceSoundIndex = (nextPlaceSoundIndex + 1) % placeSounds.Length;
                    }

                    // 抑止：逆戻り or ピンポン
                    bool suppress = false;
                    var topCb = draggingCards[0] ? draggingCards[0].GetComponent<CardBehaviour>() : null;
                    bool isKing = (topCb != null && topCb.Rank == 13);

                    // ★ K を“カード的に空”の列へ置く“初回”か？
                    bool kFirstToEmpty = (targetWasEmptyBefore && isKing && topCb != null && !topCb.kingEmptyOnceAwarded);
                    if (kFirstToEmpty)
                    {
                        // この手だけは抑止無視＆一度だけ許可
                        topCb.kingEmptyOnceAwarded = true;
                    }
                    else if (sm != null && topCb != null)
                    {
                        if (sm.IsReverseOfLastMove(topCb, originalParent, target) ||
                            sm.ShouldSuppressForPingPong(topCb, target))
                        {
                            suppress = true;
                            sm.BreakCombo();
                        }
                    }

                    // ★ スコア付与
                    if (!suppress && sm != null && draggingCards[0] != null)
                    {
                        if (kFirstToEmpty)
                        {
                            // 初回 K→空列：必ず +5（コンボ適用）
                            awarded = sm.AwardTableauReorder(draggingCards[0].position, isAuto: false);
                        }
                        else
                        {
                            // 通常：空列は0点、カードがある列なら+5
                            awarded = (!targetWasEmptyBefore)
                                ? sm.AwardTableauReorder(draggingCards[0].position, isAuto: false)
                                : 0;
                        }
                    }
                    else
                    {
                        awarded = 0;
                    }
                }

                // ---- 旧列自動めくり ----
                FlipAction flipAct = null;
                if (originalParent != null && originalParent.CompareTag("TableauColumn") && originalParent.childCount > 0)
                {
                    var lastTr = originalParent.GetChild(originalParent.childCount - 1);
                    if (lastTr != null)
                    {
                        var last = lastTr.GetComponent<CardBehaviour>();
                        if (last != null && last.Data != null && !last.Data.isFaceUp)
                        {
                            flipAct = new FlipAction(last);
                            last.Data.isFaceUp = true;
                            last.UpdateVisual();
                            RefreshTableauColumnSafely(originalParent);

                            var sm2 = ScoreManager.Instance;
                            if (sm2 != null)
                            {
                                sm2.AddScoreAt(ScoreAction.FlipCard, last.transform.position);
                                awarded += ScoreManager.GetPoints(ScoreAction.FlipCard); // +15 を合算
                            }
                        }
                    }
                }
                if (flipAct != null) actions.Add(flipAct);

                // ムーブ+1（手動ドラッグ）
                AwardMoveSafely();

                // MoveAction：スコア差分/ムーブ戻しは先頭のみ
                for (int i = 0; i < draggingCards.Count; i++)
                {
                    var tf = draggingCards[i];
                    if (tf == null) continue;

                    var cb = tf.GetComponent<CardBehaviour>();
                    if (cb == null) continue;

                    int delta = (i == 0) ? awarded : 0;
                    bool didCount = (i == 0);

                    var move = new MoveAction(
                        cb,
                        cb.originalParent,
                        target,
                        cb.originalLocalPos,
                        cb.originalLocalScale,
                        cb.originalSortingOrder,
                        cb.originalSiblingIndex,
                        delta,
                        didCount
                    );
                    actions.Add(move);
                }

                // Waste→Foundation のときの追加Draw
                if (toFound && originalParent == factory.wasteParent && factory.wasteParent != null && factory.wasteParent.childCount == 0)
                {
                    var nextData = factory.PeekNextCardData(); // nullでもOK
                    var nextGo = factory.DrawToWaste(recordUndo: false);
                    actions.Add(new DrawAction(factory, nextData, nextGo));
                }

                // 1回のCompositeとして記録
                if (UndoManager.Instance != null) UndoManager.Instance.Record(new CompositeAction(actions.ToArray()));

                // 次の“逆戻り”検出用にこの手を記録（手動）
                var topForNote = draggingCards[0] != null ? draggingCards[0].GetComponent<CardBehaviour>() : null;
                var smNote = ScoreManager.Instance;
                if (smNote != null && topForNote != null) smNote.NoteManualMove(topForNote, originalParent, target);

                // ★前進した時だけコンボ時間をリセット
                if (awarded > 0 || toFound || flipAct != null)
                {
                    smNote?.MarkManualComboTick();
                }

                if (toFound) factory.CheckVictory();
            }
            else
            {
                // ④ 失敗 → 完全復帰
                RestoreDragToOriginal();
                if (originalParent != null && originalParent.CompareTag("TableauColumn"))
                    RefreshTableauColumnSafely(originalParent);
            }

            // ⑤ 後片付け
            if (factory != null) factory.TryAutoComplete();
            draggingCards = null;

            // 軽いドラッグはタップ扱い
            float dragDistance = Vector2.Distance(eventData.position, pointerDownPos);
            float holdTime = Time.time - pointerDownTime;
            if (dragDistance < 10f && holdTime < 0.2f)
            {
                awardForTapMove = true;
                TryAutoMoveSafely();
                FindObjectOfType<NoMoveDetector>()?.CheckNow();
            }

            // ------- ローカル関数（元のまま＋一部安全化） -------
            void RestoreDragToOriginal()
            {
                if (draggingCards == null) return;
                foreach (var tf in draggingCards)
                {
                    if (tf == null) continue;
                    var cb = tf.GetComponent<CardBehaviour>();
                    if (cb != null && cb.originalParent != null) tf.SetParent(cb.originalParent, false);
                }
                draggingCards.Sort((a, b) =>
                {
                    var ca = a ? a.GetComponent<CardBehaviour>() : null;
                    var cb = b ? b.GetComponent<CardBehaviour>() : null;
                    int ia = ca != null ? ca.originalSiblingIndex : 0;
                    int ib = cb != null ? cb.originalSiblingIndex : 0;
                    return ia.CompareTo(ib);
                });
                foreach (var tf in draggingCards)
                {
                    if (tf == null) continue;
                    var cb = tf.GetComponent<CardBehaviour>();
                    if (cb != null)
                    {
                        tf.localPosition = cb.originalLocalPos;
                        tf.localScale = cb.originalLocalScale;
                        tf.SetSiblingIndex(cb.originalSiblingIndex);
                        var srd = tf.GetComponent<SpriteRenderer>();
                        if (srd != null) { srd.sortingOrder = cb.originalSortingOrder; srd.enabled = true; }
                    }
                    tf.gameObject.SetActive(true);
                }
            }
            void PostCleanupAfterFail()
            {
                if (originalParent != null && originalParent.CompareTag("TableauColumn"))
                    RefreshTableauColumnSafely(originalParent);
                draggingCards = null;
            }
            void FitColliderToSpriteSafely() { try { FitColliderToSprite(); } catch { } }
            void RefreshTableauColumnSafely(Transform col) { if (col == null) return; try { RefreshTableauColumn(col); } catch { } }
            void RefreshFoundationSlotSafely(Transform slot) { if (slot == null) return; try { RefreshFoundationSlot(slot); } catch { } }
            void AwardMoveSafely() { try { AwardMove(); } catch { } }
            void TryAutoMoveSafely() { try { TryAutoMove(); } catch { } }
        }
        finally
        {
            // ★ どんな経路（早期return含む）でも最後にゲートを解放
            if (_dragGateToken >= 0)
            {
                InputGate.End(_dragGateToken);
                _dragGateToken = -1;
            }
        }
    }

    public void ResetPerGameFlags()
    {
        kingEmptyOnceAwarded = false;
    }

    public void MoveToSlot(Transform slot)
    {
        // 1) ワールド空間でのスケール／回転を維持しつつ親を入れ替え
        transform.SetParent(slot, false);

        // 2) 全スロット共通：ドロップ後の枚数を取得 (1 枚目→1, 2 枚目→2…)
        int depth = slot.childCount;

        // 3) Tableau のみ Y オフセットを入れる
        if (slot.CompareTag("TableauColumn"))
        {
            float spacing = 0.3f;  // お好みで調整
            transform.localPosition = new Vector3(0f, -(depth - 1) * spacing, 0f);
        }
        else
        {
            // Foundation や Waste は位置をずらさない
            transform.localPosition = Vector3.zero;
        }

        // 4) 描画順を必ず上書き（layer も揃える）
        var sr = GetComponent<SpriteRenderer>();
        if (sr)
        {
            sr.sortingLayerID = GetTargetSortingLayerID(slot);

            if (slot.CompareTag("TableauColumn"))
            {
                // 下に行くほど手前にしたいなら i（=インデックス）でOK
                sr.sortingOrder = depth; // or depth - 1（お好み）
            }
            else if (slot.CompareTag("FoundationSlot"))
            {
                sr.sortingOrder = GetFoundationBaseOrder(slot) + depth;
            }
            else
            {
                sr.sortingOrder = 0; // Waste等のベース
            }
        }
    }

    public void ResetPosition()
    {
        // 親を戻し（localTransform を維持）
        transform.SetParent(originalParent, false);
        // ローカル位置／スケールを完全復元
        transform.localPosition = originalLocalPos;
        transform.localScale = originalLocalScale;
        // 子インデックスとソート順も戻す
        transform.SetSiblingIndex(originalSiblingIndex);
        spriteRenderer.sortingOrder = originalSortingOrder;
    }

    public bool CanMoveToFoundation(Transform targetSlot)
    {
        if (targetSlot == null) return false;

        // 1) FoundationSlotTag の suit とカードの Suit を必ず一致させる
        var tag = targetSlot.GetComponent<FoundationSlotTag>();
        if (tag == null) return false;

        Log.D($"[CanMoveToFoundation] card={Data.suit} target={tag.suit}");

        // スートが一致しないなら置けない
        if (tag.suit != Data.suit) return false;

        // 2) スロット内に別スートが紛れていたらブロック（壊れた山をこれ以上作らない）
        for (int i = 0; i < targetSlot.childCount; i++)
        {
            var cb = targetSlot.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null && cb.Suit != this.Suit) return false;
        }

        // 3) いま置ける“次のランク”を求める（同スートだけを見る）
        int top = 0; // 0=空, 1..13 = A..K
        for (int i = 0; i < targetSlot.childCount; i++)
        {
            var cb = targetSlot.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null && cb.Suit == this.Suit)
                top = Mathf.Max(top, cb.Rank);
        }

        // A から昇順に積む（A は 1、K は 13 前提）
        return this.Rank == top + 1;
    }

    private bool CanMoveToTableau(Transform column)
    {
        // 列の最後のカード情報を取得
        CardBehaviour lastCard = column.childCount > 0
            ? column.GetChild(column.childCount - 1).GetComponent<CardBehaviour>()
            : null;

        // 空列ならキングだけOK
        if (lastCard == null)
            return data.rank == 13;

        // 色が交互か？
        bool isOppositeColor =
            (data.suit == Suit.Hearts || data.suit == Suit.Diamonds)
            != (lastCard.Data.suit == Suit.Hearts || lastCard.Data.suit == Suit.Diamonds);

        // ランクが１つ下か？
        bool isOneLower = data.rank == lastCard.Data.rank - 1;

        return isOppositeColor && isOneLower;
    }

    private bool IsRed(Suit s)
    {
        return s == Suit.Hearts || s == Suit.Diamonds;
    }

    /// <summary>
    /// カードの見た目を更新。表向きなら GetCardSprite、裏向きなら Card_back をロード
    /// </summary>
    public void UpdateVisual()
    {
        Sprite sp;
        if (data.isFaceUp)
        {
            sp = factory.GetCardSprite(data);
        }
        else
        {
            sp = faceDownSprite; // ← ここが重要
        }

        // スプライトを適用
        spriteRenderer.sprite = sp;

        // ─── ここでコライダーをスプライトに合わせて再フィット ───
        if (boxCollider != null)
            FitColliderToSprite();
    }

    /// <summary>呼び出し側はこれだけ。true/false を渡すと自動で FlipWithAnimation を呼ぶ</summary>
    public void SetFaceUp(bool faceUp)
    {
        // ── 効果音を鳴らす ──
        if (faceUp && flipSound != null)
            audioSource.PlayOneShot(flipSound);

        FlipWithAnimation(faceUp);
    }

    /// <summary>
    /// スプライト切り替え＋ソート順・コライダー反映だけ行うヘルパー
    /// </summary>
    private void ApplyFaceUpVisual(bool faceUp)
    {
        data.isFaceUp = faceUp;

        // スプライト切り替え
        spriteRenderer.sprite = faceUp
            ? factory.GetCardSprite(data)
            : faceDownSprite;

        // ソート順切り替え（例として）
        //spriteRenderer.sortingOrder = faceUp ? 10 : 0;

        // コライダーをスプライトサイズに合わせ直す
        FitColliderToSprite();

        // フリップアニメーションのあと、列のソート順を正しく再構築
        var parent = transform.parent;
        if (parent != null && parent.CompareTag("TableauColumn"))
            RefreshTableauColumn(parent);
        else if (parent != null && parent.CompareTag("FoundationSlot"))
            RefreshFoundationSlot(parent);
    }

    public static void RefreshTableauColumn(Transform column)
    {
        if (column == null) return;

        // 参照スプライトのlayerを取得（無ければ "Cards"）
        var refSr = column.GetComponentInChildren<SpriteRenderer>();
        int layerId = refSr ? refSr.sortingLayerID : SortingLayer.NameToID("Cards");

        float y = 0f;
        for (int i = 0; i < column.childCount; i++)
        {
            var tf = column.GetChild(i);
            var cb = tf.GetComponent<CardBehaviour>();
            bool faceUp = (cb != null && cb.Data != null && cb.Data.isFaceUp);

            // ★ 親の縮尺影響を打ち消しつつカード幅に合わせてローカルスケールを正規化
            CardFactory.Instance?.AdjustCardSize(tf.gameObject);

            // 兄弟順・位置・描画順を整える
            tf.SetSiblingIndex(i);
            tf.localPosition = new Vector3(0f, -y, 0f);
            y += faceUp ? FACE_UP_OFFSET : FACE_DOWN_OFFSET;

            var sr = tf.GetComponent<SpriteRenderer>();
            if (sr)
            {
                sr.sortingLayerID = layerId;
                sr.sortingOrder = i;
            }
        }
    }


    private bool IsFrontMostAtPointer(UnityEngine.EventSystems.PointerEventData eventData)
    {
        var cam = Camera.main;
        if (cam == null) return true; // カメラ無いならブロックしない

        Vector2 wp = cam.ScreenToWorldPoint(eventData.position);
        var hits = Physics2D.OverlapPointAll(wp);

        int best = int.MinValue;
        CardBehaviour front = null;

        foreach (var h in hits)
        {
            var cb = h.GetComponent<CardBehaviour>();
            if (cb == null) continue;

            // ★同じ列（親）だけ比較するのがポイント
            if (cb.transform.parent != transform.parent) continue;

            int order = cb.spriteRenderer ? cb.spriteRenderer.sortingOrder : 0;
            if (order > best)
            {
                best = order;
                front = cb;
            }
        }

        // 同じ列のヒットが無ければ自分を許可（誤判定の保険）
        return (front == null) || (front == this);
    }

    // PointerEventData.position からワールド座標へ
    private static Vector2 ScreenToWorld(Vector2 screenPos)
    {
        var cam = Camera.main;
        return cam ? (Vector2)cam.ScreenToWorldPoint(screenPos) : screenPos;
    }

    // その地点にある TableauColumn / FoundationSlot を拾う（EventSystem には依存しない）
    private static (Transform column, Transform foundation) GetDropTargetsAt(Vector2 screenPos)
    {
        var world = ScreenToWorld(screenPos);
        var hits = Physics2D.OverlapPointAll(world);

        Transform column = null;
        Transform foundation = null;

        foreach (var h in hits)
        {
            var t = h.transform;
            if (t.CompareTag("TableauColumn")) column = t;
            else if (t.CompareTag("FoundationSlot")) foundation = t;
        }
        return (column, foundation);
    }

    public void TryAutoMoveForFactory(bool countMove = false)
    {
        // 内部の private フィールドに触れるのはこのクラス内でだけOK
        awardForTapMove = countMove;
        try
        {
            // 既存の TryAutoMove をそのまま使う（Foundation優先の自動移動）
            TryAutoMove();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TryAutoMoveForFactory] " + ex.Message);
        }
        finally
        {
            awardForTapMove = false; // 必ず原状復帰
        }
    }

    // 失敗ドロップ時にドラッググループを安全に元へ戻す
    private void RestoreDragGroupToOriginal()
    {
        if (draggingCards == null || draggingCards.Count == 0) return;

        // 親だけ先に戻す
        foreach (var tf in draggingCards)
        {
            var cb = tf.GetComponent<CardBehaviour>();
            if (cb && cb.originalParent) tf.SetParent(cb.originalParent, false);
        }

        // オリジナルの SiblingIndex 昇順で座標/順序復元
        draggingCards.Sort((a, b) =>
            a.GetComponent<CardBehaviour>().originalSiblingIndex
            .CompareTo(b.GetComponent<CardBehaviour>().originalSiblingIndex));

        foreach (var tf in draggingCards)
        {
            var cb = tf.GetComponent<CardBehaviour>();
            tf.localPosition = cb.originalLocalPos;
            tf.localScale = cb.originalLocalScale;

            // 念のため可視/レイヤ復帰
            var sr = tf.GetComponent<SpriteRenderer>();
            if (sr)
            {
                sr.sortingOrder = cb.originalSortingOrder;
                sr.enabled = true;
            }
            tf.gameObject.SetActive(true);

            tf.SetSiblingIndex(cb.originalSiblingIndex);
        }

        // 列の並び直しは1回だけ
        if (originalParent != null && originalParent.CompareTag("TableauColumn"))
            RefreshTableauColumn(originalParent);
    }

    public static void RefreshFoundationSlot(Transform slot)
    {
        if (!slot) return;

        // 基準Sprite（RefCardが無ければ最初に見つかったSpriteRenderer）を取得
        var refSr = slot.Find("RefCard")?.GetComponent<SpriteRenderer>();
        if (refSr == null) refSr = slot.GetComponentInChildren<SpriteRenderer>();
        int baseOrder = refSr ? refSr.sortingOrder : 100;
        int layerID = refSr ? refSr.sortingLayerID : SortingLayer.NameToID("Cards");

        // スロット内のカードだけ収集
        var cards = new List<Transform>();
        for (int i = 0; i < slot.childCount; i++)
        {
            var t = slot.GetChild(i);
            if (t.GetComponent<CardBehaviour>() != null) cards.Add(t);
        }

        // ★ ランク昇順（A=1 → K=13）に並べ替え
        cards.Sort((a, b) =>
        {
            var ca = a.GetComponent<CardBehaviour>();
            var cb = b.GetComponent<CardBehaviour>();
            int ra = ca ? Mathf.Clamp(ca.Rank, 1, 13) : 13;
            int rb = cb ? Mathf.Clamp(cb.Rank, 1, 13) : 13;
            return ra.CompareTo(rb);
        });

        // ★ 並び確定：位置はズラさず、兄弟順と描画順だけランクで決める
        foreach (var t in cards)
        {
            var cb = t.GetComponent<CardBehaviour>();
            int r = cb ? Mathf.Clamp(cb.Rank, 1, 13) : 13; // 1=A … 13=K

            // 兄弟順：A=0 … K=12
            t.SetSiblingIndex(r - 1);

            // 描画順：A<…<K で前面（refSrがあればそのレイヤに合わせる）
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr)
            {
                sr.sortingLayerID = layerID;
                sr.sortingOrder = baseOrder + r;
            }

            // 位置は完全に同一点（Zも0に）
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            var lp = t.localPosition;
            t.localPosition = new Vector3(lp.x, lp.y, 0f);
            t.localScale = t.GetComponent<CardBehaviour>()?.DefaultLocalScale ?? Vector3.one; // ★
        }

        // 背景などの並び順を再適用（既存）
        var bg = slot.GetComponent<StockRedealBackground>();
        if (bg) bg.ApplySortingOrder();
        var bgFit = slot.GetComponent<SlotBackgroundFitter>();
        if (bgFit) bgFit.ApplySortingOrder();
    }

    public IEnumerator AnimateMoveToSlot(Transform targetSlot, float duration)
    {
        if (targetSlot == null) yield break;

        var factory = CardFactory.Instance;
        bool isAuto = (factory != null && factory.isAutoCompleting);

        // Foundation への移動か？
        bool toFoundation = targetSlot.CompareTag("FoundationSlot");

        // ★保険：オート吸い込みで Foundation に入るとき、一度だけ +20 & コンボ
        if (isAuto && toFoundation && !_autoAwardedThisTween)
        {
            _autoAwardedThisTween = true;
            ScoreManager.Instance?.OnFoundationPlaced(transform.position, isAuto: true);
        }

#if DOTWEEN || DOTWEEN_PRESENT
    // 既存演出に寄せた緩急（必要なら調整可）
    yield return transform
        .DOMove(targetSlot.position, duration)
        .SetEase(DG.Tweening.Ease.InOutSine)
        .WaitForCompletion();
#else
        transform.position = targetSlot.position;
        yield return null;
#endif

        // 親付け替えと整列
        transform.SetParent(targetSlot, false);

        if (toFoundation)
        {
            // Foundation の重なり整列（あなたの既存ユーティリティ）
            CardBehaviour.RefreshFoundationSlot(targetSlot);
        }
        else if (targetSlot.CompareTag("TableauColumn"))
        {
            CardBehaviour.RefreshTableauColumn(targetSlot);
        }

        // このTweenでの保険フラグをリセット
        _autoAwardedThisTween = false;
    }


    // このクラス内に追加。あなたのプロジェクトのRank保持方法に合わせて実装。
    private int GetRankSafe()
    {
        // 1=A … 13=K（既存の Rank プロパティをそのまま使う）
        return Mathf.Clamp(Rank, 1, 13);
    }

    public void TryAutoMove()
    {
        // Stock上のカードは自動移動の対象外（必ずWaste経由）
        var parent = transform.parent;
        if (parent != null)
        {
            if (parent.CompareTag("StockSlot") ||
                (CardFactory.Instance != null && parent == CardFactory.Instance.stockPileObject.transform))
            {
                return;
            }
        }

        bool isAuto = (CardFactory.Instance != null && CardFactory.Instance.isAutoCompleting);
        if (!data.isFaceUp) return;

        // ★再入防止：ユーザー操作中に別経路から重ねて呼ばれたら弾く（オート時は通す）
        if (InputGate.Busy && !isAuto) return;

        var factory = FindObjectOfType<CardFactory>();
        if (factory == null) return;

        bool tapAward = awardForTapMove;   // この呼び出しで使い切り
        awardForTapMove = false;
        bool countMove = tapAward;         // 手動タップのときだけムーブ+1

        Transform oldParent = transform.parent;
        if (oldParent == null) return;

        // 移動開始インデックス（めくり対象はこの1つ手前）
        int movingStartIndex = transform.GetSiblingIndex();

        // 自分以降の移動カード
        var movingCards = new List<Transform>();
        bool startAdding = false;
        foreach (Transform child in oldParent)
        {
            if (child == transform) startAdding = true;
            if (startAdding && child.GetComponent<CardBehaviour>() != null)
                movingCards.Add(child);
        }
        if (movingCards.Count == 0) return;

        // =======================
        // Foundation（単体）
        // =======================
        if (movingCards.Count == 1)
        {
            foreach (var foundation in factory.foundationParents)
            {
                if (foundation == null) continue;
                if (!CanMoveToFoundation(foundation)) continue;

                var sm = ScoreManager.Instance;
                var stm = FindObjectOfType<ScoreTimerManager>();

                int awarded = sm != null
                    ? sm.OnFoundationPlaced(transform.position, isAuto)
                    : ScoreManager.GetPoints(ScoreAction.FoundationMove);

                if (this.Rank == 13)
                {
                    sm?.AwardSuitComplete(foundation.position);
                    awarded += ScoreManager.GetPoints(ScoreAction.ComboBonus);
                }

                // 旧列めくり（movingStartIndex-1）
                FlipAction flipAct = null;
                int flipIndex = movingStartIndex - 1;
                if (oldParent.CompareTag("TableauColumn") && flipIndex >= 0)
                {
                    var last = oldParent.GetChild(flipIndex).GetComponent<CardBehaviour>();
                    if (last != null && !last.Data.isFaceUp)
                    {
                        flipAct = new FlipAction(last);
                        last.Data.isFaceUp = true;
                        last.UpdateVisual();
                        RefreshTableauColumn(oldParent);

                        sm?.AddScoreAt(ScoreAction.FlipCard, last.transform.position);
                        awarded += ScoreManager.GetPoints(ScoreAction.FlipCard);
                    }
                }

                var actions = new List<IUndoable>();
                if (flipAct != null) actions.Add(flipAct);

                var sr = GetComponent<SpriteRenderer>();
                var move1 = new MoveAction(
                    this,
                    oldParent,
                    foundation,
                    transform.localPosition,
                    transform.localScale,
                    sr ? sr.sortingOrder : 0,
                    transform.GetSiblingIndex(),
                    awarded,
                    countMove
                );
                actions.Add(move1);
                UndoManager.Instance.Record(new CompositeAction(actions.ToArray()));

                if (countMove) stm?.AddMove();

                SetAutoMoving(true);

                // ★Tween寿命でゲートを保持（オート時は不要）
                int tweenToken = -1;
                if (!isAuto) tweenToken = InputGate.Begin();

#if DOTWEEN || DOTWEEN_PRESENT
            // ★前回の単体Tweenが残っていたらKill
            if (currentTween != null && currentTween.IsActive()) currentTween.Kill(false);

            currentTween = transform.DOMove(foundation.position, 0.3f)
                .SetEase(DG.Tweening.Ease.InOutCubic)
                .OnComplete(() =>
                {
                    transform.SetParent(foundation, false);       // ローカル基準
                    factory.AdjustCardSize(gameObject);           // ★ここで再計算
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    CardBehaviour.RefreshFoundationSlot(foundation);

                    SetAutoMoving(false);
                    if (tweenToken >= 0) InputGate.End(tweenToken);

                    // 後始末
                    currentTween = null;
                })
                // ★追加：中断（Kill等）でも必ず復帰
                .OnKill(() =>
                {
                    // OnCompleteが走らなくても操作可能に戻す
                    ForceEnableCollider();
                    SetAutoMoving(false);
                    if (tweenToken >= 0) InputGate.End(tweenToken);

                    currentTween = null;
                });
#else
                transform.position = foundation.position;
                transform.SetParent(foundation, false);       // ローカル基準
                factory.AdjustCardSize(gameObject);           // ★ここで再計算
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                CardBehaviour.RefreshFoundationSlot(foundation);
                SetAutoMoving(false);
                if (tweenToken >= 0) InputGate.End(tweenToken);
#endif
                return;
            }
        }

        // =======================
        // Tableau（複数OK）
        // =======================
        var _factory = FindObjectOfType<CardFactory>();
        bool _isAuto = _factory != null && _factory.isAutoCompleting;

        if (_isAuto)
        {
            // 1) オート中に Tableau への移動を全面禁止したい場合はここで return
            if (!_factory.allowTableauMovesDuringAuto) return;

            // 2) 許可している場合でも、Waste 由来の“逃がし”は原則禁止
            bool fromWasteAuto =
                (oldParent == _factory.wasteParent) ||
                (oldParent != null && oldParent.CompareTag("WasteSlot"));

            if (fromWasteAuto)
            {
                // 例外：K を“カード的に空”の列へ置くときだけ許可
                var top = (movingCards[0] != null) ? movingCards[0].GetComponent<CardBehaviour>() : null;
                bool isKing = (top != null && top.Rank == 13);

                bool hasEmptyColumn = false;
                if (isKing)
                {
                    foreach (var col in factory.columnParents)
                    {
                        if (col != null && IsColumnEmptyOfCards(col)) { hasEmptyColumn = true; break; }
                    }
                }

                if (!(isKing && hasEmptyColumn))
                {
                    // ← 無限ループ抑止
                    return;
                }
            }
        }

        foreach (var column in factory.columnParents)
        {
            if (column == null) continue;
            if (!CanMoveToTableau(column)) continue;

            var stm2 = FindObjectOfType<ScoreTimerManager>();
            var sm = ScoreManager.Instance;

            bool targetWasEmptyBefore = IsColumnEmptyOfCards(column); // ドラッグ側と同じ“カード的に空”判定へ

            // 加点：手動タップのみ +5（逆戻り/ピンポンは抑止）
            // さらに「K→空列」の“初回だけOK”をタップ移動にも適用
            int awarded = 0;
            if (countMove)
            {
                bool suppress = false;
                var topCb = (movingCards[0] != null) ? movingCards[0].GetComponent<CardBehaviour>() : null;
                bool isKing2 = (topCb != null && topCb.Rank == 13);

                // ★ K を“カード的に空”の列へ置く“初回”か？
                bool kFirstToEmpty = (targetWasEmptyBefore && isKing2 && topCb != null && !topCb.kingEmptyOnceAwarded);

                if (kFirstToEmpty)
                {
                    // この手だけは抑止無視＆一度だけ許可
                    topCb.kingEmptyOnceAwarded = true;
                }
                else if (sm != null && topCb != null)
                {
                    if (sm.IsReverseOfLastMove(topCb, oldParent, column) ||
                        sm.ShouldSuppressForPingPong(topCb, column) ||
                        (targetWasEmptyBefore && isKing2)) // ← “初回”以外の K→空列は抑止
                    {
                        suppress = true;
                        sm.BreakCombo();
                    }
                }

                if (!suppress && sm != null)
                {
                    // 初回 K→空列 も +5（コンボ適用）／通常は「空列=0、カード有り列=+5」
                    awarded = (kFirstToEmpty || !targetWasEmptyBefore)
                        ? sm.AwardTableauReorder(movingCards[0].position, isAuto: false)
                        : 0;
                }
                else
                {
                    awarded = 0;
                }
            }

            // 旧列めくり（movingStartIndex-1）
            FlipAction flipAct2 = null;
            int flipIndex2 = movingStartIndex - 1;
            if (oldParent.CompareTag("TableauColumn") && flipIndex2 >= 0)
            {
                var last = oldParent.GetChild(flipIndex2).GetComponent<CardBehaviour>();
                if (last != null && !last.Data.isFaceUp)
                {
                    flipAct2 = new FlipAction(last);
                    last.Data.isFaceUp = true;
                    last.UpdateVisual();
                    RefreshTableauColumn(oldParent);

                    sm?.AddScoreAt(ScoreAction.FlipCard, last.transform.position);
                    awarded += ScoreManager.GetPoints(ScoreAction.FlipCard);
                }
            }

            var actions = new List<IUndoable>();
            if (flipAct2 != null) actions.Add(flipAct2);

            bool moveCountGiven = false;
            for (int i = 0; i < movingCards.Count; i++)
            {
                var cardTf = movingCards[i];
                var cb2 = cardTf.GetComponent<CardBehaviour>();
                if (cb2 == null) continue;

                int delta2 = (i == 0) ? awarded : 0;
                bool counted2 = (!moveCountGiven && countMove);
                if (counted2) moveCountGiven = true;

                var move2 = new MoveAction(
                    cb2,
                    oldParent,
                    column,
                    cardTf.localPosition,
                    cardTf.localScale,
                    cb2.GetComponent<SpriteRenderer>()?.sortingOrder ?? 0,
                    cardTf.GetSiblingIndex(),
                    delta2,
                    counted2
                );
                actions.Add(move2);
            }
            UndoManager.Instance.Record(new CompositeAction(actions.ToArray()));

            if (countMove) stm2?.AddMove();

            SetAutoMoving(true);

            // ★Tween寿命でゲートを保持（オート時は不要）
            int tweenToken2 = -1;
            if (!isAuto) tweenToken2 = InputGate.Begin();

            // ★前回の連結Tweenが残っていたらKillしてクリア
            for (int i = 0; i < currentTweens.Count; i++)
            {
                var tw = currentTweens[i];
                if (tw != null && tw.IsActive()) tw.Kill(false);
            }
            currentTweens.Clear();

            int baseIndex = column.childCount;
            int pending = movingCards.Count;

            // 完了/中断の共通ハンドラ（どちらでも1枚分の終了処理）
            System.Action finishOne = () =>
            {
                pending--;
                if (pending == 0)
                {
                    RefreshTableauColumn(column);
                    SetAutoMoving(false);
                    if (tweenToken2 >= 0) InputGate.End(tweenToken2);

                    // 後始末
                    currentTweens.Clear();
                }
            };

            for (int k = 0; k < movingCards.Count; k++)
            {
                var card = movingCards[k];
                Vector3 targetPos = column.position + Vector3.down * 0.3f * (baseIndex + k);
#if DOTWEEN || DOTWEEN_PRESENT
            var tw = card.DOMove(targetPos, 0.3f)
                .SetEase(DG.Tweening.Ease.InOutCubic)
                .OnComplete(() =>
                {
                    card.SetParent(column, false);
                    // 親のスケール影響を打ち消して見かけサイズを即正規化
                    CardFactory.Instance?.AdjustCardSize(card.gameObject);
                    finishOne();
                })
                // ★追加：中断（Kill等）でも必ず復帰・集計
                .OnKill(() =>
                {
                    // Kill時点で親付けが済んでいない可能性もあるが、
                    // 少なくとも操作不能にならないよう復帰させる
                    var cb = card.GetComponent<CardBehaviour>();
                    if (cb) cb.ForceEnableCollider();
                    finishOne();
                });
            currentTweens.Add(tw);
#else
                card.position = targetPos;
                card.SetParent(column, false);
                // 親のスケール影響を打ち消して見かけサイズを即正規化
                CardFactory.Instance?.AdjustCardSize(card.gameObject);
                finishOne();
#endif
            }

            // 手動タップとしての“記録＆前進した時だけコンボ延長”
            if (countMove && sm != null)
            {
                var topCb = movingCards[0].GetComponent<CardBehaviour>();
                sm.NoteManualMove(topCb, oldParent, column);

                if (awarded > 0 || flipAct2 != null)
                    sm.MarkManualComboTick();
            }
            return;
        }

        // 動けない（フィードバック）
        if (invalidMoveSfx != null) cardAudioSource.PlayOneShot(invalidMoveSfx);
#if DOTWEEN || DOTWEEN_PRESENT
    transform.DOKill();
    Vector3 originalWorldPos = transform.position;
    Vector3 originalLocalPos = transform.localPosition;
    transform.DOPunchPosition(new Vector3(0.15f, 0.15f, 0), 0.3f, 10, 0.0f)
        .OnStart(() => { transform.position = originalWorldPos; transform.localPosition = originalLocalPos; })
        .OnComplete(() => { transform.position = originalWorldPos; transform.localPosition = originalLocalPos; });
#endif
    }


    public void TryAutoFlipOldColumn(Transform oldParent)
    {
        if (oldParent != null && oldParent.CompareTag("TableauColumn") && oldParent.childCount > 0)
        {
            var last = oldParent.GetChild(oldParent.childCount - 1).GetComponent<CardBehaviour>();
            if (last != null && !last.Data.isFaceUp)
            {
                // Undo 記録 → 実際に表へ
                //UndoManager.Instance.Record(new FlipAction(last));
                last.SetFaceUp(true);

                // ★ 表にしたカードの位置で +15 ポップアップ
                //ScoreManager.Instance?.AddScoreAt(ScoreAction.FlipCard, last.transform.position);
            }
        }

        // 既存のオート完了チェックは維持
        var factory = FindObjectOfType<CardFactory>();
        if (factory != null) factory.TryAutoComplete();
    }

    // 追加 or 置き換え: タップで自動移動をトリガ
    public void OnPointerUp(PointerEventData e)
    {
        // 早期リターン：無効なイベント
        if (e == null) return;

        // オート進行中はユーザー入力を受け付けない
        if (CardFactory.Instance != null && CardFactory.Instance.isAutoCompleting) return;

        // 自身のTween中（移動アニメ中）はタップ処理をしない
        if (DG.Tweening.DOTween.IsTweening(transform)) return;

        // 裏向きカードはタップ移動不可
        if (data != null && !data.isFaceUp) return;

        // 純粋な「タップ」だけを許可（距離・時間しきい値）
        bool isTap =
            (Vector2.Distance(e.position, _pressScreenPos) < tapMaxPx) &&
            ((Time.unscaledTime - _pressTime) < tapMaxTime);
        if (!isTap) return;

        // その山の最上段のカードのみ（誤タップで下のカードが反応しないように）
        if (!IsTopCardInPile()) return;

        // 既に他操作中なら弾く（ドラッグや別タップと競合しないように）
        int tapToken = InputGate.Begin();
        if (tapToken < 0) return;

        try
        {
            // この一手は“手動タップ”として扱う（ムーブ+1やスコア側の分岐用）
            awardForTapMove = true;

            // 既存の自動移動ロジックに委譲（Foundation優先 → Tableau）
            TryAutoMove();
        }
        finally
        {
            // どんな経路でも必ず解放
            InputGate.End(tapToken);
        }
    }


    public void FlipCard()
    {
        // ① Undo 用アクションを記録
        UndoManager.Instance.Record(new FlipAction(this));
        // ② 実際の表裏切り替え
        Data.isFaceUp = !Data.isFaceUp;
        UpdateVisual();
    }

    private static CardBehaviour FindTopFoundationCard(Transform slot)
    {
        if (!slot) return null;
        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            var cb = slot.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null) return cb;   // 背景やRefCardは無視
        }
        return null;
    }

    // === Standard Scoring helper (Microsoft風) ===
    // ・Waste→Tableau: +5
    // ・Tableau→Foundation: +10
    // ・Waste→Foundation: +10
    // ・Foundation→Tableau: -15
    static void AwardStandardOnManualMove(Transform fromParent, Transform toParent)
    {
        var sm = FindObjectOfType<ScoreManager>();
        if (sm == null) return;

        bool fromWaste = (fromParent != null) && (fromParent.CompareTag("WasteSlot") || fromParent.name.Contains("Waste"));
        bool fromTableau = (fromParent != null) && (fromParent.CompareTag("TableauColumn") || fromParent.name.Contains("Tableau"));
        bool fromFoundation = (fromParent != null) && (fromParent.CompareTag("FoundationSlot") || fromParent.name.Contains("Foundation"));
        
        bool toWaste = (toParent != null) && (toParent.CompareTag("WasteSlot") || toParent.name.Contains("Waste"));
        bool toTableau = (toParent != null) && (toParent.CompareTag("TableauColumn") || toParent.name.Contains("Tableau"));
        bool toFoundation = (toParent != null) && (toParent.CompareTag("FoundationSlot") || toParent.name.Contains("Foundation"));

        if (fromWaste && toTableau) { sm.AddScore(5); return; }
        if (fromTableau && toFoundation) { sm.AddScore(10); return; }
        if (fromWaste && toFoundation) { sm.AddScore(10); return; }
        if (fromFoundation && toTableau) { sm.AddScore(-15); return; }
        // 他の移動（Tableau↔Tableau 等）は 0 点
    }

    // 裏カードをめくった瞬間の+5点
    static void AwardFlipBonus()
    {
        var sm = FindObjectOfType<ScoreManager>();
        sm?.AddScore(5);
    }

    // 手数 +1（手動成功時のみ）
    static void AwardMove()
    {
        var stm = FindObjectOfType<ScoreTimerManager>();
        stm?.AddMove();
    }

    /// <summary>
    /// 指定の Tableau 列が「カード的に空」か判定する。
    /// （背景オブジェクトなどを無視して、実際に CardBehaviour が存在するかで判定）
    /// </summary>
    private static bool IsColumnEmptyOfCards(Transform col)
    {
        if (col == null) return false;
        for (int i = 0; i < col.childCount; i++)
        {
            if (col.GetChild(i).GetComponent<CardBehaviour>() != null)
                return false;
        }
        return true;
    }

    // 兄弟の中で一番上の「カード」か？（ラベルや装飾が混じってもOK）
    private bool IsTopCardInPile()
    {
        if (transform.parent == null) return true;

        // 親の中で「カードだけ」を数え、その中で自分が最後（最前面）かどうか
        int lastCardIndex = -1;
        int myIndex = -1;
        int n = transform.parent.childCount;

        for (int i = 0; i < n; i++)
        {
            var t = transform.parent.GetChild(i);
            var cb = t.GetComponent<CardBehaviour>();
            if (cb == null) continue;

            lastCardIndex = i;
            if (t == transform) myIndex = i;
        }
        return (myIndex >= 0 && myIndex == lastCardIndex);
    }

    public static void RefreshWasteSlot(Transform slot)
    {
        if (!slot) return;

        // 参照レイヤ（RefCard があれば合わせる）
        var refSr = slot.Find("RefCard")?.GetComponent<SpriteRenderer>();
        int layerID = refSr ? refSr.sortingLayerID : SortingLayer.NameToID("Cards");
        const int baseOrder = 500; // DrawToWaste と合わせる

        // CardBehaviour を持つ子だけ対象にする（RefCard等は無視）
        var cards = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < slot.childCount; i++)
        {
            var t = slot.GetChild(i);
            if (t.GetComponent<CardBehaviour>() != null) cards.Add(t);
        }

        // 並べ替え：兄弟順と描画順のみ調整（位置/回転/スケールは触らない）
        for (int i = 0; i < cards.Count; i++)
        {
            var t = cards[i];
            t.SetSiblingIndex(i);

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr)
            {
                sr.sortingLayerID = layerID;
                sr.sortingOrder = baseOrder + i;
            }
        }
    }

    private static int GetTargetSortingLayerID(Transform slot)
    {
        // Foundation/Waste/Tableau スロット内の参照Spriteからlayerを取得
        var refSr = slot.Find("RefCard")?.GetComponent<SpriteRenderer>()
                  ?? slot.GetComponentInChildren<SpriteRenderer>();
        // 見つからない場合は "Cards" レイヤー（適宜プロジェクトの名前に）
        return refSr ? refSr.sortingLayerID : SortingLayer.NameToID("Cards");
    }

    private static int GetFoundationBaseOrder(Transform slot)
    {
        // Foundationの重なり基準（RefCard 等があればそれに合わせる）
        var refSr = slot.Find("RefCard")?.GetComponent<SpriteRenderer>()
                  ?? slot.GetComponentInChildren<SpriteRenderer>();
        return refSr ? refSr.sortingOrder : 100; // デフォルトは100（お好みで）
    }

    // --- 強制的にコライダーを復活させるメソッド ---
    public void ForceEnableCollider()
    {
        if (boxCollider) boxCollider.enabled = true;
        isAutoMoving = false; // ← 内部フラグもリセット
    }

    // 念のため、Disable/Destroyでもコライダーを戻す
    private void OnDisable()
    {
        // 触れなくなる取り残しの復帰
        if (boxCollider) boxCollider.enabled = true;
        isAutoMoving = false;

        // ★ドラッグゲートの取りこぼし解除
        if (_dragGateToken >= 0)
        {
            InputGate.End(_dragGateToken);
            _dragGateToken = -1;
        }
    }

    private void OnDestroy()
    {
        if (boxCollider) boxCollider.enabled = true;
        isAutoMoving = false;
        if (_dragGateToken >= 0)
        {
            InputGate.End(_dragGateToken);
            _dragGateToken = -1;
        }
    }

    private void OnEnable()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider) boxCollider.enabled = true;
        isAutoMoving = false;
    }

    // 失敗後の後片付けでコライダーも明示的に復旧
    void PostCleanupAfterFail()
    {
        if (boxCollider) boxCollider.enabled = true;
        isAutoMoving = false;
        // 既存の Refresh などはそのまま
    }
}