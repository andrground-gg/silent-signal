using UnityEngine;
using UnityEditor;
using System.IO;

public class DayCyclePaletteEditor : EditorWindow
{
    // ── Light colors (Row 0) ──────────────────────────────────────────────
    private Color morningLight = new Color(1.00f, 0.95f, 0.80f);
    private Color noonLight    = new Color(1.00f, 1.00f, 0.95f);
    private Color eveningLight = new Color(1.00f, 0.75f, 0.45f);
    private Color nightLight   = new Color(0.25f, 0.30f, 0.55f);

    // ── Shadow colors (Row 1) ─────────────────────────────────────────────
    private Color morningShadow = new Color(0.45f, 0.40f, 0.55f);
    private Color noonShadow    = new Color(0.55f, 0.55f, 0.65f);
    private Color eveningShadow = new Color(0.40f, 0.20f, 0.35f);
    private Color nightShadow   = new Color(0.05f, 0.05f, 0.15f);

    // ── Import ────────────────────────────────────────────────────────────
    private Texture2D _importTexture = null;

    // ── Export settings ───────────────────────────────────────────────────
    private string exportFolder  = "Assets";
    private string exportName    = "DayCyclePalette";
    private bool   showPreview   = true;

    // ── Status banner ─────────────────────────────────────────────────────
    private enum StatusType { None, Success, Error }
    private StatusType _statusType    = StatusType.None;
    private string     _statusMessage = "";

    // ── Layout constants ──────────────────────────────────────────────────
    private const float SWATCH_H      = 48f;
    private const float PREVIEW_SCALE = 48f;   // each pixel drawn this wide
    private const float LABEL_W       = 140f;

    // ── Column headers ────────────────────────────────────────────────────
    private static readonly string[] TimeLabels =
        { "Morning", "Noon", "Evening", "Night" };

    // ─────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Day Cycle Palette Editor")]
    public static void OpenWindow()
    {
        var win = GetWindow<DayCyclePaletteEditor>("Day Cycle Palette");
        win.minSize = new Vector2(520, 420);
        win.Show();
    }

    // ─────────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        DrawHeader();
        GUILayout.Space(8);
        DrawColorGrid();
        GUILayout.Space(12);
        DrawPreviewToggle();
        if (showPreview) DrawPreview();
        GUILayout.Space(12);
        DrawImportSection();
        GUILayout.Space(8);
        DrawExportSection();
        if (_statusType != StatusType.None) DrawStatusBanner();
    }

    // ── Header ────────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Space(6);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("☀  Day Cycle Palette Editor  ☾", titleStyle);

        GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField(
            "Define light & shadow colors for each time of day, then export a 4×2 texture.",
            subStyle);

        GUILayout.Space(6);
        EditorGUILayout.EndVertical();
    }

    // ── Color grid ────────────────────────────────────────────────────────
    private void DrawColorGrid()
    {
        // Column header row
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(LABEL_W + 4);
        foreach (string label in TimeLabels)
        {
            GUIStyle colStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(label, colStyle, GUILayout.ExpandWidth(true));
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Light row
        DrawColorRow("☀  Light", ref morningLight, ref noonLight,
                                  ref eveningLight, ref nightLight);
        GUILayout.Space(4);

        // Shadow row
        DrawColorRow("🌑  Shadow", ref morningShadow, ref noonShadow,
                                    ref eveningShadow, ref nightShadow);
    }

    private void DrawColorRow(string rowLabel,
        ref Color c0, ref Color c1, ref Color c2, ref Color c3)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        // Row label
        GUIStyle rowLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize  = 12
        };
        GUILayout.Label(rowLabel, rowLabelStyle, GUILayout.Width(LABEL_W));

        // Four color pickers
        c0 = DrawColorSwatch(c0);
        c1 = DrawColorSwatch(c1);
        c2 = DrawColorSwatch(c2);
        c3 = DrawColorSwatch(c3);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private Color DrawColorSwatch(Color color)
    {
        return EditorGUILayout.ColorField(
            GUIContent.none,
            color,
            showEyedropper : true,
            showAlpha       : false,
            hdr             : false,
            GUILayout.Height(SWATCH_H),
            GUILayout.ExpandWidth(true));
    }

    // ── Preview ───────────────────────────────────────────────────────────
    private void DrawPreviewToggle()
    {
        showPreview = EditorGUILayout.Foldout(showPreview,
            "  Texture Preview  (4 × 2, each pixel scaled)", true);
    }

    private void DrawPreview()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Space(8);

        // Center the 4-pixel-wide preview
        float totalW = PREVIEW_SCALE * 4;
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginVertical();

        // Row 0 – Lights
        DrawPreviewRow(morningLight, noonLight, eveningLight, nightLight, "Light  →");

        // 1-pixel gap
        GUILayout.Space(2);

        // Row 1 – Shadows
        DrawPreviewRow(morningShadow, noonShadow, eveningShadow, nightShadow, "Shadow →");

        EditorGUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(8);
        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewRow(Color c0, Color c1, Color c2, Color c3, string rowTag)
    {
        EditorGUILayout.BeginHorizontal();
        GUIStyle tagStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };
        GUILayout.Label(rowTag, tagStyle, GUILayout.Width(68));

        foreach (Color c in new[] { c0, c1, c2, c3 })
        {
            Rect r = GUILayoutUtility.GetRect(PREVIEW_SCALE, PREVIEW_SCALE,
                GUILayout.Width(PREVIEW_SCALE), GUILayout.Height(PREVIEW_SCALE));
            EditorGUI.DrawRect(r, c);
        }
        EditorGUILayout.EndHorizontal();
    }

    // ── Import section ────────────────────────────────────────────────────
    private void DrawImportSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Space(6);

        EditorGUILayout.LabelField("Import", EditorStyles.boldLabel);

        // Object field — accepts any Texture2D from the project
        EditorGUILayout.BeginHorizontal();
        _importTexture = (Texture2D)EditorGUILayout.ObjectField(
            "Palette Texture",
            _importTexture,
            typeof(Texture2D),
            allowSceneObjects: false);

        // Validate size and give the user a quick hint
        if (_importTexture != null && (_importTexture.width != 4 || _importTexture.height != 2))
        {
            GUIStyle warnStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.90f, 0.65f, 0.10f) }
            };
            GUILayout.Label("⚠ must be 4 × 2", warnStyle, GUILayout.Width(100));
        }
        else
        {
            GUILayout.Space(104); // keep layout stable when no warning
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Import button — greyed out when no valid texture is selected
        bool canImport = _importTexture != null
                      && _importTexture.width  == 4
                      && _importTexture.height == 2;

        EditorGUI.BeginDisabledGroup(!canImport);
        GUIStyle importStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 14,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 38f
        };
        GUI.backgroundColor = canImport ? new Color(0.35f, 0.70f, 1.00f) : Color.white;
        if (GUILayout.Button("⬆  Import Palette", importStyle))
            ImportPalette();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(6);
        EditorGUILayout.EndVertical();
    }

    // ── Palette import logic ──────────────────────────────────────────────
    private void ImportPalette()
    {
        // Read the raw PNG bytes from disk so we never depend on the
        // asset's Read/Write import flag being enabled.
        string assetPath = AssetDatabase.GetAssetPath(_importTexture);
        string fullPath  = Path.Combine(
            Path.GetDirectoryName(Application.dataPath)!, assetPath);

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (System.Exception e)
        {
            SetStatus(StatusType.Error, $"⚠  Could not read file: {e.Message}");
            return;
        }

        // Load into a temporary readable texture
        var tmp = new Texture2D(4, 2, TextureFormat.RGBA32, mipChain: false);
        if (!ImageConversion.LoadImage(tmp, bytes, markNonReadable: false))
        {
            DestroyImmediate(tmp);
            SetStatus(StatusType.Error, "⚠  Failed to decode image data.");
            return;
        }

        if (tmp.width != 4 || tmp.height != 2)
        {
            DestroyImmediate(tmp);
            SetStatus(StatusType.Error, $"⚠  Expected 4×2 texture, got {tmp.width}×{tmp.height}.");
            return;
        }

        // y=1 → Light row (top), y=0 → Shadow row (bottom)
        morningLight  = tmp.GetPixel(0, 1);
        noonLight     = tmp.GetPixel(1, 1);
        eveningLight  = tmp.GetPixel(2, 1);
        nightLight    = tmp.GetPixel(3, 1);

        morningShadow = tmp.GetPixel(0, 0);
        noonShadow    = tmp.GetPixel(1, 0);
        eveningShadow = tmp.GetPixel(2, 0);
        nightShadow   = tmp.GetPixel(3, 0);

        DestroyImmediate(tmp);

        // Auto-fill the export name to match the imported asset
        exportName = Path.GetFileNameWithoutExtension(assetPath);
        exportFolder = Path.GetDirectoryName(assetPath)!.Replace('\\', '/');

        SetStatus(StatusType.Success, $"✓  Imported  {assetPath}");
        Repaint();
    }

    // ── Export section ────────────────────────────────────────────────────
    private void DrawExportSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Space(6);

        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

        // ── Folder row ────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        exportFolder = EditorGUILayout.TextField("Folder", exportFolder);
        if (GUILayout.Button("…", GUILayout.Width(28)))
        {
            // Open a folder picker rooted inside the project
            string absProject = Path.GetDirectoryName(Application.dataPath);
            string absChosen   = EditorUtility.OpenFolderPanel(
                "Choose export folder", exportFolder, "");
            if (!string.IsNullOrEmpty(absChosen))
            {
                // Convert absolute path → project-relative (Assets/…)
                if (absChosen.StartsWith(absProject))
                    exportFolder = absChosen.Substring(absProject.Length + 1)
                                            .Replace('\\', '/');
                else
                    exportFolder = absChosen; // outside project – keep as-is
            }
        }
        EditorGUILayout.EndHorizontal();

        // ── File name row ─────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        exportName = EditorGUILayout.TextField("File Name", exportName);
        GUIStyle extStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
        };
        GUILayout.Label(".png", extStyle, GUILayout.Width(36));
        EditorGUILayout.EndHorizontal();

        // Computed path preview
        string previewPath = $"{exportFolder.TrimEnd('/')}/{exportName}.png";
        GUIStyle pathStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.50f, 0.50f, 0.50f) }
        };
        EditorGUILayout.LabelField($"→  {previewPath}", pathStyle);

        GUILayout.Space(8);

        // ── Export button ─────────────────────────────────────────────────
        GUIStyle exportStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 14,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 38f
        };
        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.55f);
        if (GUILayout.Button("⬇  Export 4 × 2 Texture", exportStyle))
            ExportTexture();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(6);
        EditorGUILayout.EndVertical();
    }

    // ── Status banner ─────────────────────────────────────────────────────
    private void DrawStatusBanner()
    {
        GUILayout.Space(4);

        bool isError = _statusType == StatusType.Error;
        Color bg  = isError ? new Color(0.75f, 0.22f, 0.22f)
                            : new Color(0.22f, 0.65f, 0.35f);
        Color fg  = Color.white;

        GUIStyle bannerStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize  = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = fg }
        };

        GUI.backgroundColor = bg;
        GUILayout.Label(_statusMessage, bannerStyle, GUILayout.MinHeight(36));
        GUI.backgroundColor = Color.white;
    }

    // ── Texture generation ────────────────────────────────────────────────
    private void ExportTexture()
    {
        // ── Validation ────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(exportFolder))
        {
            SetStatus(StatusType.Error, "⚠  Folder path cannot be empty.");
            return;
        }
        if (string.IsNullOrWhiteSpace(exportName))
        {
            SetStatus(StatusType.Error, "⚠  File name cannot be empty.");
            return;
        }

        string assetPath = $"{exportFolder.TrimEnd('/')}/{exportName}.png";

        // Layout: columns = time-of-day  (Morning, Noon, Evening, Night)
        //         row 0   = Light colors
        //         row 1   = Shadow colors
        //
        // Texture2D pixel (x, y): x = column, y = row FROM BOTTOM.
        // Unity stores y=0 at the bottom, so:
        //   y=1  → top row    → Light
        //   y=0  → bottom row → Shadow

        var tex = new Texture2D(4, 2, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };

        // Row y=1 – Lights (top)
        tex.SetPixel(0, 1, morningLight);
        tex.SetPixel(1, 1, noonLight);
        tex.SetPixel(2, 1, eveningLight);
        tex.SetPixel(3, 1, nightLight);

        // Row y=0 – Shadows (bottom)
        tex.SetPixel(0, 0, morningShadow);
        tex.SetPixel(1, 0, noonShadow);
        tex.SetPixel(2, 0, eveningShadow);
        tex.SetPixel(3, 0, nightShadow);

        tex.Apply();

        // Write PNG
        byte[] png = tex.EncodeToPNG();
        DestroyImmediate(tex);

        string fullPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),   // project root
            assetPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, png);
        }
        catch (System.Exception e)
        {
            SetStatus(StatusType.Error, $"⚠  Write failed: {e.Message}");
            return;
        }

        // Tell Unity about the new asset and configure it for palette use
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType        = TextureImporterType.Default;
            importer.filterMode         = FilterMode.Point;
            importer.mipmapEnabled      = false;
            importer.npotScale          = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture        = true;
            importer.SaveAndReimport();
        }

        SetStatus(StatusType.Success, $"✓  Saved  {assetPath}");

        // Ping the asset in the Project window
        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (asset != null) EditorGUIUtility.PingObject(asset);
    }

    private void SetStatus(StatusType type, string message)
    {
        _statusType    = type;
        _statusMessage = message;
        Repaint();
    }
}
