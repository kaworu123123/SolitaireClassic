using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform _rt;
    Rect _lastSafe;
    Vector2 _lastSize;

    void Reset() { Cache(); }
    void OnEnable()
    {
        Cache();
        ApplySafeArea();
        _lastSafe = Screen.safeArea;
        _lastSize = new Vector2(Screen.width, Screen.height);
    }

    void Cache()
    {
        if (_rt == null) _rt = transform as RectTransform;
    }

    void OnRectTransformDimensionsChange()
    {
        Cache();
        if (_rt == null || !isActiveAndEnabled) return;

        var sa = Screen.safeArea;
        var sz = new Vector2(Screen.width, Screen.height);
        if (sa != _lastSafe || sz != _lastSize)
        {
            ApplySafeArea();
            _lastSafe = sa;
            _lastSize = sz;
        }
    }

    public void ApplySafeArea()
    {
        Cache();
        if (_rt == null) return; // îOÇÃÇΩÇﬂÉKÅ[Éh

        var sa = Screen.safeArea;
        // 0èúéZÉKÅ[Éh
        float w = Mathf.Max(1, Screen.width);
        float h = Mathf.Max(1, Screen.height);

        Vector2 anchorMin = sa.position;
        Vector2 anchorMax = sa.position + sa.size;
        anchorMin.x /= w; anchorMin.y /= h;
        anchorMax.x /= w; anchorMax.y /= h;

        _rt.anchorMin = anchorMin;
        _rt.anchorMax = anchorMax;
        _rt.offsetMin = _rt.offsetMax = Vector2.zero;
    }
}
