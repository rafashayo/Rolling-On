using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public class CarInteraction : MonoBehaviourPun
{
    [Header("Asientos")]
    public Transform driverSeat;
    public Transform passengerSeat;

    [Header("Puntos de salida")]
    public Transform driverDoorPoint;
    public Transform passengerDoorPoint;


    [Header("UI")]
    public Text promptText;

    private GameObject driver;
    private GameObject passenger;

    void Start()
    {
        if (promptText) promptText.enabled = false;
        GetComponent<CarController>().enabled = false;
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!other.GetComponent<PhotonView>().IsMine) return;

        float distDriver = Vector3.Distance(other.transform.position, driverDoorPoint.position);
        float distPassenger = Vector3.Distance(other.transform.position, passengerDoorPoint.position);

        if (driver == null && distDriver < 2f) // rango 2 metros
        {
            ShowPrompt("Presiona E para entrar como CONDUCTOR");
            if (Input.GetKeyDown(KeyCode.E)) EnterCar(other.gameObject, true);
        }
        else if (passenger == null && distPassenger < 2f)
        {
            ShowPrompt("Presiona E para entrar como PASAJERO");
            if (Input.GetKeyDown(KeyCode.E)) EnterCar(other.gameObject, false);
        }
        else
        {
            HidePrompt();
        }
    }


    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponent<PhotonView>().IsMine)
        {
            HidePrompt();
        }
    }

    void ShowPrompt(string msg)
    {
        if (promptText)
        {
            promptText.text = msg;
            promptText.enabled = true;
        }
    }

    void HidePrompt()
    {
        if (promptText) promptText.enabled = false;
    }

    void EnterCar(GameObject player, bool asDriver)
    {
        // Desactivar control del player
        var fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;
        player.GetComponent<CharacterController>().enabled = false;

        // Cámara del player → mover al asiento
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            Transform seat = asDriver ? driverSeat : passengerSeat;
            cam.transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        // Guardar referencia
        if (asDriver)
        {
            driver = player;
            photonView.RequestOwnership();

            // habilitar control del coche (solo dueño local)
            if (photonView.IsMine)
                GetComponent<CarController>().enabled = true;
        }
        else
        {
            passenger = player;
        }

        HidePrompt();
    }

    public void ExitCar(GameObject player)
    {
        if (player == driver)
        {
            driver = null;
            if (photonView.IsMine)
                GetComponent<CarController>().enabled = false;

            player.transform.position = driverDoorPoint.position;
        }
        else if (player == passenger)
        {
            passenger = null;
            player.transform.position = passengerDoorPoint.position;
        }



        // Reactivar control del player
        var fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = true;
        player.GetComponent<CharacterController>().enabled = true;

        // Resetear la cámara a la posición original del player
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.transform.localPosition = new Vector3(0, 0.9f, 0); // altura típica de ojos
            cam.transform.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        if (driver != null && driver.GetComponent<PhotonView>().IsMine && Input.GetKeyDown(KeyCode.E))
        {
            ExitCar(driver);
        }
        else if (passenger != null && passenger.GetComponent<PhotonView>().IsMine && Input.GetKeyDown(KeyCode.E))
        {
            ExitCar(passenger);
        }
    }
}
