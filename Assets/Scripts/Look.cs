using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;

public class Look : MonoBehaviour
{
    public Transform camHolder;
    public Transform altHolder;
    public Transform player;
    public float scrollScale = 0.7f;

    public float RotX => _rotX;
    public float RotY => _rotY;

    private float _rotX,_rotY;
    private float _currentScroll;
    private float _targetScroll;

    private bool _cursorLock;

    private RaycastHit[] _hits = new RaycastHit[4];

    public void Awake()
    {
        _targetScroll = -altHolder.localPosition.z;
        _currentScroll = _targetScroll;
        Cursor.lockState = CursorLockMode.Locked;
        _cursorLock = true;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            _cursorLock = !_cursorLock;
            switch(_cursorLock)
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
        _rotX = Mathf.Clamp(_rotX, -89f, 89f);

        
        _targetScroll -= Input.mouseScrollDelta.y * scrollScale;
        _targetScroll = Mathf.Clamp(_targetScroll, 0f, 8f);
        _currentScroll = Mathf.Lerp(_currentScroll, _targetScroll, 6f * Time.deltaTime);

        if(_targetScroll > 0f)
        {
            var thirdPersonCamHit = Physics.SphereCastNonAlloc(transform.position + transform.forward * 0.012f, 0.2f, -transform.forward, _hits,_currentScroll + 0.012f);
            var closestDist = Mathf.Infinity;
            if (thirdPersonCamHit > 0)
            {
                foreach (var hit in _hits)
                {
                    if (hit.transform == player)
                        continue;

                    if(hit.distance > 0.0f && hit.distance < closestDist)
                        closestDist = hit.distance;
                }
            }

            closestDist = Mathf.Max(0.0f, closestDist - 0.012f);

            if (closestDist != Mathf.Infinity && closestDist != 0.0f)
            {
                _currentScroll = closestDist;
            }
        }

        altHolder.transform.localPosition = Vector3.back * _currentScroll;

        transform.rotation = Quaternion.Euler(_rotX,_rotY,0);
    }
    public void LateUpdate()
    {
        if(ReferenceEquals(player,null)) return;

        transform.position = player.position + Vector3.up * 1.2192f;
    }
}
