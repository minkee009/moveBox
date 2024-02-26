using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using Unity.Burst.CompilerServices;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEditorInternal;
using UnityEngine;


[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Move : MonoBehaviour
{
    public Transform camHolder;
    public BoxCollider playerCollider;
    public Rigidbody playerRigidbody;
    public bool showDebugMovement;
    public Vector3 debugMoveVector;
    public Vector3 Velocity { get; private set; }

    private RaycastHit[] _moveHits = new RaycastHit[5];
    private Vector3 _internalPosition = Vector3.zero;
    private Vector3 _lastTickPostion = Vector3.zero;
    private Collider[] _overlapCols = new Collider[8];

    public bool isLerpMotion;
    public float speed;

    const float SWEEP_TEST_EPSILON = 0.0002f;
    const float COLLISION_OFFSET = 0.002f;
    const int MAX_MOVE_ITERATION = 4;
    const float MIN_PUSHBACK_DIST = 0.00005f;

    public float skinWidth = 0.01f;

    private float _inputX, _inputY, _inputZ;

    private void OnValidate()
    {
        playerCollider = GetComponent<BoxCollider>();
        playerRigidbody = GetComponent<Rigidbody>();
    }
    

    // Start is called before the first frame update
    void Start()
    {
        playerCollider = GetComponent<BoxCollider>();
        playerRigidbody = GetComponent<Rigidbody>();
        _internalPosition = transform.position;
    }

    Vector3 currentVelocity = Vector3.zero;

    private void Update()
    {
        // _internalPosition = transform.position;
        //_internalPosition = transform.position;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryPlayerMove(debugMoveVector);
        }

        if (Input.GetMouseButtonDown(1))
            isLerpMotion = !isLerpMotion;


        _inputX = (Input.GetKey(KeyCode.A) ? -1 : 0) + (Input.GetKey(KeyCode.D) ? 1 : 0);
        _inputY = (Input.GetKey(KeyCode.S) ? -1 : 0) + (Input.GetKey(KeyCode.W) ? 1 : 0);
        _inputZ = (Input.GetKey(KeyCode.E) ? -1 : 0) + (Input.GetKey(KeyCode.Q) ? 1 : 0);
    }

    float _lastTickTime = 0f;

    // Update is called once per frame
    void FixedUpdate()
    {
        var targetVelocity = new Vector3(_inputX, _inputZ, _inputY).normalized * speed * Time.deltaTime;

        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1 - Mathf.Exp(-6f * Time.deltaTime));

        Velocity = currentVelocity;

        _lastTickPostion = _internalPosition;

        TryPlayerMove(isLerpMotion ? currentVelocity : targetVelocity, true);

        _lastTickTime = Time.time;
    }

    private void LateUpdate()
    {
        float interpolationFactor = (Time.time - _lastTickTime) / (Time.fixedDeltaTime);
        transform.position = Vector3.Lerp(_lastTickPostion,_internalPosition, interpolationFactor);
    }

    /// <summary>
    /// 충돌 미끌어짐 알고리즘으로 플레이어를 움직임
    /// </summary>
    /// <param name="moveVector"></param>
    /// <param name="useCamDir">카메라 방향 사용여부</param>
    void TryPlayerMove(Vector3 moveVector,bool useCamDir = false)
    {
        if(moveVector.magnitude < COLLISION_OFFSET)
        {
            return;
        }

        Vector3 initMoveVector = useCamDir ? Quaternion.Euler(0, camHolder.eulerAngles.y, 0) * moveVector : moveVector;

        //푸쉬 백(오버랩핑)
        int pushBacks = BoxPushBack(_internalPosition, playerCollider, out Vector3[] pbVec);

        if (pushBacks > 0)
        {
            for (int i = 0; i < pushBacks; i++)
            {
                _internalPosition += pbVec[i].normalized * (pbVec[i].magnitude + MIN_PUSHBACK_DIST);

                if (pbVec[i].sqrMagnitude > 0)
                {
                    Vector3 newVel = -Vector3.Project(initMoveVector, pbVec[i].normalized);
                    initMoveVector -= Vector3.ProjectOnPlane(initMoveVector, newVel.normalized);
                }
            }
        }

        //스윕
        int numBump = 0;
        int bumpCount = MAX_MOVE_ITERATION;
        Vector3 originDir = initMoveVector.normalized;
        Vector3 prevPlaneNormal = Vector3.zero;

        for (numBump = 0; numBump < bumpCount; numBump++)
        {
            int hitCount = BoxSweepTest(initMoveVector.normalized, initMoveVector.magnitude + COLLISION_OFFSET, _internalPosition, playerCollider, out RaycastHit hit);
            if (hitCount > 0)
            {
                var dist = Mathf.Max(0.0f, hit.distance - COLLISION_OFFSET);

                _internalPosition += dist * initMoveVector.normalized;
                initMoveVector -= dist * initMoveVector.normalized;

                ClipVelocity(initMoveVector, hit.normal, out Vector3 outVec);

                if(numBump > 0)
                {
                    var creaseDir = Vector3.Cross(hit.normal, prevPlaneNormal).normalized;
                    outVec = Vector3.Project(initMoveVector, creaseDir);
                }

                initMoveVector = outVec;

                prevPlaneNormal = hit.normal;
            }
            else
            {
                _internalPosition += initMoveVector;
                break;
            }
        }
    }

    /// <summary>
    /// 박스캐스트 후 가장 가까운 히트의 정보와 박스캐스트의 모든 히트 수를 반환한다.
    /// </summary>
    /// <param name="wishDir">방향</param>
    /// <param name="wishDist">거리</param>
    /// <param name="initialPos">초기 위치</param>
    /// <param name="box">플레이어 콜라이더</param>
    /// <param name="closestHit">가장 가까운 히트</param>
    /// <returns>유효한 충돌 수</returns>
    int BoxSweepTest(Vector3 wishDir, float wishDist, Vector3 initialPos,BoxCollider box,out RaycastHit closestHit)
    {
        var hitCount = Physics.BoxCastNonAlloc(
            initialPos + box.center + (wishDir * -SWEEP_TEST_EPSILON), 
            box.size * 0.5f, wishDir, 
            _moveHits, Quaternion.identity, 
            wishDist + SWEEP_TEST_EPSILON , 
            -1,
            QueryTriggerInteraction.Ignore);

        var closestDistInLoop = Mathf.Infinity;
        var closestHitInLoop = new RaycastHit();

        var validHitCount = hitCount;

        for (int i = 0; i < hitCount; i++)
        {
            _moveHits[i].distance -= SWEEP_TEST_EPSILON;
            if (_moveHits[i].distance < closestDistInLoop)
            {
                if (_moveHits[i].distance <= 0f
                    || _moveHits[i].collider.isTrigger
                    || _moveHits[i].collider == box)
                {
                    validHitCount--;
                    continue;
                }
                closestDistInLoop = _moveHits[i].distance;
                closestHitInLoop = _moveHits[i];
            }
        }

        closestHit = closestHitInLoop;
        

        return validHitCount;
    }

    int BoxSweepFTest(Vector3 wishDir, float wishDist, Vector3 initialPos, BoxCollider box, out RaycastHit closestHit)
    {

        Vector3 skinHE = new Vector3(skinWidth, skinWidth, skinWidth);
        var hitCount = Physics.BoxCastNonAlloc(
            initialPos + box.center,
            box.size * 0.5f - skinHE, wishDir,
            _moveHits, Quaternion.identity,
            wishDist + skinWidth,
            -1,
            QueryTriggerInteraction.Ignore);

        var closestDistInLoop = Mathf.Infinity;
        var closestHitInLoop = new RaycastHit();
        int validHit = hitCount;

        for (int i = 0; i < hitCount; i++)
        {
            _moveHits[i].distance -= skinWidth;
            if (_moveHits[i].distance < closestDistInLoop)
            {
                if (_moveHits[i].distance <= skinWidth
                    || _moveHits[i].collider.isTrigger 
                    || _moveHits[i].collider == box)
                {
                    validHit--;
                    continue;
                }
                closestDistInLoop = _moveHits[i].distance;
                closestHitInLoop = _moveHits[i];
            }
        }

        closestHit = closestHitInLoop;

        return validHit;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="initialPos"></param>
    /// <param name="box"></param>
    /// <param name="pbVec"></param>
    /// <param name="skinWidth"></param>
    /// <returns>유효한 겹침상태 수</returns>
    int BoxPushBack(Vector3 initialPos, BoxCollider box, out Vector3[] pbVec, float skinWidth = 0f)
    {
        pbVec = new Vector3[_overlapCols.Length];

        var overlaps = Physics.OverlapBoxNonAlloc(initialPos + box.center, (box.size * 0.5f) + Vector3.one * skinWidth, _overlapCols, Quaternion.identity, -1, QueryTriggerInteraction.Ignore);

        var validOverlaps = overlaps;

        if (overlaps > 0)
        {
            var pbDir = Vector3.zero;
            var pbDist = 0f;
            var elmentCount = 0;

            for(int i = 0; i < overlaps; i++)
            {
                var other = _overlapCols[i];
                if(other == box
                    || other.isTrigger)
                {
                    validOverlaps--;
                    continue;
                }

                Vector3 otherPosition = other.gameObject.transform.position;
                Quaternion otherRotation = other.gameObject.transform.rotation;

                if (Physics.ComputePenetration(box, initialPos, Quaternion.identity, other, otherPosition, otherRotation,
                        out pbDir, out pbDist))
                {
                    initialPos += pbDir * pbDist;
                    pbVec[elmentCount++] = pbDir * pbDist;
                }
                else
                {
                    validOverlaps--;
                }
            }
        }

        return validOverlaps;
    }

    /// <summary>
    /// 속도를 제한하고 꺾는다. 퀘이크 엔진 알고리즘 참조
    /// </summary>
    /// <returns>충돌 플래그</returns>
    int ClipVelocity(Vector3 inputVelocity, Vector3 normal, out Vector3 outputVelocity, float overbounce = 1.0f)
    {
        float backoff = 0.0f;
        float change = 0.0f;
        float angle = normal.y;
        outputVelocity = Vector3.zero;

        int blocked = 0x00;
        if (angle > 0)
            blocked |= 0x01;
        if (angle == 0)
            blocked |= 0x02;

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

    //디버깅용 가시화
    private void OnDrawGizmos()
    {
        if (!showDebugMovement) return;

        var initMoveVector = debugMoveVector; //camHolder.TransformVector(new Vector3(_inputX, _inputZ, _inputY).normalized * speed * Time.fixedDeltaTime);

        var constantWishPos = transform.position + playerCollider.center + initMoveVector;
        Gizmos.color = Color.green;
        Gizmos.DrawCube(constantWishPos, playerCollider.size);
        Gizmos.DrawLine(transform.position, constantWishPos);

        int pushBacks = BoxPushBack(_internalPosition, playerCollider, out Vector3[] pbVec);

        if (pushBacks > 0)
        {
            for (int i = 0; i < pushBacks; i++)
            {
                Debug.Log(pbVec[i]);
                Debug.DrawRay(transform.position, pbVec[i], Color.cyan);

                _internalPosition += pbVec[i].normalized * (pbVec[i].magnitude + COLLISION_OFFSET);

                if (pbVec[i].sqrMagnitude > 0)
                {
                    Vector3 newVel = -Vector3.Project(initMoveVector, pbVec[i].normalized);
                    initMoveVector -= Vector3.ProjectOnPlane(initMoveVector, newVel.normalized);
                }
            }
        }

        int bumpCount = MAX_MOVE_ITERATION;
        int numBump = 0;
        var tmpPosition = transform.position;
        var originDir = initMoveVector.normalized;
        var prevPlaneNormal = Vector3.zero;

        for (numBump = 0; numBump < bumpCount; numBump++)
        {
            Gizmos.color = Color.yellow + new Color(-numBump/4f,-numBump/4f,0f);
            var hitcount = BoxSweepTest(initMoveVector.normalized, initMoveVector.magnitude + COLLISION_OFFSET, tmpPosition, playerCollider, out RaycastHit hit);
            var lastTmpPos = tmpPosition;

            if (hitcount > 0)
            {
                var dist = Mathf.Max(0.0f, hit.distance - COLLISION_OFFSET);
                tmpPosition += dist * initMoveVector.normalized;
                Gizmos.DrawWireCube(tmpPosition, playerCollider.size);
                Gizmos.DrawLine(lastTmpPos, tmpPosition);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(hit.point, hit.normal);
                Gizmos.DrawSphere(hit.point, 0.05f);

                initMoveVector -= dist * initMoveVector.normalized;

                ClipVelocity(initMoveVector, hit.normal, out Vector3 outVec);

                if (numBump > 0)
                {
                    Gizmos.color = Color.blue;


                    var creaseDir = Vector3.Cross(hit.normal, prevPlaneNormal).normalized;
                    Gizmos.DrawRay(hit.point, creaseDir);

                    outVec = Vector3.Project(initMoveVector, creaseDir);
                    Gizmos.color = Color.red;
                }

                initMoveVector = outVec;

                prevPlaneNormal = hit.normal;
            }
            else
            {
                tmpPosition += initMoveVector;
                Gizmos.DrawWireCube(tmpPosition, playerCollider.size);
                Gizmos.DrawLine(lastTmpPos, tmpPosition);
                break;
            }
        }

        Gizmos.color = tmpPosition != constantWishPos ? Color.magenta : Color.green;

        Gizmos.color -= new Color(0, 0, 0, 0.3f);

        Gizmos.DrawCube(tmpPosition, playerCollider.size - Vector3.one * 0.002f);
    }

}
