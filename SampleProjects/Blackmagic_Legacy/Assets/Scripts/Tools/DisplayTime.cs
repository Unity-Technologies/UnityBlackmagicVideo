using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class DisplayTime : MonoBehaviour
{
    [SerializeField]
    TextMesh text;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        text.text = DateTime.Now.Millisecond.ToString();
    }
}
