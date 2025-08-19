using UnityEngine;

public class TrackGenerator : MonoBehaviour
{
    public TrackPiece[] piecePrefabs;     // tus 8 prefabs con TrackPiece + sockets
    public Transform player;

    public int initialPieces = 5;
    public float aheadDistance = 300f;
    public float roadHeight = 0.01f;
    public float pieceLifetime = 1200f;

    public int initialOneSideCount = 8;   // cuántas piezas “bias” al inicio
    public bool initialLeft = true;       // true: sólo curvas a la IZQ al principio

    [Range(0f,1f)] public float noRepeatBias = 0.6f; // evita repetir exactamente el mismo prefab
    [Range(0f,1f)] public float avoidOppositeAfterTurn = 0.5f; // evita inmediato opuesto

    Transform lastSocketEnd;
    Vector3 nextPos;
    Quaternion nextRot = Quaternion.identity;
    int lastIndex = -1;
    int spawnedCount = 0;
    TrackPiece.PieceType lastType;

    void Start()
    {
        nextPos = Vector3.zero; nextRot = Quaternion.identity; lastSocketEnd = null;

        for (int i = 0; i < initialPieces; i++) SpawnNextPiece();
        while (player && Vector3.Distance(player.position, nextPos) < aheadDistance) SpawnNextPiece();
    }

    void Update()
    {
        if (!player) return;
        while (Vector3.Distance(player.position, nextPos) < aheadDistance) SpawnNextPiece();
    }

    void SpawnNextPiece()
    {
        int idx = ChooseIndex();
        var prefab = piecePrefabs[idx];
        var piece = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        var t = piece.transform;

        if (lastSocketEnd == null) AlignByStart(t, prefab.SocketStart, nextPos, nextRot);
        else AlignStartToEnd(t, prefab.SocketStart, lastSocketEnd);

        if (Mathf.Abs(roadHeight) > 0f) t.position = new Vector3(t.position.x, roadHeight, t.position.z);

        lastSocketEnd = prefab.SocketEnd;
        nextPos = lastSocketEnd.position;
        nextRot = lastSocketEnd.rotation;

        Destroy(piece.gameObject, pieceLifetime);

        lastIndex = idx;
        lastType = prefab.type;
        spawnedCount++;
    }

    int ChooseIndex()
    {
        // filtro por regla de “un solo lado” al inicio
        bool biasActive = spawnedCount < initialOneSideCount;
        var pool = System.Array.FindAll(piecePrefabs, p => PassesFilters(p, biasActive));

        if (pool.Length == 0) pool = piecePrefabs; // fallback

        // evitar repetir exactamente el mismo prefab
        int idx = IndexOf(piecePrefabs, pool[Random.Range(0, pool.Length)]);
        if (lastIndex >= 0 && Random.value < noRepeatBias)
        {
            int safety = 6;
            while (idx == lastIndex && safety-- > 0)
                idx = IndexOf(piecePrefabs, pool[Random.Range(0, pool.Length)]);
        }
        return idx;
    }

    bool PassesFilters(TrackPiece p, bool biasActive)
    {
        var t = p.type;

        if (biasActive)
        {
            // permitir recta siempre; curvas sólo del lado elegido
            if (IsStraight(t)) return true;
            if (initialLeft)  return IsLeft(t);
            else              return IsRight(t);
        }

        // evitar inmediatamente la curva opuesta a la anterior (suave)
        if (Random.value < avoidOppositeAfterTurn && IsCurve(t) && IsCurve(lastType))
        {
            if (IsLeft(t) && IsRight(lastType))  return false;
            if (IsRight(t) && IsLeft(lastType))  return false;
        }

        return true;
    }

    // helpers tipo
    bool IsStraight(TrackPiece.PieceType t) => t == TrackPiece.PieceType.Straight;
    bool IsLeft(TrackPiece.PieceType t) =>
        t == TrackPiece.PieceType.GentleLeft || t == TrackPiece.PieceType.SharpLeft ||
        t == TrackPiece.PieceType.SLeft     || t == TrackPiece.PieceType.HairpinLeft;
    bool IsRight(TrackPiece.PieceType t) =>
        t == TrackPiece.PieceType.GentleRight || t == TrackPiece.PieceType.SharpRight ||
        t == TrackPiece.PieceType.SRight      || t == TrackPiece.PieceType.HairpinRight;
    bool IsCurve(TrackPiece.PieceType t) => IsLeft(t) || IsRight(t);

    // alineación por sockets
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

    int IndexOf(TrackPiece[] arr, TrackPiece x)
    {
        for (int i = 0; i < arr.Length; i++) if (arr[i] == x) return i;
        return -1;
    }
}
