using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent (typeof(Rigidbody))]
public class move : MonoBehaviour
{
    public BoxCollider PlayerCollider;
    public Rigidbody PlayerRigidbody;
    public bool ShowDebugMovement;
    public Vector3 DebugMoveVector;

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

    // Update is called once per frame
    void Update()
    {
        _internalPosition = transform.position;
        //_internalPosition = transform.position;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryPlayerMove(DebugMoveVector);
        }
    }


    /// <summary>
    /// 미끌어짐 벡터 알고리즘으로 플레이어를 움직임
    /// </summary>
    /// <param name="moveVector"></param>
    void TryPlayerMove(Vector3 moveVector)
    {
        var moveHitsCount = BoxSweepTest(moveVector.normalized, moveVector.magnitude, _internalPosition, PlayerCollider, out RaycastHit hit);
        if(moveHitsCount > 0)
        {
            _internalPosition += moveVector.normalized * (hit.distance - SWEEP_TEST_EPSILON);
        }
        else
        {
            _internalPosition += moveVector;
        }

        transform.position = _internalPosition;
    }

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
        var hitCount = Physics.BoxCastNonAlloc(initialPos + box.center + (wishDir * -SWEEP_TEST_EPSILON), box.size * 0.5f, wishDir, _moveHits, Quaternion.identity, wishDist, -1,QueryTriggerInteraction.Ignore);
        var closestDistInLoop = Mathf.Infinity;
        var closestHitInLoop = new RaycastHit();
        closestHit = new RaycastHit();

        for (int i = 0; i < hitCount; i++)
        {
            if (_moveHits[i].distance < closestDistInLoop)
            {
                if (_moveHits[i].collider == box)
                {
                    hitCount -= 1;
                    continue;
                }
                closestDistInLoop = _moveHits[i].distance;
                closestHitInLoop = _moveHits[i];
            }
        }

        closestHit = closestHitInLoop;

        return hitCount;
    }

    
    private void OnDrawGizmos()
    {
       
        var wishPos = Vector3.zero;

        //Debug.Log(hitCount);

        var constantWishPos = transform.position + PlayerCollider.center + DebugMoveVector;
        Gizmos.color = Color.green;
        Gizmos.DrawCube(constantWishPos, PlayerCollider.size);
        Gizmos.DrawLine(transform.position, constantWishPos);

        var initMoveVector = DebugMoveVector;
        var initMoveVectorMag = DebugMoveVector.magnitude;
        var currentMoveIteration = 0;

        var tmpPosition = transform.position;

        while (ShowDebugMovement && currentMoveIteration < MAX_MOVE_ITERATION && initMoveVectorMag > 0)
        {
            var hitCount = BoxSweepTest(initMoveVector.normalized, initMoveVectorMag, tmpPosition, PlayerCollider, out RaycastHit hit);
            var lastTmpPos = tmpPosition;

            if (hitCount > 0)
            {
                
                initMoveVectorMag -= (hit.distance - SWEEP_TEST_EPSILON);
                tmpPosition += (hit.distance - SWEEP_TEST_EPSILON) * initMoveVector.normalized;
                Gizmos.color = Color.yellow;
                Gizmos.DrawCube(tmpPosition, PlayerCollider.size);
                Gizmos.DrawLine(lastTmpPos, tmpPosition);

                initMoveVector = initMoveVector.normalized * initMoveVectorMag;
                initMoveVector = Vector3.ProjectOnPlane(initMoveVector, hit.normal);
                initMoveVectorMag = initMoveVector.magnitude;
            }
            else
            {
                tmpPosition += initMoveVector.normalized * initMoveVectorMag;
                Gizmos.color = Color.yellow;
                Gizmos.DrawCube(tmpPosition, PlayerCollider.size);
                Gizmos.DrawLine(lastTmpPos, tmpPosition);
                initMoveVectorMag = 0f;
            }



            currentMoveIteration++;
        }
       
/*        if(hitCount > 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(wishPos, PlayerCollider.size);
            Gizmos.DrawLine(transform.position, wishPos);
        }*/
        
    }
}
