using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TrackGenerator : MonoBehaviour
{
    [Header("Road pieces")]
    public GameObject[] piecePrefabs;
    public string startSocketName = "SocketStart";
    public string endSocketName   = "SocketEnd";
    public float avoidImmediateRepeat = 0.7f;
    public int initialPieces = 3;
    public float triggerDistance = 60f;
    public float pieceLifetime = 1200f;
    public float roadHeight = 0.01f;

    [Header("Floor (suelo)")]
    public GameObject floorPrefab;
    public int floorSideTiles = 2;
    public float floorTileSpacing = 20f;
    public float floorHeight = 0f;
    public bool rotateFloorWithRoad = true;

    [Header("Runtime")]
    public Transform player;

    // Internos
    private Transform _lastEndSocket;
    private readonly List<GameObject> _livePieces = new();
    private readonly List<GameObject> _liveFloors = new();
    private GameObject _piecesRoot;
    private GameObject _floorsRoot;
    private GameObject _root;

    void Start()
    {
        if (piecePrefabs == null || piecePrefabs.Length == 0)
        {
            Debug.LogError("No hay piecePrefabs asignados.");
            enabled = false;
            return;
        }

        _root       = new GameObject("TrackRuntime");
        _piecesRoot = new GameObject("Pieces"); _piecesRoot.transform.SetParent(_root.transform);
        _floorsRoot = new GameObject("Floors"); _floorsRoot.transform.SetParent(_root.transform);

        // Primer segmento
        var first = SpawnRoadPiece(piecePrefabs[0], Vector3.zero, Quaternion.identity, true);
        for (int i = 1; i < initialPieces; i++)
        {
            SpawnNextPiece();
        }
    }

    void Update()
    {
        if (!player || _lastEndSocket == null) return;

        float dist = Vector3.Distance(player.position, _lastEndSocket.position);
        if (dist < triggerDistance)
        {
            SpawnNextPiece();
        }

        // Limpieza por lifetime
        CleanupOld(_livePieces, pieceLifetime);
        CleanupOrphans(_liveFloors);
    }

    #region Road + Floor

    GameObject SpawnNextPiece()
    {
        // Selección evitando repetir el último prefab con cierta probabilidad
        GameObject prefab;
        if (piecePrefabs.Length == 1)
        {
            prefab = piecePrefabs[0];
        }
        else
        {
            var candidates = piecePrefabs.ToList();
            if (_livePieces.Count > 0 && Random.value < avoidImmediateRepeat)
            {
                var last = _livePieces.Last();
                candidates.RemoveAll(p => p.name == last.name.Replace("(Clone)", "").Trim());
            }
            prefab = candidates[Random.Range(0, candidates.Count)];
        }

        // Alinear con el último end socket
        if (_lastEndSocket == null)
        {
            return SpawnRoadPiece(prefab, Vector3.zero, Quaternion.identity, false);
        }
        else
        {
            Transform endSocketPrev = _lastEndSocket;

            // OJO: si los sockets del prefab están anidados, usar búsqueda recursiva
            var start = FindInPrefabRecursive(prefab, startSocketName);
            var end   = FindInPrefabRecursive(prefab, endSocketName);
            if (!start || !end)
            {
                Debug.LogError($"El prefab {prefab.name} no tiene sockets {startSocketName}/{endSocketName}");
                return null;
            }

            Quaternion rot = endSocketPrev.rotation * Quaternion.Inverse(start.rotation);
            Vector3 pos = endSocketPrev.position - (rot * (start.position - prefab.transform.position));

            return SpawnRoadPiece(prefab, pos, rot, false);
        }
    }

    GameObject SpawnRoadPiece(GameObject prefab, Vector3 pos, Quaternion rot, bool first)
    {
        var go = Instantiate(prefab, pos, rot, _piecesRoot.transform);
        go.name = $"{prefab.name}";

        // Ajuste leve en Y para que no parpadee con el terreno
        go.transform.position += Vector3.up * roadHeight;

        // Encuentra sockets en el instanciado (recursivo)
        Transform startSock = FindChildRecursive(go.transform, startSocketName);
        Transform endSock   = FindChildRecursive(go.transform, endSocketName);
        if (!startSock || !endSock)
        {
            Debug.LogError($"Instancia {go.name} no tiene sockets {startSocketName}/{endSocketName}");
        }
        _lastEndSocket = endSock;

        _livePieces.Add(go);

        // Generar floor tiles a ambos lados de la ruta
        BuildFloorAroundPiece(go.transform, startSock, endSock);

        return go;
    }

    void BuildFloorAroundPiece(Transform piece, Transform startSock, Transform endSock)
    {
        if (!floorPrefab || floorSideTiles <= 0 || floorTileSpacing <= 0f) return;

        // Dirección de la pieza (de start a end)
        Vector3 forward = (endSock.position - startSock.position);
        float length = forward.magnitude;
        if (length < 0.01f) return;
        forward.Normalize();

        // Rotación base del floor (opcionalmente la misma que la ruta)
        Quaternion baseRot = rotateFloorWithRoad ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;

        // Columna a lo largo de la pieza, distribuyendo tiles cada 'floorTileSpacing'
        int alongCount = Mathf.Max(1, Mathf.RoundToInt(length / floorTileSpacing));
        float step = length / alongCount;

        // Vector lateral (perpendicular)
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Offset lateral mínimo para NO tocar la ruta
        // (el margen anti-ruta lo aplicará el decorador; aquí solo ubicamos tiles)
        float minLateral = 0f;

        for (int side = -1; side <= 1; side += 2) // -1 izquierda, 1 derecha
        {
            for (int s = 0; s < floorSideTiles; s++)
            {
                float lateral = minLateral + (s * floorTileSpacing);
                for (int i = 0; i < alongCount; i++)
                {
                    float t = (i + 0.5f) * step;
                    Vector3 basePos = startSock.position + forward * t + right * (lateral * side);
                    basePos.y += floorHeight;

                    var tile = Instantiate(floorPrefab, basePos, baseRot, _floorsRoot.transform);
                    tile.name = $"FloorTile_{(side<0?"L":"R")}_{s}_{i}";
                    _liveFloors.Add(tile);

                    // (Opcional) etiqueta para que el decorador los encuentre fácil
                    if (!tile.CompareTag("FloorTile")) tile.tag = "FloorTile";
                }
            }
        }
    }

    #endregion

    #region Utils

    static Transform FindInPrefabRecursive(GameObject prefab, string childName)
    {
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        if (!parent) return null;
        if (parent.name == name) return parent;
        foreach (Transform c in parent)
        {
            var r = FindChildRecursive(c, name);
            if (r) return r;
        }
        return null;
    }

    void CleanupOld(List<GameObject> list, float lifeSeconds)
    {
        if (lifeSeconds <= 0f) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var go = list[i];
            if (!go) { list.RemoveAt(i); continue; }
            if (player && Vector3.Distance(player.position, go.transform.position) > triggerDistance * 4f)
            {
                Destroy(go);
                list.RemoveAt(i);
            }
        }
    }

    void CleanupOrphans(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (!list[i]) list.RemoveAt(i);
    }

    #endregion
}
