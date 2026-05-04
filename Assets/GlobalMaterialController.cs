using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GlobalMaterialController : MonoBehaviour
{
    [SerializeField] private List<Material> materials;

    [SerializeField, Range(0,1)] private float currentTime;

    // Update is called once per frame
    void Update()
    {
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
