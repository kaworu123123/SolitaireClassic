using System.Collections;
using UnityEngine;

public class ToastTester : MonoBehaviour
{
    IEnumerator Start()
    {
        // 1フレ待つ：すべてのAwakeが終わって Toast.Instance が確実にセットされる
        yield return null;
        Toast.Instance?.Show("移動先がありません。リトライしてください。", 1.2f);

        // テスト用なので自分を外す（任意）
        // Destroy(this);
    }
}
