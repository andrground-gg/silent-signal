using UnityEngine;

public static class CursorManager
{
    private static int _uiRequests;

    public static bool IsLocked  => Cursor.lockState == CursorLockMode.Locked;
    public static bool UIHasCursor => _uiRequests > 0;

    public static void Lock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    public static void Unlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // Call when a UI panel needs the cursor. Automatically unlocks.
    public static void RequestUI()
    {
        _uiRequests++;
        Unlock();
    }

    // Call when the UI panel is done. Re-locks when all requests are released.
    public static void ReleaseUI()
    {
        if (_uiRequests > 0) _uiRequests--;
        if (_uiRequests == 0) Lock();
    }
}
