using System.Collections.Generic;
// using UnityEditor;
using UnityEngine;

// [ExecuteAlways]
public class GlobalMaterialController : MonoBehaviour
{
    [SerializeField] private List<Material> materials;

    [SerializeField, Range(0,1)] private float currentTime;

    void Update()
    {
        if (TimeManager.Instance != null)
        {
            SetNormalizedTime(TimeManager.Instance.Service.NormalizedTime);
        }

        UpdateMaterials();
    }


    void TempCycle()
    {
        currentTime = Mathf.Repeat(Time.time * .05f, 1f);
    }


    void UpdateMaterials()
    {
        foreach (var mat in materials)
        {
            mat.SetFloat("_time", currentTime);
        }
    }


    /// <summary>
    /// 0 = morning, 0.25 = noon, 0.5 = evening, 0.75 = night, 1 = mporning 
    /// </summary>
    public void SetNormalizedTime(float time)
    {
        currentTime = time;
    }
}
//
// [CustomEditor(typeof(GlobalMaterialController))]
// public class GlobalMaterialControllerEditor : Editor
// {
//     public override void OnInspectorGUI()
//     {
//         DrawDefaultInspector();
//
//         GlobalMaterialController controller = (GlobalMaterialController)target;
//
//         GUILayout.BeginVertical();
//
//         if (GUILayout.Button("Morning"))
//         {
//             controller.SetNormalizedTime(0);
//         }
//
//         if (GUILayout.Button("Noon"))
//         {
//             controller.SetNormalizedTime(0.25f);
//         }
//
//         if (GUILayout.Button("Evening"))
//         {
//             controller.SetNormalizedTime(0.5f);
//         }
//
//         if (GUILayout.Button("Night"))
//         {
//             controller.SetNormalizedTime(0.75f);
//         }
//
//         GUILayout.EndVertical();
//
//     }
// }
