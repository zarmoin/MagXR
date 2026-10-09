using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class image_movement : MonoBehaviour
{


    public float move_speed = 1.0f;





    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        move();
        if (Input.GetButtonDown("B") || Input.GetKeyDown(KeyCode.RightArrow) && GameObject.Find("Menu").GetComponent<enter_info>().zoom_level < 4)
        {
            if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 1f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 2.5f;
            else if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 2.5f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 4f;
            GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
            GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
        }
        
        if (Input.GetButtonDown("A") || Input.GetKeyDown(KeyCode.LeftArrow) && GameObject.Find("Menu").GetComponent<enter_info>().zoom_level > 1)
        {
            if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 4f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 2.5f;
            else if (GameObject.Find("Menu").GetComponent<enter_info>().zoom_level == 2.5f) GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = 1f;
            GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
            GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
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
