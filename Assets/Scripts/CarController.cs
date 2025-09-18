using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Movimiento")]
    public float maxSpeed = 15f;
    public float acceleration = 25f;
    public float turnSpeed = 100f;

    [Header("Física")]
    public float downForce = 30f;
    public LayerMask groundMask;
    public float groundCheckDist = 0.6f;

    [Header("Agarre")]
    [Range(0f,1f)] public float lateralGrip = 0.95f;   // 0 = derrapa, 1 = sin deriva
    [Range(0f,1f)] public float steerAssist = 0.85f;    // empuja la velocidad hacia el heading

    Rigidbody rb;
    bool grounded;
    Vector3 groundNormal = Vector3.up;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.centerOfMass = new Vector3(0f, -0.3f, 0.1f);
    }

    void FixedUpdate()
    {
        float v = Input.GetAxis("Vertical");
        float h = Input.GetAxis("Horizontal");

        // Suelo + normal
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
            out var hit, groundCheckDist, (groundMask.value == 0 ? ~0 : groundMask)))
        {
            grounded = true;
            groundNormal = hit.normal;
            rb.AddForce(-groundNormal * downForce, ForceMode.Acceleration);
        }
        else { grounded = false; groundNormal = Vector3.up; }

        // Ejes sobre el plano del suelo
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(groundNormal, fwd).normalized;

        // Velocidad descompuesta en el plano del suelo
        Vector3 vel = rb.linearVelocity;
        Vector3 velPlanar = Vector3.ProjectOnPlane(vel, groundNormal);
        float vForward = Vector3.Dot(velPlanar, fwd);
        float vLateral = Vector3.Dot(velPlanar, right);

        // Acelerar / frenar hacia adelante del coche
        float targetSpeed = Mathf.Clamp(v, -1f, 1f) * maxSpeed;
        float speedDiff = targetSpeed - vForward;
        rb.AddForce(fwd * speedDiff * acceleration, ForceMode.Acceleration);

        // Giro (invertido en reversa) sobre la normal del suelo
        float dir = (vForward >= -0.1f) ? 1f : -1f;
        float speed01 = Mathf.Clamp01(velPlanar.magnitude / (maxSpeed * 0.5f));
        float turnThisFrame = h * turnSpeed * dir * speed01 * (grounded ? 1f : 0.2f) * Time.fixedDeltaTime;
        rb.MoveRotation(Quaternion.AngleAxis(turnThisFrame, groundNormal) * rb.rotation);

        // Recalcular ejes tras girar (heading nuevo)
        fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        right = Vector3.Cross(groundNormal, fwd).normalized;

        // Grip lateral: matar deriva en 'right'
        vLateral *= (1f - lateralGrip);

        // Steer assist: llevar la velocidad hacia el heading (sin tocar magnitud total)
        Vector3 velPlanarAfterGrip = fwd * vForward + right * vLateral;
        Vector3 targetPlanarDir = fwd * Mathf.Max(0f, velPlanarAfterGrip.magnitude);
        Vector3 velPlanarAligned = Vector3.Lerp(velPlanarAfterGrip, targetPlanarDir,
            steerAssist * (grounded ? 1f : 0.2f));

        // Reconstruir velocidad total manteniendo componente vertical original
        Vector3 velVertical = vel - velPlanar;
        rb.linearVelocity = velPlanarAligned + velVertical;
    }
}
