using UnityEngine;
using System.Collections;

public class RadioScript : MonoBehaviour
{
    private bool encendido;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(Input.GetButtonDown("R") && encendido == false)
        {
            encendido = true;
            Debug.Log(encendido);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
