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

        carModel = PhotonNetwork.Instantiate("Car", new Vector3(Random.Range(11, 12), 1, 0), Quaternion.identity);

        // Primer jugador (MasterClient) tiene auto + cámara
        if (PhotonNetwork.IsMasterClient)
        {
            //carModel.SetActive(true);
            //INSTANTIATE CAMERACONTROLLER
            GameObject mycamera = Instantiate(playerCameraPF, new Vector3(-0.79f, 3.4f, -16.5f), Quaternion.identity);
            //ASIGNO CAMERACONTROLLER
            mycamera.transform.SetParent(carModel.transform, false);
            //INSTANTIATE CONTROLLER
            CarController controladorCar = Instantiate(carControllerPF, Vector3.zero, Quaternion.identity);
            //ASIGNO
            controladorCar.transform.SetParent(carModel.transform, false);
            controladorCar.AssignWheels();
            //asigno valores
        }
        else
        {
            // Jugadores que no son Master → solo cámara
            //carModel.SetActive(false);
            GameObject mycamera = Instantiate(playerCameraPF, new Vector3(0.79f, 3.4f, -16.5f), Quaternion.identity);
            //ASIGNO CAMERACONTROLLER
            mycamera.transform.SetParent(carModel.transform, false);
            //INSTANTIATE CONTROLLER
        }
    }
}
