using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ComboVfx : MonoBehaviour
{
    public static ComboVfx Instance { get; private set; }

    [Header("Prefabs / References")]
    [Tooltip("ワールド空間で弾けるスパークのParticleSystemプレハブ")]
    public ParticleSystem sparkPrefab;

    [Header("Tuning")]
    [Tooltip("ストリークが上がるほど発生数を増やす: base + perLevel*(streak-1)")]
    public int baseBurst = 12;
    public int perLevel = 6;
    public int maxBurst = 48;

    [Tooltip("ストリークに応じて色を変える（未設定なら白）")]
    public Gradient colorByStreak;

    [Tooltip("スパークのスケール補正")]
    public float sizeMul = 1f;

    public enum ComboColorMode { Gradient, Array, Profile }

    [Header("Color Source")]
    public ComboColorMode colorMode = ComboColorMode.Gradient;

    [Tooltip("段階色を固定配列で管理（index = combo-1）")]
    [SerializeField] private Color[] comboColors;

    [Tooltip("しきい値ベースで色管理したい場合（ScriptableObject）")]
    [SerializeField] private ComboVfxProfile colorProfile;

    [SerializeField] int poolSize = 12;
    Queue<ParticleSystem> sparkPool;

    void Start()
    {
        if (sparkPrefab && (sparkPool == null))
        {
            sparkPool = new Queue<ParticleSystem>(poolSize);
            for (int i = 0; i < poolSize; i++)
            {
                var ps = Instantiate(sparkPrefab);
                ps.gameObject.SetActive(false);
                sparkPool.Enqueue(ps);
            }
        }
    }

    ParticleSystem Rent(Queue<ParticleSystem> q, ParticleSystem prefab, Vector3 pos)
    {
        var ps = (q != null && q.Count > 0) ? q.Dequeue() : Instantiate(prefab);
        ps.transform.position = pos;
        ps.gameObject.SetActive(true);
        return ps;
    }

    void Return(Queue<ParticleSystem> q, ParticleSystem ps, float delay)
    {
        StartCoroutine(ReturnAfter(ps, delay, q));
    }
    IEnumerator ReturnAfter(ParticleSystem ps, float t, Queue<ParticleSystem> q)
    {
        yield return new WaitForSeconds(t);
        ps.gameObject.SetActive(false);
        q?.Enqueue(ps);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (sparkPrefab == null)
            Log.W("[ComboVfx] sparkPrefab 未割り当て。演出は出ません。");
    }

    /// <summary>カード付近でスパーク（ストリークに応じて強度UP）</summary>
    public void PlayCardSparkAt(Vector3 worldPos, int comboStreak)
    {
        if (sparkPrefab == null) { Log.W("[ComboVfx] sparkPrefab=NULL"); return; }

        Log.D($"[VFX] PlayCardSparkAt mode={colorMode} combo={comboStreak} colors={(comboColors != null ? comboColors.Length : 0)}");

        var ps = Instantiate(sparkPrefab, worldPos, Quaternion.identity);
        var main = ps.main;
        var emission = ps.emission;

        // --- 色の決定 ---
        Color chosen = Color.white;
        switch (colorMode)
        {
            case ComboColorMode.Profile:
                chosen = (colorProfile != null) ? colorProfile.GetColorForCombo(comboStreak) : Color.white;
                break;

            case ComboColorMode.Array:
                if (comboColors != null && comboColors.Length > 0)
                {
                    int idx = Mathf.Clamp(comboStreak - 1, 0, comboColors.Length - 1);
                    chosen = comboColors[idx];

                    // ★ ここで実際に何色を選んだかログに出す（数値確認用）
                    Log.D($"[VFX] ARRAY idx={idx} color=({chosen.r:F2},{chosen.g:F2},{chosen.b:F2},{chosen.a:F2})");

                    // ★ もし配列の色が全部ほぼ同じ（=見分けづらい）なら、自動パレットで差を付ける
                    //   - 最初の要素とほとんど同じ（±2%以内）の場合にだけ上書き
                    if (ApproximatelyEqual(chosen, comboColors[0], 0.02f))
                    {
                        chosen = AutoVividColorByIndex(idx);
                        Log.D($"[VFX] ARRAY auto-vivid override -> ({chosen.r:F2},{chosen.g:F2},{chosen.b:F2},{chosen.a:F2})");
                    }
                }
                else
                {
                    chosen = Color.white;
                }
                break;

            default: // Gradient
                if (colorByStreak != null && colorByStreak.colorKeys != null && colorByStreak.colorKeys.Length > 0)
                {
                    float t = Mathf.Clamp01((comboStreak - 1) / 4f);
                    chosen = colorByStreak.Evaluate(t);
                }
                else
                {
                    chosen = Color.white;
                }
                break;
        }

        // startColor 反映
        main.startColor = chosen;

        // Color over Lifetime が有効でも単色に固定（Prefab側の上書きを抑止）
        var col = ps.colorOverLifetime;
        if (col.enabled)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(chosen, 0f),
                    new GradientColorKey(chosen, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        // 発生数（連鎖で増強）
        int count = Mathf.Clamp(baseBurst + perLevel * (comboStreak - 1), baseBurst, maxBurst);
        var burst = new ParticleSystem.Burst(0f, (short)count);
        if (emission.burstCount > 0) emission.SetBurst(0, burst);
        else emission.SetBursts(new ParticleSystem.Burst[] { burst });

        // サイズ補正
        main.startSizeMultiplier = main.startSizeMultiplier *
                                   Mathf.Lerp(1f, 1.6f, Mathf.Clamp01((comboStreak - 1) / 4f)) *
                                   sizeMul;

        ps.Play();

        // 寿命に合わせて破棄
        float life = 1f;
        var lifeCurve = main.startLifetime;
        switch (lifeCurve.mode)
        {
            case ParticleSystemCurveMode.Constant: life = lifeCurve.constant; break;
            case ParticleSystemCurveMode.TwoConstants: life = lifeCurve.constantMax; break;
            default: life = 1f; break;
        }
        Destroy(ps.gameObject, life + 2f);
    }

    private static bool ApproximatelyEqual(Color a, Color b, float eps = 0.02f)
    {
        return Mathf.Abs(a.r - b.r) <= eps &&
               Mathf.Abs(a.g - b.g) <= eps &&
               Mathf.Abs(a.b - b.b) <= eps;
    }

    // インデックスに応じて強めの色を自動割り当て（2,3,4…で確実に違う色に）
    private static Color AutoVividColorByIndex(int idx)
    {
        // 代表色のローテーション（必要に応じて好みで変更可）
        // 0=1手目用, 1=2手目用, 2=3手目用, ...
        Color[] palette = new[]
        {
        new Color(1f, 1f, 1f, 1f), // combo=1 (参考)
        new Color(0.20f, 0.80f, 1.00f, 1f), // 2: シアン系
        new Color(1.00f, 0.30f, 0.30f, 1f), // 3: 赤系
        new Color(0.20f, 1.00f, 0.40f, 1f), // 4: 緑系
        new Color(1.00f, 0.90f, 0.30f, 1f), // 5: 黄系
        new Color(1.00f, 0.40f, 0.90f, 1f), // 6: マゼンタ系
        new Color(0.70f, 0.50f, 1.00f, 1f), // 7: 紫
    };
        if (idx < palette.Length) return palette[idx];

        // それ以上はHSVで回す（派手め＆白飛びしにくいようにV下げ／S上げ）
        float h = (idx * 0.15f) % 1f;
        float s = 0.95f;
        float v = 0.70f;
        var c = Color.HSVToRGB(h, s, v);
        c.a = 1f;
        return c;
    }
}
