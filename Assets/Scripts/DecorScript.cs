using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DecorSpawner : MonoBehaviour
{
    [Header("Fuente de tiles de floor")]
    public Transform floorsRoot;              // Opcional: si no lo asignas, lo busca por nombre
    public string floorsRootPath = "TrackRuntime/Floors"; // ruta de búsqueda si floorsRoot es null

    [Header("Decor")]
    public GameObject[] decorPrefabs;
    public int decorPerTile = 2;
    [Range(0f, 1f)] public float decorChance = 0.5f;
    public float decorRadius = 5f;
    public bool decorRandomYaw = true;

    [Tooltip("Mitad del ancho de la ruta para dejar margen.")]
    public float roadHalfWidth = 5f;

    [Tooltip("Margen extra para que el decor no toque la ruta.")]
    public float decorClearMargin = 2f;

    [Tooltip("Separación mínima entre decor.")]
    public float decorSeparation = 1.25f;

    [Header("Masks (capas)")]
    public LayerMask floorMask;   // capa del floor
    public LayerMask roadMask;    // capa de la ruta
    public LayerMask decorMask;   // capa del decor

    [Header("Performance")]
    public float scanInterval = 0.5f; // cada cuánto revisa tiles nuevos

    // Internos
    private readonly HashSet<Transform> _processedTiles = new();

    void Start()
    {
        if (floorsRoot == null)
        {
            var go = GameObject.Find(floorsRootPath);
            if (go) floorsRoot = go.transform;
        }
        if (floorsRoot == null)
        {
            Debug.LogWarning($"[{nameof(DecorSpawner)}] No se encontró floorsRoot. Asigna la referencia o ajusta floorsRootPath.");
        }

        StartCoroutine(ScanLoop());
    }

    IEnumerator ScanLoop()
    {
        var wait = new WaitForSeconds(scanInterval);
        while (true)
        {
            try
            {
                ScanAndDecorate();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{nameof(DecorSpawner)}] Error en Scan: {e}");
            }
            yield return wait;
        }
    }

    void ScanAndDecorate()
    {
        if (!floorsRoot || decorPrefabs == null || decorPrefabs.Length == 0 || decorPerTile <= 0) return;

        // Explora todos los tiles hijos (puedes filtrar por tag "FloorTile" si quieres)
        foreach (Transform t in floorsRoot)
        {
            if (!t || !t.gameObject.activeInHierarchy) continue;

            if (_processedTiles.Contains(t)) continue;

            // Marca como procesado para no duplicar decor
            _processedTiles.Add(t);
            if (!t.GetComponent<DecorStamped>()) t.gameObject.AddComponent<DecorStamped>();

            SpawnDecorOnFloorTile(t);
        }
    }

    void SpawnDecorOnFloorTile(Transform floorTile)
    {
        Bounds tileBounds = GetWorldBounds(floorTile.gameObject);
        if (tileBounds.size == Vector3.zero) return;

        int target   = decorPerTile;
        int attempts = Mathf.Max(10, target * 8);
        int placed   = 0;

        for (int a = 0; a < attempts && placed < target; a++)
        {
            if (Random.value > decorChance) continue;

            Vector3 randomXZ = RandomPointInBoundsXZ(tileBounds, decorRadius);

            Vector3 rayStart = randomXZ + Vector3.up * 50f;
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, floorMask, QueryTriggerInteraction.Ignore))
                continue;

            if (!IsSameRoot(hit.collider.transform, floorTile))
                continue;

            Vector3 pos = hit.point;

            // Evitar tocar la ruta
            float roadClear = roadHalfWidth + decorClearMargin;
            if (Physics.OverlapSphere(pos, roadClear, roadMask, QueryTriggerInteraction.Ignore).Length > 0)
                continue;

            // Evitar pegarse a otros decor
            if (Physics.OverlapSphere(pos, decorSeparation, decorMask, QueryTriggerInteraction.Ignore).Length > 0)
                continue;

            // Mantenerse dentro del bounds XZ del tile
            if (!tileBounds.Contains(new Vector3(pos.x, tileBounds.center.y, pos.z)))
                continue;

            // Instanciar decor
            var prefab = decorPrefabs[Random.Range(0, decorPrefabs.Length)];
            Quaternion rot = decorRandomYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;

            var deco = Instantiate(prefab, pos + Vector3.up * 0.01f, rot); // +Y evita z-fighting
            // Asignar capa del decor si el mask es de 1 sola capa
            int decorLayer = MaskToSingleLayer(decorMask);
            if (decorLayer >= 0) SetLayerRecursively(deco, decorLayer);

            // Parent opcional: agrupar bajo un objeto "Decors" si existe
            var decorsRoot = GameObject.Find("TrackRuntime/Decors");
            if (decorsRoot) deco.transform.SetParent(decorsRoot.transform, true);

            placed++;
        }
    }

    #region Utils

    class DecorStamped : MonoBehaviour { } // marcador para no decorar dos veces el mismo tile

    static Bounds GetWorldBounds(GameObject go)
    {
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        var colls = go.GetComponentsInChildren<Collider>();
        foreach (var c in colls)
        {
            if (!hasBounds) { b = c.bounds; hasBounds = true; }
            else b.Encapsulate(c.bounds);
        }

        if (!hasBounds)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (!hasBounds) { b = r.bounds; hasBounds = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        return hasBounds ? b : new Bounds(Vector3.zero, Vector3.zero);
    }

    static Vector3 RandomPointInBoundsXZ(Bounds b, float clampRadius)
    {
        float x = Random.Range(b.min.x, b.max.x);
        float z = Random.Range(b.min.z, b.max.z);

        if (clampRadius > 0f)
        {
            Vector3 c = b.center;
            Vector2 to = new Vector2(x - c.x, z - c.z);
            if (to.magnitude > clampRadius) to = to.normalized * clampRadius;
            x = c.x + to.x;
            z = c.z + to.y;
        }
        return new Vector3(x, b.center.y + 10f, z);
    }

    static bool IsSameRoot(Transform a, Transform root)
    {
        if (!a || !root) return false;
        Transform t = a;
        while (t != null)
        {
            if (t == root) return true;
            t = t.parent;
        }
        return false;
    }

    static int MaskToSingleLayer(LayerMask mask)
    {
        int v = mask.value;
        if (v == 0) return -1;
        if ((v & (v - 1)) != 0) return -1; // más de una capa
        int layer = 0;
        while (v > 1) { v >>= 1; layer++; }
        return layer;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (!go) return;
        go.layer = layer;
        foreach (Transform c in go.transform) if (c) SetLayerRecursively(c.gameObject, layer);
    }

    #endregion
}
