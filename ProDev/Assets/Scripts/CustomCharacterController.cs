using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomCharacterController : MonoBehaviour
{
    public float Speed = 10.0f;

    Rigidbody _rigidbody;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        Vector3 movement = new Vector3( Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical") );
        _rigidbody.velocity = _rigidbody.rotation * movement * Speed;
    }
}
