using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class GreenFilterEffect : MonoBehaviour
{
    [Range(0f, 1f)]
    public float greenStrength = 0.5f;

    private Material greenMat;

    void Start()
    {
        Shader shader = Shader.Find("Custom/GreenFilter_HLSL");
        if (shader == null)
        {
            Debug.LogError("Shader not found: Custom/GreenFilter");
            return;
        }

        greenMat = new Material(shader);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Debug.Log("OnRenderImage is running");
        if (greenMat != null)
        {
            greenMat.SetFloat("_GreenStrength", greenStrength);
            Graphics.Blit(src, dst, greenMat);
        }
        else
        {
            Graphics.Blit(src, dst); // just passthrough
        }
    }
}
