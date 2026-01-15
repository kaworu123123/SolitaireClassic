using UnityEngine;

public class ComboSparkManager : MonoBehaviour
{
    public static ComboSparkManager Instance { get; private set; }

    [Header("Spark Prefab (ParticleSystem root)")]
    public ParticleSystem sparkPrefab;

    [Header("Color Profile")]
    public ComboVfxProfile profile;

    [Header("Pool Settings")]
    [Min(0)] public int preloadCount = 8;
    private ParticleSystem[] pool;
    private int poolIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (sparkPrefab == null) { Log.W("[ComboSparkManager] sparkPrefab is null"); return; }
        pool = new ParticleSystem[Mathf.Max(1, preloadCount)];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = Instantiate(sparkPrefab, transform);
            pool[i].gameObject.SetActive(false);
        }
    }

    public void PlayAt(Vector3 worldPos, int combo)
    {
        if (sparkPrefab == null) return;

        var ps = GetNext();
        ps.transform.position = worldPos;

        // 色決定
        Color c = (profile != null) ? profile.GetColorForCombo(combo) : Color.white;

        // Main.startColor をコンボ色に
        var main = ps.main;
        main.startColor = c;

        // ColorOverLifetime も単色に寄せたい場合は追加（任意）
        var col = ps.colorOverLifetime;
        if (col.enabled)
        {
            // 単色グラデーション
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(c, 0f),
                    new GradientColorKey(c, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        ps.gameObject.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();
    }

    private ParticleSystem GetNext()
    {
        if (pool == null || pool.Length == 0)
            return Instantiate(sparkPrefab, transform);

        poolIndex = (poolIndex + 1) % pool.Length;
        return pool[poolIndex];
    }
}
