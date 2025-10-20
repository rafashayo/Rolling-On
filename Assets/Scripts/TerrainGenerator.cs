using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class TrackGenerator : MonoBehaviour
{
    public GameObject[] piecePrefabs;
    public GameObject floorPrefab;
    public Transform player;

    // decor
    public GameObject[] decorPrefabs;
    public int decorPerTile = 2;
    [Range(0f, 1f)] public float decorChance = 0.5f;
    public float decorRadius = 205f;
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

    // [GLOBAL FLOOR] cachea el "floor" único de la escena
    Transform _globalFloor;
    Collider  _globalFloorCol;

    // [MESH FLOOR] acumuladores de malla
    MeshFilter _floorMF;
    Mesh       _floorMesh;
    readonly List<Vector3> _floorVerts = new List<Vector3>(4096);
    readonly List<int>     _floorTris  = new List<int>(8192);
    readonly List<Vector2> _floorUVs   = new List<Vector2>(4096);

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

        // [GLOBAL FLOOR] localizar GameObject llamado exactamente "floor"
        var gf = GameObject.Find("floor");
        if (gf)
        {
            _globalFloor = gf.transform;
            _globalFloorCol = gf.GetComponent<Collider>();
            // [MESH FLOOR] asegurar MeshFilter/MeshRenderer y un Mesh vacío
            _floorMF = gf.GetComponent<MeshFilter>();
            if (_floorMF == null) _floorMF = gf.gameObject.AddComponent<MeshFilter>();
            var mr = gf.GetComponent<MeshRenderer>();
            if (mr == null) mr = gf.gameObject.AddComponent<MeshRenderer>();
            if (mr.sharedMaterial == null)
            {
                var sh = Shader.Find("Standard");
                mr.sharedMaterial = new Material(sh);
            }
            _floorMesh = new Mesh { name = "FloorCombinedMesh" };
            _floorMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // por si crece
            _floorMF.sharedMesh = _floorMesh;
            // inicial vacío
            _floorVerts.Clear(); _floorTris.Clear(); _floorUVs.Clear();
            _floorMesh.SetVertices(_floorVerts);
            _floorMesh.SetTriangles(_floorTris, 0, true);
            _floorMesh.SetUVs(0, _floorUVs);
            _floorMesh.RecalculateNormals();
            _floorMesh.RecalculateBounds();
        }
        else
        {
            Debug.LogWarning("No se encontró GameObject llamado 'floor'. El mesh combinado no se construirá.");
        }

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

// ---- [MESH FLOOR] Generación de grilla alrededor del floor (centrada por tramo o SocketStart) ----
if (_globalFloor)
{
    Vector3 startW = start.position;
    Vector3 endW   = end.position;
    Vector3 fwd    = (endW - startW); fwd.y = 0f;
    float length   = fwd.magnitude;
    if (length > 0.01f)
    {
        fwd /= length;
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

        // === Centro de la grilla ===
        // A) centro del tramo:
        Vector3 center = (startW + endW) * 0.5f;
        // B) ó en el SocketStart:
        // Vector3 center = startW;

        center.y = floorHeight;

        // Tamaño de cada tile de la grilla
        float tileSizeFwd  = floorTileSpacing;
        float tileSizeSide = floorTileSpacing;

        // Si querés tiles exactos de 100x100, descomentá:
        tileSizeFwd = 3000f; tileSizeSide = 3000f;

        // Radio de la grilla (número de tiles a cada lado del centro)
        int side = Mathf.Max(0, floorSideTiles);

        // Genera un bloque (2*side+1) x (2*side+1) centrado en 'center'
        for (int iz = -side; iz <= side; iz++)
        {
            for (int ix = -side; ix <= side; ix++)
            {
                Vector3 tileCenter = center + fwd * (iz * tileSizeFwd) + right * (ix * tileSizeSide);
                tileCenter.y = floorHeight;

                AddFloorQuad_World(tileCenter, fwd, right, tileSizeFwd, tileSizeSide);
            }
        }

        // Actualizamos el mesh una vez por tramo
        RebuildFloorMesh();
    }
}


        else
        {
            // Si no hay "floor" global, se comporta como antes (opcional: dejar vacío)
            // (Dejamos vacío para cumplir "generación en 'floor'" solamente si existe)
        }
        // ------------------------------------------------------------------------

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

                // Densidad basada en spacing: barrido por "tiles virtuales"
                int forwardTiles = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.01f, floorTileSpacing)));
                float halfAlong = floorTileSpacing * 0.5f;
                float halfLateral = floorTileSpacing * 0.5f;

                for (int f = 0; f < forwardTiles; f++)
                {
                    float alongBase = Mathf.Clamp((f + 0.5f) * floorTileSpacing, 0f, length - 0.01f);
                    Vector3 basePos = startW + fwd * alongBase;

                    for (int s = -floorSideTiles; s <= floorSideTiles; s++)
                    {
                        Vector3 colCenter = basePos + right * (s * floorTileSpacing);

                        for (int k = 0; k < decorPerTile; k++)
                        {
                            if (Random.value > decorChance) continue;

                            // Offset aleatorio dentro del tile virtual
                            float offAlong   = Random.Range(-halfAlong,   halfAlong);
                            float offLateral = Random.Range(-halfLateral, halfLateral);

                            Vector3 pos = colCenter + fwd * offAlong + right * offLateral;

                            // [CURVA ROBUST] corredor libre siguiendo curvatura
                            float alongReal = Mathf.Clamp(Vector3.Dot(pos - startW, fwd), 0f, length);
                            Vector3 centerAtAlong = startW + fwd * alongReal;
                            float lateralFromCenter = Mathf.Abs(Vector3.Dot(pos - centerAtAlong, right));
                            if (lateralFromCenter < (roadHalfWidth + decorClearMargin))
                                continue;

                            // [GLOBAL FLOOR] apoyar sólo si pega sobre el "floor" global (si existe)
                            Vector3 rayOrigin = pos + Vector3.up * 50f;
                            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                if (_globalFloorCol != null)
                                {
                                    if (hit.collider != _globalFloorCol)
                                        continue; // no cayó sobre el floor global
                                }
                                pos.y = hit.point.y;
                            }
                            else
                            {
                                if (_globalFloorCol != null) continue;
                                pos.y = floorHeight;
                            }

                            // Evitar pequeñas superposiciones con colliders (conservador)
                            if (Physics.CheckSphere(pos + Vector3.up * 0.05f, 0.15f, ~0, QueryTriggerInteraction.Ignore))
                                continue;

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

    // [MESH FLOOR] añade un quad en el mesh del "floor" (coordenadas en mundo)
    void AddFloorQuad_World(Vector3 centerW, Vector3 fwd, Vector3 right, float sizeFwd, float sizeRight)
    {
        if (!_globalFloor) return;

        float hf = sizeFwd  * 0.5f;
        float hr = sizeRight * 0.5f;

        // Esquinas en mundo (plano XZ)
        Vector3 p00 = centerW - right * hr - fwd * hf; // atrás-izq
        Vector3 p01 = centerW - right * hr + fwd * hf; // adelante-izq
        Vector3 p11 = centerW + right * hr + fwd * hf; // adelante-der
        Vector3 p10 = centerW + right * hr - fwd * hf; // atrás-der

        p00.y = p01.y = p11.y = p10.y = floorHeight;

        // Pasamos a espacio local del "floor"
        int baseIndex = _floorVerts.Count;
        _floorVerts.Add(_globalFloor.InverseTransformPoint(p00));
        _floorVerts.Add(_globalFloor.InverseTransformPoint(p01));
        _floorVerts.Add(_globalFloor.InverseTransformPoint(p11));
        _floorVerts.Add(_globalFloor.InverseTransformPoint(p10));

        // UVs simples por quad (tiling 0..1)
        _floorUVs.Add(new Vector2(0, 0));
        _floorUVs.Add(new Vector2(0, 1));
        _floorUVs.Add(new Vector2(1, 1));
        _floorUVs.Add(new Vector2(1, 0));

        // Triángulos (sentido horario visto desde arriba)
        _floorTris.Add(baseIndex + 0);
        _floorTris.Add(baseIndex + 1);
        _floorTris.Add(baseIndex + 2);
        _floorTris.Add(baseIndex + 0);
        _floorTris.Add(baseIndex + 2);
        _floorTris.Add(baseIndex + 3);
    }

    // [MESH FLOOR] vuelca los buffers al Mesh
    void RebuildFloorMesh()
    {
        if (_floorMesh == null) return;
        _floorMesh.Clear();
        _floorMesh.SetVertices(_floorVerts);
        _floorMesh.SetTriangles(_floorTris, 0, true);
        _floorMesh.SetUVs(0, _floorUVs);
        _floorMesh.RecalculateNormals();
        _floorMesh.RecalculateBounds();
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
