using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class randomize_dist : MonoBehaviour
{
    [SerializeField]
    bool fixed_visual_angle = false;
    GameObject menu;
    // Start is called before the first frame update
    void Start()
    {
        menu = GameObject.Find("Menu");
    }

    // Update is called once per frame
    void Update()
    {
        /*if (Input.GetButtonDown("B")) fixed_visual_angle = !fixed_visual_angle;
        if (fixed_visual_angle)
        {
            float size = Mathf.Tan(0.01333f) * transform.position.z;
            transform.localScale = new Vector3(-size, -size, -size);
        }
        else
        {
            transform.localScale = new Vector3(-0.01f, -0.01f, -0.01f);
        }*/
    }

    public void randomize()
    {
        menu.GetComponent<enter_info>().next_inp();
        float dist = menu.GetComponent<enter_info>().distance;
        Debug.Log("Moving virtual object to: " + dist + " m");
        transform.localPosition = new Vector3 (transform.localPosition.x, transform.localPosition.y, dist);
        GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
    }
}
