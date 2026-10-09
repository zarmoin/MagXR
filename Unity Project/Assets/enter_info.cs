using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;
using Unity.VisualScripting;

public class enter_info : MonoBehaviour
{
    private bool showing_ipd = true;

    private int ipd_val = 63;
    private bool grabbing_ipd = false;

    private int subj_num = 1;
    private bool grabbing_subj = false;

    private bool pulled = false;

    private string[] file_data;
    public float zoom_level = 1;
    public int zoom_mode = 1;
    public float distance;
    private int row = 0;

    private string[] file_out;
    private int out_row = 0;

    private bool taking_likert = false;
    private int likert_val = 3;
    private int likert_index = 4; //previously 0

    private float likert_z_level;
    private float likert_z_mode;

    public bool blind_reaching; //if wanting to perform the blind reaching task, set to true
    public bool left_hand; //if user is using the left controller, set to true
    
    // Start is called before the first frame update
    void Start()
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = "Before Starting, please ensure the device IPD is set to 60.<br>Pull the trigger on the right controller to continue.";
    }

    // Update is called once per frame // LOOK TO TAKE THE QUESTIONNAIRE OUT OF VR, AND ALSO ADD zoom_controller VALUE CALIBRATION HERE
    void Update()
    {
        if (grabbing_ipd)
        {
            ipd_val += get_inp();
            transform.GetChild(1).GetComponent<TMP_Text>().text = ipd_val.ToString();
            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && !pulled || Input.GetKeyDown(KeyCode.Return))
            {
                pulled = true;
                grabbing_ipd = false;
                transform.GetChild(0).GetComponent<TMP_Text>().text = "Enter Subject Number.<br>Press the trigger on the right controller to continue.";
                transform.GetChild(1).GetComponent<TMP_Text>().text = subj_num.ToString();
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ipd(ipd_val);
                grabbing_subj = true;
            }
            else if ((Input.GetAxisRaw("LeftTrigger") == 0f && left_hand == true) || (Input.GetAxisRaw("XRI_Right_Trigger") == 0f && left_hand == false)) pulled = false;
        }
        else if (grabbing_subj)
        {
            subj_num += get_inp();
            transform.GetChild(1).GetComponent<TMP_Text>().text = subj_num.ToString();
            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && !pulled || Input.GetKeyDown(KeyCode.Return))
            {
                pulled = true;
                grabbing_subj = false;
                read_inp();
                transform.GetChild(1).GetComponent<TMP_Text>().enabled = false; //new, hides the likert value
                //transform.GetChild(0).GetComponent<TMP_Text>().text = "From 1 to 5, how tired are your eyes?<br>1 being not tired at all and 5 being extremely tired";
                transform.GetChild(0).GetComponent<TMP_Text>().text = "Press the trigger to continue to the next<br>block of experiment trials";

                taking_likert = true;
            }
            else if ((Input.GetAxisRaw("LeftTrigger") == 0f && left_hand == true) || (Input.GetAxisRaw("XRI_Right_Trigger") == 0f && left_hand == false)) pulled = false;
        }
        else if (showing_ipd && ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f))/* || Input.GetKeyDown(KeyCode.Return)*/)
        {
            pulled = true;
            showing_ipd = false;
            grabbing_ipd = true;
            transform.GetChild(0).GetComponent<TMP_Text>().text = "Enter IPD Number, if you aren't sure what this is then please ask the experimentor.<br>Press the trigger on the right controller to continue.";
            transform.GetChild(1).GetComponent<TMP_Text>().text = ipd_val.ToString();
        }
        else if (taking_likert)
        {
            likert_val += get_inp();

            if (likert_val > 5) likert_val = 5;
            else if (likert_val < 1) likert_val = 1;

            transform.GetChild(1).GetComponent<TMP_Text>().text = likert_val.ToString();

            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && !pulled/* || Input.GetKeyDown(KeyCode.Return)*/)
            {
                pulled = true;
                continue_likert();
            }
            else if ((Input.GetAxisRaw("LeftTrigger") == 0f && left_hand == true) || (Input.GetAxisRaw("XRI_Right_Trigger") == 0f && left_hand == false)) pulled = false;
        }

        if (Input.GetKeyDown(KeyCode.A)) //MAY NOT BE NEEDED
        {

        }
        if (Input.GetKeyDown(KeyCode.D))
        {

        }
    }

    private void continue_likert()
    {
        save_likert(likert_index + 1, likert_val);
        switch (likert_index)
        {
            case 0:
                transform.GetChild(0).GetComponent<TMP_Text>().text = "From 1 to 5, how blurry are your eyes?<br>1 being not blurry at all and 5 being extremely blurry";
                break;
            case 1:
                transform.GetChild(0).GetComponent<TMP_Text>().text = "From 1 to 5, how tired and sore are your neck and back?<br>1 being not tired or sore at all and 5 being extremely tired and sore";
                break;
            case 2:
                transform.GetChild(0).GetComponent<TMP_Text>().text = "From 1 to 5, how do your eyes feel (strain on your eyes)?<br>1 being not strained at all and 5 being extremely strained";
                break;
            case 3:
                //transform.GetChild(0).GetComponent<TMP_Text>().text = "From 1 to 5, how does your head feel (headache)?<br>1 being no headache at all and 5 being a very bad headache";
                transform.GetChild(0).GetComponent<TMP_Text>().text = "Block completed. Press the trigger to continue to the next block of trials";
                break;
            case 4:
                //if (blind_reaching == true) GameObject.Find("Py").SetActive(false); //makes the arrow invisible if it is the blind reaching task //NOT NEEDED
                GameObject.Find("Py").GetComponent<control_depth>().reset_marker();
                likert_index = 3; //previously -1
                if (row > file_data.Length - 1) end();
                else start();
                break;
        }
        likert_index++;
    }

    private void start()
    {
        taking_likert = false;
        transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;
        GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
        //GameObject.Find("Py").transform.GetChild(0).gameObject.SetActive(true);
        GameObject.Find("Py").transform.GetChild(0).gameObject.SetActive(!blind_reaching); //sets visibility of marker depending if it is the blind reaching task
        GameObject.Find("Py2").transform.GetChild(0).gameObject.SetActive(true);
        GameObject.Find("Py").GetComponent<control_depth>().moving = true;
        if (row < 2) GameObject.Find("Py2").GetComponent<randomize_dist>().randomize();
    }

    int get_inp()
    {
        if (Input.GetButtonDown("B") || Input.GetKeyDown(KeyCode.RightArrow)) return 1;
        if (Input.GetButtonDown("A") || Input.GetKeyDown(KeyCode.LeftArrow)) return -1;
        if (Input.GetButtonDown("Y") || Input.GetKeyDown(KeyCode.RightArrow)) return 1;
        if (Input.GetButtonDown("X") || Input.GetKeyDown(KeyCode.LeftArrow)) return -1;
        return 0;
    }


    //public void save_match(float dist) ORIGINAL
    public void save_match(float dist, float controller_dist, float given_controller_dist)
    {
        Debug.Log("Curr save = " + distance + "   " + dist);
        string z_method = ",";
        if (zoom_mode == 1) z_method = "high_res,";
        else if (zoom_mode == 2) z_method = "low_res,";
        int z_level = 0;
        if (zoom_level == 2.5f) z_level = 2;
        else if (zoom_level == 4) z_level = 4;
        int row_num = row - 1;
        float shift_out = GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().current_shift;
        string data_type;
        if (blind_reaching == true) data_type = "blind reaching";
        else data_type = "matching";

        //file_out[out_row] = subj_num + "," + row_num + ",matching,-1," + z_method + z_level + "," + distance + "," + dist; ORIGINAL
        //file_out[out_row] = subj_num + "," + row_num + "," + data_type + ",-1," + z_method + z_level + "," + distance + "," + dist + "," + controller_dist + "," + given_controller_dist + "," + shift_out; SECOND VERSION
        file_out[out_row] = subj_num + "," + row_num + "," + data_type + ",-1," + z_method + z_level + "," + distance + "," + dist + "," + ipd_val;
        out_row++;
    }

    public void save_likert(int question, int like)
    {
        string z_method = ",";
        if (likert_z_mode == 1) z_method = "high_res,";
        else if (likert_z_mode == 2) z_method = "low_res,";
        int row_num = row - 1;
        if (row_num > 0) row_num -= 1;
        int z_level = 0;
        if (likert_z_level == 2.5f) z_level = 2;
        else if (likert_z_level == 4) z_level = 4;
        if (row_num == 0)
        {
            z_level = -1;
            z_method = "-1,";
        }
        //file_out[out_row] = subj_num + "," + row_num + ",likert," + question + "," + z_method + z_level + ",-1," + like; ORIGINAL
        //REMOVED SINCE LIKERT IS NO LONGER TAKEN IN VR    //file_out[out_row] = subj_num + "," + row_num + ",likert," + question + "," + z_method + z_level + ",-1," + like + "," + "none" + "," + "none";
        //REMOVED SINCE LIKERT IS NO LONGER TAKEN IN VR   //out_row++;
    }

    private void start_likert()
    {
        likert_z_level = zoom_level;
        likert_z_mode = zoom_mode;
        GameObject.Find("Py").GetComponent<control_depth>().moving = false;
        GameObject.Find("Py").transform.GetChild(0).gameObject.SetActive(false);
        GameObject.Find("Py2").transform.GetChild(0).gameObject.SetActive(false);
        taking_likert = true;
        transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
        //REMOVED SO LIKERT VALUE DOESN'T APPEAR   //transform.GetChild(1).GetComponent<MeshRenderer>().enabled = true;
        GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().override_zoom();
    }

    public void next_inp()
    {
        Debug.Log("Row: " + row);
        if (row > file_data.Length - 1)
        {
            start_likert();
        }
        else
        {
            string[] line = file_data[row].Split(',');
            int new_zoom_mode = -1;
            if (line[3] == "low res") new_zoom_mode = 2;
            else new_zoom_mode = 1;
            float new_zoom_level = float.Parse(line[4]);

            if ((new_zoom_level != zoom_level || new_zoom_mode != zoom_mode) && row > 1) start_likert();

            zoom_level = new_zoom_level;
            zoom_mode = new_zoom_mode;

            distance = float.Parse(line[5]) / 100f;
            row++;
        }
    }

    void end()
    {
        taking_likert = false;
        transform.GetChild(1).GetComponent<MeshRenderer>().enabled = false;
        transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
        transform.GetChild(0).GetComponent<TMP_Text>().text = "Experiment Complete!";
        GameObject.Find("Py").transform.GetChild(0).gameObject.SetActive(false);
        GameObject.Find("Py").GetComponent<control_depth>().moving = false;
        GameObject.Find("Py2").transform.GetChild(0).gameObject.SetActive(false);
        zoom_level = 1;
        GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();

        string path = "data/s" + subj_num + "_data.csv";
        File.WriteAllLines(path, file_out);
    }

    public void read_inp()
    {
        //string path = Application.persistentDataPath + "/inputFiles/subj" + subj_num + "_input.csv";
        string path;
        //if (blind_reaching == true) path = Application.streamingAssetsPath + "/subj" + subj_num.ToString() + "_input_blind_reaching.csv";
        path = Application.streamingAssetsPath + "/subj" + subj_num.ToString() + "_input.csv";
            Debug.LogWarning(path);
        file_data = File.ReadAllLines(path);

        file_out = new string[file_data.Length + 1 + 7 * 5];
        //file_out[out_row] = "Subject,TrialNum,DataType,Question,ZoomType,ZoomLevel,VirtualObjDepth,Value"; ORIGINAL
        //file_out[out_row] = "Subject,TrialNum,DataType,Question,ZoomType,ZoomLevel,VirtualObjDepth,Value,ControllerDist,GivenDist,ShiftOut"; SECOND VERSION
        file_out[out_row] = "Subject,TrialNum,DataType,Question,ZoomType,ZoomLevel,VirtualObjDepth,PerceivedDistance,IPD";

        out_row++;

        row = 1;
    }
}