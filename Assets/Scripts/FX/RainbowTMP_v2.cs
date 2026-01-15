using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// TextMeshProUGUI に虹色グラデーションのアニメを付与するコンポーネント。
/// 文字ごとに HSV の H をずらして、時間経過で循環させます。
/// - playOnAwake=false なら、手動で Play()/Stop() で制御できます。
/// - Disable/Enable で停止/再開。Disable時は色を baseColor に戻します。
/// - TimeScaleの影響を受けないよう unscaledTime を使用。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TextMeshProUGUI))]
public class RainbowTMP_v2 : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("アニメ速度。1 で 1秒で一周。0 で停止。")]
    public float speed = 0.5f;

    [Tooltip("グラデーションの幅（文字数単位）。小さいほど細かく色が変化。")]
    [Min(0.1f)] public float gradientWidth = 8f;

    [Range(0f, 1f), Tooltip("彩度(S)。")] public float saturation = 1f;
    [Range(0f, 1f), Tooltip("明度(V)。")] public float value = 1f;

    [Tooltip("同一テキスト内の位相オフセット。複数重ねる時などに使う。")]
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Header("Control")]
    [Tooltip("有効化時に自動再生するか。")]
    public bool playOnAwake = true;

    [Header("Base Color")]
    [Tooltip("停止/無効化時に戻す色")]
    public Color baseColor = Color.yellow;

    TextMeshProUGUI _tmp;
    string _lastText;
    bool _dirtyMesh = true;
    bool _active = true; // Play/Stop で切り替えるフラグ

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        _active = playOnAwake;
        MarkDirty();
    }

    void OnEnable()
    {
        if (!_tmp) _tmp = GetComponent<TextMeshProUGUI>();
        SafeForceMeshUpdate();
        if (playOnAwake) _active = true;
        MarkDirty();
    }

    // ★ 無効化時のみ頂点色を復元（Destroy では触らない）
    void OnDisable() { RestoreVertexColorsSafe(); }

    // ★ OnDestroy では Mesh/TMP に触れない（破棄レース回避）
    void OnDestroy() { /* no-op: unsubscribe events here if you have any */ }

    void OnRectTransformDimensionsChange() => MarkDirty();
    void OnTransformParentChanged() => MarkDirty();
    public void MarkDirty() => _dirtyMesh = true;

    public void Play(float duration = -1f)
    {
        _active = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        if (duration > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(StopAfter(duration));
        }
    }

    public void Stop()
    {
        _active = false;
        RestoreVertexColorsSafe();
    }

    IEnumerator StopAfter(float sec)
    {
        yield return new WaitForSecondsRealtime(sec);
        Stop();
    }

    void Update()
    {
        if (_tmp == null) return;

        // テキスト変更検出
        if (_lastText != _tmp.text)
        {
            _lastText = _tmp.text;
            SafeForceMeshUpdate();
            _dirtyMesh = true;
        }

        if (_dirtyMesh)
        {
            Apply(true);   // 初回は即反映
            _dirtyMesh = false;
        }
        else if (speed != 0f && _active)
        {
            Apply(false);  // 色相だけ更新
        }
    }

    void Apply(bool forceRemesh)
    {
        if (!_active || _tmp == null) return;
        if (forceRemesh) SafeForceMeshUpdate();

        var textInfo = _tmp.textInfo;
        if (textInfo == null || textInfo.characterCount == 0) return;

#if UNITY_EDITOR
        float t = Application.isPlaying ? Time.unscaledTime : (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
        float t = Time.unscaledTime;
#endif
        float baseHue = (t * speed + phaseOffset) % 1f;

        // 文字単位で colors32 を更新
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var ch = textInfo.characterInfo[i];
            if (!ch.isVisible) continue;

            int mi = ch.materialReferenceIndex;
            int vi = ch.vertexIndex;

            // 安全ガード
            if (mi < 0 || mi >= textInfo.meshInfo.Length) continue;
            var colors = textInfo.meshInfo[mi].colors32;
            if (colors == null || colors.Length < vi + 4) continue;

            float hue = Mathf.Repeat(baseHue + (i / Mathf.Max(gradientWidth, 0.01f)), 1f);
            Color col = Color.HSVToRGB(hue, saturation, value);
            var c32 = (Color32)col;

            colors[vi + 0] = c32;
            colors[vi + 1] = c32;
            colors[vi + 2] = c32;
            colors[vi + 3] = c32;
        }

        // Mesh へ反映（各サブメッシュごと）
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            try
            {
                if (meshInfo.mesh == null || meshInfo.colors32 == null) continue;

                // 頂点数不一致のときはスキップ（版面が変わる瞬間など）
                if (meshInfo.mesh.vertexCount != meshInfo.colors32.Length) continue;

                meshInfo.mesh.colors32 = meshInfo.colors32;
                _tmp.UpdateGeometry(meshInfo.mesh, i);
            }
            catch (MissingReferenceException)
            {
                // Destroy レース時は安全に無視
            }
            catch { /* その他例外は無視 */ }
        }
    }

    // Mesh/TMP の生存と配列長を厳密チェックしてから色を戻す
    void RestoreVertexColorsSafe()
    {
        if (_tmp == null) return;

        // Editor 停止や再生切替などでも安全に Mesh 更新
        SafeForceMeshUpdate();

        var textInfo = _tmp.textInfo;
        if (textInfo == null || textInfo.meshInfo == null) return;

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            try
            {
                if (meshInfo.mesh == null || meshInfo.colors32 == null) continue;

                var colors = meshInfo.colors32;
                for (int c = 0; c < colors.Length; c++) colors[c] = (Color32)baseColor;

                // 頂点数不一致のときは無理に入れない
                if (meshInfo.mesh.vertexCount != colors.Length) continue;

                meshInfo.mesh.colors32 = colors;
                _tmp.UpdateGeometry(meshInfo.mesh, i);
            }
            catch (MissingReferenceException)
            {
                // 破棄済みならスキップ
            }
            catch
            {
                // その他は無視（ログ汚染を避ける）
            }
        }
    }

    // TMP の Mesh 強制更新を例外安全に
    void SafeForceMeshUpdate()
    {
        if (_tmp == null) return;
        try
        {
            // Editor でも Play 中でも Mesh を作り直す
            _tmp.ForceMeshUpdate();
        }
        catch { /* 無視 */ }
    }
}
