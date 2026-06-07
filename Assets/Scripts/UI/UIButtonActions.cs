using UnityEngine;

public class UIButtonActions : MonoBehaviour
{
    // Hook to a Button OnClick. Drag the UI object into the argument slot.
    public void Toggle(GameObject target)
    {
        if (target != null)
            target.SetActive(!target.activeSelf);
    }

    // Hook to a Button OnClick to quit the game.
    public void QuitGame()
    {
        Application.Quit();
    }
}
