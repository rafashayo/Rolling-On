using UnityEngine;
using System;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public enum Axe1 { Front, Rear }

    [Serializable]
    public struct Wheel
    {
        public GameObject wheelModel;
        public WheelCollider wheelCollider;
        public Axe1 axe1;
    }

    public float maxAcceleration = 30.0f;
    public float brakeAcceleration = 50.0f;
    public float turnSensitivity = 1.0f;
    public float maxSteerAngle = 30.0f;

    public Vector3 _centerOfMass;
    public List<Wheel> wheels;

    float moveInput, steerInput;
    Rigidbody carRb;

    // NUEVO: offset fijo por rueda respecto a la rotación que devuelve el WheelCollider
    Quaternion[] _modelOffsetWorld;

    void Start()
    {
        carRb = GetComponent<Rigidbody>();
        carRb.centerOfMass = _centerOfMass;

        // Capturamos el offset real una sola vez, comparando la rotación que
        // da el WheelCollider (GetWorldPose) con la del modelo tal como está en escena.
        _modelOffsetWorld = new Quaternion[wheels.Count];
        for (int i = 0; i < wheels.Count; i++)
        {
            var w = wheels[i];
            if (w.wheelModel && w.wheelCollider)
            {
                // rot del collider en world (sin spin/steer iniciales relevantes)
                w.wheelCollider.GetWorldPose(out _, out Quaternion rotWC0);
                // offset que “convierte” la rot del collider en la del modelo que dejaste en el editor
                _modelOffsetWorld[i] = Quaternion.Inverse(rotWC0) * w.wheelModel.transform.rotation;
            }
            else
            {
                _modelOffsetWorld[i] = Quaternion.identity;
            }
        }
    }

    void Update()
    {
        GetInputs();
        AnimatedWheels();
    }

    void LateUpdate()
    {
        Move();
        Steer();
        Brake();
    }

    void GetInputs()
    {
        moveInput  = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
    }

    void Move()
    {
        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.motorTorque = moveInput * 600f * maxAcceleration * Time.deltaTime;
        }
    }

    void Steer()
    {
        foreach (var wheel in wheels)
        {
            if (wheel.axe1 == Axe1.Front)
            {
                var target = steerInput * turnSensitivity * maxSteerAngle;
                wheel.wheelCollider.steerAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, target, 0.6f);
            }
        }
    }

    void Brake()
    {
        if(Input.GetKey(KeyCode.Space))
        {
            foreach(var wheel in wheels)
            {
                wheel.wheelCollider.brakeTorque = 300 * brakeAcceleration * Time.deltaTime;
            }
        }
        else
        {
            foreach(var wheel in wheels)
            {
                wheel.wheelCollider.brakeTorque = 0;
            }
        }
    }

    void AnimatedWheels()
    {
        for (int i = 0; i < wheels.Count; i++)
        {
            var w = wheels[i];
            if (!w.wheelModel || !w.wheelCollider) continue;

            w.wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            // Aplicamos SIEMPRE: rotación del collider * offset guardado (no se acumula)
            w.wheelModel.transform.SetPositionAndRotation(pos, rot * _modelOffsetWorld[i]);
        }
    }
}
