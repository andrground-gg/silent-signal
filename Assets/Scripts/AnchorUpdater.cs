using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Кидаєш на UI елемент — натискаєш кнопку в інспекторі (або вмикаєш autoUpdate)
/// і anchors автоматично виставляються по поточній зоні прямокутника.
/// Аналог кнопки "Anchor Presets → Custom" але без ручної роботи.
/// </summary>
[ExecuteAlways]
public class AnchorUpdater : MonoBehaviour
{
    [Tooltip("Якщо увімкнено — anchors оновлюються автоматично в Edit Mode при будь-якій зміні трансформу")]
    public bool autoUpdate = false;

    [Tooltip("Після застосування обнулити localPosition і offsets (елемент \"прилипне\" до нових anchors)")]
    public bool resetOffsets = true;

    private RectTransform _rt;
    private RectTransform _parentRt;

    void OnValidate() => TryInit();
    void Awake()      => TryInit();

    void TryInit()
    {
        _rt       = GetComponent<RectTransform>();
        _parentRt = _rt != null ? _rt.parent as RectTransform : null;
    }

    /// <summary>
    /// Головний метод — виставляє anchorMin / anchorMax по поточному rect
    /// відносно батьківського RectTransform.
    /// </summary>
    public void ApplyAnchors()
    {
        TryInit();

        if (_rt == null || _parentRt == null)
        {
            Debug.LogWarning("[AnchorUpdater] Потрібен RectTransform і батьківський RectTransform.");
            return;
        }

        // Поточні розміри батька у світових пікселях (з урахуванням scale)
        Rect parentRect = _parentRt.rect;
        Vector2 parentSize = new Vector2(
            parentRect.width  * _parentRt.lossyScale.x,
            parentRect.height * _parentRt.lossyScale.y
        );

        if (parentSize.x == 0 || parentSize.y == 0)
        {
            Debug.LogWarning("[AnchorUpdater] Розмір батька == 0, не можу обчислити anchors.");
            return;
        }

        // Кути поточного елемента у світовому просторі
        Vector3[] corners = new Vector3[4];
        _rt.GetWorldCorners(corners);
        // corners: 0=BL, 1=TL, 2=TR, 3=BR

        // Кути батьківського елемента у світовому просторі
        Vector3[] parentCorners = new Vector3[4];
        _parentRt.GetWorldCorners(parentCorners);
        // parentCorners: 0=BL, 1=TL, 2=TR, 3=BR

        Vector3 parentBL = parentCorners[0];
        Vector3 parentTR = parentCorners[2];
        float pW = parentTR.x - parentBL.x;
        float pH = parentTR.y - parentBL.y;

        if (Mathf.Approximately(pW, 0) || Mathf.Approximately(pH, 0))
        {
            Debug.LogWarning("[AnchorUpdater] Батько має нульовий розмір у світових координатах.");
            return;
        }

        // Нормалізовані позиції кутів елемента відносно батька
        float minX = (corners[0].x - parentBL.x) / pW;
        float minY = (corners[0].y - parentBL.y) / pH;
        float maxX = (corners[2].x - parentBL.x) / pW;
        float maxY = (corners[2].y - parentBL.y) / pH;

#if UNITY_EDITOR
        Undo.RecordObject(_rt, "Apply Anchors From Rect");
#endif

        _rt.anchorMin = new Vector2(minX, minY);
        _rt.anchorMax = new Vector2(maxX, maxY);

        if (resetOffsets)
        {
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(_rt);
#endif

        Debug.Log($"[AnchorUpdater] Anchors оновлено: min=({minX:F3}, {minY:F3}) max=({maxX:F3}, {maxY:F3})");
    }

#if UNITY_EDITOR
    // Оновлення в Edit Mode при зміні трансформу
    void Update()
    {
        if (!Application.isPlaying && autoUpdate)
            ApplyAnchors();
    }
#endif
}


// ─── Кастомний Inspector ──────────────────────────────────────────────────────
#if UNITY_EDITOR
[CustomEditor(typeof(AnchorUpdater))]
public class AnchorUpdaterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6);

        var script = (AnchorUpdater)target;

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.5f);
        if (GUILayout.Button("▶  Apply Anchors From Rect", GUILayout.Height(32)))
        {
            script.ApplyAnchors();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "Виставляє anchorMin/anchorMax точно по поточній позиції та розміру елемента відносно батьківського RectTransform.",
            MessageType.Info
        );
    }
}
#endif