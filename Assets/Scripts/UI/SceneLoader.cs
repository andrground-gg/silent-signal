using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Tooltip("Drag the scene to load here. Must be added to Build Settings.")]
    [SerializeField] private SceneReference sceneToLoad;

    // Hook this to a Button's OnClick to load the scene set in the inspector.
    public void LoadSelectedScene()
    {
        if (!sceneToLoad.IsValid)
        {
            Debug.LogWarning("SceneLoader: no scene assigned.", this);
            return;
        }

        SceneManager.LoadScene(sceneToLoad.ScenePath);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
