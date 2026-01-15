using UnityEngine;
using TMPro;

public class VersionText : MonoBehaviour
{
    void Start()
    {
        var text = GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = $"v{Application.version}";
        }
    }
}
