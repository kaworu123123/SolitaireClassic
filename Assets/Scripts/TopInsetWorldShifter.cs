using UnityEngine;
using System.Collections;

public class TopInsetWorldShifter : MonoBehaviour
{
    [SerializeField] Camera targetCamera;        // 未指定なら Start で Camera.main
    [SerializeField] Transform worldRoot;        // 盤面の親（WorldRoot など）
    [SerializeField] int extraPaddingPx = 0;     // UIと同じ分を追い足し（例: 50）
    [SerializeField] float worldShiftMultiplier = 1f; // 必要なら倍率調整

    Vector3 basePos;
    bool ready = false;

    void Start()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!worldRoot) worldRoot = transform;

        // 1フレーム待って、他の初期化が済んだ後の“実際の初期位置”を基準にする
        StartCoroutine(CaptureBaseNextFrame());
    }

    IEnumerator CaptureBaseNextFrame()
    {
        yield return null;
        basePos = worldRoot.position;
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready || targetCamera == null || worldRoot == null) return;

        // ★ イベントではなく毎フレーム 直接ポーリング ★
        int insetPx = Mathf.Max(0, AdBannerController.LastTopInsetPx) + Mathf.Max(0, extraPaddingPx);

        // 1pxがワールドで何ユニットかを都度計算
        float unitsPerPixel = CalcUnitsPerPixel();
        float dy = insetPx * unitsPerPixel * Mathf.Max(0.0001f, worldShiftMultiplier);

        // 最後に必ず上書き（他のUpdate系の動作より後に効く）
        worldRoot.position = basePos - new Vector3(0f, dy, 0f);

        // 必要ならデバッグ
        // Log.D($"[WorldShift] insetPx={insetPx}, dy={dy}, posY={worldRoot.position.y}");
    }

    float CalcUnitsPerPixel()
    {
        if (targetCamera.orthographic)
        {
            // 直交カメラ：画面のワールド高さ = orthoSize * 2
            return (targetCamera.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
        }
        else
        {
            // 透視カメラ：worldRootまでの深度での画面ワールド高さから換算
            var toRoot = worldRoot.position - targetCamera.transform.position;
            float zDist = Mathf.Max(0.01f, Vector3.Dot(toRoot, targetCamera.transform.forward));
            float fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;
            float worldH = 2f * Mathf.Tan(fovRad * 0.5f) * zDist;
            return worldH / Mathf.Max(1, Screen.height);
        }
    }
}
