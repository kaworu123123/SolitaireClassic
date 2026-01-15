using UnityEngine;

[ExecuteAlways]
public class KeepWorldTextConstantSize : MonoBehaviour
{
    public float targetWorldHeight = 0.35f;  // 画面上の見かけサイズ（好みで調整）
    public Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // 正投影カメラ前提：画面高さに対するワールド高さは orthographicSize*2
        float worldScreenH = cam.orthographicSize * 2f;
        if (worldScreenH <= 0f) return;

        // “画面の高さ=1.0” を基準に、一定見かけになるようスケール
        float scale = targetWorldHeight / worldScreenH;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}