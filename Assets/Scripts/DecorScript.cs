using UnityEngine;
using Photon.Pun;

public class GeneradorDecoraciones : MonoBehaviourPun
{
    [Header("Prefabs a Instanciar (deben estar en Resources)")]
    public string[] nombresPrefabs; // Nombres de los prefabs en Resources

    [Header("Área de generación")]
    public Vector2 rangoX = new Vector2(-50, 50);
    public Vector2 rangoZ = new Vector2(-50, 50);
    public float alturaSpawn = 50f;

    [Header("Cantidad de objetos")]
    public int cantidadObjetos = 50;

    void Start()
    {
        // Solo el Master Client genera objetos
        if (PhotonNetwork.IsMasterClient)
            GenerarObjetos();
    }

    void GenerarObjetos()
    {
        for (int i = 0; i < cantidadObjetos; i++)
        {
            float x = Random.Range(rangoX.x, rangoX.y);
            float z = Random.Range(rangoZ.x, rangoZ.y);
            Vector3 origen = new Vector3(x, alturaSpawn, z);

            RaycastHit hit;
            if (Physics.Raycast(origen, Vector3.down, out hit, alturaSpawn * 2))
            {
                // Seleccionar prefab por nombre
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
}
