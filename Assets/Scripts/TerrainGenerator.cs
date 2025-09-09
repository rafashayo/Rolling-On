using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class TrackGenerator : MonoBehaviour
{
    public GameObject[] piecePrefabs;   // prefabs con SocketStart/SocketEnd
    public Transform player;

    public int initialPieces = 3;
    public float triggerDistance = 60f;
    public float roadHeight = 0.01f;
    public float pieceLifetime = 1200f;
    public string startSocketName = "SocketStart";
    public string endSocketName = "SocketEnd";

    public int scoreSamples = 5;        // cuántos prefabs probar por frame
    public float angleWeight = 1f;      // peso de desviación angular respecto a Z+
    public float lateralWeight = 0.3f;  // peso de desviación lateral (X)
    public float backwardRejectDot = 0.05f; // descarta si no avanza en Z (dot <= esto)
    public float overlapEpsilon = 0.12f;
    public int maxRecentBounds = 24;

    Transform lastSocketEnd;
    Vector3 nextPos;
    Quaternion nextRot = Quaternion.identity;
    bool halted = false;

    readonly List<Bounds> recentBounds = new();

    void Start()
    {
        if (piecePrefabs == null || piecePrefabs.Length == 0) { Debug.LogError("Sin prefabs."); halted = true; return; }
        // valida sockets
        for (int i = piecePrefabs.Length - 1; i >= 0; i--)
           // if (!PrefabHasSockets(piecePrefabs[i])) { Debug.LogError($"'{piecePrefabs[i].name}' sin sockets. Removido."); piecePrefabs = piecePrefabs.Where((p, idx) => idx != i).ToArray(); }
        if (piecePrefabs.Length == 0) { Debug.LogError("No hay prefabs válidos."); halted = true; return; }

        nextPos = Vector3.zero; nextRot = Quaternion.identity; lastSocketEnd = null;

        for (int i = 0; i < Mathf.Max(1, initialPieces) && !halted; i++) TrySpawnNext();
    }

    void Update()
    {
        if (halted || !player) return;
        float d = Vector3.Distance(player.position, lastSocketEnd ? lastSocketEnd.position : nextPos);
        if (d < triggerDistance) TrySpawnNext(); // de a una
    }

    bool TrySpawnNext()
    {
        if (piecePrefabs.Length == 0) return false;

        // elige candidato por SCORE (seguir Z+ y no solapar)
        int samples = Mathf.Clamp(scoreSamples, 1, piecePrefabs.Length);
        var indices = Enumerable.Range(0, piecePrefabs.Length).OrderBy(_ => Random.value).Take(samples).ToArray();

        GameObject bestPrefab = null;
        float bestScore = float.PositiveInfinity;
        Transform bestStart = null, bestEnd = null;
        Bounds bestBounds = default;
        GameObject bestInstance = null;

        foreach (int idx in indices)
        {
            var prefab = piecePrefabs[idx];
            var inst = Instantiate(prefab);
            var t = inst.transform;

            var start = FindSocket(inst.transform, startSocketName);
            var end = FindSocket(inst.transform, endSocketName);
            if (!start || !end) { Destroy(inst); continue; }

            // encastre hipotético
            if (lastSocketEnd == null) AlignByStart(t, start, nextPos, nextRot);
            else AlignStartToEnd(t, start, lastSocketEnd);

            if (roadHeight != 0f) t.position = new Vector3(t.position.x, roadHeight, t.position.z);

            // avance y rumbo
            Vector3 toEnd = end.position - (lastSocketEnd ? lastSocketEnd.position : nextPos);
            Vector3 fwdZ = Vector3.forward; // queremos seguir Z+
            float dot = Vector3.Dot(toEnd.normalized, fwdZ);
            if (dot <= backwardRejectDot) { Destroy(inst); continue; } // no avanza en Z+

            // desviaciones
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg; // ángulo con Z+
            float lateral = Mathf.Abs(toEnd.x); // alejamiento lateral

            // solape (permitimos tocar al inmediatamente anterior)
            Bounds b = GetCombinedBounds(inst);
            bool overlap = false;
            for (int i = 0; i < recentBounds.Count; i++)
             //   if (IntersectsBeyond(b, recentBounds[i], overlapEpsilon)) { overlap = true; break; }
            if (overlap) { Destroy(inst); continue; }

            float score = angleWeight * angle + lateralWeight * lateral;
            if (score < bestScore)
            {
                // descartar candidato anterior
                if (bestInstance) Destroy(bestInstance);
                bestScore = score;
                bestPrefab = prefab;
                bestStart = start; bestEnd = end;
                bestBounds = b;
                bestInstance = inst; // conservar este
            }
            else
            {
                Destroy(inst);
            }
        }

        if (!bestInstance) return false; // no hubo candidato viable este frame

        // aceptar el mejor
        lastSocketEnd = bestEnd;
        nextPos = lastSocketEnd.position;
        nextRot = lastSocketEnd.rotation;

        recentBounds.Add(bestBounds);
        if (recentBounds.Count > maxRecentBounds) recentBounds.RemoveAt(0);

        Destroy(bestInstance, pieceLifetime);
        return true;
    }

    // utilidades
    Transform FindSocket(Transform root, string nameExact)
    {
        var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nameExact);
        if (t) return t;
        string key = nameExact.ToLower();
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.ToLower() == key);
    }

    void AlignByStart(Transform piece, Transform socketStart, Vector3 targetPos, Quaternion targetRot)
    {
        var rotDelta = targetRot * Quaternion.Inverse(socketStart.rotation);
        piece.rotation = rotDelta * piece.rotation;
        piece.position += (targetPos - socketStart.position);
    }

    void AlignStartToEnd(Transform piece, Transform socketStart, Transform prevEnd)
    {
        AlignByStart(piece, socketStart, prevEnd.position, prevEnd.rotation);
    }

    Bounds GetCombinedBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}

   /* bool IntersectsBeyond(Bounds a, Bounds b, float eps)
    {
        float ox = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
   */