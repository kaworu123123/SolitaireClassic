using UnityEngine;
using UnityEngine.EventSystems;

public class WasteClickForwarder : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData e)
    {
        // 最上段の表カードに転送
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var cb = transform.GetChild(i).GetComponent<CardBehaviour>();
            if (cb != null && cb.Data.isFaceUp)
            {
                // タップ移動として扱う
                cb.SendMessage("OnPointerDown", e, SendMessageOptions.DontRequireReceiver);
                return;
            }
        }
    }
}
