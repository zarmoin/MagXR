using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public Vector3 rotationSpeed = new Vector3(0f, 0f, 90f);

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}
