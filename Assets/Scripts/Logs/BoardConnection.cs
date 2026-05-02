using UnityEngine;

/// <summary>
/// A connection (string/line/marker) on the investigation board between two nodes.
/// Becomes visible only when BOTH endpoint keys have been revealed.
///
/// Place this on a child GameObject of InvestigationBoard (e.g. a string / red line).
/// </summary>
public class BoardConnection : MonoBehaviour
{
    [Tooltip("First endpoint — must match a BoardInteractable's Data.key on this board.")]
    public LogKeys keyA;

    [Tooltip("Second endpoint — must match a BoardInteractable's Data.key on this board.")]
    public LogKeys keyB;

    /// <summary>True if this connection links the given key to anything.</summary>
    public bool Involves(LogKeys key) => key == keyA || key == keyB;

    /// <summary>True if both endpoints are present in the revealed set.</summary>
    public bool CanActivate(System.Collections.Generic.HashSet<LogKeys> revealed)
        => keyA != LogKeys.None && keyB != LogKeys.None
           && revealed.Contains(keyA) && revealed.Contains(keyB);
}
