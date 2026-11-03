using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;

public struct VisualSearchTrial
{
    public string mapImageName;
    public string targetCubeColor;
    public string zoomMethod;        //"Full", "Selective", or "Image"
    public bool checkpointsEnabled;  //true or false
}

public class enter_info : MonoBehaviour
{
    public GameObject mapParent;
    private image_movement imgMoveScript;
    private float trialTimer = 0f;
    public float TRIAL_DURATION = 120f;
    private bool isTimerRunning = false;
    private bool waitingForTriggerToAdvance = false;
    private bool experimentComplete = false;
    private float blockBreakCooldownTimer = 0f;
    private const float COOLDOWN_DURATION = 2.0f;
    private bool isCooldownActive = false;

    private bool showing_ipd = true;
    private int ipd_val = 63;
    private bool grabbing_ipd = false;

    private int study_mode = 1; //0 = Demo, 1 = Experiment
    private bool grabbing_mode = false;

    private int subj_num = 1;
    private bool grabbing_subj = false;

    private bool pulled = false;
    private List<string> file_out = new List<string>();
    private int out_row = 0;

    private List<VisualSearchTrial> experimentalTrials = new List<VisualSearchTrial>();
    private int currentTrialIndex = 0;
    private bool isPracticePhase = true;

    void Start()
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = "Before Starting, please ensure the device IPD is set to 60.<br>Pull the trigger on the right controller to continue.";
        if (mapParent != null)
        {
            mapParent.SetActive(false);
            imgMoveScript = mapParent.GetComponent<image_movement>();
        }
    }

    void Update()
    {
        if (experimentComplete) return;
        if (isCooldownActive)
        {
            blockBreakCooldownTimer -= Time.deltaTime;
            if (blockBreakCooldownTimer <= 0f)
            {
                isCooldownActive = false;
                UpdateIntermissionTextWithNextColor();
            }
        }
        if (waitingForTriggerToAdvance)
        {
            if (isCooldownActive) return;

            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && !pulled || Input.GetKeyDown(KeyCode.Return))
            {
                pulled = true;
                waitingForTriggerToAdvance = false;
                if (currentTrialIndex == 0 && (mapParent == null || !mapParent.activeSelf))
                {
                    LoadTrialData(currentTrialIndex);
                }
                else
                {
                    AdvanceToNextTrial();
                }
            }
            else if (TriggerReleased()) pulled = false;

            return;
        }

        if (grabbing_ipd)
        {
            ipd_val += get_inp();
            transform.GetChild(1).GetComponent<TMP_Text>().text = ipd_val.ToString();
            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && !pulled || Input.GetKeyDown(KeyCode.Return))
            {
                pulled = true;
                grabbing_ipd = false;
                transform.GetChild(0).GetComponent<TMP_Text>().text = "Select Mode.<br>0 = Demo, 1 = Actual Experiment.<br>Press the trigger to continue.";
                transform.GetChild(1).GetComponent<TMP_Text>().text = study_mode.ToString();
                GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ipd(ipd_val);
                grabbing_mode = true;
            }
            else if (TriggerReleased()) pulled = false;
        }
        else if (grabbing_mode)
        {
            study_mode += get_inp();
            if (study_mode > 1) study_mode = 1;
            else if (study_mode < 0) study_mode = 0;

            transform.GetChild(1).GetComponent<TMP_Text>().text = study_mode.ToString();

            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && !pulled || Input.GetKeyDown(KeyCode.Return))
            {
                pulled = true;
                grabbing_mode = false;

                if (study_mode == 0)
                {
                    subj_num = 0;
                    InitializeTrialStructure();
                    transform.GetChild(1).GetComponent<TMP_Text>().enabled = false;
                    StartIntermissionPeriod();
                }
                else
                {
                    transform.GetChild(0).GetComponent<TMP_Text>().text = "Enter Subject Number.<br>Press the trigger on the right controller to continue.";
                    transform.GetChild(1).GetComponent<TMP_Text>().text = subj_num.ToString();
                    grabbing_subj = true;
                }
            }
            else if (TriggerReleased()) pulled = false;
        }
        else if (grabbing_subj)
        {
            subj_num += get_inp();
            transform.GetChild(1).GetComponent<TMP_Text>().text = subj_num.ToString();
            if ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f) && !pulled || Input.GetKeyDown(KeyCode.Return))
            {
                pulled = true;
                grabbing_subj = false;

                InitializeTrialStructure();
                transform.GetChild(1).GetComponent<TMP_Text>().enabled = false;
                StartIntermissionPeriod();
            }
            else if (TriggerReleased()) pulled = false;
        }
        else if (showing_ipd && ((Input.GetAxisRaw("LeftTrigger") > 0.9f || Input.GetAxisRaw("XRI_Right_Trigger") > 0.9f)))
        {
            pulled = true;
            showing_ipd = false;
            grabbing_ipd = true;
            transform.GetChild(0).GetComponent<TMP_Text>().text = "Enter IPD Number.<br>Press the trigger to continue.";
            transform.GetChild(1).GetComponent<TMP_Text>().text = ipd_val.ToString();
        }

        if (isTimerRunning && study_mode == 1)
        {
            trialTimer += Time.deltaTime;
            if (trialTimer >= TRIAL_DURATION)
            {
                OnTrialTimeExpired();
            }
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            string path = "data/s" + subj_num + "_summary.csv";
            File.WriteAllLines(path, file_out.ToArray());
            Debug.Log("Data exported early");
        }
    }


    private bool TriggerReleased()
    {
        return Input.GetAxisRaw("LeftTrigger") == 0f && Input.GetAxisRaw("XRI_Right_Trigger") == 0f;
    }

    int get_inp()
    {
        if (Input.GetButtonDown("B") || Input.GetKeyDown(KeyCode.RightArrow)) return 1;
        if (Input.GetButtonDown("A") || Input.GetKeyDown(KeyCode.LeftArrow)) return -1;
        if (Input.GetButtonDown("Y") || Input.GetKeyDown(KeyCode.RightArrow)) return 1;
        if (Input.GetButtonDown("X") || Input.GetKeyDown(KeyCode.LeftArrow)) return -1;
        return 0;
    }

    public void InitializeTrialStructure()
    {
        experimentalTrials.Clear();
        currentTrialIndex = 0;
        if (study_mode == 0)
        {
            isPracticePhase = true;
            BuildPracticeQueue();
        }
        else
        {
            isPracticePhase = true;
            BuildPracticeQueue();
            LoadCounterbalancedFile();
        }
        file_out.Clear();
        file_out.Add("Subject,TrialNum,Phase,MapImage,TargetColor,ZoomMethod,CheckpointsEnabled,ClickIndex,CubeName,TimeOffset_Seconds,CubeMaterial,ClickAccuracy");
    }

    private void BuildPracticeQueue()
    {
        string[] colors = { "Blue", "Red", "Yellow", "Green", "Blue", "Red" };
        string[] methods = { "Full", "Selective", "Image", "Full", "Selective", "Image" };
        bool[] checkpoints = { true, false, true, false, true, false };

        for (int i = 0; i < 6; i++)
        {
            VisualSearchTrial trial;
            trial.mapImageName = $"Practice_Map_{i + 1}";
            trial.targetCubeColor = colors[i];
            trial.zoomMethod = methods[i];
            trial.checkpointsEnabled = checkpoints[i];
            experimentalTrials.Add(trial);
        }
    }

    private void LoadCounterbalancedFile()
    {
        string path = Application.streamingAssetsPath + "/subj" + subj_num + "_input.csv";
        if (!File.Exists(path))
        {
            Debug.LogError($"[File Missing] Cannot find subject config file at: {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        int assignedSetIndex = (subj_num - 1) % 6;

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] columns = lines[i].Split(',');
            if (columns.Length < 5)
            {
                Debug.LogWarning($"[CSV Format Error] Line {i + 1} has fewer than 5 columns! Skipping row.");
                continue;
            }

            VisualSearchTrial trial;
            trial.mapImageName = $"Set_{assignedSetIndex}_Map_{i}";
            trial.targetCubeColor = columns[2].Trim();
            trial.zoomMethod = columns[3].Trim();
            string checkpointString = columns[4].Trim();

            if (checkpointString.Equals("true", StringComparison.OrdinalIgnoreCase) || checkpointString == "1")
            {
                trial.checkpointsEnabled = true;
            }
            else
            {
                trial.checkpointsEnabled = false;
            }
            experimentalTrials.Add(trial);
        }
    }

    public void AdvanceToNextTrial()
    {
        isTimerRunning = false;
        trialTimer = 0f;
        waitingForTriggerToAdvance = false;
        if (mapParent != null)
        {
            image_movement imgMove = mapParent.GetComponent<image_movement>();
            if (imgMove != null)
            {
                imgMove.magnification_method = 0f;
                if (imgMove.mapQuadTransform != null)
                {
                    imgMove.mapQuadTransform.localScale = new Vector3(15f, 15f, 15f);
                }
                imgMove.ResetInternalCheckpointMemory();
                imgMove.UpdateMagnificationState();
            }
            GameObject zoomCtrlObj = GameObject.Find("Zoom Controller");
            if (zoomCtrlObj != null)
            {
                zoom_controller zoomCtrl = zoomCtrlObj.GetComponent<zoom_controller>();
                if (zoomCtrl != null)
                {
                    zoomCtrl.zoomLevel = 1f;
                    zoomCtrl.set_zoom();
                }
            }
            for (int i = mapParent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = mapParent.transform.GetChild(i);
                if (child.name.Contains("Checkpoint"))
                {
                    Destroy(child.gameObject);
                }
            }
        }
        if (isPracticePhase && currentTrialIndex >= 6)
        {
            if (study_mode == 0) { end(); return; }
            isPracticePhase = false;
        }
        if (currentTrialIndex >= experimentalTrials.Count)
        {
            end();
            return;
        }
        LoadTrialData(currentTrialIndex);
    }

    private void LoadTrialData(int index)
    {
        if (index >= experimentalTrials.Count)
        {
            end();
            return;
        }
        if (mapParent != null) mapParent.SetActive(true);
        if (imgMoveScript == null && mapParent != null)
        {
            imgMoveScript = mapParent.GetComponent<image_movement>();
        }

        VisualSearchTrial currentTrial = experimentalTrials[index];
        int displayedTrialNum = index + 1;
        int totalExpected = study_mode == 0 ? 6 : 12;
        string phaseLabel = isPracticePhase ? "PRACTICE" : "REAL";

        transform.GetChild(0).GetComponent<TMP_Text>().text = $"{phaseLabel} TRIAL {displayedTrialNum}/{totalExpected}<br>Find all the <color={currentTrial.targetCubeColor.ToLower()}>{currentTrial.targetCubeColor}</color> cubes!<br>Pull trigger when ready.";
        if (imgMoveScript != null)
        {
            if (currentTrial.zoomMethod == "Full") imgMoveScript.magnification_method = 0f;
            else if (currentTrial.zoomMethod == "Selective") imgMoveScript.magnification_method = 1f;
            else if (currentTrial.zoomMethod == "Image") imgMoveScript.magnification_method = 2f;

            imgMoveScript.UpdateMagnificationState();
        }
        Texture2D loadedMap = Resources.Load<Texture2D>($"MapImages/{currentTrial.mapImageName}");
        if (loadedMap != null)
        {
            mapParent.GetComponent<Renderer>().material.mainTexture = loadedMap;
        }
        else
        {
            Debug.LogWarning($"[Asset Missing] Could not find texture resource at Assets/Resources/MapImages/{currentTrial.mapImageName}");
        }
        foreach (Transform childCubeGroup in mapParent.transform)
        {
            childCubeGroup.gameObject.SetActive(childCubeGroup.name == currentTrial.mapImageName);
        }
        trialTimer = 0f;
        isTimerRunning = true;
    }

    private void StartIntermissionPeriod()
    {
        isTimerRunning = false;
        trialTimer = 0f;
        blockBreakCooldownTimer = COOLDOWN_DURATION;
        isCooldownActive = true;
        waitingForTriggerToAdvance = true;

        if (mapParent != null) mapParent.SetActive(false);
        transform.GetChild(0).GetComponent<TMP_Text>().text = "Please wait... preparing next block.";
    }

    private void UpdateIntermissionTextWithNextColor()
    {
        VisualSearchTrial nextTrial = GetActiveTrialConfig();
        if (currentTrialIndex < experimentalTrials.Count)
        {
            nextTrial = experimentalTrials[currentTrialIndex];
            string contextMessage = (currentTrialIndex == 0) ? "Setup Complete." : "Time is up!";
            string checkpointStatus = nextTrial.checkpointsEnabled ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
            transform.GetChild(0).GetComponent<TMP_Text>().text =
                $"{contextMessage}<br>" +
                $"Next Target: <color={nextTrial.targetCubeColor.ToLower()}>{nextTrial.targetCubeColor}</color>.<br>" +
                $"{nextTrial.zoomMethod} Zoom<br>" +
                $"Spatial Checkpoints: {checkpointStatus}<br><br>" +
                "Press the trigger to begin.";
        }
    }

    private void OnTrialTimeExpired()
    {
        isTimerRunning = false;
        trialTimer = 0f;

        if (mapParent != null)
        {
            image_movement imgMove = mapParent.GetComponent<image_movement>();
            if (imgMove != null)
            {
                imgMove.magnification_method = 0f;
                if (imgMove.mapQuadTransform != null)
                {
                    imgMove.mapQuadTransform.localScale = new Vector3(15f, 15f, 15f);
                }
                imgMove.ResetInternalCheckpointMemory();
                imgMove.UpdateMagnificationState();
            }
            for (int i = mapParent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = mapParent.transform.GetChild(i);
                if (child.name.Contains("Checkpoint")) Destroy(child.gameObject);
            }
            mapParent.SetActive(false);
        }
        currentTrialIndex++;
        if (currentTrialIndex >= experimentalTrials.Count)
        {
            end();
            return;
        }
        StartIntermissionPeriod();
    }

    public float GetRelativeTrialTime()
    {
        return trialTimer;
    }
    public VisualSearchTrial GetActiveTrialConfig()
    {
        if (currentTrialIndex < experimentalTrials.Count) return experimentalTrials[currentTrialIndex];
        return default(VisualSearchTrial);
    }
    public void LogIndividualClick(string cubeName, string timeStamp, string materialName, string accuracyLabel)
    {
        VisualSearchTrial currentTrial = GetActiveTrialConfig();
        string phaseLabel = isPracticePhase ? "PRACTICE" : "REAL";
        int clickIndex = file_out.Count;
        string rowEntry = $"{subj_num},{currentTrialIndex + 1},{phaseLabel},{currentTrial.mapImageName},{currentTrial.targetCubeColor},{currentTrial.zoomMethod},{currentTrial.checkpointsEnabled},{clickIndex},{cubeName},{timeStamp},{materialName},{accuracyLabel}";
        file_out.Add(rowEntry);
        Debug.Log($"[DATALOG REGISTERED] Added row: {rowEntry}");
    }
    public bool IsCheckpointAllowedOnCurrentTrial()
    {
        VisualSearchTrial trial = GetActiveTrialConfig();
        return trial.checkpointsEnabled;
    }
    void end()
    {
        isTimerRunning = false;
        waitingForTriggerToAdvance = false;
        experimentComplete = true;
        if (mapParent != null) mapParent.SetActive(false);
        transform.GetChild(0).GetComponent<TMP_Text>().text = "Experiment Complete!";
        if (transform.childCount > 1) transform.GetChild(1).gameObject.SetActive(false);
        string path = "data/s" + subj_num + "_summary.csv";
        File.WriteAllLines(path, file_out.ToArray());
        Debug.Log("Experiment completed successfully. Target file written out to: " + path);
    }
}