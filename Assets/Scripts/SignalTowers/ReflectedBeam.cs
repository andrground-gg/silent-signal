using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(Rigidbody))]
public class ReflectedBeam : MonoBehaviour
{
    [SerializeField] private float     cylinderRadius    = 0.5f;
    [SerializeField] private float     endRadius         = 2f;
    [SerializeField] private int       segments          = 16;
    [SerializeField] private LayerMask hitMask           = ~0;
    [SerializeField] private float     maxDistance       = 300f;

    public Vector3 BeamDirection { get; private set; }

    private MeshFilter   _meshFilter;
    private MeshCollider _meshCollider;
    private Mesh         _mesh;
    private SignalTower  _hitTower;
    private Vector3[]    _diskRingLocal; // outer ring in parent local space, sorted by angle

    private void Awake()
    {
        _meshFilter      = GetComponent<MeshFilter>();
        _mesh            = new Mesh { name = "ReflectedBeamMesh" };
        _meshFilter.mesh = _mesh;

        _meshCollider           = GetComponent<MeshCollider>();
        _meshCollider.convex    = true;
        _meshCollider.isTrigger = true;

        var rb         = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        _diskRingLocal = BuildDiskRingLocal();

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }

    // Reads outer-edge vertices from the parent disk mesh, deduplicates, sorts by angle.
    private Vector3[] BuildDiskRingLocal()
    {
        var mf = transform.parent != null ? transform.parent.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return null;

        var verts = mf.sharedMesh.vertices; // parent local space

        float maxR = 0f;
        foreach (var v in verts) maxR = Mathf.Max(maxR, v.magnitude);
        if (maxR < 0.001f) return null;

        float thresh = maxR * 0.98f;
        var outer = new List<Vector3>();
        foreach (var v in verts)
            if (v.magnitude >= thresh)
                outer.Add(v);

        // Deduplicate
        var unique = new List<Vector3>();
        foreach (var v in outer)
        {
            bool dup = false;
            for (int i = 0; i < unique.Count; i++)
                if ((v - unique[i]).sqrMagnitude < 0.0001f) { dup = true; break; }
            if (!dup) unique.Add(v);
        }

        if (unique.Count < 3) return null;

        // Find center and two orthogonal axes to sort by angle
        var center = Vector3.zero;
        foreach (var v in unique) center += v;
        center /= unique.Count;

        Vector3 v0   = (unique[0] - center).normalized;
        Vector3 v1   = (unique[unique.Count / 3] - center).normalized;
        Vector3 nrm  = Vector3.Cross(v0, v1).normalized;
        Vector3 perp = Vector3.Cross(nrm, v0).normalized;

        unique.Sort((a, b) =>
        {
            var da = a - center;
            var db = b - center;
            float angA = Mathf.Atan2(Vector3.Dot(da, perp), Vector3.Dot(da, v0));
            float angB = Mathf.Atan2(Vector3.Dot(db, perp), Vector3.Dot(db, v0));
            return angA.CompareTo(angB);
        });

        return unique.ToArray();
    }

    public void Activate(Vector3 incomingDir, bool updateCollider = true)
    {
        _hitTower = null;

        var d = new Vector3(incomingDir.x, 0f, incomingDir.z).normalized;
        if (d.sqrMagnitude < 0.001f) return;
        var n = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
        BeamDirection = Vector3.Reflect(d, n);
        if (Vector3.Dot(d, transform.up) < 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        var direction  = BeamDirection;
        var ownerTower = GetComponentInParent<SignalTower>();
        var origin     = transform.parent.position;

        if (updateCollider && Physics.SphereCast(origin, cylinderRadius, direction, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            var hitTower = hit.collider.GetComponentInParent<SignalTower>();
            _hitTower = (hitTower != null && hitTower != ownerTower) ? hitTower : null;
        }

        float length = maxDistance;

        Debug.DrawRay(origin, d * 5f,            Color.red,   0.5f);
        Debug.DrawRay(origin, BeamDirection * 5f, Color.green, 0.5f);
        Debug.DrawRay(origin, n * 3f,            Color.blue,  0.5f);

        BuildCylinder(direction, length, origin);

        if (updateCollider)
        {
            _meshCollider.sharedMesh = _mesh;
        }

        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        _hitTower?.ClearExternalBeamHit();
        _hitTower = null;
        gameObject.SetActive(false);
    }

    private void BuildCylinder(Vector3 direction, float length, Vector3 worldStart)
    {
        direction = direction.normalized;

        // End ring axes (perpendicular to beam direction)
        Vector3 endRight = Vector3.Cross(direction, Vector3.up);
        if (endRight.sqrMagnitude < 0.001f)
            endRight = Vector3.Cross(direction, Vector3.forward);
        endRight.Normalize();
        Vector3 endUp    = Vector3.Cross(endRight, direction).normalized;
        Vector3 worldEnd = worldStart + direction * length;

        bool hasDisk  = _diskRingLocal != null && _diskRingLocal.Length >= 3;
        int  segCount = hasDisk ? _diskRingLocal.Length : segments;

        var vertices  = new Vector3[segCount * 2];
        var normals   = new Vector3[segCount * 2];
        var uvs       = new Vector2[segCount * 2];
        var triangles = new int[segCount * 6];

        for (int i = 0; i < segCount; i++)
        {
            // Start vertex: exact disk ring vertex (world space)
            Vector3 startVert, startOutward;
            if (hasDisk)
            {
                startVert    = transform.parent.TransformPoint(_diskRingLocal[i]);
                startOutward = (startVert - worldStart).normalized;
            }
            else
            {
                float ang    = 2f * Mathf.PI * i / segCount;
                startOutward = endRight * Mathf.Cos(ang) + endUp * Mathf.Sin(ang);
                startVert    = worldStart + startOutward * cylinderRadius;
            }

            // End vertex: project start outward onto beam-perpendicular plane to keep alignment
            Vector3 endOutward = Vector3.ProjectOnPlane(startOutward, direction);
            if (endOutward.sqrMagnitude < 0.001f)
            {
                float ang  = 2f * Mathf.PI * i / segCount;
                endOutward = endRight * Mathf.Cos(ang) + endUp * Mathf.Sin(ang);
            }
            endOutward = endOutward.normalized;
            Vector3 endVert = worldEnd + endOutward * endRadius;

            vertices[i]            = transform.InverseTransformPoint(startVert);
            vertices[i + segCount] = transform.InverseTransformPoint(endVert);

            Vector3 approxOut     = ((startOutward + endOutward) * 0.5f).normalized;
            normals[i]            = transform.InverseTransformDirection(approxOut).normalized;
            normals[i + segCount] = normals[i];

            uvs[i]            = new Vector2((float)i / segCount, 0f);
            uvs[i + segCount] = new Vector2((float)i / segCount, 1f);

            int next = (i + 1) % segCount;
            int t    = i * 6;
            triangles[t]     = i;
            triangles[t + 1] = i + segCount;
            triangles[t + 2] = next;
            triangles[t + 3] = next;
            triangles[t + 4] = i + segCount;
            triangles[t + 5] = next + segCount;
        }

        _mesh.Clear();
        _mesh.vertices  = vertices;
        _mesh.normals   = normals;
        _mesh.uv        = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
    }
}
