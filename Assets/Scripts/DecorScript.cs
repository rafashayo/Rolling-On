using UnityEngine;
using Photon.Pun;

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

    [Header("Objetos Prohibidos (GameObjects)")]
    public GameObject[] objetosProhibidos;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
            GenerarObjetos();
    }

    void GenerarObjetos()
    {
        float maxDist = alturaRaycast + 1000f;

        for (int i = 0; i < cantidadObjetos; i++)
        {
            Vector2 punto = Random.insideUnitCircle * radioGeneracion;

            Vector3 origen = new Vector3(punto.x, alturaRaycast, punto.y);

            // ❌ No generar si Z < 0
            if (origen.z < 0)
                continue;

            Debug.DrawRay(origen, Vector3.down * maxDist, Color.red, 2f);

            if (Physics.Raycast(origen, Vector3.down, out RaycastHit hit, maxDist))
            {
                if (EsObjetoProhibido(hit.collider.transform))
                    continue;

                string nombrePrefab = nombresPrefabs[Random.Range(0, nombresPrefabs.Length)];

                GameObject obj = PhotonNetwork.Instantiate(
                    nombrePrefab,
                    hit.point,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0)
                );

                obj.transform.parent = this.transform;
            }
        }
    }

    bool EsObjetoProhibido(Transform t)
    {
        Debug.Log(t.gameObject.name);

        if (t.gameObject.CompareTag("Track"))
            return true;

        for (int i = 0; i < objetosProhibidos.Length; i++)
        {
            Transform prohibido = objetosProhibidos[i].transform;

            if (t == prohibido)
                return true;

            if (t.IsChildOf(prohibido))
                return true;
        }
        return false;
    }
}
