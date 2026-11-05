using JetBrains.Annotations;
using Oculus.Interaction.PoseDetection.Editor;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

public class zoom_controller : MonoBehaviour
{
    [Range(1, 3)]
    [SerializeField]
    private int zoomMethod = 1;
    private int currentZoomMethod = 0;

    [SerializeField]
    private float ipd_cm = 0f;
    private float ipd_m = 0f;

    [SerializeField]
    private bool lock_shift;
    [SerializeField]
    private float shift;

    public GameObject left_cam;
    public GameObject right_cam;

    private bool ready = false;

    [SerializeField]
    public float Xoptical_2_5_40 = 11.4f;
    public float Xoptical_2_5_50 = 11.4f;
    public float Xoptical_2_5_60 = 11.4f;
    public float Xdigital_2_5_40 = 4.575f;
    public float Xdigital_2_5_50 = 4.575f;
    public float Xdigital_2_5_60 = 4.575f;
    public float Xoptical_4_40 = 22.8f;
    public float Xoptical_4_50 = 22.8f;
    public float Xoptical_4_60 = 22.8f;
    public float Xdigital_4_40 = 8.44f;
    public float Xdigital_4_50 = 8.44f;
    public float Xdigital_4_60 = 8.44f;
    public float optical_2_5 = 11.4f;
    public float optical_4 = 22.8f;
    public float digital_2_5 = 4.575f;
    public float digital_4 = 8.44f;

    public float current_shift = 0f;

    public bool isViewing2DMap = true;

    public GameObject cameraOffset;
    public GameObject image;
    public LayerMask imageLayer = 1;
    public image_movement mapImageMovement;
    public float centerShift2_5 = 11.4f;
    public float cornerShift2_5 = 20.0f;
    public float centerShift4 = 22.8f;
    public float cornerShift4 = 30.0f;

    private float currentCenterShift;
    private float currentCornerShift;
    private float maxDistanceToCorner;

    /*
     * 1 - Perfect zoom
     * 2 - Low Res zoom
     * 3 - One Eye zoom
     */

    [Range(1f, 5f)]
    [SerializeField]
    public float zoomLevel = 1f;
    private float currentZoomLevel = 1f;

    [SerializeField]
    private float zoomSpeed = 1f;





    private void Start()
    {
        InitializeBounds();
    }

    // Update is called once per frame
    void Update()
    {
        enter_info menuScript = GameObject.Find("Menu").GetComponent<enter_info>();
        var activeTrial = menuScript.GetActiveTrialConfig();
        float distance = 0.40f;

        if (currentZoomMethod == 1f && currentZoomLevel == 2.5f && distance == 0.40f) optical_2_5 = Xoptical_2_5_40;
        if (currentZoomMethod == 1f && currentZoomLevel == 2.5f && distance == 0.50f) optical_2_5 = Xoptical_2_5_50;
        if (currentZoomMethod == 1f && currentZoomLevel == 2.5f && distance == 0.60f) optical_2_5 = Xoptical_2_5_60;

        if (currentZoomMethod == 1f && currentZoomLevel == 4f && distance == 0.40f) optical_4 = Xoptical_4_40;
        if (currentZoomMethod == 1f && currentZoomLevel == 4f && distance == 0.50f) optical_4 = Xoptical_4_50;
        if (currentZoomMethod == 1f && currentZoomLevel == 4f && distance == 0.60f) optical_4 = Xoptical_4_60;

        if (currentZoomMethod == 2f && currentZoomLevel == 2.5f && distance == 0.40f) digital_2_5 = Xdigital_2_5_40;
        if (currentZoomMethod == 2f && currentZoomLevel == 2.5f && distance == 0.50f) digital_2_5 = Xdigital_2_5_50;
        if (currentZoomMethod == 2f && currentZoomLevel == 2.5f && distance == 0.60f) digital_2_5 = Xdigital_2_5_60;

        if (currentZoomMethod == 2f && currentZoomLevel == 4f && distance == 0.40f) digital_4 = Xdigital_4_40;
        if (currentZoomMethod == 2f && currentZoomLevel == 4f && distance == 0.50f) digital_4 = Xdigital_4_50;
        if (currentZoomMethod == 2f && currentZoomLevel == 4f && distance == 0.60f) digital_4 = Xdigital_4_60;

        if (Input.GetKeyDown(KeyCode.A))
        {
            if (currentZoomMethod == 1f && currentZoomLevel == 2.5f)
            {
                optical_2_5 += 0.05f;
                find_shift_1();
                set_perfect(zoomLevel);
                if (distance == 0.40f) Xoptical_2_5_40 = optical_2_5;
                if (distance == 0.50f) Xoptical_2_5_50 = optical_2_5;
                if (distance == 0.60f) Xoptical_2_5_60 = optical_2_5;
            }
            if (currentZoomMethod == 1f && currentZoomLevel == 4f)
            {
                optical_4 += 0.05f;
                find_shift_1();
                set_perfect(zoomLevel);
                if (distance == 0.40f) Xoptical_4_40 = optical_4;
                if (distance == 0.50f) Xoptical_4_50 = optical_4;
                if (distance == 0.60f) Xoptical_4_60 = optical_4;
            }
            if (currentZoomMethod == 2f && currentZoomLevel == 2.5f)
            {
                digital_2_5 += 0.05f;
                find_shift_2();
                set_low_res(zoomLevel);
                if (distance == 0.40f) Xdigital_2_5_40 = digital_2_5;
                if (distance == 0.50f) Xdigital_2_5_50 = digital_2_5;
                if (distance == 0.60f) Xdigital_2_5_60 = digital_2_5;
            }
            if (currentZoomMethod == 2f && currentZoomLevel == 4f)
            {
                digital_4 += 0.05f;
                find_shift_2();
                set_low_res(zoomLevel);
                if (distance == 0.40f) Xdigital_4_40 = digital_4;
                if (distance == 0.50f) Xdigital_4_50 = digital_4;
                if (distance == 0.60f) Xdigital_4_60 = digital_4;
            }
            reset_eye_pos();
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (currentZoomMethod == 1f && currentZoomLevel == 2.5f)
            {
                optical_2_5 -= 0.05f;
                find_shift_1();
                set_perfect(zoomLevel);
                if (distance == 0.40f) Xoptical_2_5_40 = optical_2_5;
                if (distance == 0.50f) Xoptical_2_5_50 = optical_2_5;
                if (distance == 0.60f) Xoptical_2_5_60 = optical_2_5;
            }
            if (currentZoomMethod == 1f && currentZoomLevel == 4f)
            {
                optical_4 -= 0.05f;
                find_shift_1();
                set_perfect(zoomLevel);
                if (distance == 0.40f) Xoptical_4_40 = optical_4;
                if (distance == 0.50f) Xoptical_4_50 = optical_4;
                if (distance == 0.60f) Xoptical_4_60 = optical_4;
            }
            if (currentZoomMethod == 2f && currentZoomLevel == 2.5f)
            {
                digital_2_5 -= 0.05f;
                find_shift_2();
                set_low_res(zoomLevel);
                if (distance == 0.40f) Xdigital_2_5_40 = digital_2_5;
                if (distance == 0.50f) Xdigital_2_5_50 = digital_2_5;
                if (distance == 0.60f) Xdigital_2_5_60 = digital_2_5;
            }
            if (currentZoomMethod == 2f && currentZoomLevel == 4f)
            {
                digital_4 -= 0.05f;
                find_shift_2();
                set_low_res(zoomLevel);
                if (distance == 0.40f) Xdigital_4_40 = digital_4;
                if (distance == 0.50f) Xdigital_4_50 = digital_4;
                if (distance == 0.60f) Xdigital_4_60 = digital_4;
            }
            reset_eye_pos();
        }

        if (ready)
        {
            if (zoomMethod != currentZoomMethod || zoomLevel != currentZoomLevel)
            {
                set_perfect(1f);
                set_low_res(1f);

                if (zoomMethod == 1)
                {
                    set_perfect(zoomLevel);
                }
                else if (zoomMethod == 2)
                {
                    set_low_res(zoomLevel);
                }
                else if (zoomMethod == 3)
                {
                    set_one_eye(zoomLevel);
                }

                currentZoomMethod = zoomMethod;
                currentZoomLevel = zoomLevel;
            }
            reset_eye_pos();
        }

        if (activeTrial.zoomMethod == "Full" && currentZoomLevel == 2.5f)
        {
            currentCenterShift = centerShift2_5;
            currentCornerShift = cornerShift2_5;
        }
        else if (activeTrial.zoomMethod == "Full" && currentZoomLevel == 4f)
        {
            currentCenterShift = centerShift4;
            currentCornerShift = cornerShift4;
        }

        Ray ray = new Ray(cameraOffset.transform.position, cameraOffset.transform.forward);
        RaycastHit hit;

        if (mapImageMovement != null && mapImageMovement.magnification_method == 0f)
        {
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, imageLayer))
            {
                if (hit.collider.gameObject == image)
                {
                    Vector3 imageCenter = image.transform.position;
                    float distanceFromCenter = Vector3.Distance(hit.point, imageCenter);
                    float t = Mathf.Clamp01(distanceFromCenter / maxDistanceToCorner);
                    float activeShift = Mathf.Lerp(currentCenterShift, currentCornerShift, t);

                    if (currentZoomLevel == 2.5f)
                    {
                        Xoptical_2_5_40 = activeShift;
                    }
                    else if (currentZoomLevel == 4f)
                    {
                        Xoptical_4_40 = activeShift;
                    }
                    reset_eye_pos();
                }
            }

        }
    }


    public void set_zoom()
    {
        var activeTrial = GameObject.Find("Menu").GetComponent<enter_info>().GetActiveTrialConfig();

        zoomMethod = (activeTrial.zoomMethod == "Full") ? 1 : 2;

        //zoomLevel = 2.5f; 

        Debug.Log("Zoom controller method synchronized to option: " + zoomMethod);
    }


    private float find_shift_2()  //CHANGE THESE VALUES IF NEEDED, SEEMS TO BE CAUSING DOUBLE VISION
    {
        float shift_out = 0f;
        //if (currentZoomLevel == 2.5f) shift_out = 0.225f * ipd_cm - 4.575f;
        //else if (currentZoomLevel == 4f) shift_out = 0.32f * ipd_cm - 8.44f;
        //return shift_out;
        if (currentZoomLevel == 2.5f) shift_out = 0.225f * ipd_cm - digital_2_5;
        else if (currentZoomLevel == 4f) shift_out = 0.32f * ipd_cm - digital_4;
        return shift_out;
    }

    private float find_shift_1() //ZOOM METHOD 1 SEEMS TO WORK CORRECTLY FOR ALL IPD
    {
        float shift_out = 0f;
        if (currentZoomLevel == 2.5f) shift_out = 0.2f * ipd_cm - optical_2_5;
        else if (currentZoomLevel == 4f) shift_out = 0.4f * ipd_cm - optical_4;
        return shift_out;
    }

    public void set_ipd(int new_ipd)
    {
        ipd_cm = new_ipd;
    }

    public void set_ready()
    {
        ready = true;
    }

    public void override_zoom()
    {
        ready = false;

        set_perfect(1f);
        set_low_res(1f);
        //set_one_eye(1f);

        ipd_m = (ipd_cm - 60f) / 100f;

        float right_shift = ipd_m / 2f;
        float left_shift = -ipd_m / 2f;

        right_cam.transform.localPosition = new Vector3(right_shift, 0f, 0f);
        left_cam.transform.localPosition = new Vector3(left_shift, 0f, 0f);
    }
    void reset_eye_pos()
    {
        ipd_m = (ipd_cm - 60f) / 100f;

        float right_shift = ipd_m / 2f;
        float left_shift = -ipd_m / 2f;
        float shift_1 = 0f;
        float shift_2 = 0f;

        if (currentZoomMethod == 2f)
        {
            Debug.Log("Auto Shift 2: " + find_shift_2() / 100f);
            Debug.Log("Shift: " + shift / 100f);
            if (lock_shift)
            {
                shift_2 = find_shift_2() / 100f;
            }
            else
            {
                shift_2 = shift / 100f;
            }

            //this is what is reported to output file
            if (currentZoomLevel == 2.5f) current_shift = digital_2_5;
            if (currentZoomLevel == 4f) current_shift = digital_4;
        }
        else
        {
            Debug.Log("Auto Shift 1: "+find_shift_1() / 100f);
            Debug.Log("Shift: " + shift / 100f);
            if (lock_shift)
            {
                shift_1 = find_shift_1() / 100f;
            }
            else
            {
                shift_1 = shift / 100f;
            }

            //this is what is reported to output file
            if (currentZoomLevel == 2.5f) current_shift = optical_2_5;
            if (currentZoomLevel == 4f) current_shift = optical_4;
        }

        right_cam.transform.localPosition = new Vector3(right_shift - shift_1, 0f, 0f);
        left_cam.transform.localPosition = new Vector3(left_shift + shift_1, 0f, 0f);
        left_cam.GetComponent<ZoomController>().shift_i = shift_2;
        right_cam.GetComponent<ZoomController>().shift_i = shift_2;
    }

    void set_perfect(float zoom_l)
    {
        XRDevice.fovZoomFactor = 1f;
        XRDevice.fovZoomFactor = zoom_l;
    }

    void set_low_res(float zoom_l)
    {
        left_cam.GetComponent<ZoomController>().zoomLevel = zoom_l;
        right_cam.GetComponent<ZoomController>().zoomLevel = zoom_l;
        if (currentZoomMethod == 2f)
        {
            left_cam.GetComponent<ZoomController>().shift_i = shift / 100f;
            right_cam.GetComponent<ZoomController>().shift_i = shift / 100f;
        }
    }

    void set_one_eye(float zoom_l)
    {
        //var feature = ZoomRendererFeature1.Instance;
        //feature.m_ZoomLevel = zoom_l;
    }



    private void InitializeBounds() //This just calculates the image size to get the center-to-corner distance, so the shift value can interpolate between the two
    {
        if (image == null) return;

        Renderer imgRenderer = image.GetComponent<Renderer>();
        if (imgRenderer != null)
        {
            Vector3 centerToCorner = imgRenderer.bounds.max - imgRenderer.bounds.center;
            maxDistanceToCorner = centerToCorner.magnitude;
        }
        else
        {
            maxDistanceToCorner = 0.707f;
            Debug.LogWarning("No Renderer found on image. Using fallback corner distance.");
        }
    }
}
