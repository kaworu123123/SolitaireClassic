// Assets/Scripts/Bootstrap/PerformanceBootstrap.cs
using UnityEngine;

public class PerformanceBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply()
    {
        // FPS制御：vSync切って端末上限に依存しないよう固定（30 or 60）
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = SystemInfo.batteryLevel < 0.2f ? 30 : 60;

        // 低端末判定（メモリ/CPUコア/Graphics系でざっくり）
        bool low =
            SystemInfo.systemMemorySize <= 3000 ||
            SystemInfo.processorCount <= 4 ||
            SystemInfo.graphicsShaderLevel < 35;

        // VFX/描画の簡易プリセット
        if (low)
        {
            // 2DならMSAA不要
            QualitySettings.antiAliasing = 0;
            // 影や異方性、ソフトパーティクル等をOFF（使っていれば）
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            // あなたのVFXにフック（あれば）
            var vfx = FindObjectOfType<ComboVfx>();
            if (vfx)
            {
                vfx.maxBurst = Mathf.Min(vfx.maxBurst, 24);
                vfx.sizeMul = Mathf.Min(vfx.sizeMul, 1.4f);
                // プールがあれば poolSize も縮小
                // vfx.poolSize = Mathf.Max(8, vfx.poolSize/2); ← 実装していれば
            }
        }
    }
}
