using UnityEngine;
using System.Linq;

public class TrackGenerator : MonoBehaviour
{
    [System.Serializable]
    public class TrackPrefab
    {
        public GameObject prefab;          // el prefab de ruta
        public Transform socketStart;      // asignado en runtime al instanciar
        public Transform socketEnd;
    }

    public GameObject[] piecePrefabs;      // tus 11 prefabs (arrastralos aquí)
    public Transform player;
    public int initialPieces = 6;
    public float aheadDistance = 300f;
    public float roadHeight = 0.01f;
    public float pieceLifetime = 1200f;
    public float avoidImmediateRepeat = 0.7f;

    Transform lastSocketEnd;
    Vector3 nextPos;
    Quaternion nextRot = Quaternion.identity;
    int lastIndex = -1;

    void Start()
    {
        nextPos = Vector3.zero;
        nextRot = Quaternion.identity;
        lastSocketEnd = null;

        for (int i = 0; i < initialPieces; i++) SpawnNextPiece();
        while (player && Vector3.Distance(player.position, nextPos) < aheadDistance)
            SpawnNextPiece();
    }

    void Update()
    {
        if (!player) return;
        while (Vector3.Distance(player.position, nextPos) < aheadDistance)
            SpawnNextPiece();
    }

    void SpawnNextPiece()
    {
        if (piecePrefabs == null || piecePrefabs.Length == 0) return;
        int idx = ChooseIndex();
        var prefab = piecePrefabs[idx];

        // instanciar y buscar sockets
        var instance = Instantiate(prefab);
        var t = instance.transform;
        var sockets = instance.GetComponentsInChildren<Transform>()
                              .Where(tr => tr.name.ToLower().Contains("socket"))
                              .ToArray();

        Transform socketStart = sockets.FirstOrDefault(s => s.name.ToLower().Contains("start"));
        Transform socketEnd   = sockets.FirstOrDefault(s => s.name.ToLower().Contains("end"));

        if (!socketStart || !socketEnd)
        {
            Debug.LogError($"Prefab {prefab.name} no tiene SocketStart o SocketEnd");
            Destroy(instance);
            return;
        }

        if (lastSocketEnd == null)
            AlignByStart(t, socketStart, nextPos, nextRot);
        else
            AlignStartToEnd(t, socketStart, lastSocketEnd);

        if (Mathf.Abs(roadHeight) > 0f)
            t.position = new Vector3(t.position.x, roadHeight, t.position.z);

        lastSocketEnd = socketEnd;
        nextPos = lastSocketEnd.position;
        nextRot = lastSocketEnd.rotation;

        Destroy(instance, pieceLifetime);
        lastIndex = idx;
    }

    int ChooseIndex()
    {
        int idx = Random.Range(0, piecePrefabs.Length);
        if (lastIndex >= 0 && Random.value < avoidImmediateRepeat)
        {
            int safety = 10;
            while (idx == lastIndex && safety-- > 0)
                idx = Random.Range(0, piecePrefabs.Length);
        }
        return idx;
    }

    void AlignByStart(Transform piece, Transform socketStart, Vector3 targetPos, Quaternion targetRot)
    {
        var rotDelta = targetRot * Quaternion.Inverse(socketStart.rotation);
        piece.rotation = rotDelta * piece.rotation;
        var offset = targetPos - socketStart.position;
        piece.position += offset;
    }

    void AlignStartToEnd(Transform piece, Transform socketStart, Transform prevSocketEnd)
    {
        AlignByStart(piece, socketStart, prevSocketEnd.position, prevSocketEnd.rotation);
    }
}
