using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZoomController : MonoBehaviour
{
    [Range(1.0f, 5.0f)]
    public float zoomLevel = 1.0f;
    [SerializeField]
    private bool right_eye;
    public float shift_i;
    private Material zoomMat;

    [Range(0.0f, 1.0f)]
    public float pivotY = 0.5f; //NEW TO ACCOUNT FOR UPWARDS SHIFT
    void Start()
    {
        zoomMat = new Material(Shader.Find("Zoom/ZoomShader"));
    }

    private void Update()
    {
        Debug.Log(shift_i);
    }

    //THIS IS THE ORIGINAL OnRenderImage()
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        zoomMat.SetFloat("_ZoomLevel", zoomLevel);
        if (right_eye) zoomMat.SetFloat("_eyeR", 1f);
        else zoomMat.SetFloat("_eyeR", 0f);

        if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_mode == 1) pivotY = 0.5f; //NEW
        else pivotY = 0.55f; //NEW
        zoomMat.SetFloat("_PivotY", pivotY); //NEW TO ACCOUNT FOR UPWARDS SHIFT

        zoomMat.SetFloat("_shift", shift_i);
        Graphics.Blit(src, dst, zoomMat);
    }



    //THIS IS THE REVISED VERSION THAT SHOULD BE CHANGED
    //void OnRenderImage(RenderTexture src, RenderTexture dst)
    //{
    //    float currentPivot = 1f - pivotX;
    //    if (right_eye)
    //    {
    //        currentPivot = pivotX;
    //    }
    //    zoomMat.SetVector("_Pivot", new Vector4(currentPivot, 0.5f, 0, 0));
    //    zoomMat.SetFloat("_ZoomLevel", zoomLevel);
    //    zoomMat.SetFloat("_shift", shift_i);
    //    if (right_eye) zoomMat.SetFloat("_eyeR", 1f);
    //    else zoomMat.SetFloat("_eyeR", 0f);
    //    Graphics.Blit(src, dst, zoomMat);
    //}
}
