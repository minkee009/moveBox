using UnityEngine;

public class Move : MonoBehaviour
{
    [Header("¿¬°á ÄÄÆ÷³ÍÆ®")]
    public BoxCharacterController CharacterController;
    public Transform camHolder;

    [Header("¼Ó¼º")]
    public bool isLerpMotion;
    public float speed;

    //ÇÁ¶óÀÌºø ¸â¹ö
    private float _inputX, _inputY, _inputZ;
    private Vector3 _lastPosition = Vector3.zero;
    private Vector3 _currentVelocity = Vector3.zero;
    private float _lastFixedTime = 0f;

    void FixedUpdate()
    {
        _lastPosition = CharacterController.GetInternalPosition;

        var targetVelocity = camHolder.TransformVector(new Vector3(_inputX, _inputZ, _inputY).normalized * speed * Time.deltaTime);

        _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, 1 - Mathf.Exp(-6f * Time.deltaTime));

        var moveVector = isLerpMotion ? _currentVelocity : targetVelocity;

        CharacterController.TryMove(moveVector);

        _lastFixedTime = Time.fixedTime;
    }

    // Update is called once per frame
    void Update()
    {
        _inputX = (Input.GetKey(KeyCode.A) ? -1 : 0) + (Input.GetKey(KeyCode.D) ? 1 : 0);
        _inputY = (Input.GetKey(KeyCode.S) ? -1 : 0) + (Input.GetKey(KeyCode.W) ? 1 : 0);
        _inputZ = (Input.GetKey(KeyCode.Q) ? -1 : 0) + (Input.GetKey(KeyCode.E) ? 1 : 0);

    }

    void LateUpdate()
    {
        float lerpFactor = (Time.time - _lastFixedTime) / Time.fixedDeltaTime;
        transform.position = Vector3.Lerp(_lastPosition, CharacterController.GetInternalPosition, lerpFactor);
    }
}
