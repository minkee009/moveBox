using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

public class Look : MonoBehaviour
{
    public Transform camHolder;
    public Transform altHolder;
    public float scrollScale = 0.7f;

    public float RotX => _rotX;
    public float RotY => _rotY;

    private float _rotX, _rotY;
    private float _currentScroll;
    private float _targetScroll;

    private bool _cursorLock;


    public void Awake()
    {
        _targetScroll = -altHolder.localPosition.z;
        _currentScroll = _targetScroll;
        Cursor.lockState = CursorLockMode.Locked;
        _cursorLock = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _cursorLock = !_cursorLock;
            switch (_cursorLock)
            {
                case true:
                    Cursor.lockState = CursorLockMode.Locked;
                    break;
                case false:
                    Cursor.lockState = CursorLockMode.None;
                    break;
            }
        }

        _rotY += Input.GetAxis("Mouse X");
        _rotX -= Input.GetAxis("Mouse Y");

        _rotY += Gamepad.current?.rightStick.value.x ?? 0f * 250f * Time.deltaTime;
        _rotX -= Gamepad.current?.rightStick.value.y ?? 0f * 250f * Time.deltaTime;

        _rotX = Mathf.Clamp(_rotX, -89f, 89f);

        _targetScroll -= Input.mouseScrollDelta.y * scrollScale;
        _targetScroll -= Gamepad.current?.dpad.y.value ?? 0f * 25f * scrollScale * Time.smoothDeltaTime;

        _targetScroll = Mathf.Clamp(_targetScroll, 0f, 8f);
        _currentScroll = Mathf.Lerp(_currentScroll, _targetScroll, 6f * Time.deltaTime);

        altHolder.transform.localPosition = Vector3.back * _currentScroll;

        transform.rotation = Quaternion.Euler(_rotX, _rotY, 0);
    }
}
