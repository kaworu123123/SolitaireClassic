using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RaycastSpy : MonoBehaviour
{
    void Update()
    {
        // 左クリック/タップ開始を両対応
        bool down = Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
        if (!down) return;

        Vector2 screenPos = Input.touchCount > 0 ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;

        // 1) UIヒット（EventSystem が無くても必ず何か出す）
        if (EventSystem.current == null)
        {
            Log.D("[RaycastSpy] No EventSystem in scene.");
        }
        else
        {
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            string ui = results.Count == 0 ? "(none)" : string.Join(" > ", results.Select(r => $"{r.gameObject.name}"));
            Log.D($"[RaycastSpy] UI: {ui}");
        }

        // 2) 2Dコライダー（カードなど）
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z)));
            var cols = Physics2D.OverlapPointAll(world);
            string ph = cols.Length == 0 ? "(none)" : string.Join(" > ", cols.Select(c => $"{c.name}"));
            Log.D($"[RaycastSpy] 2D: {ph}");
        }
        else
        {
            Log.D("[RaycastSpy] No Camera.main");
        }
    }
}
