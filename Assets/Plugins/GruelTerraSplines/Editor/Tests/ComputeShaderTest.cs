using UnityEngine;

public class ComputeShaderTest : MonoBehaviour
{
    void Start()
    {
        // Test if the compute shader can be loaded
        ComputeShader shapeShader = Resources.Load<ComputeShader>("Compute/ShapeRasterization");

        if (shapeShader == null)
        {
            Debug.LogError("ShapeRasterization compute shader failed to load!");
            return;
        }

        Debug.Log("ShapeRasterization compute shader loaded successfully");

        // Test if the kernel exists
        int kernelIndex = shapeShader.FindKernel("RasterizeShape");
        if (kernelIndex < 0)
        {
            Debug.LogError("RasterizeShape kernel not found!");
            return;
        }

        Debug.Log($"RasterizeShape kernel found at index: {kernelIndex}");

        // Test if we can create a simple render texture
        RenderTexture testTexture = new RenderTexture(8, 8, 0, RenderTextureFormat.RFloat);
        testTexture.enableRandomWrite = true;
        testTexture.Create();

        if (testTexture.IsCreated())
        {
            Debug.Log("Test render texture created successfully");
        }
        else
        {
            Debug.LogError("Failed to create test render texture");
        }

        // Cleanup
        testTexture.Release();
    }
}