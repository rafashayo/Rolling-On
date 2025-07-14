using UnityEngine;
using System.Collections.Generic;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject roadPrefab;    // Prefab del segmento de carretera
    public GameObject terrainPrefab; // Prefab del terreno

    [Header("Player")]
    public Transform player;         // Auto o cámara que avanza

    [Header("Settings")]
    public int segmentLength = 50;         // Largo de cada segmento
    public int initialSegments = 5;        // Cuántos segmentos al inicio
    public float triggerDistance = 30f;    // Distancia para generar nuevo segmento
    public float roadYOffset = 0.01f;      // Elevación de la carretera
    public float terrainYOffset = 0f;      // Altura del terreno
    public float segmentLifetime = 1200f;  // ⏳ Tiempo de vida en segundos (20 minutos)

    private Vector3 nextSpawnPosition;     // Dónde crear el próximo segmento

    void Start()
    {
        nextSpawnPosition = Vector3.zero;

        // Generar los primeros segmentos
        for (int i = 0; i < initialSegments; i++)
        {
            SpawnSegment();
        }
    }

    void Update()
    {
        if (player == null)
        {
            Debug.LogWarning("Player no asignado en TerrainGenerator!");
            return;
        }

        // Comprobar si el jugador está lo bastante cerca para generar otro
        float distanceToNext = Vector3.Distance(player.position, nextSpawnPosition);
        if (distanceToNext < triggerDistance)
        {
            SpawnSegment();
        }
    }

    void SpawnSegment()
    {
        // Crear un objeto padre para agrupar carretera + terrenos
        GameObject segmentContainer = new GameObject("Segment");

        // Ajustar la altura Y de la carretera
        Vector3 roadSpawnPos = nextSpawnPosition + new Vector3(0, roadYOffset, 0);

        // Instanciar la carretera y hacerla hija del contenedor
        GameObject road = Instantiate(roadPrefab, roadSpawnPos, Quaternion.identity, segmentContainer.transform);

        // Generar terreno a los lados
        float offset = 25f; // distancia lateral desde la carretera al terreno
        Vector3 leftPos = nextSpawnPosition + new Vector3(-offset, terrainYOffset, 0);
        Vector3 rightPos = nextSpawnPosition + new Vector3(offset, terrainYOffset, 0);

        Instantiate(terrainPrefab, leftPos, Quaternion.identity, segmentContainer.transform);
        Instantiate(terrainPrefab, rightPos, Quaternion.identity, segmentContainer.transform);

        // Programar la destrucción del segmento después de "segmentLifetime" segundos
        Destroy(segmentContainer, segmentLifetime);

        // Avanzar el punto de spawn
        nextSpawnPosition += new Vector3(0, 0, segmentLength);
    }
}
