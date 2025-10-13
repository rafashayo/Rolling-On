using UnityEngine;
using System.Linq;

public class TrackGenerator : MonoBehaviour
{
    public GameObject[] piecePrefabs;
    public GameObject floorPrefab;
    public Transform player;

    // decor
    public GameObject[] decorPrefabs;
    public int decorPerTile = 2;
    [Range(0f, 1f)] public float decorChance = 0.5f;
    public float decorRadius = 5f;
    public bool decorRandomYaw = true;

    public float roadHalfWidth = 5f;
    public float decorClearMargin = 2f;

    public int initialPieces = 3;
    public float triggerDistance = 60f;
    public float roadHeight = 0.01f;
    public float pieceLifetime = 1200f;
    public float avoidImmediateRepeat = 0.7f;
    public string startSocketName = "SocketStart";
    public string endSocketName = "SocketEnd";

    public int floorSideTiles = 2;
    public float floorTileSpacing = 20f;
    public float floorHeight = 0f;
    public bool rotateFloorWithRoad = true;

    Transform lastSocketEnd;
    Vector3 nextPos;
    Quaternion nextRot = Quaternion.identity;
    int lastIndex = -1;
    bool halted = false;

    void Start()
    {
        if (piecePrefabs == null || piecePrefabs.Length == 0)
        {
            Debug.LogError("Sin prefabs.");
            halted = true;
            return;
        }

        for (int i = piecePrefabs.Length - 1; i >= 0; i--)
        {
            if (!PrefabHasSockets(piecePrefabs[i]))
            {
                Debug.LogError($"Prefab '{piecePrefabs[i].name}' sin {startSocketName}/{endSocketName}. Removido.");
                piecePrefabs = piecePrefabs.Where((p, idx) => idx != i).ToArray();
            }
        }
        if (piecePrefabs.Length == 0)
        {
            Debug.LogError("No hay prefabs válidos con sockets.");
            halted = true;
            return;
        }

        nextPos = Vector3.zero;
        nextRot = Quaternion.identity;
        lastSocketEnd = null;

        int n = Mathf.Max(1, initialPieces);
        for (int i = 0; i < n && !halted; i++)
            if (!TrySpawnNext()) { halted = true; break; }
    }

    void Update()
    {
        if (halted || !player) return;

        float d = lastSocketEnd
            ? Vector3.Distance(player.position, lastSocketEnd.position)
            : Vector3.Distance(player.position, nextPos);

        if (d < triggerDistance)
            TrySpawnNext(); // genera 1 tramo nuevo
    }

    bool TrySpawnNext()
    {
        if (piecePrefabs.Length == 0) return false;

        int idx = ChooseIndex();
        var prefab = piecePrefabs[idx];
        var inst = Instantiate(prefab);
        var t = inst.transform;

        var start = FindExactSocket(inst.transform, startSocketName);
        var end = FindExactSocket(inst.transform, endSocketName);

        if (!start || !end)
        {
            Debug.LogError($"Instancia '{prefab.name}' sin sockets {startSocketName}/{endSocketName}. Removido del pool.");
            Destroy(inst);
            piecePrefabs = piecePrefabs.Where(p => p != prefab).ToArray();
            lastIndex = -1;
            return piecePrefabs.Length > 0;
        }

        if (lastSocketEnd == null) AlignByStart(t, start, nextPos, nextRot);
        else AlignStartToEnd(t, start, lastSocketEnd);

        if (roadHeight != 0f)
            t.position = new Vector3(t.position.x, roadHeight, t.position.z);

        // ---- Floor alrededor del tramo ----
        if (floorPrefab)
        {
            Vector3 startW = start.position;
            Vector3 endW = end.position;
            Vector3 fwd = (endW - startW);
            fwd.y = 0f;
            float length = fwd.magnitude;
            if (length > 0.01f)
            {
                fwd /= length;
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
                int forwardTiles = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.01f, floorTileSpacing)));

                for (int f = 0; f < forwardTiles; f++)
                {
                    float along = (f + 0.5f) * floorTileSpacing;
                    Vector3 basePos = startW + fwd * Mathf.Min(along, length - 0.01f);

                    for (int s = -floorSideTiles; s <= floorSideTiles; s++)
                    {
                        if (s == 0) continue; // evita centro
                        Vector3 pos = basePos + right * (s * floorTileSpacing);
                        pos.y = floorHeight;
                        Quaternion rot = rotateFloorWithRoad ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;

                        var tile = Instantiate(floorPrefab, pos, rot);
                        tile.transform.localScale = floorPrefab.transform.localScale;
                        tile.transform.SetParent(t, true);
                    }
                }
            }
        }
        // ------------------------------------------------------------

        // 🌲 NUEVO: decoraciones por todo el tramo (no solo costados)
        if (decorPrefabs != null && decorPrefabs.Length > 0 && decorPerTile > 0 && decorChance > 0f)
        {
            Vector3 startW = start.position;
            Vector3 endW = end.position;
            Vector3 fwd = (endW - startW); fwd.y = 0f;
            float length = fwd.magnitude;
            if (length > 0.01f)
            {
                fwd.Normalize();
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

                // Definir área total del tramo
                float lateralRange = floorSideTiles * floorTileSpacing * 2f;
                int totalDecor = Mathf.RoundToInt(length / floorTileSpacing * floorSideTiles * decorPerTile * 2f);

                for (int i = 0; i < totalDecor; i++)
                {
                    if (Random.value > decorChance) continue;

                    float along = Random.Range(0f, length);
                    float lateral = Random.Range(-lateralRange, lateralRange);
                    Vector3 pos = startW + fwd * along + right * lateral;
                    pos.y = floorHeight;

                    var prefabDeco = decorPrefabs[Random.Range(0, decorPrefabs.Length)];
                    Quaternion rot = decorRandomYaw
                        ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                        : Quaternion.identity;

                    var deco = Instantiate(prefabDeco, pos, rot);
                    deco.transform.localScale = prefabDeco.transform.localScale;
                    deco.transform.SetParent(t, true);
                }
            }
        }
        // ------------------------------------------------------------

        lastSocketEnd = end;
        nextPos = lastSocketEnd.position;
        nextRot = lastSocketEnd.rotation;

        Destroy(inst, pieceLifetime);
        lastIndex = idx;
        return true;
    }

    int ChooseIndex()
    {
        int idx = Random.Range(0, piecePrefabs.Length);
        if (lastIndex >= 0 && piecePrefabs.Length > 1 && Random.value < avoidImmediateRepeat)
        {
            int safety = 10;
            while (idx == lastIndex && safety-- > 0)
                idx = Random.Range(0, piecePrefabs.Length);
        }
        return idx;
    }

    Transform FindExactSocket(Transform root, string exactName)
    {
        var t = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(x => x.name == exactName);
        if (t) return t;
        string key = exactName.ToLower();
        return root.GetComponentsInChildren<Transform>(true)
                   .FirstOrDefault(x => x.name.ToLower() == key);
    }

    void AlignByStart(Transform piece, Transform socketStart, Vector3 targetPos, Quaternion targetRot)
    {
        var rotDelta = targetRot * Quaternion.Inverse(socketStart.rotation);
        piece.rotation = rotDelta * piece.rotation;
        piece.position += (targetPos - socketStart.position);
    }

    void AlignStartToEnd(Transform piece, Transform socketStart, Transform prevSocketEnd)
    {
        AlignByStart(piece, socketStart, prevSocketEnd.position, prevSocketEnd.rotation);
    }

    bool PrefabHasSockets(GameObject prefab)
    {
        var temp = Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        bool ok = FindExactSocket(temp.transform, startSocketName) &&
                  FindExactSocket(temp.transform, endSocketName);
        DestroyImmediate(temp);
        return ok;
    }
}
