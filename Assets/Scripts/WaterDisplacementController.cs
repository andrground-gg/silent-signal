using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this to your Water plane GameObject (the one using WaterDisplacement.shader).
/// It collects all WaterDisplacer components in the scene each frame and pushes
/// their world positions + radii into the shader.
/// Supports up to 8 simultaneous displacers.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class WaterDisplacementController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty to auto-find all WaterDisplacer components in the scene.")]
    public List<WaterDisplacer> displacers = new();

    [Header("Shader Settings")]
    [Range(0.1f, 10f)]  public float displacementStrength = 1.5f;
    [Range(0.1f, 5f)]   public float displacementRadius   = 1.0f;

    // Shader property IDs (cached for performance)
    static readonly int[] DisplacerIDs = new int[8];
    static readonly int   CountID    = Shader.PropertyToID("_DisplacerCount");
    static readonly int   StrengthID = Shader.PropertyToID("_DisplacementStrength");
    static readonly int   RadiusID   = Shader.PropertyToID("_DisplacementRadius");

    static readonly Vector4 Zero = Vector4.zero;

    Material _mat;

    void Awake()
    {
        _mat = GetComponent<Renderer>().material;

        for (int i = 0; i < 8; i++)
            DisplacerIDs[i] = Shader.PropertyToID($"_Displacer{i}");
    }

    void Update()
    {
        // Auto-discover displacers if the list is empty
        if (displacers.Count == 0)
        {
            var found = FindObjectsByType<WaterDisplacer>(FindObjectsSortMode.None);
            displacers.AddRange(found);
        }

        _mat.SetFloat(StrengthID, displacementStrength);
        _mat.SetFloat(RadiusID,   displacementRadius);

        int count = Mathf.Min(displacers.Count, 8);
        _mat.SetInt(CountID, count);

        for (int i = 0; i < 8; i++)
        {
            if (i < count && displacers[i] != null && displacers[i].isActiveAndEnabled)
            {
                Vector3 pos = displacers[i].transform.position;
                _mat.SetVector(DisplacerIDs[i],
                    new Vector4(pos.x, pos.y, pos.z, displacers[i].radius));
            }
            else
            {
                _mat.SetVector(DisplacerIDs[i], Zero);
            }
        }
    }

    /// <summary>
    /// Manually register a displacer at runtime (e.g. when spawning objects).
    /// </summary>
    public void Register(WaterDisplacer d)
    {
        if (!displacers.Contains(d)) displacers.Add(d);
    }

    /// <summary>
    /// Unregister a displacer (e.g. when it's destroyed).
    /// </summary>
    public void Unregister(WaterDisplacer d) => displacers.Remove(d);
}
