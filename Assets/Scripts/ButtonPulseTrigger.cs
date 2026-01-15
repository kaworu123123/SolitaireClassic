using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ButtonPulse))]
public class ButtonPulseTrigger : MonoBehaviour, IPointerDownHandler
{
    ButtonPulse pulse;
    void Awake() { pulse = GetComponent<ButtonPulse>(); }
    public void OnPointerDown(PointerEventData e) => pulse.Pulse();
}
