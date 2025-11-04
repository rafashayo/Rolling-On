using UnityEngine;

public class RadioScript : MonoBehaviour
{
    private bool encendido = false;
    private AudioSource audioSource;

    void Start()
    {
        // Obtenemos el componente AudioSource que debe estar en el mismo GameObject
        audioSource = GetComponent<AudioSource>();

        // Por si acaso, comenzamos con el audio apagado
        audioSource.mute = true;
    }

    void Update()
    {
        // Detectamos cuando se presiona la tecla R (una sola vez, no mientras se mantiene)
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Cambiamos el estado (toggle)
            encendido = !encendido;

            // Si encendido es true, se escucha el audio; si es false, se mutea
            audioSource.mute = !encendido;

            // Si el audio no se está reproduciendo, lo iniciamos
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }

            Debug.Log("Radio encendida: " + encendido);
        }
    }
}
