using UnityEngine;

public static class GameState
{
    public static bool VictoryOpen = false;
    public static bool AutoCollecting = false; // ‹z‚¢ž‚Ý’†‚É‚àŽg‚¦‚é
    public static bool IsUiBlocked => VictoryOpen || AutoCollecting;
}