using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class image_movement : MonoBehaviour
{
    public float move_speed = 1.0f;
    public Material mapMaterial;
    public Transform mapQuadTransform;
    public float magnification_method = 0f; // 0 = full-view, 1 = selective, 2 = image

    [Header("VR Fullscreen Setup")]
    public VRFullscreenMagnifier[] cameraMagnifierScripts;
    private float currentZoomFactor = 2f;

    void Start()
    {
        UpdateMagnificationState();
    }

    void UpdateMagnificationState()
    {
        if (cameraMagnifierScripts == null || cameraMagnifierScripts.Length == 0) return;

        bool isSelectiveMode = (magnification_method >= 0.9f && magnification_method <= 1.1f);

        foreach (var eyeScript in cameraMagnifierScripts)
        {
            if (eyeScript != null)
            {
                eyeScript.enabled = isSelectiveMode;
            }
        }

        if (isSelectiveMode)
        {
            currentZoomFactor = 2f;
            Shader.SetGlobalFloat("_ZoomFactor", currentZoomFactor);
            Shader.SetGlobalFloat("_LensRadius", 0.3f);
            Debug.Log("Global GPU Sync: Selective Zoom Mode Baseline set to 2x.");
        }
    }

    void Update()
    {
        if (magnification_method >= 1.9f && magnification_method <= 2.1f)
        {
            if (mapQuadTransform.localScale == new Vector3(15, 15, 15)) move_speed = 1.0f;
            else if (mapQuadTransform.localScale == new Vector3(30, 30, 30)) move_speed = 2.0f;
            else if (mapQuadTransform.localScale == new Vector3(60, 60, 60)) move_speed = 4.0f;
        }

        move();

        if (Input.GetButtonDown("B") || Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (magnification_method == 0f) //Full
            {
                if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 1f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 2.5f;
                else if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 2.5f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 4f;
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
            }
            else if (magnification_method == 1f) //Selective
            {
                if (currentZoomFactor <= 1.05f) currentZoomFactor = 2f;
                else if (currentZoomFactor == 2f) currentZoomFactor = 4f;
                Shader.SetGlobalFloat("_ZoomFactor", currentZoomFactor);
                Shader.SetGlobalFloat("_LensRadius", currentZoomFactor > 1.05f ? 0.3f : 0f);
            }
            else //Image
            {
                GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 1f;
                if (mapQuadTransform.localScale == new Vector3(15, 15, 15)) mapQuadTransform.localScale = new Vector3(30, 30, 30);
                else if (mapQuadTransform.localScale == new Vector3(30, 30, 30)) mapQuadTransform.localScale = new Vector3(60, 60, 60);
            }
        }

        if (Input.GetButtonDown("A") || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (magnification_method == 0f) //Full
            {
                if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 4f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 2.5f;
                else if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 2.5f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 1f;
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
            }
            else if (inSelectiveMode) //Selective
            {
                if (currentZoomFactor == 4f) currentZoomFactor = 2f;
                else if (currentZoomFactor == 2f) currentZoomFactor = 1f;
                Shader.SetGlobalFloat("_ZoomFactor", currentZoomFactor);
                Shader.SetGlobalFloat("_LensRadius", currentZoomFactor > 1.05f ? 0.3f : 0f);
            }
            else //Image
            {
                GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 1f;
                if (mapQuadTransform.localScale == new Vector3(60, 60, 60)) mapQuadTransform.localScale = new Vector3(30, 30, 30);
                else if (mapQuadTransform.localScale == new Vector3(30, 30, 30)) mapQuadTransform.localScale = new Vector3(15, 15, 15);
            }
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (magnification_method == 0f) magnification_method = 1f;
            else if (magnification_method == 1f) magnification_method = 2f;
            else magnification_method = 0f;

            UpdateMagnificationState();
        }
    }

    void move()
    {
        float left_y = Input.GetAxisRaw("Left Vertical");
        float left_x = Input.GetAxisRaw("Left Horizontal");
        float right_y = Input.GetAxisRaw("Right Vertical");
        float right_x = Input.GetAxisRaw("Right Horizontal");

        if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == true)
        {
            transform.localPosition = new Vector3(transform.localPosition.x - Time.deltaTime * move_speed * left_x, transform.localPosition.y - Time.deltaTime * move_speed * left_y, transform.localPosition.z);
        }
        else
        {
            transform.localPosition = new Vector3(transform.localPosition.x - Time.deltaTime * move_speed * right_x, transform.localPosition.y - Time.deltaTime * move_speed * right_y, transform.localPosition.z);
        }
    }
}
