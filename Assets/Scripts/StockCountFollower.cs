using UnityEngine;

[DisallowMultipleComponent]
public class StockCountFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    void LateUpdate()
    {
        if (!target) return;
        // 親子スケールの影響を避けるため worldPosition を直接追従
        transform.position = target.position + offset;
    }
}