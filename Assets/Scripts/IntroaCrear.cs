using System.Collections;    
using System.Collections.Generic;    
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class IntroaCrear : MonoBehaviour
{
    void Update()
    {
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("Joinearse");
    }
}
