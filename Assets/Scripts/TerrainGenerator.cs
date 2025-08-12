using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    public GameObject roadPrefab, terrainPrefab;
    public Transform player;

    public int segmentLength = 50, initialSegments = 5;
    public float triggerDistance = 30f, roadHeight = 0.01f, terrainHeight = 0f, segmentLifetime = 1200f;

    public float normalCurveChange = 1.2f, maxTotalCurve = 60f;
    public float sharpTurnChance = 0.07f, sharpAngleMin = 18f, sharpAngleMax = 40f;
    public int sharpEaseMin = 3, sharpEaseMax = 7;

    public int minSubSegments = 4, maxSubSegments = 12; // adaptativo
    public float subOverlapZ = 0.3f;                    // solape para evitar cortes
    public float subSmoothing = 0.7f;                   // suavizado intra-segmento

    private Vector3 nextSpawnPosition;
    private Quaternion nextSpawnRotation = Quaternion.identity;
    private float currentCurve = 0f, smoothCurve = 0f, smoothVel;

    private int easeStepsLeft = 0;
    private float easeStepDelta = 0f;

    void Start()
    {
        nextSpawnPosition = Vector3.zero;
        for (int i = 0; i < initialSegments; i++) SpawnSegment();
    }

    void Update()
    {
        if (!player) return;
        if (Vector3.Distance(player.position, nextSpawnPosition) < triggerDistance) SpawnSegment();
    }

    void SpawnSegment()
    {
        GameObject container = new GameObject("Segment");

        float curveDelta;
        if (easeStepsLeft > 0)
        {
            curveDelta = easeStepDelta; easeStepsLeft--;
        }
        else if (Random.value < sharpTurnChance)
        {
            float sign = Random.value < 0.5f ? -1f : 1f;
            float total = sign * Random.Range(sharpAngleMin, sharpAngleMax);
            int steps = Random.Range(sharpEaseMin, sharpEaseMax + 1);
            easeStepDelta = total / steps; easeStepsLeft = steps - 1;
            curveDelta = easeStepDelta;
        }
        else
        {
            curveDelta = Random.Range(-normalCurveChange, normalCurveChange);
        }

        currentCurve = Mathf.Clamp(currentCurve + curveDelta, -maxTotalCurve, maxTotalCurve);
        Quaternion targetRot = Quaternion.Euler(0f, currentCurve, 0f);

        int subSegments = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(minSubSegments, maxSubSegments,
                Mathf.InverseLerp(0f, sharpAngleMax, Mathf.Abs(curveDelta)))),
            minSubSegments, maxSubSegments);

        float pieceLen = (segmentLength / (float)subSegments);
        Vector3 pos = nextSpawnPosition;
        float targetAngle = currentCurve;

        for (int s = 0; s < subSegments; s++)
        {
            smoothCurve = Mathf.SmoothDampAngle(smoothCurve, targetAngle, ref smoothVel, 1f - subSmoothing);
            Quaternion stepRot = Quaternion.Euler(0f, smoothCurve, 0f);

            Vector3 roadPos = pos + stepRot * new Vector3(0f, roadHeight, 0f);
            Instantiate(roadPrefab, roadPos, stepRot, container.transform);

            float offset = 25f;
            Vector3 leftPos  = pos + stepRot * new Vector3(-offset, terrainHeight, 0f);
            Vector3 rightPos = pos + stepRot * new Vector3( offset, terrainHeight, 0f);
            Instantiate(terrainPrefab, leftPos,  Quaternion.identity, container.transform);
            Instantiate(terrainPrefab, rightPos, Quaternion.identity, container.transform);

            float advance = Mathf.Max(0.01f, pieceLen - subOverlapZ);
            pos += stepRot * new Vector3(0f, 0f, advance);
        }

        Destroy(container, segmentLifetime);
        nextSpawnRotation = targetRot;
        nextSpawnPosition = pos;
    }
}
