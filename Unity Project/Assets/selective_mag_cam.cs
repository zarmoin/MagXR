using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class VRFullscreenMagnifier : MonoBehaviour
{
    [Header("Assign your SelectiveZoom_Material here")]
    public Material magnificationMaterial;

    // This built-in Unity function automatically intercepts the final camera image
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (magnificationMaterial != null)
        {
            // Blits (copies) the entire screen through your shader
            // In VR, Graphics.Blit automatically handles both the left and right eyes
            Graphics.Blit(source, destination, magnificationMaterial);
        }
        else
        {
            // If no material is assigned, just draw the screen normally
            Graphics.Blit(source, destination);
        }
    }
}