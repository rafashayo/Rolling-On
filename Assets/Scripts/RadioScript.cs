using UnityEngine;

public class RadioScript : MonoBehaviour
{
    public AudioClip[] playlist; 
    private int cancionActual = 0;

    private bool encendido = false;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.mute = true;
    }

    void Update()
    {
        // Encender / apagar radio con R
        if (Input.GetKeyDown(KeyCode.R))
        {
            encendido = !encendido;

            if (encendido)
            {
                audioSource.mute = false;
                ReproducirCancion();
            }
            else
            {
                audioSource.mute = true;
            }
        }

        // Pasar a la siguiente canción con N
        if (encendido && Input.GetKeyDown(KeyCode.N))
        {
            PasarASiguienteCancion();
        }

        // Si termina la canción, pasar a la siguiente automáticamente
        if (encendido && !audioSource.isPlaying)
        {
            PasarASiguienteCancion();
        }
    }

    void ReproducirCancion()
    {
        audioSource.clip = playlist[cancionActual];
        audioSource.Play();
        Debug.Log("Reproduciendo: " + audioSource.clip.name);
    }

    void PasarASiguienteCancion()
    {
        cancionActual = (cancionActual + 1) % playlist.Length;
        ReproducirCancion();
    }
}
