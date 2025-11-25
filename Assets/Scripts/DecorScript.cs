using UnityEngine;
using Photon.Pun;
using System.Collections;

public class GeneradorDecoraciones : MonoBehaviourPun
{
    [Header("Prefabs a Instanciar (en Resources)")]
    public string[] nombresPrefabs;

    [Header("Radio de generación")]
    public float radioGeneracion = 2000f;

    [Header("Cantidad de objetos")]
    public int cantidadObjetos = 3000;

    [Header("Raycast")]
    public float alturaRaycast = 500f;

    private int generados = 0;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(GenerarContinuo());
    }

    IEnumerator GenerarContinuo()
    {
        float maxDist = alturaRaycast + 1000f;

        while (generados < cantidadObjetos)
        {
            Vector2 punto = Random.insideUnitCircle * radioGeneracion;
            Vector3 origen = new Vector3(punto.x, alturaRaycast, punto.y);

            // evitar zona negativa si querés
            if (origen.z < 0)
            {
                yield return null;
                continue;
            }

            if (Physics.Raycast(origen, Vector3.down, out RaycastHit hit, maxDist))
            {
                // ❗ NO generar sobre rutas NI sobre decoraciones ya existentes
                if (hit.collider.CompareTag("Track") || hit.collider.CompareTag("Decor"))
                {
                    yield return null;
                    continue;
                }

                string nombrePrefab = nombresPrefabs[Random.Range(0, nombresPrefabs.Length)];

                GameObject obj = PhotonNetwork.Instantiate(
                    nombrePrefab,
                    hit.point,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0)
                );

                obj.transform.parent = this.transform;

                generados++;
            }

            yield return null;
        }
    }
}
