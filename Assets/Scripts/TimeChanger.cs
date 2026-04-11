using UnityEngine;

public class TimeChanger : MonoBehaviour
{
    [SerializeField] private Material _skybox;
    private float _elapsedTime;
    private float _timeScale = 2.0f;
    private static readonly int Rotation = Shader.PropertyToID("_Rotation");
    private static readonly int Exposure = Shader.PropertyToID("_Exposure");

    private void Update()
    {
        _elapsedTime += Time.deltaTime;
        _skybox.SetFloat(Rotation, _elapsedTime);
        _skybox.SetFloat(Exposure, Mathf.Clamp(Mathf.Sin(_elapsedTime), 0.15f, 0.85f));
    }
    
}
