using UnityEngine;

// Drag a scene asset into the inspector instead of typing its name.
// In the editor it holds the SceneAsset; at runtime only the path survives.
[System.Serializable]
public class SceneReference : ISerializationCallbackReceiver
{
#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif
    [SerializeField] private string scenePath;

    public string ScenePath => scenePath;
    public bool IsValid => !string.IsNullOrEmpty(scenePath);

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        scenePath = sceneAsset != null ? UnityEditor.AssetDatabase.GetAssetPath(sceneAsset) : "";
#endif
    }

    public void OnAfterDeserialize() { }
}
