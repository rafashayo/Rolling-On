using UnityEngine;
using System.Linq;
using Photon.Pun;

public class TrackGenerator : MonoBehaviourPun
{
    [Header("Prefabs y referencias")]
    public GameObject[] piecePrefabs;
    public GameObject floorPrefab;
    public Transform player;

    [Header("Configuración de generación")]
    public int initialPieces = 1;
    public float triggerDistance = 60f;
    public float roadHeight = 0.01f;
    public float pieceLifetime = 1200f;
    public float avoidImmediateRepeat = 0.7f;

    [Header("Sockets")]
    public string startSocketName = "SocketStart";
    public string endSocketName = "SocketEnd";

    [Header("Grass / suelo lateral")]
    public int floorSideTiles = 2;
    public float floorTileSpacing = 20f;
    public float floorHeight = 0f;
    public bool rotateFloorWithRoad = true;

    private Transform lastSocketEnd;
    private Vector3 nextPos;
    private Quaternion nextRot = Quaternion.identity;
    private int lastIndex = -1;
    private bool halted = false;
    private bool waitingForTrigger = false;

    void Start()
    {
        if (piecePrefabs == null || piecePrefabs.Length == 0)
        {
            Debug.LogError("Sin prefabs.");
            halted = true;
            return;
        }

        // Validar sockets localmente (solo en Master Client)
        if (PhotonNetwork.IsMasterClient)
        {
            for (int i = piecePrefabs.Length - 1; i >= 0; i--)
            {
                if (!PrefabHasSockets(piecePrefabs[i]))
                {
                    Debug.LogError($"Prefab '{piecePrefabs[i].name}' sin sockets válidos. Removido.");
                    piecePrefabs = piecePrefabs.Where((p, idx) => idx != i).ToArray();
                }
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

        if (PhotonNetwork.IsMasterClient)
        {
            int n = Mathf.Max(1, initialPieces);
            for (int i = 0; i < n && !halted; i++)
            {
                TrySpawnNext();
            }
        }

        waitingForTrigger = true;
    }

    void Update()
    {
        if (halted || !player) return;
        if (!PhotonNetwork.IsMasterClient) return; // solo el host genera

        float d = lastSocketEnd
            ? Vector3.Distance(player.position, lastSocketEnd.position)
            : Vector3.Distance(player.position, nextPos);

        if (waitingForTrigger && d < triggerDistance)
        {
            if (TrySpawnNext())
                waitingForTrigger = false;
        }
        else if (!waitingForTrigger && d > triggerDistance * 1.5f)
        {
            waitingForTrigger = true;
        }
    }

    bool TrySpawnNext()
    {
        if (piecePrefabs.Length == 0) return false;

        int idx = ChooseIndex();
        var prefab = piecePrefabs[idx];

        // 🚀 Instancia en red
        GameObject inst = PhotonNetwork.Instantiate(prefab.name, Vector3.zero, Quaternion.identity);
        var t = inst.transform;

        var start = FindExactSocket(t, startSocketName);
        var end = FindExactSocket(t, endSocketName);

        if (!start || !end)
        {
            Debug.LogError($"'{prefab.name}' sin sockets válidos. Eliminado del pool.");
            PhotonNetwork.Destroy(inst);
            piecePrefabs = piecePrefabs.Where(p => p != prefab).ToArray();
            lastIndex = -1;
            return piecePrefabs.Length > 0;
        }

        // Alinear
        if (lastSocketEnd == null)
            AlignByStart(t, start, nextPos, nextRot);
        else
            AlignStartToEnd(t, start, lastSocketEnd);

        if (roadHeight != 0f)
            t.position = new Vector3(t.position.x, roadHeight, t.position.z);

        // Grass (no sincronizado)
        if (floorPrefab)
            GenerateFloor(t, start.position, end.position);

        lastSocketEnd = end;
        nextPos = end.position;
        nextRot = end.rotation;
        lastIndex = idx;

        // Destruir después de X segundos en todos los clientes
        photonView.RPC(nameof(RemoteDestroy), RpcTarget.AllBuffered, inst.GetComponent<PhotonView>().ViewID, pieceLifetime);

        return true;
    }

    [PunRPC]
    void RemoteDestroy(int viewID, float delay)
    {
        PhotonView pv = PhotonView.Find(viewID);
        if (pv != null)
            Destroy(pv.gameObject, delay);
    }

    void GenerateFloor(Transform parent, Vector3 startW, Vector3 endW)
    {
        Vector3 fwd = (endW - startW);
        fwd.y = 0f;
        float length = fwd.magnitude;
        if (length < 0.01f) return;

        fwd.Normalize();
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        int forwardTiles = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.01f, floorTileSpacing)));

        for (int f = 0; f < forwardTiles; f++)
        {
            float along = (f + 0.5f) * floorTileSpacing;
            Vector3 basePos = startW + fwd * Mathf.Min(along, length - 0.01f);

            for (int s = -floorSideTiles; s <= floorSideTiles; s++)
            {
                if (s == 0) continue;
                Vector3 pos = basePos + right * (s * floorTileSpacing);
                pos.y = floorHeight;
                Quaternion rot = rotateFloorWithRoad ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
                Instantiate(floorPrefab, pos, rot, parent);
            }
        }
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
