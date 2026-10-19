using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class image_movement : MonoBehaviour
{


    public float move_speed = 1.0f;
    public Material mapMaterial;       // Assign your new ScreenSpaceMagnifier material here
    public Transform mapQuadTransform;
    public float magnification_method = 0f; //0 = full-view, 1 = selective, 2 = image



    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (magnification_method == 2f)
        {
            if (mapQuadTransform.localScale == new Vector3(15, 15, 15)) move_speed = 1.0f;
            else if (mapQuadTransform.localScale == new Vector3(30, 30, 30)) move_speed = 2.0f;
            else if (mapQuadTransform.localScale == new Vector3(60, 60, 60)) move_speed = 4.0f;
        }


        move();
        if (Input.GetButtonDown("B") || Input.GetKeyDown(KeyCode.RightArrow)) //&& GameObject.Find("Menu").GetComponent<enter_info>().zoom_level < 4)
        {
            if (magnification_method == 0f)
            {
                if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 1f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 2.5f;
                else if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 2.5f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 4f;
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
            }
            else if (magnification_method == 1f)
            {
                if (mapMaterial != null)
                {
                    float zoom = 1f;

                    if (mapMaterial.GetFloat("_ZoomFactor") == 1f)
                    {
                        zoom = 2f;
                        mapMaterial.SetFloat("_ZoomFactor", zoom);
                    }
                    else if (mapMaterial.GetFloat("_ZoomFactor") == 2f)
                    {
                        zoom = 4f;
                        mapMaterial.SetFloat("_ZoomFactor", zoom);
                    }
                    // Turn on the lens by setting its radius (e.g., 20% of screen)
                    // If zoom is 1x, we visually turn it off by setting radius to 0
                    mapMaterial.SetFloat("_LensRadius", zoom > 1f ? 0.3f : 0f);
                }
            }
            else
            {
                GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 1f;
                mapMaterial.SetFloat("_ZoomFactor", 1f);
                mapMaterial.SetFloat("_LensRadius", 1f > 1f ? 0.3f : 0f);

                if (mapQuadTransform.localScale == new Vector3(15, 15, 15)) mapQuadTransform.localScale = new Vector3(30, 30, 30);
                else if (mapQuadTransform.localScale == new Vector3(30, 30, 30)) mapQuadTransform.localScale = new Vector3(60, 60, 60);
            }

        }
        
        if (Input.GetButtonDown("A") || Input.GetKeyDown(KeyCode.LeftArrow)) //&& GameObject.Find("Menu").GetComponent<enter_info>().zoom_level > 1)
        {
            if (magnification_method == 0f)
            {
                if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 4f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 2.5f;
                else if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 2.5f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 1f;
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
            }
            else if (magnification_method == 1f)
            {
                if (mapMaterial != null)
                {
                    float zoom = 4f;

                    if (mapMaterial.GetFloat("_ZoomFactor") == 4f)
                    {
                        zoom = 2f;
                        mapMaterial.SetFloat("_ZoomFactor", zoom);
                    }
                    else if (mapMaterial.GetFloat("_ZoomFactor") == 2f)
                    {
                        zoom = 1f;
                        mapMaterial.SetFloat("_ZoomFactor", zoom);
                    }
                    // Turn on the lens by setting its radius (e.g., 20% of screen)
                    // If zoom is 1x, we visually turn it off by setting radius to 0
                    mapMaterial.SetFloat("_LensRadius", zoom > 1f ? 0.3f : 0f);
                }
            }
            else
            {
                GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 1f;
                mapMaterial.SetFloat("_ZoomFactor", 1f);
                mapMaterial.SetFloat("_LensRadius", 1f > 1f ? 0.3f : 0f);

                if (mapQuadTransform.localScale == new Vector3(60, 60, 60)) mapQuadTransform.localScale = new Vector3(30, 30, 30);
                else if (mapQuadTransform.localScale == new Vector3(30, 30, 30)) mapQuadTransform.localScale = new Vector3(15, 15, 15);
            }
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (magnification_method == 0f) magnification_method = 1;
            else if (magnification_method == 1f) magnification_method = 2;
            else magnification_method = 0f;
        }

        if (Input.GetButtonDown("Y") || Input.GetKeyDown(KeyCode.RightArrow))
        {

        }

        if (Input.GetButtonDown("X") || Input.GetKeyDown(KeyCode.LeftArrow))
        {

        }
    }


    void move()
    {
        float left_y = Input.GetAxisRaw("Left Vertical");
        float left_x = Input.GetAxisRaw("Left Horizontal");


        float right_y = Input.GetAxisRaw("Right Vertical");
        float right_x = Input.GetAxisRaw("Right Horizontal");
        //Vector3 move_dir = Quaternion.AngleAxis(-45f, Vector3.up) * transform.forward;
        //transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z) + Time.deltaTime * move_speed * left_y * move_dir;

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
