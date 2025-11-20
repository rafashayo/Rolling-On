using UnityEngine;
using System.Linq;
using Photon.Pun;

public class TrackGenerator : MonoBehaviourPun
{
    [Header("Prefabs y referencias")]
    public GameObject[] piecePrefabs;
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

        if (PhotonNetwork.IsMasterClient)
        {
            // Validar sockets solo una vez en el master
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

        lastSocketEnd = end;
        nextPos = end.position;
        nextRot = end.rotation;
        lastIndex = idx;

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

    // -------------------------------------------------------------
    // 🔥 SOLO ESTA FUNCIÓN SE MODIFICÓ PARA AGREGAR EL 10%
    // -------------------------------------------------------------
    int ChooseIndex()
{
    // 🎯 Prefab especial = índice 7 → 10 % de probabilidad real
    if (Random.value < 0.10f)
        return 7;

    // 👉 Elegir entre los otros prefabs
    int idx = Random.Range(0, piecePrefabs.Length);

    // Evitar elegir el índice 7 fuera del 10%
    if (idx == 7)
        idx = (idx + 1) % piecePrefabs.Length;

    // ✔ Evitar repetición inmediata (si está activado)
    if (lastIndex >= 0 && piecePrefabs.Length > 2 && Random.value < avoidImmediateRepeat)
    {
        int safety = 10;
        while (idx == lastIndex && safety-- > 0)
        {
            idx = Random.Range(0, piecePrefabs.Length);

            if (idx == 7) // evitar el especial fuera del 10%
                idx = (idx + 1) % piecePrefabs.Length;
        }
    }

    return idx;
}


    // -------------------------------------------------------------

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
