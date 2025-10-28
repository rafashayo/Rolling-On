using Photon.Pun;
using Unity.VisualScripting;
using UnityEngine;

public class CarSpawnHandler : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject carModel;   // el mesh del auto
    [SerializeField] private GameObject playerCameraPF;   // la cámara del jugador
    [SerializeField] private CarController carControllerPF;   // el controlador del jugador
    void Start()
    {
        //PHOTONINSTANTIANTIATE CARMODEL

        if (PhotonNetwork.IsMasterClient)
        {
            carModel = PhotonNetwork.Instantiate("CarOne", new Vector3(Random.Range(11, 12), 1, 120), Quaternion.identity);
            CarController controladorCar = Instantiate(carControllerPF, Vector3.zero, Quaternion.identity);
            controladorCar.transform.SetParent(carModel.transform, false);
            controladorCar.AssignWheels();
            GameObject mycamera = Instantiate(playerCameraPF, new Vector3(-0.617231011f, 2.38640857f, -12.2114201f), Quaternion.identity);
            mycamera.transform.SetParent(carModel.transform, false);
        }
        else { 
            GameObject mycamera = Instantiate(playerCameraPF, new Vector3(0.617231011f, 2.38640857f, -12.2114201f), Quaternion.identity);
            mycamera.transform.SetParent(carModel.transform, false);

        }

        // Primer jugador (MasterClient) tiene auto + cámara
        //carModel.SetActive(true);
        //INSTANTIATE CAMERACONTROLLER
        //ASIGNO CAMERACONTROLLER
        //INSTANTIATE CONTROLLER
        //ASIGNO
        //asigno valores
    }
    
   /* void SpawnSecondCamera()
    {
        GameObject mycamera = Instantiate(playerCameraPF, new Vector3(-0.79f, 3.4f, -16.5f), Quaternion.identity);

    }*/
}
