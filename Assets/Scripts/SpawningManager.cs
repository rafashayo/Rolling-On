using Photon.Pun;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections;

public class CarSpawnHandler : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject carModel;         // el mesh del auto
    [SerializeField] private GameObject playerJugador;    // la cámara del jugador
    [SerializeField] private CarController carControllerPF; // el controlador del jugador
    public TrackGenerator myterrainGenerator;

    IEnumerator Start()
    {
        // Buscar el generador de terreno
        if (myterrainGenerator == null)
            myterrainGenerator = FindObjectOfType<TrackGenerator>();

        if (myterrainGenerator == null)
        {
            Debug.LogError("[CarSpawnHandler] No se encontró TrackGenerator en escena.");
            yield break;
        }

        GameObject theplayer = null;

        if (PhotonNetwork.IsMasterClient)
        {
            // Spawnea el auto en red (esto sí aparece en todos)
            carModel = PhotonNetwork.Instantiate("CarOne", new Vector3(Random.Range(11, 12), 1, 120), Quaternion.identity);

            // Localmente, le agregás el controlador y la cámara
            CarController controladorCar = Instantiate(carControllerPF, Vector3.zero, Quaternion.identity);
            controladorCar.transform.SetParent(carModel.transform, false);
            controladorCar.AssignWheels();

            theplayer = Instantiate(playerJugador, new Vector3(-0.6172f, 2.3864f, -12.2114f), Quaternion.identity);
        }
        else
        {
            // Esperar hasta que CarOne(Clone) exista (instanciado por red)
            yield return new WaitUntil(() => GameObject.Find("CarOne(Clone)") != null);

            carModel = GameObject.Find("CarOne(Clone)");
            theplayer = Instantiate(playerJugador, new Vector3(0.6172f, 2.3864f, -12.2114f), Quaternion.identity);
        }

        // Parentar la cámara solo cuando el auto exista
        if (carModel != null && theplayer != null)
        {
            theplayer.transform.SetParent(carModel.transform, false);
            myterrainGenerator.player = carModel.transform;
            Debug.Log($"[CarSpawnHandler] {PhotonNetwork.NickName} vinculado a {carModel.name}");
        }
        else
        {
            Debug.LogError("[CarSpawnHandler] Error: carModel o player no válidos.");
        }
    }
}
