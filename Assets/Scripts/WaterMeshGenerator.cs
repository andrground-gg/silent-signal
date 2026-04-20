using UnityEngine;

/// <summary>
/// Generates a subdivided water plane mesh at runtime.
/// Attach to an empty GameObject alongside a MeshFilter, MeshRenderer,
/// and your WaterDisplacementController.
///
/// Size and resolution are set in the Inspector; the mesh is rebuilt
/// automatically whenever you change them in Play mode via the
/// context-menu "Rebuild Mesh" or at Start.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaterMeshGenerator : MonoBehaviour
{
    [Header("Plane dimensions")]
    [Tooltip("Total width (X axis) of the water plane in world units.")]
    public float width  = 20f;
    [Tooltip("Total depth (Z axis) of the water plane in world units.")]
    public float depth  = 20f;

    [Header("Resolution")]
    [Tooltip("Number of vertex columns. 200–400 gives smooth waves; above 500 is rarely needed.")]
    [Range(10, 500)] public int columns = 200;
    [Tooltip("Number of vertex rows.")]
    [Range(10, 500)] public int rows    = 200;

    MeshFilter _mf;

    void Start() => BuildMesh();

    /// <summary>Rebuild the mesh — call from code or the Inspector context menu.</summary>
    [ContextMenu("Rebuild Mesh")]
    public void BuildMesh()
    {
        _mf = GetComponent<MeshFilter>();

        int vertCount = (columns + 1) * (rows + 1);
        int triCount  = columns * rows * 6;

        var vertices  = new Vector3[vertCount];
        var uvs       = new Vector2[vertCount];
        var triangles = new int[triCount];

        // ── Vertices & UVs ───────────────────────────────────────────────
        float colStep = width  / columns;
        float rowStep = depth  / rows;
        float uStep   = 1f    / columns;
        float vStep   = 1f    / rows;

        int vi = 0;
        for (int r = 0; r <= rows; r++)
        {
            for (int c = 0; c <= columns; c++, vi++)
            {
                vertices[vi] = new Vector3(c * colStep - width * 0.5f,
                                           0f,
                                           r * rowStep - depth * 0.5f);
                uvs[vi]      = new Vector2(c * uStep, r * vStep);
            }
        }

        // ── Triangles ────────────────────────────────────────────────────
        int ti = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int bl = r * (columns + 1) + c;   // bottom-left
                int br = bl + 1;                   // bottom-right
                int tl = bl + (columns + 1);       // top-left
                int tr = tl + 1;                   // top-right

                // Triangle 1
                triangles[ti++] = bl;
                triangles[ti++] = tl;
                triangles[ti++] = tr;

                // Triangle 2
                triangles[ti++] = bl;
                triangles[ti++] = tr;
                triangles[ti++] = br;
            }
        }

        // ── Build Mesh ───────────────────────────────────────────────────
        var mesh = new Mesh
        {
            name = "WaterPlane",
            // Use 32-bit indices so we can exceed 65 535 vertices
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _mf.sharedMesh = mesh;

        Debug.Log($"[WaterMeshGenerator] Built {columns}×{rows} mesh — " +
                  $"{vertCount} verts, {triCount / 3} tris.");
    }

    // Live preview: rebuild whenever Inspector values change in the Editor
#if UNITY_EDITOR
    void OnValidate()
    {
        // Delay one frame so all serialised fields are written before we read them
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) BuildMesh();
        };
    }
#endif
}
