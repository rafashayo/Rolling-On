using Photon.Pun;
using UnityEngine;

public class PlayerSetup : MonoBehaviourPun
{
    public Camera playerCamera;  // arrastrá acá la cámara del prefab

    void Start()
    {
        if (!photonView.IsMine)
        {
            // Esta instancia pertenece a otro jugador → desactivar su cámara
            if (playerCamera != null)
            {
                playerCamera.enabled = false;

                // también desactivar el AudioListener (Unity no permite más de uno)
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }
    }
}
