using UnityEngine;

public class HintButton : MonoBehaviour
{
    public void OnClickHint()
    {
        var hs = FindObjectOfType<HintSystem>();
        if (hs != null) hs.ShowHint();
    }
}
