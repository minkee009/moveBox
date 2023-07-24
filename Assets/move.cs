using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Text;
using Unity.Burst.CompilerServices;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class move : MonoBehaviour
{
    public BoxCollider PlayerCollider;
    public Rigidbody PlayerRigidbody;
    public bool ShowDebugMovement;
    public Vector3 DebugMoveVector;
    public Vector3 Velocity { get; private set; }

    private RaycastHit[] _moveHits = new RaycastHit[5];
    private Vector3 _internalPosition = Vector3.zero;

    const float SWEEP_TEST_EPSILON = 0.002f;
    const int MAX_MOVE_ITERATION = 5;

    private void OnValidate()
    {
        PlayerCollider = GetComponent<BoxCollider>();
        PlayerRigidbody = GetComponent<Rigidbody>();
        
    }

    // Start is called before the first frame update
    void Start()
    {
        PlayerCollider = GetComponent<BoxCollider>();
        PlayerRigidbody = GetComponent<Rigidbody>();
        _internalPosition = transform.position;
    }

    Vector3 currentVelocity = Vector3.zero;

    // Update is called once per frame
    void Update()
    {
       // _internalPosition = transform.position;
        //_internalPosition = transform.position;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryPlayerMove(ref DebugMoveVector);
        }

        float inputX = (Input.GetKey(KeyCode.A) ? -1 : 0) + (Input.GetKey(KeyCode.D) ? 1 : 0);
        float inputY = (Input.GetKey(KeyCode.S) ? -1 : 0) + (Input.GetKey(KeyCode.W) ? 1 : 0);
        float inputZ = (Input.GetKey(KeyCode.E) ? -1 : 0) + (Input.GetKey(KeyCode.Q) ? 1 : 0);

        var targetVelocity = new Vector3(inputX, inputZ, inputY) * 6f * Time.deltaTime;

        Velocity = Vector3.Lerp(Velocity, targetVelocity, 6f * Time.deltaTime);

        currentVelocity = Velocity;

        TryPlayerMove(ref currentVelocity);
    }


    /// <summary>
    /// 미끌어짐 벡터 알고리즘으로 플레이어를 움직임
    /// </summary>
    /// <param name="moveVector"></param>
    void TryPlayerMove(ref Vector3 moveVector)
    {
        Vector3 initMoveVector = moveVector;

        int bumpCount = 0;
        int numbumps = 4;
        

        for (bumpCount = 0; bumpCount < numbumps; bumpCount++)
        {
            var moveHitsCount = BoxSweepTest(initMoveVector.normalized, initMoveVector.magnitude, _internalPosition, PlayerCollider, out RaycastHit hit);
            if (moveHitsCount > 0)
            {
                _internalPosition += hit.distance * initMoveVector.normalized;
                initMoveVector -= hit.distance * initMoveVector.normalized;
                var savedVector = initMoveVector;

                ClipVelocity(savedVector, hit.normal, out initMoveVector);
            }
            else
            {
                _internalPosition += initMoveVector;
                break;
            }
        }
        transform.position = _internalPosition;
    }

    Vector3 debugPos1 = Vector3.zero;

    /// <summary>
    /// 박스캐스트 후 가장 가까운 히트의 정보와 박스캐스트의 모든 히트 수를 반환한다.
    /// </summary>
    /// <param name="wishDir"></param>
    /// <param name="wishDist"></param>
    /// <param name="initialPos"></param>
    /// <param name="box"></param>
    /// <param name="closestHit"></param>
    /// <returns></returns>
    int BoxSweepTest(Vector3 wishDir, float wishDist, Vector3 initialPos,BoxCollider box,out RaycastHit closestHit)
    {
        var hitCount = Physics.BoxCastNonAlloc((initialPos + box.center), (box.size - Vector3.one * SWEEP_TEST_EPSILON) * 0.5f, wishDir, _moveHits, Quaternion.identity, wishDist + SWEEP_TEST_EPSILON, -1,QueryTriggerInteraction.Ignore);
        var closestDistInLoop = Mathf.Infinity;
        var closestHitInLoop = new RaycastHit();
        closestHit = new RaycastHit();

        for (int i = 0; i < hitCount; i++)
        {
            if (_moveHits[i].distance < closestDistInLoop)
            {
                if (_moveHits[i].distance <= 0f || _moveHits[i].collider == box)
                {
                    hitCount--;
                    continue;
                }
                _moveHits[i].distance -= SWEEP_TEST_EPSILON;
                closestDistInLoop = _moveHits[i].distance;
                closestHitInLoop = _moveHits[i];
            }
        }

        closestHit = closestHitInLoop;

        return hitCount;
    }

    /// <summary>
    /// 속도를 제한한다. 퀘이크 엔진 알고리즘 참조
    /// </summary>
    /// <returns></returns>
    int ClipVelocity(Vector3 inputVelocity, Vector3 normal, out Vector3 outputVelocity, float overbounce = 1.0f)
    {
        float backoff = 0.0f;
        float change = 0.0f;
        float angle = normal.y;
        outputVelocity = Vector3.zero;

        int blocked = 0;
        if (angle > 0)
            blocked |= 1;
        if (angle == 0)
            blocked |= 2;

        backoff = Vector3.Dot(inputVelocity, normal) * overbounce;

        for(int i = 0; i < 3; i++)
        {
            change = normal[i] * backoff;
            outputVelocity[i] = inputVelocity[i] - change;
        }

        float adjust = Vector3.Dot(outputVelocity, normal);
        if(adjust < 0.0f)
        {
            outputVelocity -= (normal * adjust);
        }

        return blocked;
    }

    
    private void OnDrawGizmos()
    {
        if (!ShowDebugMovement) return;
        var wishPos = Vector3.zero;

        var constantWishPos = transform.position + PlayerCollider.center + DebugMoveVector;
        Gizmos.color = Color.green;
        Gizmos.DrawCube(constantWishPos, PlayerCollider.size);
        Gizmos.DrawLine(transform.position, constantWishPos);

        var initMoveVector = DebugMoveVector;
        int bumpCount = 4;
        int numBump = 0;

        var tmpPosition = transform.position;


        for (numBump = 0; numBump < bumpCount; numBump++)
        {
            Gizmos.color = Color.yellow;
            var hitcount = BoxSweepTest(initMoveVector.normalized, initMoveVector.magnitude, tmpPosition, PlayerCollider, out RaycastHit hit);
            var lastTmpPos = tmpPosition;

            if (hitcount > 0)
            {
                tmpPosition += hit.distance * initMoveVector.normalized;
                Gizmos.DrawWireCube(tmpPosition, PlayerCollider.size);
                Gizmos.DrawLine(lastTmpPos, tmpPosition);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(hit.point, hit.normal * 2.0f);
                Gizmos.DrawSphere(hit.point, 0.05f);

                initMoveVector -= hit.distance * initMoveVector.normalized;
                var savedVector = initMoveVector;
                ClipVelocity(savedVector, hit.normal, out initMoveVector);
            }
            else
            {
                tmpPosition += initMoveVector;
                Gizmos.DrawWireCube(tmpPosition, PlayerCollider.size);
                Gizmos.DrawLine(lastTmpPos, tmpPosition);
                break;
            }
        }

        Gizmos.color = tmpPosition != constantWishPos ? Color.magenta : Color.green;
        Gizmos.DrawCube(tmpPosition, PlayerCollider.size);
    }
}
