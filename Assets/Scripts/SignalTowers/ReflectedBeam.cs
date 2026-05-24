using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(Rigidbody))]
public class ReflectedBeam : MonoBehaviour
{
    [SerializeField] private float     cylinderRadius = 0.5f;
    [SerializeField] private int       segments       = 16;
    [SerializeField] private LayerMask hitMask        = ~0;
    [SerializeField] private float     maxDistance    = 300f;

    private MeshFilter   _meshFilter;
    private MeshCollider _meshCollider;
    private Mesh         _mesh;
    private SignalTower  _hitTower;

    private void Awake()
    {
        _meshFilter      = GetComponent<MeshFilter>();
        _mesh            = new Mesh { name = "ReflectedBeamMesh" };
        _meshFilter.mesh = _mesh;

        _meshCollider         = GetComponent<MeshCollider>();
        _meshCollider.convex   = true;
        _meshCollider.isTrigger = true;

        var rb         = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }

    public void Activate()
    {
        _hitTower = null;

        var direction  = transform.up;
        var ownerTower = GetComponentInParent<SignalTower>();

        float length;
        if (Physics.SphereCast(transform.position, cylinderRadius, direction, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            length = hit.distance + 5;
            var hitTower = hit.collider.GetComponentInParent<SignalTower>();
            _hitTower = (hitTower != null && hitTower != ownerTower) ? hitTower : null;
        }
        else
        {
            length = maxDistance;
        }

        BuildCylinder(transform.up, length);

        // Null-assign forces MeshCollider to re-bake the new shape before activation
        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;

        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        // Напряму повідомляємо башту бо OnTriggerExit ненадійний всередині physics callback
        _hitTower?.ClearExternalBeamHit();
        _hitTower = null;
        gameObject.SetActive(false);
    }

    private void BuildCylinder(Vector3 direction, float length)
    {
        direction = direction.normalized;

        Vector3 right = Vector3.Cross(direction, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(direction, Vector3.forward);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, direction).normalized;

        Vector3 worldStart = transform.position;
        Vector3 worldEnd   = worldStart + direction * length;

        var vertices  = new Vector3[segments * 2];
        var normals   = new Vector3[segments * 2];
        var uvs       = new Vector2[segments * 2];
        var triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float   angle   = 2f * Mathf.PI * i / segments;
            Vector3 outward = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);

            vertices[i]            = transform.InverseTransformPoint(worldStart + outward * cylinderRadius);
            vertices[i + segments] = transform.InverseTransformPoint(worldEnd   + outward * cylinderRadius);

            Vector3 localNormal    = transform.InverseTransformDirection(outward).normalized;
            normals[i]             = localNormal;
            normals[i + segments]  = localNormal;

            uvs[i]            = new Vector2((float)i / segments, 0f);
            uvs[i + segments] = new Vector2((float)i / segments, 1f);

            int next = (i + 1) % segments;
            int t    = i * 6;
            triangles[t]     = i;
            triangles[t + 1] = i + segments;
            triangles[t + 2] = next;
            triangles[t + 3] = next;
            triangles[t + 4] = i + segments;
            triangles[t + 5] = next + segments;
        }

        _mesh.Clear();
        _mesh.vertices  = vertices;
        _mesh.normals   = normals;
        _mesh.uv        = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
    }
}
