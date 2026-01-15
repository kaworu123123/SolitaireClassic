using UnityEngine;

public class DynamicPerformanceScaler : MonoBehaviour
{
    [SerializeField] int targetFpsHigh = 60;
    [SerializeField] int targetFpsLow = 30;
    [SerializeField] float checkWindow = 2.0f; // 2ïbïΩãœ
    [SerializeField] float downThresh = 28f;  // 2ïbïΩãœÇ™28fpsñ¢ñûÇ≈ç~äi
    [SerializeField] float upThresh = 35f;  // 2ïbïΩãœÇ™35fpsí¥Ç≈è∏äi

    enum Level { High, Low }
    Level level = Level.High;
    float accum; int frames;

    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFpsHigh;
    }

    void Update()
    {
        accum += 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
        frames++;
        if (Time.unscaledTime >= checkWindow)
        {
            float avgFps = accum / frames;
            accum = 0f; frames = 0;

            if (level == Level.High && avgFps < downThresh) ApplyLow();
            else if (level == Level.Low && avgFps > upThresh) ApplyHigh();
        }
    }

    void ApplyHigh()
    {
        level = Level.High;
        Application.targetFrameRate = targetFpsHigh;

        var vfx = FindObjectOfType<ComboVfx>();
        if (vfx)
        {
            vfx.maxBurst = Mathf.Max(vfx.maxBurst, 36);
            vfx.sizeMul = Mathf.Max(vfx.sizeMul, 1.6f);
        }

        QualitySettings.antiAliasing = 0;               // 2DÇ»ÇÁ0Ç≈OK
        QualitySettings.shadows = ShadowQuality.Disable; // îOÇÃÇΩÇﬂå≈íË
    }

    void ApplyLow()
    {
        level = Level.Low;
        Application.targetFrameRate = targetFpsLow;

        var vfx = FindObjectOfType<ComboVfx>();
        if (vfx)
        {
            vfx.maxBurst = Mathf.Min(vfx.maxBurst, 20);
            vfx.sizeMul = Mathf.Min(vfx.sizeMul, 1.2f);
        }

        // í·ïââ◊å¸ÇØÇ…Ç≥ÇÁÇ…çiÇÈ
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
    }
}
