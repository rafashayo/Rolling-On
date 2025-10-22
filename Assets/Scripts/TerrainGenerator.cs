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

    // evitar ruta
    public LayerMask roadMask;
    public float roadRejectRadius = 2f;

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
        {
            if (!TrySpawnNext())
            {
                halted = true;
                break;
            }
        }
    }

    void Update()
    {
        if (halted || !player) return;

        float d = lastSocketEnd
            ? Vector3.Distance(player.position, lastSocketEnd.position)
            : Vector3.Distance(player.position, nextPos);

        if (d < triggerDistance)
            TrySpawnNext();
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

        if (lastSocketEnd == null)
            AlignByStart(t, start, nextPos, nextRot);
        else
            AlignStartToEnd(t, start, lastSocketEnd);

        if (roadHeight != 0f)
            t.position = new Vector3(t.position.x, roadHeight, t.position.z);

        // ---- Floor + decor alrededor del tramo ----
        if (floorPrefab)
        {
            Vector3 startW = start.position;
            Vector3 endW = end.position;
            Vector3 fwd = (endW - startW);  fwd.y = 0f;
            float length = fwd.magnitude;

            if (length > 0.01f)
            {
                // base ortonormal del tramo
                Vector3 fwdDir = fwd / length;
                Vector3 rightDir = new Vector3(fwdDir.z, 0f, -fwdDir.x);

                int forwardTiles = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.01f, floorTileSpacing)));
                float roadClear = roadHalfWidth + decorClearMargin;

                for (int f = 0; f < forwardTiles; f++)
                {
                    float along = (f + 0.5f) * floorTileSpacing;
                    Vector3 centerOnAxis = startW + fwdDir * Mathf.Min(along, length - 0.01f);

                    for (int s = -floorSideTiles; s <= floorSideTiles; s++)
                    {
                        if (s == 0) continue;
                        float lateral = s * floorTileSpacing;
                        if (Mathf.Abs(lateral) < roadClear) continue; // NO piso la ruta

                        Vector3 pos = centerOnAxis + rightDir * lateral;
                        pos.y = floorHeight;
                        Quaternion rot = rotateFloorWithRoad ? Quaternion.LookRotation(fwdDir, Vector3.up) : Quaternion.identity;

                        var tile = Instantiate(floorPrefab, pos, rot);
                        tile.transform.localScale = floorPrefab.transform.localScale;
                        tile.transform.SetParent(t, true);

                        // ======= DECORACIÓN: alrededor del camino SIN tocar la ruta =======
                        if (decorPrefabs != null && decorPrefabs.Length > 0 && decorPerTile > 0 && decorRadius > 0f && decorChance > 0f)
                        {
                            int toPlace = Random.Range(0, decorPerTile + 1);
                            int placed = 0;
                            int safety = toPlace * 10;

                            while (placed < toPlace && safety-- > 0)
                            {
                                if (Random.value > decorChance) break;

                                // punto aleatorio alrededor del tile (en círculo)
                                Vector2 rnd = Random.insideUnitCircle * decorRadius;
                                Vector3 candidate = pos + new Vector3(rnd.x, 0f, rnd.y);

                                // *** MEDICIÓN LATERAL ROBUSTA (sigue la curva) ***
                                // proyectamos el candidate sobre el eje del tramo, encontramos el centro local exacto y medimos la componente lateral
                                float alongReal = Mathf.Clamp(Vector3.Dot(candidate - startW, fwdDir), 0f, length);
                                Vector3 centerAtAlong = startW + fwdDir * alongReal;
                                float lateralFromAxis = Mathf.Abs(Vector3.Dot(candidate - centerAtAlong, rightDir));

                                // mantener fuera del corredor libre (ruta + margen)
                                if (lateralFromAxis < roadClear) continue;

                                // evitar capas de ruta por seguridad
                                if (roadMask.value != 0 && Physics.CheckSphere(candidate + Vector3.up * 0.25f, roadRejectRadius, roadMask))
                                    continue;

                                // apoyar con raycast
                                Vector3 rayOrigin = candidate + Vector3.up * 50f;
                                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                                {
                                    // si raycast pegó en algo marcado como ruta, descartamos
                                    bool hitIsRoad = (roadMask.value != 0) && (((1 << hit.collider.gameObject.layer) & roadMask.value) != 0);
                                    if (hitIsRoad) continue;

                                    Vector3 place = hit.point;

                                    var decoPrefab = decorPrefabs[Random.Range(0, decorPrefabs.Length)];
                                    Quaternion drot = decorRandomYaw
                                        ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                                        : Quaternion.identity;

                                    var deco = Instantiate(decoPrefab, place, drot);
                                    deco.transform.localScale = decoPrefab.transform.localScale;
                                    deco.transform.SetParent(tile.transform, true);

                                    placed++;
                                }
                            }
                        }
                        // ===================================================================
                    } // s
                } // f
            } // length
        } // floorPrefab
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
        // Si no hay al menos 3 prefabs, usamos la lógica original
        if (piecePrefabs.Length < 3)
        {
            int idxFallback = Random.Range(0, piecePrefabs.Length);
            if (lastIndex >= 0 && piecePrefabs.Length > 1 && Random.value < avoidImmediateRepeat)
            {
                int safety = 10;
                while (idxFallback == lastIndex && safety-- > 0)
                    idxFallback = Random.Range(0, piecePrefabs.Length);
            }
            return idxFallback;
        }

        const float pElement2 = 0.80f; // 80%
        int idx;

        if (Random.value < pElement2)
        {
            idx = 2; // Element 2
        }
        else
        {
            if (piecePrefabs.Length == 1) idx = 0;
            else
            {
                idx = Random.Range(0, piecePrefabs.Length - 1);
                if (idx >= 2) idx += 1;
            }
        }

        if (lastIndex >= 0 && piecePrefabs.Length > 1 && Random.value < avoidImmediateRepeat)
        {
            int safety = 10;
            while (idx == lastIndex && safety-- > 0)
            {
                if (Random.value < pElement2)
                    idx = 2;
                else
                {
                    int alt = Random.Range(0, piecePrefabs.Length - 1);
                    idx = (alt >= 2) ? alt + 1 : alt;
                }
            }
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
        bool ok = FindExactSocket(temp.transform, startSocketName)
               && FindExactSocket(temp.transform, endSocketName);
        DestroyImmediate(temp);
        return ok;
    }
}
