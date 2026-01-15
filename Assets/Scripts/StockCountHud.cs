using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class StockCountHud : MonoBehaviour
{
    public CardFactory factory;
    public bool hideWhenZero = true;

    TMP_Text label;

    void Awake()
    {
        if (!factory) factory = FindObjectOfType<CardFactory>();
        label = GetComponent<TMP_Text>();
        if (!label) Log.W("[StockCountHud] TMP_Text ‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñ");
    }

    void LateUpdate()
    {
        if (!factory || !label) return;

        int n = factory.GetDeckCount();   // Žc‚èŽRŽD
        label.text = n.ToString();
        if (hideWhenZero) label.enabled = n > 0;
    }
}
