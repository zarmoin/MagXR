using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public struct MapCheckpoint
{
    public Vector3 imageLocalPosition;
    public float magnificationMethod;
    public float fullZoomLevel;
    public float selectiveZoomFactor;
    public Vector3 imageLocalScale;
    public GameObject visualIndicator;
}


public class image_movement : MonoBehaviour
{
    public float move_speed = 1.0f;
    public Material mapMaterial;
    public Transform mapQuadTransform;
    public float magnification_method = 0f; //0 = full-view, 1 = selective, 2 = image
    public GameObject cameraOffset;
    public Transform rightControllerTransform;


    [Header("VR Fullscreen Setup")]
    public VRFullscreenMagnifier cameraMagnifierScripts;
    private float currentZoomFactor = 2f;

    [Header("Checkpoint Settings")]
    public GameObject checkpointPrefab;
    private List<MapCheckpoint> checkpoints = new List<MapCheckpoint>();
    private int currentCheckpointIndex = -1;
    private int maxCheckpoints = 5;

    [Header("Visual Search Data Tracking")]
    private Dictionary<string, int> cubeSelectionCounts = new Dictionary<string, int>();
    private List<string> selectionHistoryLog = new List<string>();
    private List<string> selectionMaterialHistoryLog = new List<string>();

    void Start()
    {
        cameraOffset = GameObject.Find("Camera Offset");
        UpdateMagnificationState();
    }

    void UpdateMagnificationState()
    {
        if (cameraMagnifierScripts == null) return;

        bool isSelectiveMode = (magnification_method >= 0.9f && magnification_method <= 1.1f);
        cameraMagnifierScripts.enabled = isSelectiveMode;

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

        if (Input.GetButtonDown("XRI_Right_Primary2DAxisClick") || Input.GetKeyDown(KeyCode.C))
        {
            HandleCheckpointPlacementOrRemoval();
        }

        if (Input.GetButtonDown("XRI_Right_GripButton") || Input.GetKeyDown(KeyCode.V))
        {
            CycleCheckpoints();
        }

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
            else if (magnification_method == 1f) //Selective
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

        if (Input.GetButtonDown("XRI_Right_Trigger") || Input.GetKeyDown(KeyCode.Space))
        {
            SelectCubeWithRaycast();
        }

        if (Input.GetKeyDown(KeyCode.K)) //Force data file to save
        {
            SaveSelectionDataToFile();
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
    void HandleCheckpointPlacementOrRemoval()
    {
        if (currentCheckpointIndex != -1 && checkpoints.Count > 0)
        {
            MapCheckpoint activeCP = checkpoints[currentCheckpointIndex];

            if (transform.localPosition == activeCP.imageLocalPosition && magnification_method == activeCP.magnificationMethod)
            {
                if (activeCP.visualIndicator != null) Destroy(activeCP.visualIndicator);

                checkpoints.RemoveAt(currentCheckpointIndex);
                currentCheckpointIndex = -1;
                UpdateIndicatorNumbers();
                Debug.Log("Checkpoint removed! Remaining total: " + checkpoints.Count);
                return;
            }
        }

        if (checkpoints.Count >= maxCheckpoints)
        {
            //Destroy physical object before removing reference index
            if (checkpoints[0].visualIndicator != null) Destroy(checkpoints[0].visualIndicator);
            checkpoints.RemoveAt(0);
            if (currentCheckpointIndex > 0) currentCheckpointIndex--;
        }

        MapCheckpoint cp = new MapCheckpoint();
        cp.imageLocalPosition = transform.localPosition;
        cp.magnificationMethod = magnification_method;
        cp.fullZoomLevel = GameObject.Find("Menu").GetComponent<enter_info>().zoom_level;
        cp.selectiveZoomFactor = currentZoomFactor;
        cp.imageLocalScale = mapQuadTransform.localScale;

        if (checkpointPrefab != null) //Place checkpoint at headset position
        {
            Transform headCamera = Camera.main.transform;
            Vector3 spawnWorldPos = new Vector3(headCamera.position.x, headCamera.position.y, 4f);
            cp.visualIndicator = Instantiate(checkpointPrefab, spawnWorldPos, Quaternion.identity, null);
            cp.visualIndicator.transform.SetParent(transform, true);
        }

        checkpoints.Add(cp);
        currentCheckpointIndex = checkpoints.Count - 1;
        UpdateIndicatorNumbers();
        Debug.Log("New checkpoint placed at headset coordinates and anchored to map! Index: " + currentCheckpointIndex);
    }

    void CycleCheckpoints()
    {
        if (checkpoints.Count == 0)
        {
            Debug.LogWarning("No checkpoints saved to cycle through!");
            return;
        }

        currentCheckpointIndex = (currentCheckpointIndex + 1) % checkpoints.Count;
        MapCheckpoint cp = checkpoints[currentCheckpointIndex];
        magnification_method = cp.magnificationMethod;
        UpdateMagnificationState();
        transform.localPosition = cp.imageLocalPosition;

        if (magnification_method == 0f)
        {
            GameObject.Find("Menu").GetComponent<enter_info>().zoom_level = cp.fullZoomLevel;
            GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_zoom();
            GameObject.Find("Zoom Controller").GetComponent<zoom_controller>().set_ready();
        }
        else if (magnification_method == 1f)
        {
            currentZoomFactor = cp.selectiveZoomFactor;
            Shader.SetGlobalFloat("_ZoomFactor", currentZoomFactor);
            Shader.SetGlobalFloat("_LensRadius", currentZoomFactor > 1.05f ? 0.3f : 0f);
        }
        else
        {
            mapQuadTransform.localScale = cp.imageLocalScale;
        }
        Debug.Log("Loaded Checkpoint slot index: " + currentCheckpointIndex);
    }

    void UpdateIndicatorNumbers()
    {
        for (int i = 0; i < checkpoints.Count; i++)
        {
            if (checkpoints[i].visualIndicator != null)
            {
                TMP_Text tmpComponent = checkpoints[i].visualIndicator.GetComponentInChildren<TMP_Text>();
                if (tmpComponent != null)
                {
                    tmpComponent.text = (i + 1).ToString();
                }
                else
                {
                    TextMesh standardText = checkpoints[i].visualIndicator.GetComponentInChildren<TextMesh>();
                    if (standardText != null) standardText.text = (i + 1).ToString();
                }
            }
        }
    }
    void SelectCubeWithRaycast()
    {
        if (rightControllerTransform == null)
        {
            GameObject foundObj = GameObject.Find("Right Controller") ?? GameObject.Find("RightHand Controller") ?? GameObject.Find("Right Hand");
            if (foundObj != null) rightControllerTransform = foundObj.transform;
        }

        if (rightControllerTransform == null)
        {
            Debug.LogError("[Selection Error] Trigger pressed, but rightControllerTransform is missing!");
            return;
        }

        Vector3 rayOrigin = rightControllerTransform.position;
        Vector3 rayDirection = rightControllerTransform.forward;
        var rayInteractor = rightControllerTransform.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>();
        if (rayInteractor == null)
        {
            rayInteractor = rightControllerTransform.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>();
        }

        if (rayInteractor != null)
        {
            rayOrigin = rayInteractor.rayOriginTransform.position;
            rayDirection = rayInteractor.rayOriginTransform.forward;
        }
        else
        {
            Debug.LogWarning("[Selection Trace] XRRayInteractor component not found on the assigned Transform or its children. Falling back to transform.forward.");
        }

        Ray ray = new Ray(rayOrigin, rayDirection);
        RaycastHit hit;

        int cubeLayerIndex = LayerMask.NameToLayer("SearchCube");
        if (cubeLayerIndex == -1)
        {
            Debug.LogError("[Selection Error] The layer 'SearchCube' does not exist in your Project Settings!");
            return;
        }

        int layerMask = 1 << cubeLayerIndex;
        Debug.DrawRay(ray.origin, ray.direction * 200f, Color.yellow, 2.0f);

        if (Physics.Raycast(ray, out hit, 200f, layerMask))
        {
            if (hit.transform.IsChildOf(transform))
            {
                string targetCubeName = hit.transform.name;

                string materialName = "UnknownMaterial";
                Renderer cubeRenderer = hit.transform.GetComponent<Renderer>();
                if (cubeRenderer != null && cubeRenderer.material != null)
                {
                    materialName = cubeRenderer.material.name.Replace(" (Instance)", "");
                }

                selectionHistoryLog.Add(targetCubeName);
                selectionMaterialHistoryLog.Add(materialName);

                if (cubeSelectionCounts.ContainsKey(targetCubeName))
                {
                    cubeSelectionCounts[targetCubeName]++;
                }
                else
                {
                    cubeSelectionCounts[targetCubeName] = 1;
                }

                Debug.Log($"SUCCESS! Selected Target: {targetCubeName} [{materialName}]. Total Clicks: {cubeSelectionCounts[targetCubeName]}");
            }
            else
            {
                Debug.LogWarning($"[Selection Trace] Hit object '{hit.transform.name}', but it is not a child of the map image script parent!");
            }
        }
        else
        {
            Debug.Log("[Selection Trace] Trigger pressed, but the physics ray didn't intersect with any object on the 'SearchCube' layer.");
        }
    }
    void SaveSelectionDataToFile()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string directoryPath = Path.Combine(projectRoot, "data");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        string fileName = $"SearchMetrics_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string fullPath = Path.Combine(directoryPath, fileName);

        using (StreamWriter writer = new StreamWriter(fullPath))
        {
            writer.WriteLine("Click Index,Cube Identifier Name,Material Name");
            for (int i = 0; i < selectionHistoryLog.Count; i++)
            {
                writer.WriteLine($"{i + 1},{selectionHistoryLog[i]},{selectionMaterialHistoryLog[i]}");
            }

            writer.WriteLine("\n");
            writer.WriteLine("Cube Identifier Name,Total Selection Occurrences,Repeat Counts");
            foreach (var kvp in cubeSelectionCounts)
            {
                int repeats = kvp.Value - 1;
                writer.WriteLine($"{kvp.Key},{kvp.Value},{repeats}");
            }
        }
        Debug.Log($"Visual search log file successfully compiled and saved directly to your Unity Project: {fullPath}");
    }
    void OnApplicationQuit()
    {
        if (selectionHistoryLog.Count > 0)
        {
            SaveSelectionDataToFile();
        }
    }
}