using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugLife : MonoBehaviour
{
    void OnEnable() { Log.D("[DebugLife] OnEnable " + name); }
    void OnDisable() { Log.D("[DebugLife] OnDisable " + name); }
    void OnDestroy() { Log.D("[DebugLife] OnDestroy " + name); }
}
