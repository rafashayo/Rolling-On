using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Photon.Pun;

public class Spowner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PhotonNetwork.Instantiate("Car", new Vector3(Random.Range(11, 12), 1 , 0) , Quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
