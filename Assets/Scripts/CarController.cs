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

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.centerOfMass = Vector3.down;
        // Si querés: rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        // Inputs
        float v = Input.GetAxis("Vertical");   // W/S o flechas
        float h = Input.GetAxis("Horizontal"); // A/D o flechas

        // Chequeo de suelo
        grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDist, groundMask.value == 0 ? ~0 : groundMask);
        if (grounded)
            rb.AddForce(-transform.up * downForce, ForceMode.Acceleration);

        // Velocidad deseada
        float targetSpeed = v * maxSpeed;
        float currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedDiff = targetSpeed - currentSpeed;

        // Acelera/frena
        Vector3 force = transform.forward * speedDiff * acceleration;
        rb.AddForce(force, ForceMode.Acceleration);

        // --- Giro con inversión en reversa ---
        // Determino si voy hacia adelante o hacia atrás
        float dir = (currentSpeed >= -0.1f) ? 1f : -1f;

        float speed01 = Mathf.Clamp01(rb.linearVelocity.magnitude / (maxSpeed * 0.5f));
        float turnThisFrame = h * turnSpeed * dir * speed01 * (grounded ? 1f : 0.2f) * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnThisFrame, 0f));
    }
}
