using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Member;

public class reset_position : MonoBehaviour
{
    GameObject headset_go;
    // Start is called before the first frame update
    void Start()
    {
        headset_go = GameObject.Find("Camera Offset");
    }

    // Update is called once per frame
    void Update()
    {
        if (/*Input.GetButtonDown("Y")*/ Input.GetKeyDown(KeyCode.R))
        {
            reset_pos();
        }
    }

    void reset_pos()
    {
        transform.rotation = Quaternion.Euler(0f, headset_go.transform.rotation.eulerAngles.y, 0f);
        transform.position = headset_go.transform.position - new Vector3(0f, 1.12f, 0f); //was 1.12f
    }
}
