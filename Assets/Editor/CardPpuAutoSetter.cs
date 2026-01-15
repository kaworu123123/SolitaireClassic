#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CardPpuAutoSetter
{
    [MenuItem("Tools/カードPPUを自動設定")]
    public static void SetCardPpu()
    {
        // ① 対象スプライトをパス指定
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cards/card_heart_1.png");
        // ※実際には「card_heart_1.png」が置かれているパスで揃えてください
        if (sprite == null)
        {
            Debug.LogError("対象スプライトが見つかりません。パスを確認してください。");
            return;
        }
        int spritePixelWidth = (int)sprite.rect.width;

        // ② カメラ情報・列数・隙間
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main Camera が見つかりません。");
            return;
        }
        float worldW = cam.orthographicSize * 2f * cam.aspect;
        int columns = 7;        // Tableau の列数
        float gap = 0.05f;      // カード間の隙間（ワールド単位）

        // ③ 必要な PPU を計算
        float totalGap = gap * (columns - 1);
        float desiredW = (worldW - totalGap) / columns;
        int newPpu = Mathf.RoundToInt(spritePixelWidth / desiredW);

        // ④ Import 設定に反映
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.spritePixelsPerUnit = newPpu;
        ti.SaveAndReimport();

        Debug.Log($"カードPPUを {newPpu} に設定しました");
    }
}
#endif
