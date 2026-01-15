using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class StockRedealBackground : MonoBehaviour
{
    [Header("Look & Feel")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("カード裏面(Stock)の SpriteRenderer。未設定なら親階層から自動取得")]
    [SerializeField] private SpriteRenderer referenceRenderer;
    [Tooltip("Inset: +で内側(小さく)、-で外側(大きく)")]
    [Range(-0.2f, 0.2f)] public float inset = 0.00f;
    [Range(0f, 1f)] public float inactiveAlpha = 0f;
    [Range(0f, 1f)] public float activeAlpha = 0.9f;
    [Tooltip("「山札が空 & Wasteに札あり」のときだけ表示")]
    public bool showOnlyWhenRedealable = true;

    [Header("Scale")]
    [Tooltip("全体倍率（見かけ幅に対して乗算）")]
    [Range(0.5f, 1.5f)] public float scaleMultiplier = 1.00f;

    [Header("Sorting")]
    [SerializeField] private SpriteRenderer target; // 未設定なら自分
    [SerializeField] private int orderOffset = -2;
    [SerializeField] private int orderOffsetWhenHidden = -10;

    [Header("Pulse (optional)")]
    public bool pulseWhenActive = true;
    [Range(0f, 3f)] public float pulseSpeed = 1.2f;
    [Range(0f, 0.25f)] public float pulseScale = 0.06f; // 非累積

    [Header("Stabilize")]
    [SerializeField] bool lockToParentCenter = true; // 毎フレーム localPosition=0 に固定
    [SerializeField] bool pixelSnap = false;         // デフォ OFF（ズレの原因になりやすい）

    // --- private
    SpriteRenderer _sr;
    CardFactory _factory;
    bool _isVisible;
    Vector3 _baseScale = Vector3.one;     // パルスの基準
    Transform _expectedParent;            // 想定する親（= Stock）

    void OnEnable()
    {
        _sr = GetComponent<SpriteRenderer>();
        _expectedParent = transform.parent;
        ResolveRefs();

        if (target == null) target = _sr;
        if (backgroundSprite) _sr.sprite = backgroundSprite;
        if (_factory == null) _factory = FindObjectOfType<CardFactory>();

        RecalcBase();
        ApplyVisibility(true);
        ApplySortingOrder();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        ResolveRefs();
        RecalcBase();
        ApplyVisibility(true);
        ApplySortingOrder();
    }

    void LateUpdate()
    {
        // 親が変えられても強制的に戻す（Waste へ“迷子”防止）
        if (_expectedParent && transform.parent != _expectedParent)
            transform.SetParent(_expectedParent, false);

        ResolveRefs();
        RecalcBase();
        ApplyVisibility(false);
        ApplySortingOrder();

        // 位置安定化
        if (lockToParentCenter)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        if (pixelSnap) PixelSnapToCamera();

        // 非累積パルス（毎フレーム base × wave を代入）
        if (Application.isPlaying && _isVisible && pulseWhenActive)
        {
            float t = Time.unscaledTime * Mathf.Max(0.01f, pulseSpeed) * Mathf.PI * 2f;
            float wave = 1f + pulseScale * Mathf.Sin(t);
            transform.localScale = _baseScale * wave;
        }
        else if ((transform.localScale - _baseScale).sqrMagnitude > 1e-8f)
        {
            transform.localScale = _baseScale;
        }
    }

    // --- helpers ---
    void ResolveRefs()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();

        if (referenceRenderer == null)
        {
            var p = transform.parent;
            if (p)
            {
                var pr = p.GetComponent<SpriteRenderer>();
                if (pr && pr != _sr) referenceRenderer = pr;
                else
                {
                    foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sr != _sr) { referenceRenderer = sr; break; }
                }
            }
        }
        if (target == null) target = _sr;
    }

    // 親ロッシースケールを打ち消して、見かけ幅でフィット
    void RecalcBase()
    {
        if (_sr == null || referenceRenderer == null) return;
        if (_sr.sprite == null || referenceRenderer.sprite == null) return;
        if (referenceRenderer == _sr) return; // 自己参照ガード

        float refWorldW = referenceRenderer.bounds.size.x; // 見かけ幅（ワールド）
        float ownW = _sr.sprite.bounds.size.x;        // 素の幅（スプライト）
        float targetW = refWorldW * (1f - inset) * scaleMultiplier;

        Vector3 pLoss = transform.parent ? transform.parent.lossyScale : Vector3.one;
        float worldScale = targetW / Mathf.Max(1e-6f, ownW);
        float s = worldScale / Mathf.Max(1e-6f, pLoss.x);

        _baseScale = new Vector3(s, s, 1f);

        if (!Application.isPlaying || !pulseWhenActive || !_isVisible)
            transform.localScale = _baseScale;
    }

    void ApplyVisibility(bool force)
    {
        if (_sr == null) return;

        bool show = true;
        if (showOnlyWhenRedealable)
        {
            if (_factory == null) _factory = FindObjectOfType<CardFactory>();
            if (_factory != null)
            {
                bool deckEmpty = _factory.IsDeckEmpty();
                bool hasWaste = _factory.HasWaste();
                show = deckEmpty && hasWaste;
            }
        }
        _isVisible = show;

        var c = _sr.color;
        float a = show ? activeAlpha : inactiveAlpha;
        if (!Mathf.Approximately(c.a, a)) { c.a = a; _sr.color = c; }
    }

    public void ApplySortingOrder()
    {
        if (target == null || referenceRenderer == null || target == referenceRenderer) return;

        target.sortingLayerID = referenceRenderer.sortingLayerID;
        target.sortingLayerName = referenceRenderer.sortingLayerName;

        int baseOrder = referenceRenderer.sortingOrder;
        target.sortingOrder = baseOrder + (_isVisible ? orderOffset : orderOffsetWhenHidden);
    }

    void PixelSnapToCamera()
    {
        if (_sr == null || _sr.sprite == null) return;
        var cam = Camera.main;
        if (!cam || !cam.orthographic) return;

        float ppu = _sr.sprite.pixelsPerUnit;
        if (ppu <= 0f) return;

        var wp = transform.position;
        float step = 1f / ppu;
        wp.x = Mathf.Round(wp.x / step) * step;
        wp.y = Mathf.Round(wp.y / step) * step;
        transform.position = wp;
    }
}
