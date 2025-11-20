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
        public GameObject WheelEffectObj;
        public Axe1 axe1;
    }

    public float aceleradorNumero;
    public float maxAcceleration;
    public float brakeAcceleration;
    public float turnSensitivity;
    public float maxSteerAngle;

    public Vector3 _centerOfMass;
    public List<Wheel> wheels;

    float moveInput, steerInput;
    Rigidbody carRb;

    Quaternion[] _modelOffsetWorld;

    public float RotationSpeed;

    // --- FUEL ---
    public float fuel = 100f;              
    public float fuelConsumptionRate = 5f; 

    void Start()
    {
        carRb = GetComponent<Rigidbody>();
        carRb.centerOfMass = _centerOfMass;

        _modelOffsetWorld = new Quaternion[wheels.Count];
        for (int i = 0; i < wheels.Count; i++)
        {
            var w = wheels[i];
            if (w.wheelModel && w.wheelCollider)
            {
                w.wheelCollider.GetWorldPose(out _, out Quaternion rotWC0);
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
        WheelEffects();

        ConsumeFuel();
    }

    void LateUpdate()
    {
        Move();
        Steer();
        Brake();
    }

    void GetInputs()
    {
        moveInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
    }

    void Move()
    {
        foreach (var wheel in wheels)
        {
            if (wheel.axe1 == Axe1.Rear)
            {
                // --- MODIFICADO: si fuel <= 0, no acelera ---
                wheel.wheelCollider.motorTorque = (fuel > 0f) ? moveInput * maxAcceleration * aceleradorNumero : 0f;
            }
            else
            {
                wheel.wheelCollider.motorTorque = 0f;
            }
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
        foreach (var wheel in wheels)
        {
            // si presionás espacio, frena normalmente
            if (Input.GetKey(KeyCode.Space))
            {
                wheel.wheelCollider.brakeTorque = 300 * brakeAcceleration * Time.deltaTime;
            }
            // --- MODIFICADO: si fuel <= 0, frena automáticamente ---
            else if (fuel <= 0f)
            {
                wheel.wheelCollider.brakeTorque = 1000f; // fuerza de freno alta para detener
            }
            else
            {
                wheel.wheelCollider.brakeTorque = 0f;
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
            w.wheelModel.transform.SetPositionAndRotation(pos, rot * _modelOffsetWorld[i]);
        }
    }

    void WheelEffects()
    {
        foreach (var wheel in wheels)
        {
            if (Input.GetKey(KeyCode.Space) && wheel.axe1 == Axe1.Rear)
                wheel.WheelEffectObj.GetComponentInChildren<TrailRenderer>().emitting = true;
            else
                wheel.WheelEffectObj.GetComponentInChildren<TrailRenderer>().emitting = false;
        }
    }

    public void AssignWheels()
    {
        wheels = GameObject.FindFirstObjectByType<wheelMaster>().wheels;
    }

    void ConsumeFuel()
    {
        if (fuel <= 0f) return; // ya está en 0, no resta más

        fuel -= fuelConsumptionRate * Time.deltaTime;
        if (fuel < 0f) fuel = 0f;

        Debug.Log("Fuel: " + fuel.ToString("F1") + "%");
    }
}
