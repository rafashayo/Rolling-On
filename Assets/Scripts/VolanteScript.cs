using UnityEngine;

public class VolanteController : MonoBehaviour
{
    [Header("Configuración del volante")]
    public float velocidadGiro = 100f;      // Velocidad del giro del volante
    public float limiteGiro = 45f;          // Ángulo máximo de giro
    public float velocidadRetorno = 3f;     // Velocidad al volver al centro

    private float anguloActual = 0f;        // Ángulo actual del volante
    private Quaternion rotacionInicial;     // Rotación base del volante

    void Start()
    {
        rotacionInicial = transform.localRotation;
    }

    void Update()
    {
        float input = 0f;

        if (Input.GetKey(KeyCode.A))
            input = -1f;
        else if (Input.GetKey(KeyCode.D))
            input = 1f;

        if (input != 0)
        {
            // Actualizamos el ángulo según la entrada
            anguloActual -= input * velocidadGiro * Time.deltaTime;
            anguloActual = Mathf.Clamp(anguloActual, -limiteGiro, limiteGiro);
        }
        else
        {
            // Cuando no se presionan teclas, volver al centro suavemente
            anguloActual = Mathf.Lerp(anguloActual, 0f, Time.deltaTime * velocidadRetorno);
        }

        // Rotación en eje local Y
        Quaternion rotacionGiro = Quaternion.AngleAxis(anguloActual, Vector3.up);

        transform.localRotation = rotacionInicial * rotacionGiro;
    }
}
