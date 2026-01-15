using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;

public class CardFactory : MonoBehaviour
{
    [Header("カード生成設定")]
    public GameObject cardPrefab;
    public string resourcesPath = "Cards/Deck05";

    [Header("列親設定 (Tableau)")]
    [Tooltip("Tableau の各列を並べる親 Transform (要素数 = columns)")]
    public Transform[] columnParents;

    [Header("ストック／廃棄山設定")]
    [Tooltip("山札クリック時にめくったカードを置く親 Transform (Waste スロット)")]
    public Transform wasteParent;
    [Tooltip("山札の裏面表示用 GameObject (Stock スロットとしても使用)")]
    public GameObject stockPileObject;

    [Header("Foundation 設定")]
    [Tooltip("組札スロット４つの Transform (Foundation1…4) を順番にセット")]
    public Transform[] foundationParents;

    [Header("レイアウト設定")]
    [Tooltip("Tableau の列数 (通常 Klondike は 7)")]
    public int columns = 7;
    [Tooltip("カード間の隙間 (ワールド単位)")]
    public float gap = 0.05f;
    [Header("SlotBar 設定")]
    [Tooltip("Foundation／Waste／Stock を並べる Y 座標 (ワールド単位)")]
    public float slotRowY = 4.0f;
    [Tooltip("SlotBar 内アイテム間の水平隙間 (ワールド単位)")]
    public float slotGap = 0.1f;

    [Header("Tableau 配置設定")]
    [Tooltip("Tableau 列を並べる行の Y 座標 (ワールド単位)")]
    public float tableauRowY = -1.5f;  // Foundation／Stock 行より少し下あたり

    [Header("Tableau 配置設定（自動）")]
    [Tooltip("Foundation のすぐ下に並べたいときは 1.0 などカード高さに応じた値を設定")]
    public float rowSpacing = 1.2f;

    [Header("AutoComplete options")]
    public bool allowTableauMovesDuringAuto = false; // オート中のTableau移動を許可するならtrue

    // インスペクタで切り替え可能なフラグを追加（既存の "AutoComplete options" の近く）
    [Header("AutoComplete options")]
    public bool dumpAllStockAtAutoStart = false;   // ★追加: 既定 false で全Dumpをやめる

    // 配牌済みフラグ
    private bool _dealt = false;
    // 山札データ
    private List<CardData> deck;

    [Header("AutoComplete Speed")]
    [Range(0f, 1f)] public float autoTweenDuration = 0.12f;  // 1枚を動かす時間（既存0.25f→短縮）
    [Range(0f, 0.5f)] public float perCardDelay = 0.00f;   // 1枚置いた後の待ち（既存のまま or 0）

    private AudioSource audioSource;

    public static CardFactory Instance;
    public bool isAutoCompleting = false;
    private bool _autoRoutineRunning = false;

    // ★ 追加：オート中に一度でもリサイクルしたら true
    private bool _recycledDuringAuto = false;

    [SerializeField] private GameObject inputBlocker;

    // --- 追加: ハイスコアを保存するキー ---
    public const string HighScoreKey = "BestScore";


    private bool victoryHandled = false;

    private bool timeBonusGranted = false;    // ←追加

    private bool _autoRequestedFromVictory = false;  // CheckVictory からの再起動は一度だけ

    [Header("易しめ配り")]
    [Range(0, 10)]
    public int maxSameSuitPairsInFirst28 = 3; // 小さいほど易しい

    int _lastFoundationCount;
    float _lastProgressAt;

    private float _nextAutoAllowedAt = 0f;
    private bool _autoClearedThisDeal = false;


    private Coroutine _autoRoutine;

    void Update()
    {
        if (isAutoCompleting)
            AutoTickWatchdog();
        else
            TryStartAutoWhenReady();

        // ★ 勝利ウォッチャ（オート停止後・手動完了も拾う）
        if (!isAutoCompleting && !victoryHandled && CountFoundationCards() >= 52)
            CheckVictory();
    }

    public void StartAutoNow()
    {
        TryAutoComplete();
    }

    // どこかで明示的に起動（Tableauが全表/Waste空でOKな時）
    private void TryStartAutoWhenReady()
    {
        // すでにオート中なら何もしない
        if (isAutoCompleting) return;

        // ★強ガード：Tableau に裏カードが1枚でも残っていれば絶対に開始しない
        //   ついでに軽いクールダウンを入れて、短時間の連打・抖動を抑える
        if (HasAnyFaceDownInTableau())
        {
            // 既存のクールダウンより短い設定なら上書きしない（Maxで保護）
            float guardCooldown = 1.0f;
            _nextAutoAllowedAt = Mathf.Max(_nextAutoAllowedAt, Time.time + guardCooldown);
            return;
        }

        // クールダウン中は起動しない
        if (Time.time < _nextAutoAllowedAt) return;

        // すでにこのディールでオート完了済みなら再起動しない
        if (_autoClearedThisDeal) return;

        // 念のため：すでに全て上がっていれば起動しない
        if (CountFoundationCards() >= 52) return;

        // DOTween 全体ブロックはしない（UI/VFX で永遠に始まらないため）
        // 代わりに既存のオート可否判定を通す
        if (!ShouldAutoComplete())
        {
            // 小さめのクールダウンを挟んで再評価を頻発させない
            _nextAutoAllowedAt = Time.time + 0.5f;
            return;
        }

        Log.D("[AutoComplete] Start requested.");
        isAutoCompleting = true;
        StartCoroutine(AutoCompleteCoroutine());
    }

    void Start()
    {
        InputGate.ForceClear();
        victoryHandled = false;
        timeBonusGranted = false;

        Time.timeScale = 1f;
        ScoreTimerManager.Instance?.ResetAll();   // ←そのまま
        ScoreTimerManager.Instance?.StartTimer(); // ←そのまま

        deck = Deck.CreateEasedNew(
            maxSameSuitPairsInFirst28: 2,     // 序盤の同スート連続を強く抑制
            maxSameSuitPairsInWholeDeck: 10,   // 全体もやや抑える（8〜10くらい目安）
            maxBuriedCriticalInFaceDown: 1,   // A/2/3 が裏の山に埋まるのは最大1枚まで
            requireAtLeastOneCriticalFaceUp: true, // 表7枚のどれかに A/2/3 を1枚は置く
            maxTries: 5000                    // 試行回数を増やす
        );
        Deck.DebugDeckStats(deck);            // 先頭28/全体52の同スート連続が出ます

        // 2) StockPileObject を確保
        EnsureStockObject();

        // 3) スロット列を並べる
        ArrangeSlotRow();

        // 4) Tableau 列を並べる
        ArrangeColumns();

        // 5) Tableau の配牌
        DealTableau();

        // ★ ここ！配り終わった“直後”にリセット
        ResetKingFlagsAll();

        // 👇 ここに追加：Stock裏面スプライト設定
        if (stockPileObject != null)
        {
            var sr = stockPileObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Sprite back = GetBackSprite();
                if (back != null)
                {
                    sr.sprite = back;
                    Log.D("[Stock] 裏面スプライトを設定: " + back.name);

                    EnsureStockSorting();

                    SyncStockBackScale();
                }
            }
        }
    }

    void Awake()
    {
        // ① AudioSource を取得、なければ自動で追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        Instance = this;
    }

    // 勝利確定時に呼ぶ
    public void StartAutoCompleteAfterWin()
    {
        if (_autoRoutine != null) StopCoroutine(_autoRoutine);
        _autoRoutine = StartCoroutine(AutoCompleteLoop());
    }

    IEnumerator AutoCompleteLoop()
    {
        isAutoCompleting = true;
        bool prevAllowTableau = allowTableauMovesDuringAuto;
        allowTableauMovesDuringAuto = false; // Foundation 吸い込みに専念

        yield return null; // 1フレーム待って演出/スコア反映を落ち着かせる

        // ループ：動ける限り Foundation へ送り続ける
        while (true)
        {
            bool movedSomething = false;

            // 1) Waste のトップを優先して Foundation へ
            var wasteTop = GetTopCard(wasteParent);
            if (wasteTop && wasteTop.Data.isFaceUp)
            {
                int before = CountFoundationCards();   // ★ Foundation の総数を記録

                // 手数は加点しない（演出目的のため）
                wasteTop.TryAutoMoveForFactory(false);
                yield return WaitForMoveToFinish();

                int after = CountFoundationCards();    // ★ 移動後に数え直す
                movedSomething |= (after > before);    // 実際に置けたら true
            }

            // 2) Tableau 各列のトップ（最前面の表カード）を Foundation へ
            foreach (var col in columnParents)
            {
                var top = GetTopFaceUpCard(col);
                if (top == null) continue;

                top.TryAutoMoveForFactory(false);
                yield return WaitForMoveToFinish();
                // Foundation に乗れなかった場合は動かないので “movedSomething” は更新しない
                // （TryAutoMove は成立しなければ return で終わる想定）
            }

            // まだ動くかをざっくり判定：InputGate が空いていて、Waste/Tableau どちらにも Foundation に出せる見込みが無ければ終了
            // 簡易終了条件：直近で1回も移動が成立しなかったら抜ける
            //   → Waste優先 → 各列のトップ試行、の1周で何も起きない == もう手がない
            if (!movedSomething) break;
        }

        // 後始末
        allowTableauMovesDuringAuto = prevAllowTableau;
        isAutoCompleting = false;
        _autoRoutine = null;
    }

    // ---- ヘルパ ----

    // スロット内の“カード的トップ”を返す（末尾から CardBehaviour を探す）
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

    // Tableau の“最前面で表のカード”を返す
    private CardBehaviour GetTopFaceUpCard(Transform column)
    {
        if (!column) return null;
        for (int i = column.childCount - 1; i >= 0; i--)
        {
            var cb = column.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null && cb.Data.isFaceUp) return cb;
        }
        return null;
    }

    // 直近の移動（Tween/ゲート）が終わるまで待つ
    private IEnumerator WaitForMoveToFinish()
    {
        float t = 0f, timeout = 3.0f;
        while ((InputGate.Busy || DG.Tweening.DOTween.TotalPlayingTweens() > 0) && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(0.05f);
    }

    /// <summary>
    /// Stock／Waste／Foundation1～4 を横一列に配置する
    /// </summary>
    private void ArrangeSlotRow()
    {
        // Stock の Transform
        if (stockPileObject == null)
        {
            Log.W("[CardFactory] stockPileObject がアサインされていません");
            return;
        }
        var stockTf = stockPileObject.transform;
        var wasteTf = wasteParent;

        int foundationCount = foundationParents.Length;      // 4 のはず
        int slotCount = foundationCount + 2;          // 4 + Waste + Stock
        var cam = Camera.main;
        float worldW = cam.orthographicSize * 2f * cam.aspect;
        float totalGap = slotGap * (slotCount - 1);
        float slotW = (worldW - totalGap) / slotCount;
        float startX = -worldW / 2f + slotW / 2f;

        // 1) Foundation slots (i=0..foundationCount-1)
        for (int i = 0; i < foundationCount; i++)
        {
            float x = startX + i * (slotW + slotGap);
            //foundationParents[i].position = new Vector3(x, slotRowY, 0f);
            var t = foundationParents[i];
            t.position = new Vector3(x, slotRowY, 0f);
            t.localScale = Vector3.one;   // ★親スケールのリセット
        }

        // 2) Waste slot (index = foundationCount)
        {
            float x = startX + foundationCount * (slotW + slotGap);
            wasteTf.position = new Vector3(x, slotRowY, 0f);
            wasteTf.localScale = Vector3.one;   // ← 追加：親スケールをリセット
        }

        // 3) Stock slot
        {
            float x = startX + (foundationCount + 1) * (slotW + slotGap);
            stockTf.position = new Vector3(x, slotRowY, 0f);
            stockTf.localScale = Vector3.one;   // ← 追加：親スケールをリセット
        }

        if (stockPileObject) AdjustCardSize(stockPileObject);
        EnsureStockSorting();

        SyncStockBackScale();
    }

    // 山札の残り枚数を他から参照するための簡単なゲッター
    public int GetDeckCount()
    {
        return deck != null ? deck.Count : 0;
    }

    /// <summary>Tableau の列親を画面幅いっぱいに等間隔配置</summary>
    public void ArrangeColumns()
    {
        var cam = Camera.main;
        float worldW = cam.orthographicSize * 2f * cam.aspect;
        float totalGap = gap * (columnParents.Length - 1);
        float columnW = (worldW - totalGap) / columnParents.Length;
        float startX = -worldW / 2f + columnW / 2f;

        // Foundation の Y を基準に、自動で Tableau 行の Y を決定
        float foundationY = foundationParents.Length > 0
            ? foundationParents[0].position.y
            : 0f;
        float tableauY = foundationY - rowSpacing;

        for (int i = 0; i < columnParents.Length; i++)
        {
            Vector3 pos = columnParents[i].position;
            pos.x = startX + i * (columnW + gap);
            pos.y = tableauY;
            columnParents[i].position = pos;
            columnParents[i].localScale = Vector3.one;
        }
    }

    public void DealTableau()
    {
        if (_dealt) return;
        _dealt = true;

        int deckIndex = 0;
        for (int col = 0; col < columns; col++)
        {
            float y = 0f;
            for (int row = 0; row <= col; row++)
            {
                GameObject go = CreateCard(deck[deckIndex++]);
                var cb = go.GetComponent<CardBehaviour>();

                bool faceUp = (row == col);
                cb.SetFaceUp(faceUp);

                go.transform.SetParent(columnParents[col], false);
                go.transform.localPosition = new Vector3(0f, -y, 0f);


                AdjustCardSize(go);
                // 次のカードのオフセットを加算
                y += faceUp ? CardBehaviour.FACE_UP_OFFSET
                            : CardBehaviour.FACE_DOWN_OFFSET;

                // ソート順（下から上へ）
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr) sr.sortingOrder = row;
            }
        }
        deck.RemoveRange(0, deckIndex);
    }

    /// <summary>DrawToWaste／PeekNextCardData／UndoDraw／ResetWasteToStock…は既存のまま</summary>
    public CardData PeekNextCardData() => deck != null && deck.Count > 0 ? deck[0] : null;

    public GameObject DrawToWaste(bool recordUndo)
    {
        EnsureStockObject();
        if (deck == null) return null;

        // 山札空 → リサイクル
        if (deck.Count == 0)
        {
            if (wasteParent.childCount == 0) return null;
            ResetWasteToStock();

            _recycledDuringAuto = _recycledDuringAuto || isAutoCompleting; // ★ 追加

            ScoreManager.Instance?.AddScore(ScoreAction.StockRecycle);

            if (stockPileObject != null)
            {
                var sr0 = stockPileObject.GetComponent<SpriteRenderer>();
                if (sr0 != null) sr0.enabled = true;
            }
        }

        // 残り1枚 → 裏面非表示
        if (deck.Count == 1 && stockPileObject != null)
        {
            var sr1 = stockPileObject.GetComponent<SpriteRenderer>();
            if (sr1 != null) sr1.enabled = false;
        }

        // 1枚めくる
        CardData data = deck[0];
        deck.RemoveAt(0);

        GameObject go = CreateCard(data);                  // カード生成（標準サイズが入る）
        var beh = go.GetComponent<CardBehaviour>();

        // ① 先に Waste 配下へ
        go.transform.SetParent(wasteParent, false);
        go.transform.localPosition = Vector3.zero;

        // ② 親の影響は考えず “標準のカード幅” を再適用（←ココがポイント）
        AdjustCardSize(go);

        // ③ スケール確定後に Flip（演出がスケールを巻き戻しても正しい値）
        beh.SetFaceUp(true);

        // ④ ソート順
        int baseOrder = 500;
        int offset = wasteParent.childCount - 1;
        go.GetComponent<SpriteRenderer>().sortingOrder = baseOrder + offset;

        if (recordUndo)
            UndoManager.Instance.Record(new DrawAction(this, data, go));

        // ムーブ数+1（必要なら）
        var stm = FindObjectOfType<ScoreTimerManager>();
        if (stm != null) stm.AddMove();

        CardBehaviour.RefreshWasteSlot(wasteParent);

        return go;
    }



    // 引数なし版も同様に呼べるように
    public GameObject DrawToWaste()
    {
        return DrawToWaste(true);
    }

    public void UndoDraw(GameObject go, CardData data)
    {
        // Waste からカードを即時に削除
        DestroyImmediate(go);
        // 山札リストの先頭に CardData を戻す
        deck.Insert(0, data);
    }

    private void ResetWasteToStock()
    {
        if (wasteParent == null) return;
        if (deck == null) deck = new List<CardData>(52);

        int count = wasteParent.childCount;
        var recovered = new List<CardData>(count);

        for (int i = count - 1; i >= 0; i--)
        {
            var child = wasteParent.GetChild(i);
            if (child == null) continue;

            var cb = child.GetComponent<CardBehaviour>();
            if (cb != null && cb.Data != null)
            {
                recovered.Add(cb.Data);
            }
            else
            {
                Log.W($"[ResetWasteToStock] non-card child: {child.name}");
            }

            DestroyImmediate(child.gameObject);
        }

        recovered.Reverse();
        deck.AddRange(recovered);

        // Stock 裏面を再表示
        if (stockPileObject != null)
        {
            var sr = stockPileObject.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;

            AdjustCardSize(stockPileObject);
            EnsureStockSorting();
        }
    }

    public GameObject CreateCard(CardData data)
    {
        var go = Instantiate(cardPrefab);
        var beh = go.GetComponent<CardBehaviour>();
        beh.Initialize(data, this);
        AdjustCardSize(go);
        return go;
    }

    public void AdjustCardSize(GameObject cardGO)
    {
        var sr = cardGO.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        var cam = Camera.main;
        float worldW = cam.orthographicSize * 2f * cam.aspect;
        float totalGap = gap * (columns - 1);
        float desiredW = (worldW - totalGap) / columns;           // 画面幅から求めるカードの“見かけ幅”

        float spriteW = sr.sprite.bounds.size.x;
        float worldScale = desiredW / Mathf.Max(0.0001f, spriteW); // “ワールド上”で必要なスケール

        // ★親のスケールを打ち消して localScale を決定（親が 1 以外でも見かけサイズ一定）
        Vector3 pLoss = (cardGO.transform.parent != null) ? cardGO.transform.parent.lossyScale : Vector3.one;
        cardGO.transform.localScale = new Vector3(
            worldScale / Mathf.Max(0.0001f, pLoss.x),
            worldScale / Mathf.Max(0.0001f, pLoss.y),
            1f
        );

        var beh = cardGO.GetComponent<CardBehaviour>();
        if (beh != null) beh.DefaultLocalScale = cardGO.transform.localScale;
    }


    Sprite[] deck05Sprites;

    public Sprite GetCardSprite(CardData data)
    {
        // １度だけまとめ読み
        if (deck05Sprites == null)
        {
            deck05Sprites = Resources.LoadAll<Sprite>($"{resourcesPath}/Deck05");
            if (deck05Sprites == null || deck05Sprites.Length == 0)
                Debug.LogError($"[GetCardSprite] Deck05.png のサブスプライトが読み込めません: {resourcesPath}/Deck05");
        }

        // suit と rank から探したい名前を作る
        string suitName = data.suit switch
        {
            Suit.Clubs => "club",
            Suit.Diamonds => "diamond",
            Suit.Hearts => "heart",
            Suit.Spades => "spade",
            _ => data.suit.ToString().ToLower()
        };
        string key = $"card_{suitName}_{data.rank}";

        // 配列から同名のものを探す
        var sp = System.Array.Find(deck05Sprites, s => s.name == key);
        if (sp == null)
            Debug.LogError($"[GetCardSprite] サブスプライト {key} が見つかりません");
        return sp;
    }

    public Sprite GetBackSprite()
    {
        if (deck05Sprites == null)
        {
            deck05Sprites = Resources.LoadAll<Sprite>($"{resourcesPath}/Deck05");
        }
        Sprite back = System.Array.Find(deck05Sprites, s => s.name.ToLower().Contains("back"));
        if (back == null)
        {
            // エラーは残す（本当に異常な時だけ出る）
            Debug.LogError("[GetBackSprite] 裏面スプライトが見つかりません");
        }
        return back;
    }

    // Foundationに積まれているカード総数を数える
    private int CountFoundationCards()
    {
        int total = 0;
        foreach (var slot in foundationParents)
        {
            for (int i = 0; i < slot.childCount; i++)
            {
                if (slot.GetChild(i).GetComponent<CardBehaviour>() != null)
                    total++;
            }
        }
        return total;
    }

    public void CheckVictory()
    {
        int f = CountFoundationCards();

        if (!isAutoCompleting && f >= 48 && f < 52)
        {
            if (!_autoRequestedFromVictory && !_autoClearedThisDeal)
            {
                _autoRequestedFromVictory = true;

                // 直で StartAutoNow() せず、"準備が整ったら" に任せる
                // 裏カードがある限り起動しないので暴発しない
                TryStartAutoWhenReady();
            }
        }

        if (isAutoCompleting) return;
        if (victoryHandled) return;
        if (f < 52) return;

        victoryHandled = true;
        _autoClearedThisDeal = true;
        _autoRequestedFromVictory = false;

        ScoreTimerManager.Instance?.StopTimer();

        // ハイスコア処理だけ残す（SE再生は VictoryUIController に任せる）
        TryFinalizeAndUpdateHighScore();

        if (!timeBonusGranted)
        {
            Log.D("[CardFactory] time bonus granted (TODO: award)");
            timeBonusGranted = true;
        }

        // ★ SE再生は VictoryUIController に任せる
        var ui = FindObjectOfType<VictoryUIController>(true);
        if (ui != null) ui.ShowVictory();
        else Log.W("[CardFactory] VictoryUIController がシーンに見つかりません");
    }


    // ★ ハイスコア保存ロジックをこのキーで統一
    private bool TryFinalizeAndUpdateHighScore()
    {
        var sm = ScoreManager.Instance;
        if (sm == null) return false;

        int final = sm.CurrentScore;

        // ← 直書きの "HighScore" をやめて統一キーを使う
        int best = PlayerPrefs.GetInt(HighScoreKey, int.MinValue);
        bool isNew = final > best;
        if (isNew)
        {
            PlayerPrefs.SetInt(HighScoreKey, final);
            PlayerPrefs.Save();
        }
        return isNew;
    }

    /// <summary>
    /// stockPileObject が null のとき、StockClickHandler から探してセットします
    /// </summary>
    private void EnsureStockObject()
    {
        if (stockPileObject != null) return;
        var handler = FindObjectOfType<StockClickHandler>();
        if (handler != null)
            stockPileObject = handler.gameObject;
    }

    /// <summary>
    /// 山札（deck）が空かどうか
    /// </summary>
    public bool IsDeckEmpty()
    {
        return deck == null || deck.Count == 0;
    }

    /// <summary>
    /// 廃棄山（wasteParent）にカードがあるかどうか
    /// </summary>
    public bool HasWaste()
    {
        return wasteParent != null && wasteParent.childCount > 0;
    }

    /// <summary>
    /// 山札を再構成 (山札が空で、かつ廃棄山にカードがあればリサイクル)
    /// </summary>
    public void RecycleStock()
    {
        if (!IsDeckEmpty() || !HasWaste()) return;

        if (isAutoCompleting && _recycledDuringAuto) return; // ★ 追加

        ResetWasteToStock();

        _recycledDuringAuto = _recycledDuringAuto || isAutoCompleting; // ★ 追加

        // 位置は null セーフに
        Vector3 pos = (stockPileObject != null) ? stockPileObject.transform.position : Vector3.zero;
        ScoreManager.Instance?.AddScoreAt(ScoreAction.StockRecycle, pos);

        if (stockPileObject != null)
        {
            var sr = stockPileObject.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;
        }
    }

    /// <summary>
    /// 「残りはファウンデーションに積むしかない」状態か？
    /// ・廃棄山が 空 で
    /// ・各 Tableau 列の一番上カードが
    ///   すべて Foundation に置けるか（または列が空）である
    /// </summary>
    private bool ShouldAutoComplete()
    {
        // 1) Tableau に裏向きが残っていたら開始しない
        foreach (var col in columnParents)
        {
            if (col == null) continue;
            for (int i = 0; i < col.childCount; i++)
            {
                var cb = col.GetChild(i).GetComponent<CardBehaviour>();
                if (cb != null && cb.Data != null && !cb.Data.isFaceUp)
                    return false;
            }
        }

        // ★ stock / waste の有無は無視する
        // → オートコンプリート中に自動で処理してもらう

        return true;
    }

    /// <summary>
    /// ShouldAutoComplete が true のとき、残りのカードをすべて Foundation に積む
    /// </summary>
    // 使わない場合は Obsolete を付けておくと混入防止に役立ちます
    [System.Obsolete("Use AutoCompleteCoroutine instead")]
    public void AutoComplete()
    {
        inputBlocker.SetActive(true);
        isAutoCompleting = true;

        // カードをどんどん動かすループ
        while (true)
        {
            bool moved = false;

            // (1) 各 Tableau 列のトップからチェックして
            foreach (var col in columnParents)
            {
                if (col.childCount == 0) continue;

                var topTf = col.GetChild(col.childCount - 1);
                var cb = topTf.GetComponent<CardBehaviour>();
                if (cb == null) continue;

                // 置ける Foundation があれば
                foreach (var slot in foundationParents)
                {
                    if (cb.CanMoveToFoundation(slot))
                    {
                        // Move カウント増やさない（ふやすならコメントオン）
                        //var stm = FindObjectOfType<ScoreTimerManager>();
                        //if (stm != null) stm.AddMove();

                        // 実際に親を差し替え
                        cb.transform.SetParent(slot, false);
                        cb.transform.localPosition = Vector3.zero;
                        CardBehaviour.RefreshFoundationSlot(slot);

                        // 自動めくり＆再構築
                        if (col.CompareTag("TableauColumn") && col.childCount > 0)
                        {
                            var last = col.GetChild(col.childCount - 1)
                                         .GetComponent<CardBehaviour>();
                            if (last != null && !last.Data.isFaceUp)
                                last.SetFaceUp(true);
                        }
                        CardBehaviour.RefreshTableauColumn(col);

                        moved = true;
                        break;
                    }
                }
                if (moved) break;
            }

            if (!moved) break;
        }

        isAutoCompleting = false;
        if (inputBlocker) inputBlocker.SetActive(false);
    }

    public void TryAutoComplete()
    {
        if (isAutoCompleting) return;

        // ★ここで自前の ShouldAutoComplete をチェック
        if (!ShouldAutoComplete()) return;

        StartCoroutine(AutoCompleteCoroutine());
    }

    // IsClearStateReady は使わない or 保険だけにする（残すなら以下のようにより厳密に）
    private bool IsClearStateReady()
    {
        // 1) 全て表向き
        foreach (var col in columnParents)
        {
            if (col == null) continue;
            for (int i = 0; i < col.childCount; i++)
            {
                var cb = col.GetChild(i).GetComponent<CardBehaviour>();
                if (cb != null && !cb.Data.isFaceUp) return false;
            }
        }
        // 2) 厳密：ShouldAutoComplete と同義条件で最終確認
        return ShouldAutoComplete();
    }

    private IEnumerator AutoCompleteRoutine()
    {
        // 例：各カードを順に動かす処理
        yield return new WaitForSeconds(1f); // 仮の処理
        // 終了後
        isAutoCompleting = false;
    }

    // 山札を全部めくって Waste に積む（簡易版）
    // 新：自前の deck / DrawToWaste() を使って確実にWasteへ積む
    private IEnumerator DumpAllStockToWaste()
    {
        // deck が空になるまで 1枚ずつめくる
        while (!IsDeckEmpty())
        {
            DrawFromStockOne();      // = DrawToWaste(false) と同義
            yield return null;       // 画面更新のため1フレ待つ
        }
    }

    public System.Collections.IEnumerator AutoCompleteCoroutine()
    {
        // ---- 直前のTweenが落ち着くまで待つ（UI/VFX含めて最大 ~1秒弱）----
        int guard = 0;
        while (true)
        {
            int playing = 0;
            try { playing = DG.Tweening.DOTween.TotalActiveTweens(); }
            catch { playing = 0; } // DOTween未導入でも落ちないように

            if (playing == 0 || guard >= 60) break;

            guard++;
            yield return null;
        }

        // 二重起動ガード
        if (_autoRoutineRunning) yield break;
        _autoRoutineRunning = true;

        try
        {
            if (inputBlocker) inputBlocker.SetActive(true);
            isAutoCompleting = true;
            _recycledDuringAuto = false;

            // 「最初に全部Wasteへ吐き出す」方式は今回はOFF推奨
            if (dumpAllStockAtAutoStart)
                yield return StartCoroutine(DumpAllStockToWaste());

            // 進捗監視の初期化
            _lastFoundationCount = CountFoundationCards();
            _lastProgressAt = Time.time;

            bool moved = false;
            int safetyLoops = 0;

            int lastFoundation = _lastFoundationCount;
            int noProgressLoops = 0;
            const int NO_PROGRESS_LIMIT = 20;   // 進捗が全く無いループ上限

            // ===== メインループ（1ループ=最大1手だけ進める）=====
            while (true)
            {
                moved = false;

                // ========= ① Tableau 各列トップ → Foundation（最優先）=========
                foreach (var col in columnParents)
                {
                    if (col == null || col.childCount == 0) continue;

                    var topTf = col.GetChild(col.childCount - 1);
                    if (!topTf) continue;

                    var cb = topTf.GetComponent<CardBehaviour>();
                    if (!cb) continue;

                    Transform targetSlot = null;
                    foreach (var slot in foundationParents)
                    {
                        if (slot != null && cb.CanMoveToFoundation(slot))
                        {
                            targetSlot = slot;
                            break;
                        }
                    }
                    if (!targetSlot) continue;

                    // 1手だけ動かす
                    cb.RevealIfNeeded();
                    ScoreManager.Instance?.OnFoundationPlaced(cb.transform.position, isAuto: true);

                    yield return StartCoroutine(cb.AnimateMoveToSlot(targetSlot, autoTweenDuration));

                    cb.transform.SetParent(targetSlot, false);
                    AdjustCardSize(cb.gameObject);
                    cb.transform.localPosition = Vector3.zero;
                    cb.transform.localRotation = Quaternion.identity;
                    CardBehaviour.RefreshFoundationSlot(targetSlot);

                    if (cb.Rank == 13)
                        ScoreManager.Instance?.AwardSuitComplete(targetSlot.position);

                    // 元列の自動めくり（表返しは得点も付与）
                    if (col != null && col.CompareTag("TableauColumn") && col.childCount > 0)
                    {
                        var lastTr = col.GetChild(col.childCount - 1);
                        var lastCb = lastTr ? lastTr.GetComponent<CardBehaviour>() : null;
                        if (lastCb != null && lastCb.Data != null && !lastCb.Data.isFaceUp)
                        {
                            lastCb.Data.isFaceUp = true;
                            lastCb.UpdateVisual();
                            CardBehaviour.RefreshTableauColumn(col);
                            ScoreManager.Instance?.AddScoreAt(ScoreAction.FlipCard, lastCb.transform.position);
                        }
                    }

                    moved = true;
                    if (CountFoundationCards() >= 52) goto AUTO_END; // クリア
                    if (perCardDelay > 0f) yield return new WaitForSeconds(perCardDelay);
                    break; // 1ループ=1手で抜ける
                }

                // ========= ② Waste トップ → Foundation =========
                if (!moved && wasteParent != null && wasteParent.childCount > 0)
                {
                    var topTf = wasteParent.GetChild(wasteParent.childCount - 1);
                    var cb = topTf ? topTf.GetComponent<CardBehaviour>() : null;
                    if (cb != null)
                    {
                        Transform targetSlot = null;
                        foreach (var slot in foundationParents)
                        {
                            if (slot != null && cb.CanMoveToFoundation(slot))
                            {
                                targetSlot = slot;
                                break;
                            }
                        }

                        if (targetSlot != null)
                        {
                            ScoreManager.Instance?.OnFoundationPlaced(cb.transform.position, isAuto: true);

                            yield return StartCoroutine(cb.AnimateMoveToSlot(targetSlot, autoTweenDuration));

                            cb.transform.SetParent(targetSlot, false);
                            AdjustCardSize(cb.gameObject);
                            cb.transform.localPosition = Vector3.zero;
                            cb.transform.localRotation = Quaternion.identity;
                            CardBehaviour.RefreshFoundationSlot(targetSlot);

                            if (cb.Rank == 13)
                                ScoreManager.Instance?.AwardSuitComplete(targetSlot.position);

                            moved = true;
                            if (CountFoundationCards() >= 52) goto AUTO_END; // クリア
                            if (perCardDelay > 0f) yield return new WaitForSeconds(perCardDelay);
                        }
                    }
                }

                // ===== ③ 進捗監視＆デッキ回し（Tableau/Wasteで動けない時）=====
                int now = CountFoundationCards();
                if (now > lastFoundation)
                {
                    lastFoundation = now;
                    noProgressLoops = 0;
                    _recycledDuringAuto = false; // 進捗が出たら再リサイクル可
                }
                else
                {
                    if (!moved)
                    {
                        // (a) 山札が残っていれば 1枚ドロー → Waste へ
                        if (!IsDeckEmpty())
                        {
                            DrawToWaste(recordUndo: false);
                            moved = true;
                            if (perCardDelay > 0f) yield return new WaitForSeconds(perCardDelay);
                        }
                        // (b) 山札が空で Waste あり → リサイクル（Stock へ戻す）
                        else if (HasWaste())
                        {
                            RecycleStock();
                            _recycledDuringAuto = true;
                            moved = true;
                            yield return null; // 配置更新待ち
                        }
                        // (c) それでも動かない → 無進捗カウント
                        else
                        {
                            noProgressLoops++;
                            if (noProgressLoops >= NO_PROGRESS_LIMIT)
                            {
                                Log.W("[AutoComplete] No progress. Abort safely.");
                                break;
                            }
                        }
                    }
                }

                // ===== セーフティ =====
                safetyLoops++;
                if (safetyLoops > 800)
                {
                    Log.W("[AutoComplete] safety break");
                    break;
                }

                // 何も動いていなければ終了
                if (!moved) break;

                yield return null;
            }

        AUTO_END:
            // ---- 正常経路の終了処理 ----
            foreach (var col in columnParents) if (col) CardBehaviour.RefreshTableauColumn(col);
            foreach (var f in foundationParents) if (f) CardBehaviour.RefreshFoundationSlot(f);

            yield return null;
            yield return null;

            _autoRequestedFromVictory = false;
            if (CountFoundationCards() >= 52) _autoClearedThisDeal = true;

            CheckVictory();
        }
        finally
        {
            isAutoCompleting = false;
            _autoRoutineRunning = false;

            if (inputBlocker) inputBlocker.SetActive(false);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

            // 例外経路でも最低限の整列
            foreach (var col in columnParents) if (col) CardBehaviour.RefreshTableauColumn(col);
            foreach (var f in foundationParents) if (f) CardBehaviour.RefreshFoundationSlot(f);

            if (CountFoundationCards() >= 52) _autoClearedThisDeal = true;
        }
    }

    // ================== ヘルパ ==================

    // 見た目の移動（簡単な補間）。既存のアニメ関数があるなら差し替えてOK。
    private IEnumerator MoveCardToSlot(CardBehaviour cb, Transform slot)
    {
        cb.RevealIfNeeded();

        // 目標位置はスロットの現在枚数に応じて軽くオフセット（任意）
        int current = CountCardsInSlot(slot);
        Vector3 start = cb.transform.position;
        Vector3 end = slot.position + new Vector3(0f, -0.02f * current, 0f);

        const float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            cb.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / dur));
            yield return null;
        }

        cb.transform.SetParent(slot, false);
        cb.transform.localPosition = new Vector3(0f, -0.02f * current, 0f);
    }

    // スロット内の CardBehaviour の枚数（子に他のオブジェクトが混ざってもOK）
    private int CountCardsInSlot(Transform slot)
    {
        int n = 0;
        for (int i = 0; i < slot.childCount; i++)
            if (slot.GetChild(i).GetComponent<CardBehaviour>() != null) n++;
        return n;
    }

    // 各スートの山が13枚（A〜K）そろったかを厳格に確認
    private bool AllFoundationsCompleteBySuitCount(Dictionary<Suit, Transform> suitToSlot)
    {
        foreach (var kv in suitToSlot)
        {
            var slot = kv.Value;
            if (slot == null) return false;

            int count = 0;
            int maxRank = 0;
            for (int i = 0; i < slot.childCount; i++)
            {
                var cb = slot.GetChild(i).GetComponent<CardBehaviour>();
                if (cb == null) continue;
                if (cb.Suit != kv.Key) continue; // 別スートが紛れ込んでいたら未完とみなす
                count++;
                if (cb.Rank > maxRank) maxRank = cb.Rank;
            }

            // 13枚 & 一番上がK(=13)
            if (count < 13 || maxRank < 13) return false;
        }
        return true;
    }

    public void SetInputEnabled(bool enabled)
    {
        if (inputBlocker != null) inputBlocker.SetActive(!enabled);
    }

    public void DrawFromStockOne()
    {
        // オートコンプリート等から「1枚めくるだけ」で呼ぶ
        DrawToWaste(recordUndo: false);

        // ★ 追加：めくりも“進捗あり”として扱う
        if (isAutoCompleting)
            _lastProgressAt = Time.time;
    }

    private System.Collections.IEnumerator SafeMoveToFoundation(CardBehaviour cb, Transform targetSlot, float moveDuration = 0.25f)
    {
        if (cb == null || targetSlot == null) yield break;

        // ★ここで必ず加点（+20/コンボ/ポップ）
        ScoreManager.Instance?.OnFoundationPlaced(cb.transform.position, isAuto: true);

        // スート内で軽くずらす（見栄え）
        int orderInSuit = 0;
        for (int i = 0; i < targetSlot.childCount; i++)
        {
            var cbi = targetSlot.GetChild(i).GetComponent<CardBehaviour>();
            if (cbi != null && cbi.Suit == cb.Suit) orderInSuit++;
        }
        Vector3 end = targetSlot.position + new Vector3(0f, -0.02f * orderInSuit, 0f);

#if DOTWEEN || DOTWEEN_PRESENT
    yield return cb.transform.DOMove(end, moveDuration)
                             .SetEase(DG.Tweening.Ease.InOutSine)
                             .WaitForCompletion();
#else
        cb.transform.position = end;
        yield return null;
#endif

        cb.transform.SetParent(targetSlot, false);
        cb.transform.localPosition = Vector3.zero;   // ←完全に同じ位置に揃える

        // K を置いたらスート完成ボーナス（任意）
        if (cb.Rank == 13)
            ScoreManager.Instance?.AwardSuitComplete(targetSlot.position);

        // ★ 追加：Foundation へ置いたら進捗時刻を更新
        if (isAutoCompleting)
        {
            _lastFoundationCount = GetFoundationCount();
            _lastProgressAt = Time.time;
        }
    }

    private void ResetKingFlagsAll()
    {
        var all = FindObjectsOfType<CardBehaviour>(true);
        foreach (var cb in all)
        {
            cb.ResetPerGameFlags(); // ← CardBehaviour側で kingEmptyOnceAwarded = false;
        }
    }

    public void StartAuto()
    {
        isAutoCompleting = true;
        allowTableauMovesDuringAuto = false;
        _lastFoundationCount = GetFoundationCount();
        _lastProgressAt = Time.time;
    }

    public void AutoTickWatchdog()
    {
        // —— アニメ再生中はタイムアウトを進めない（安全版） ——
#if DOTWEEN || DOTWEEN_PRESENT
    // IsTweening(null) は使わない！
    int playing = 0;
    try { playing = DG.Tweening.DOTween.TotalPlayingTweens(); } catch { /* ignore */ }
    if (playing > 0)
    {
        _lastProgressAt = Time.time;
        return;
    }
#endif

        // Foundation が増えたら進捗更新
        int fc = GetFoundationCount();
        if (fc > _lastFoundationCount)
        {
            _lastFoundationCount = fc;
            _lastProgressAt = Time.time;
        }

        const float TIMEOUT = 6f; // 2→6秒など少し余裕
        if (Time.time - _lastProgressAt > TIMEOUT)
        {
            Log.W("[AUTO] No progress. Stop.");
            StopAuto();
        }
    }


    private int GetFoundationCount()
    {
        return CountFoundationCards();
    }

    // Watchdog などから呼ぶ停止API（未定義エラー解消 & 保険で入力も戻す）
    public void StopAuto()
    {
        // 二重呼び出しガード（実行中でなくても止める処理は有効なので return しない手もあります）
        if (!isAutoCompleting && !_autoRoutineRunning)
        {
            // 既に止まっているなら後処理だけ
            if (inputBlocker) inputBlocker.SetActive(false);
            return;
        }

        Log.W("[AUTO] Stopped by request.");

        // 実行フラグを落とす
        isAutoCompleting = false;
        allowTableauMovesDuringAuto = false;

        // 走っている自動処理（AutoCompleteCoroutine 含む）を停止
        StopAllCoroutines();
        _autoRoutineRunning = false;

        // 入力ブロッカー解除
        if (inputBlocker) inputBlocker.SetActive(false);

        // ★追加：Tween中断で取り残されたカードを一括で復帰
        RecoverAllCardsInteractivity();

        // ★ここがポイント：すぐ再起動しないようにクールダウン
        _nextAutoAllowedAt = Time.time + 1.5f;

        // ★最終整列や残りTweenの終了を待ってから勝利判定
        StartCoroutine(_DeferredVictoryCheck());

        _autoRequestedFromVictory = false;  // ← 解除
    }

    // ★補助：最終整列→勝利判定（2フレ待ち）
    private IEnumerator _DeferredVictoryCheck()
    {
        // 最終の並べ替えやTween完了を待つ
        yield return null;
        yield return null;

        // 念のため見た目の整列をもう一度
        foreach (var col in columnParents) if (col) CardBehaviour.RefreshTableauColumn(col);
        foreach (var f in foundationParents) if (f) CardBehaviour.RefreshFoundationSlot(f);

        // クリア済みフラグ（再起動抑止に使う）
        if (CountFoundationCards() >= 52)
            _autoClearedThisDeal = true;

        // 勝利判定発火（VictoryPanelの表示など）
        CheckVictory();
    }

    private void SyncStockBackScale()
    {
        if (stockPileObject == null) return;
        var sr = stockPileObject.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        // 裏面が未設定なら設定（既存の GetBackSprite を再利用）
        if (sr.sprite == null) sr.sprite = GetBackSprite();

        var cam = Camera.main;
        if (cam == null || sr.sprite == null) return;

        // AdjustCardSize() と同じロジックで希望幅→スケールを算出
        float worldW = cam.orthographicSize * 2f * cam.aspect;
        float totalGap = gap * (columns - 1);
        float desiredW = (worldW - totalGap) / columns;
        float spriteW = sr.sprite.bounds.size.x;
        float worldScale = desiredW / Mathf.Max(0.0001f, spriteW);

        // 親のスケールを打ち消して「見かけスケール」を合わせる
        var p = stockPileObject.transform.parent;
        Vector3 pLoss = (p != null) ? p.lossyScale : Vector3.one;
        stockPileObject.transform.localScale = new Vector3(
            worldScale / Mathf.Max(0.0001f, pLoss.x),
            worldScale / Mathf.Max(0.0001f, pLoss.y),
            1f
        );
    }

    private void EnsureStockSorting()
    {
        if (stockPileObject == null) return;
        var sr = stockPileObject.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        // カード用のレイヤーに合わせる（なければ "Default" でもOK）
        sr.sortingLayerID = SortingLayer.NameToID("Cards");
        // Waste の 500 より十分小さいが、背景よりは手前に出る値にする
        sr.sortingOrder = 50;
    }

    public bool HasAnyFaceDownInTableau()
    {
        foreach (var col in columnParents)
        {
            if (col == null) continue;
            for (int i = 0; i < col.childCount; i++)
            {
                var cb = col.GetChild(i).GetComponent<CardBehaviour>();
                if (cb != null && cb.Data != null && !cb.Data.isFaceUp)
                    return true;
            }
        }
        return false;
    }

    // --- 追加: すべてのカードの操作性を強制復帰させるヘルパ ---
    private void RecoverAllCardsInteractivity()
    {
        var all = FindObjectsOfType<CardBehaviour>(true);
        foreach (var cb in all)
        {
            // CardBehaviour.cs に追加したメソッド（前回案内）
            cb.ForceEnableCollider();
            // SetAutoMoving(false) も外から呼びたい場合は公開にして併用してOK
            // cb.SetAutoMoving(false);
        }
    }
}
