using UnityEngine;

public class ComScript : MonoBehaviour
{
    public Transform com;
    Rigidbody rb;
    void Awake() {
    rb = GetComponent<Rigidbody>();
    if (com) rb.centerOfMass = transform.InverseTransformPoint(com.position);
}

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
