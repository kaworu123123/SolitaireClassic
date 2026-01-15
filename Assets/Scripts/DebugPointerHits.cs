using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DebugPointerHits : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var cam = Camera.main;
            var sp = Input.mousePosition;
            var wp = cam ? (Vector2)cam.ScreenToWorldPoint(sp) : (Vector2)sp;

            // 1) UIƒqƒbƒg
            var ev = EventSystem.current;
            var results = new List<RaycastResult>();
            if (ev)
            {
                var data = new PointerEventData(ev) { position = sp };
                ev.RaycastAll(data, results);
            }
            Log.D($"[UI Hits {results.Count}]");
            foreach (var r in results) Log.D($"  UI: {r.gameObject.name} ({r.module?.GetType().Name})");

            // 2) 2Dƒqƒbƒg
            var hits = Physics2D.OverlapPointAll(wp);
            Log.D($"[2D Hits {hits.Length}]");
            foreach (var h in hits) Log.D($"  2D: {h.name} (Layer={LayerMask.LayerToName(h.gameObject.layer)})");
        }
    }
}
