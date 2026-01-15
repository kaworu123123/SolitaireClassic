using UnityEngine;

public class NoMoveDetector : MonoBehaviour
{
    [Tooltip("自動補完中はチェックしない")]
    public bool ignoreWhileAutoCompleting = true;

    [Tooltip("ヒント候補0ならトースト表示")]
    public bool showToast = true;

    [Tooltip("メッセージ")]
    public string noMoveMessage = "No more moves";

    public void CheckNow()
    {
        var fac = CardFactory.Instance;
        if (ignoreWhileAutoCompleting && fac != null && fac.isAutoCompleting) return;

        var hs = FindObjectOfType<HintSystem>();
        if (hs == null) return;

        bool any = hs.HasAnyMove();
        if (!any && showToast)
        {
            Toast.Instance?.Show(noMoveMessage, 1.6f);
            // 任意: Handheld.Vibrate(); // Androidで軽いバイブ
        }
    }
}
