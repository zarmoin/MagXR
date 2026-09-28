using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class control_depth : MonoBehaviour
{
    public bool moving = false;
    bool submitable = false;
    public float move_speed = 1.0f;
    public bool fixed_visual_angle = false;
    public float visible_time = 2.0f; //this is the amount of time that the virtual object is visible for the blind reaching task
    public float elapsed_time = 0f; //variable for the amount of time the object has been visible

    GameObject other_py; //other_py is the virtual object
    GameObject right_cont;
    GameObject left_cont; //NEW
    // Start is called before the first frame update
    void Start()
    {
        other_py = GameObject.Find("Py2");
        right_cont = GameObject.Find("Right Controller");
        left_cont = GameObject.Find("Left Controller"); //NEW
    }

    // Update is called once per frame
    void Update()
    {
        if (moving == true) elapsed_time += Time.deltaTime; //moving == true makes sure the object is not disabled before trials
        if (moving)
        {
            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && submitable && moving) submit();
            if (GameObject.Find("Menu").GetComponent<enter_info>().blind_reaching == true)
            {
                if (elapsed_time > visible_time) other_py.SetActive(false); //makes the object invisible after the visible time period is over
                if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == true && !submitable && Input.GetAxisRaw("LeftTrigger") == 0f && (elapsed_time > visible_time)) //makes sure that the visible time has passed before the user can make an input
                {
                    submitable = true;
                    Debug.Log("RESET " + Input.GetAxisRaw("LeftTrigger"));
                }
                else if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == false && !submitable && Input.GetAxisRaw("XRI_Right_Trigger") == 0f && (elapsed_time > visible_time)) //makes sure that the visible time has passed before the user can make an input
                {
                    submitable = true;
                    Debug.Log("RESET " + Input.GetAxisRaw("XRI_Right_Trigger"));
                }
            }
            else if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == true && !submitable && Input.GetAxisRaw("LeftTrigger") == 0f)
            {
                submitable = true;
                Debug.Log("RESET " + Input.GetAxisRaw("LeftTrigger"));
            }
            else if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == false && !submitable && Input.GetAxisRaw("XRI_Right_Trigger") == 0f)
            {
                submitable = true;
                Debug.Log("RESET " + Input.GetAxisRaw("XRI_Right_Trigger"));
            }
            move();
        }
        
        //if (Input.GetAxisRaw("Trigger Axis") > 0.9f && submitable) submit2();
        
        /*if (Input.GetButtonDown("B")) fixed_visual_angle=!fixed_visual_angle;
        if (fixed_visual_angle)
        {
            float size = Mathf.Tan(0.006666f) * transform.localPosition.z;
            transform.localScale = new Vector3(size, size, size);
        }
        else
        {
            transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        }*/
    }

    void move()
    {
        float left_y = Input.GetAxisRaw("Left Vertical");
        float right_y = Input.GetAxisRaw("Right Vertical");
        Vector3 move_dir = Quaternion.AngleAxis(-45f, Vector3.up) * transform.forward;
        //transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z) + Time.deltaTime * move_speed * left_y * move_dir;

        if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == true)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z + Time.deltaTime * move_speed * left_y);
        }
        else
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z + Time.deltaTime * move_speed * right_y);
        }
    }

    void submit()
    {
        submitable = false;
        elapsed_time = 0f;
        other_py.SetActive(true);
        //float distance = other_py.transform.localPosition.z - transform.localPosition.z; ORIGINAL, PERCEIVED DEPTH
        float distance;
        if (GameObject.Find("Menu").GetComponent<enter_info>().blind_reaching == false)
        {
            distance = transform.position.z - GameObject.Find("Camera Offset").transform.position.z; //PERCEIVED DISTANCE
        }
        else
        {
            if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == true) distance = (left_cont.transform.position.z + 0.06f) - GameObject.Find("Camera Offset").transform.position.z;
            else distance = (right_cont.transform.position.z + 0.06f) - GameObject.Find("Camera Offset").transform.position.z;
        }

        float given_controller_distance;
        if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == true) given_controller_distance = Mathf.Abs(Vector3.Distance(left_cont.transform.position + (left_cont.transform.forward * 0.06f), other_py.transform.position));
        else given_controller_distance = Mathf.Abs(Vector3.Distance(right_cont.transform.position + (right_cont.transform.forward * 0.06f), other_py.transform.position));

        float controller_distance;
        if (GameObject.Find("Menu").GetComponent<enter_info>().left_hand == true) controller_distance = Mathf.Abs(left_cont.transform.position.z + 0.06f - other_py.transform.position.z);
        else controller_distance = Mathf.Abs(right_cont.transform.position.z + 0.06f - other_py.transform.position.z); //NEW //this should be working correctly

        //GameObject.Find("Menu").GetComponent<enter_info>().save_match(distance); //ORIGINAL
        GameObject.Find("Menu").GetComponent<enter_info>().save_match(distance, controller_distance, given_controller_distance);
        Debug.Log("SUBMITTING MATCH DISTANCE OF : " + distance);
        other_py.GetComponent<randomize_dist>().randomize();
        //transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0.4f);
    }

    public void reset_marker()
    {
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0.45f); //used to be 0.4f, but since distances changed to .4f, .5f, and .6f, it needs to be .45f
    }
}
