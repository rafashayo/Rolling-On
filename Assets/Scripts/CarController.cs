using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Movimiento")]
    public float maxSpeed = 15f;
    public float acceleration = 25f;
    public float turnSpeed = 100f;

    [Header("Física")]
    public float downForce = 20f;
    public LayerMask groundMask;
    public float groundCheckDist = 0.6f;

    Rigidbody rb;
    bool grounded;
    Vector3 groundNormal = Vector3.up;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.centerOfMass = new Vector3(0f, -0.3f, 0.1f); // baja un poco el COM
    }

    void FixedUpdate()
    {
        float v = Input.GetAxis("Vertical");
        float h = Input.GetAxis("Horizontal");

        // Chequeo de suelo + normal
        Ray ray = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
        if (Physics.Raycast(ray, out var hit, groundCheckDist, (groundMask.value == 0 ? ~0 : groundMask)))
        {
            grounded = true;
            groundNormal = hit.normal;
            // pegado al piso
            rb.AddForce(-groundNormal * downForce, ForceMode.Acceleration);
        }
        else
        {
            grounded = false;
            groundNormal = Vector3.up;
        }

        // Proyectar forward y velocidad sobre el plano del suelo
        Vector3 fwdOnGround = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        Vector3 velOnGround = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);

        // Velocidad escalar en dirección de avance
        float currentSpeed = Vector3.Dot(velOnGround, fwdOnGround);
        float targetSpeed = Mathf.Clamp(v, -1f, 1f) * maxSpeed;
        float speedDiff = targetSpeed - currentSpeed;

        // Acelerar/frenar
        rb.AddForce(fwdOnGround * speedDiff * acceleration, ForceMode.Acceleration);

        // Dirección del giro: invertir cuando vas marcha atrás
        float dir = (currentSpeed >= -0.1f) ? 1f : -1f;

        // Cuánto girar este frame (más giro con más agarre/suelo)
        float speed01 = Mathf.Clamp01(velOnGround.magnitude / (maxSpeed * 0.5f));
        float turnThisFrame = h * turnSpeed * dir * speed01 * (grounded ? 1f : 0.2f) * Time.fixedDeltaTime;

        // *** GIRO CORRECTO: sobre la NORMAL DEL SUELO (o Y si estás en el aire) ***
        Quaternion yaw = Quaternion.AngleAxis(turnThisFrame, groundNormal);
        rb.MoveRotation(yaw * rb.rotation);
    }
}
