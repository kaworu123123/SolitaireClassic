using UnityEngine;
using UnityEngine.UI;

public class BoardLayout : MonoBehaviour
{
    [Header("World Slots")]
    public Transform stockSlot;
    public Transform wasteSlot;
    public Transform[] foundationSlots; // 4 elements
    public Transform[] tableauSlots;    // 7 elements

    [Header("UI References")]
    public RectTransform adContainer;
    public RectTransform statsBar;
    public RectTransform slotBar;
    public RectTransform boardFiller;

    [Header("Layout Settings")]
    public float gap = 0.05f;

    void Start()
    {
        // 1) UI の合計ピクセル高さを計算
        float uiPixels =
            adContainer.rect.height +
            statsBar.rect.height +
            slotBar.rect.height;

        // 2) カメラの world 単位 高さ／幅
        Camera cam = Camera.main;
        float worldH = cam.orthographicSize * 2f;
        float worldW = worldH * cam.aspect;

        // 3) UI ピクセル→world 単位に変換
        float uiWorldH = uiPixels / Screen.height * worldH;

        // 4) スロットバー（Stock/Foundation/Waste）を UI の真下に
        float slotY = cam.orthographicSize - (statsBar.rect.height / Screen.height * cam.orthographicSize) -
                      (adContainer.rect.height / Screen.height * cam.orthographicSize) -
                      (slotBar.rect.height / Screen.height * cam.orthographicSize) / 2f;
        ArrangeTopSlots(worldW, slotY);

        // 5) Tableau は「UI 全体分 + SlotBar の半分」下げた位置に
        float tableauY = cam.orthographicSize - uiWorldH - ((worldW - gap * (tableauSlots.Length - 1)) / tableauSlots.Length) * 0.5f;
        ArrangeTableau(worldW, tableauY);
    }

    void ArrangeTopSlots(float worldW, float y)
    {
        int count = 2 + foundationSlots.Length; // Stock + Foundations + Waste
        float slotW = (worldW - gap * (tableauSlots.Length - 1)) / tableauSlots.Length;
        float startX = -worldW / 2f + slotW / 2f;
        int i = 0;
        stockSlot.position = new Vector3(startX + i * (slotW + gap), y, 0);
        for (int f = 0; f < foundationSlots.Length; f++, i++)
            foundationSlots[f].position = new Vector3(startX + i * (slotW + gap), y, 0);
        i++;
        wasteSlot.position = new Vector3(startX + i * (slotW + gap), y, 0);
    }

    void ArrangeTableau(float worldW, float y)
    {
        float slotW = (worldW - gap * (tableauSlots.Length - 1)) / tableauSlots.Length;
        float startX = -worldW / 2f + slotW / 2f;
        for (int c = 0; c < tableauSlots.Length; c++)
        {
            float x = startX + c * (slotW + gap);
            tableauSlots[c].position = new Vector3(x, y, 0);
        }
    }
}
