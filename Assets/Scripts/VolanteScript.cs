using UnityEngine;

public class VolanteController : MonoBehaviour
{
    [Header("Configuración del volante")]
    public float velocidadGiro = 100f;   // Velocidad del giro del volante
    public float limiteGiro = 45f;       // Ángulo máximo hacia cada lado

    private float anguloActual = 0f;     // Ángulo actual de giro
    private Quaternion rotacionInicial;  // Rotación inicial del volante

    void Start()
    {
        // Guardamos la rotación base del volante
        rotacionInicial = transform.localRotation;
    }

    void Update()
    {
        // Detectamos entrada del jugador
        float input = 0f;

        if (Input.GetKey(KeyCode.A))
            input = -1f;
        else if (Input.GetKey(KeyCode.D))
            input = 1f;

        // Actualizamos el ángulo actual según la entrada
        anguloActual -= input * velocidadGiro * Time.deltaTime;

        // Limitamos el ángulo de giro
        anguloActual = Mathf.Clamp(anguloActual, -limiteGiro, limiteGiro);

        // Creamos una rotación adicional SOLO sobre el eje verde (Y local)
        Quaternion rotacionGiro = Quaternion.AngleAxis(anguloActual, Vector3.up);

        // Aplicamos la rotación inicial + la del giro
        transform.localRotation = rotacionInicial * rotacionGiro;
    }
}
