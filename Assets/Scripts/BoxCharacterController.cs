using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum CCDebugDrawMode
{
    None = 0,
    Sweep = 1,
    Overlap = 2,
}

[RequireComponent(typeof(BoxCollider))]
//[RequireComponent(typeof(Rigidbody))]
public class BoxCharacterController : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    public BoxCollider playerCollider;
    public Rigidbody playerRigidbody;

    [Header("최적화")]
    public bool useInitalOverlap = true;
    public bool useFinalPushback = true;

    [Header("속성")]
    public float characterMass = 1f;

    [Header("디버깅")]
    public CCDebugDrawMode drawDebugMode;
    public Vector3 debugMoveVector;
    [Range(0, MAX_PUSHBACK_ITERATION)] public int debugOverlapCount = 1;

    //프로퍼티
    public Vector3 GetInternalPosition => _internalPosition;
    public Vector3 Velocity { get; private set; }

    //상수
    const float COLLISION_OFFSET = 0.02f;
    const float PUSHBACK_BIAS = 0.005f;
    const float SWEEP_TEST_BIAS = 0.002f;
    const int MAX_MOVE_ITERATION = 5;
    const int MAX_PUSHBACK_ITERATION = 8;
    const int MAX_CLIP_PLANES = 3;
    const float MIN_MOVEDISTANCE = 0.001f;

    //프라이빗 멤버
    private RaycastHit[] _moveHits = new RaycastHit[5];
    private Vector3 _internalPosition = Vector3.zero;
    private Collider[] _overlapCols = new Collider[8];
    private Collider _ground;

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

    public bool SaveClipPlaneNormal(ref int idx, ref Vector3[] array, Vector3 normal)
    {
        if (idx < array.Length)
        {
            array[idx++] = normal;
            return true;
        }
        return false;
    }

    public void CalcClipPlaneVelForAir(in int count, in Vector3[] array, ref Vector3 velocity)
    {
        switch (count)
        {
            case 1:
                velocity = Vector3.ProjectOnPlane(velocity, array[0]);
                break;
            case 2:
                velocity = Vector3.Project(velocity, Vector3.Cross(array[0], array[1]).normalized);
                break;
            case 3:
                velocity = Vector3.zero;
                break;
        }
    }

    /// <summary>
    /// 충돌 미끌어짐 알고리즘으로 플레이어를 움직임
    /// </summary>
    /// <param name="moveVector"></param>
    /// <param name="useCamDir">카메라 방향 사용여부</param>
    public void TryMove(Vector3 moveVector)
    {
        if (moveVector.magnitude < MIN_MOVEDISTANCE)
            return;

        Vector3 initMoveVector = moveVector;
        _internalPosition = transform.position;

        //푸쉬 백(오버랩핑) -> kinematicbody 및 rigidbody interaction
        BoxPushBack(_internalPosition, -1, out _internalPosition);

       
        //Collide and Slide
        Vector3 currentVelocity = moveVector;
        Vector3 currentInternalPos = _internalPosition;
        Vector3 playerExt = playerCollider.size * 0.5f;
        Vector3[] savedPlanes = new Vector3[MAX_CLIP_PLANES];
        int savedPlanesIdx = 0;

        for(int numBump = 0; numBump < MAX_MOVE_ITERATION; numBump++)
        {
            Vector3 currentDir = currentVelocity.normalized;
            float currentMag = currentVelocity.magnitude;

            if (Mathf.Approximately(0.0f, currentMag))
                break;

            bool foundOverlappedPlane = false;

            if (useInitalOverlap)
            {
                Array.Clear(_overlapCols, 0, _overlapCols.Length);
                int overlaps = Physics.OverlapBoxNonAlloc(currentInternalPos + playerCollider.center, playerExt, _overlapCols, Quaternion.identity, -1, QueryTriggerInteraction.Ignore);
                if(overlaps > 0)
                {
                    for (int i = 0; i < overlaps; i++)
                    {
                        var other = _overlapCols[i];
                        if (other != playerCollider
                            &&  !other.isTrigger
                            &&  Physics.ComputePenetration(
                                playerCollider, 
                                currentInternalPos, 
                                Quaternion.identity,
                                other,
                                other.transform.position,
                                other.transform.rotation,
                                out Vector3 pbDir, out float pbDist)
                            &&  Vector3.Dot(pbDir, moveVector.normalized) < 0f)
                        {
                            currentInternalPos += pbDir * (pbDist + PUSHBACK_BIAS);
                            currentVelocity = Vector3.ProjectOnPlane(currentVelocity, pbDir);
                            SaveClipPlaneNormal(ref savedPlanesIdx,ref savedPlanes, pbDir);
                            foundOverlappedPlane = true;
                            break;
                        }
                    }
                }
            }

            int hitCount = BoxSweepTest(currentDir, currentMag + COLLISION_OFFSET,currentInternalPos,playerCollider,-1,out RaycastHit hit);
            if (!foundOverlappedPlane && hitCount > 0)
            {
                //현재 이동력만큼 이동
                hit.distance = Mathf.Max(0.0f, hit.distance - COLLISION_OFFSET);
                currentInternalPos += currentDir * hit.distance;
                currentVelocity = currentDir * Mathf.Max(0.0f, currentMag - hit.distance);

                SaveClipPlaneNormal(ref savedPlanesIdx, ref savedPlanes, hit.normal);
               
                if (savedPlanesIdx < 2)
                {
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, savedPlanes[0]);
                }
                else if(savedPlanesIdx == 2)
                {

                    Vector3 creaseDir = Vector3.Cross(savedPlanes[0], savedPlanes[1]).normalized;
                    currentVelocity = Vector3.Project(currentVelocity, creaseDir);
                    //var tempVel = Vector3.ProjectOnPlane(currentVelocity, savedPlanes[1]);

                    //if (Mathf.Abs(Vector3.Dot(savedPlanes[0], savedPlanes[1])) < 1.0f)
                    //{
                    //    Vector3 creaseDir = Vector3.Cross(savedPlanes[0], savedPlanes[1]).normalized;
                    //    currentVelocity = Vector3.Project(currentVelocity, creaseDir);
                    //}
                    //else
                    //{
                    //    currentVelocity = tempVel;
                    //    savedPlanesIdx = 0;
                    //}
                }
                else
                {
                    currentVelocity = Vector3.zero;
                    break;
                }
            }
            else if (hitCount == 0)
            {
                currentInternalPos += currentVelocity;
                break;
            }
        }

        Velocity = currentVelocity;
        _internalPosition = currentInternalPos;

        //푸쉬 백(오버랩핑)
        if (useFinalPushback)
            BoxPushBack(_internalPosition, -1,out _internalPosition);

        //transform.position = _internalPosition;
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
    int BoxSweepTest(Vector3 wishDir, float wishDist, Vector3 initialPos, BoxCollider box, int layer, out RaycastHit closestHit)
    {
        //bool isRuntime = Application.isPlaying;
        Array.Clear(_moveHits, 0, _moveHits.Length);
        int hitCount = Physics.BoxCastNonAlloc(initialPos + box.center -(wishDir * SWEEP_TEST_BIAS), box.size * 0.5f, wishDir, _moveHits, Quaternion.identity, wishDist + SWEEP_TEST_BIAS, layer, QueryTriggerInteraction.Ignore);
        float closestHitDist = Mathf.Infinity;
        closestHit = new RaycastHit();

        int loopCount = hitCount;
        
        //if(isRuntime)
        //    Debug.Log(hitCount);

        for (int i = 0; i < loopCount; i++)
        {
            //if (isRuntime)
            //    Debug.Log($"[{i}] : {_moveHits[i].transform.gameObject.name} / dist -> {_moveHits[i].distance} / normal -> {_moveHits[i].normal}");

            if (_moveHits[i].distance < closestHitDist)
            {
                if (_moveHits[i].distance <= SWEEP_TEST_BIAS
                    || _moveHits[i].collider == box
                    || _moveHits[i].collider.isTrigger
                    || (_moveHits[i].collider.attachedRigidbody != null && !_moveHits[i].collider.attachedRigidbody.isKinematic))
                {
                    hitCount--;
                    continue;
                }
                closestHitDist = _moveHits[i].distance;
                closestHit = _moveHits[i];
            }
        }

        closestHit.distance -= SWEEP_TEST_BIAS;
        closestHit.distance = Mathf.Max(0.0f, closestHit.distance);
        
        return hitCount;
    }

    /// <summary>
    /// 박스 형태로 겹침을 확인하여 풉니다, 최대횟수는 MAX_PUSHBACK_ITERATION 에 의해 결정됩니다.
    /// </summary>
    /// <param name="initialPos"></param>
    /// <param name="box"></param>
    /// <param name="pbVec"></param>
    /// <param name="skinWidth"></param>
    void BoxPushBack(Vector3 initialPos, int layer, out Vector3 afterPos, float skinWidth = 0f)
    {
        Array.Clear(_overlapCols, 0, _overlapCols.Length);
        afterPos = initialPos;

        int currentPBIteration = 0;
        bool finishedPB = false;
        Vector3 halfext = (playerCollider.size * 0.5f) + Vector3.one * skinWidth;

        while (currentPBIteration < MAX_PUSHBACK_ITERATION && !finishedPB)
        {
            var overlaps = Physics.OverlapBoxNonAlloc(afterPos + playerCollider.center, halfext, _overlapCols, Quaternion.identity, layer, QueryTriggerInteraction.Ignore);

            if (overlaps > 0)
            {
                for (int i = 0; i < overlaps; i++)
                {
                    var other = _overlapCols[i];
                    if (other != playerCollider 
                        && !other.isTrigger
                        && Physics.ComputePenetration(
                                playerCollider,
                                afterPos,
                                Quaternion.identity,
                                other,
                                other.gameObject.transform.position,
                                other.gameObject.transform.rotation,
                            out Vector3 pbDir, out float pbDist))
                    {
                        afterPos += pbDir * (pbDist + PUSHBACK_BIAS);
                    }
                }
            }
            else
                finishedPB = true;

            currentPBIteration++;
        }
    }

    //디버깅용 가시화
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 800, 20), $"{transform.position.y}");
    }

    private void OnDrawGizmos()
    {
        if (drawDebugMode == CCDebugDrawMode.None) return;


        switch(drawDebugMode)
        {
            case CCDebugDrawMode.Sweep:
                var initMoveVector = debugMoveVector; //camHolder.TransformVector(new Vector3(_inputX, _inputZ, _inputY).normalized * speed * Time.fixedDeltaTime);

                var constantWishPos = transform.position + playerCollider.center + initMoveVector;
                Gizmos.color = Color.green;
                Gizmos.DrawCube(constantWishPos, playerCollider.size);
                Gizmos.DrawLine(transform.position, constantWishPos);

                int bumpCount = MAX_MOVE_ITERATION;
                int numBump = 0;
                //int i, j = 0;
                var tmpPosition = transform.position;

                Vector3 currentVelocity = debugMoveVector;
                Vector3 currentInternalPos = tmpPosition;
                Vector3 playerExt = playerCollider.size * 0.5f;
                Vector3[] savedPlanes = new Vector3[MAX_CLIP_PLANES];
                int savedPlanesIdx = 0;

                for (numBump = 0; numBump < bumpCount; numBump++)
                {
                    Vector3 currentDir = currentVelocity.normalized;
                    float currentMag = currentVelocity.magnitude;
                    bool foundOverlappedPlane = false;

                    if (useInitalOverlap)
                    {
                        Array.Clear(_overlapCols, 0, _overlapCols.Length);
                        int overlaps = Physics.OverlapBoxNonAlloc(currentInternalPos + playerCollider.center, playerExt, _overlapCols, Quaternion.identity, -1, QueryTriggerInteraction.Ignore);
                        if (overlaps > 0)
                        {
                            for (int i = 0; i < overlaps; i++)
                            {
                                var other = _overlapCols[i];
                                if (other != playerCollider
                                    && !other.isTrigger
                                    && Physics.ComputePenetration(
                                        playerCollider,
                                        currentInternalPos,
                                        Quaternion.identity,
                                        other,
                                        other.transform.position,
                                        other.transform.rotation,
                                        out Vector3 pbDir, out float pbDist)
                                    && Vector3.Dot(pbDir, initMoveVector.normalized) < 0f)
                                {
                                    var prevPos = currentInternalPos;
                                    currentInternalPos += pbDir * (pbDist + PUSHBACK_BIAS);
                                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, pbDir);
                                    SaveClipPlaneNormal(ref savedPlanesIdx, ref savedPlanes, pbDir);
                                    foundOverlappedPlane = true;
                                    Gizmos.color = Color.cyan;
                                    Gizmos.DrawRay(prevPos, pbDir * pbDist);
                                    Gizmos.DrawSphere(prevPos, 0.05f);
                                    Gizmos.DrawWireCube(currentInternalPos, playerCollider.size);
                                    break;
                                }
                            }
                        }
                    }

                    Gizmos.color = Color.yellow;
                    var lastTmpPos = currentInternalPos;
                    int hitCount = BoxSweepTest(currentDir, currentMag + COLLISION_OFFSET, currentInternalPos, playerCollider, -1, out RaycastHit hit);
                    if (!foundOverlappedPlane && hitCount > 0)
                    {
                        //현재 이동력만큼 이동
                        hit.distance = Mathf.Max(0.0f, hit.distance - COLLISION_OFFSET);
                        currentInternalPos += currentDir * hit.distance;
                        currentVelocity = currentDir * Mathf.Max(0.0f, currentMag - hit.distance);

                        SaveClipPlaneNormal(ref savedPlanesIdx, ref savedPlanes, hit.normal);

                        Gizmos.DrawWireCube(currentInternalPos, playerCollider.size);
                        Gizmos.DrawLine(lastTmpPos, currentInternalPos);

                        //else
                        //{
                        //    Gizmos.DrawWireCube(currentInternalPos, playerCollider.size);
                        //    Gizmos.DrawLine(lastTmpPos, currentInternalPos);
                        //    break;
                        //}
                        //tmpPosition += dist * initMoveVector.normalized;
                        Gizmos.DrawWireCube(currentInternalPos, playerCollider.size);
                        Gizmos.DrawLine(lastTmpPos, currentInternalPos);
                        Gizmos.color = Color.red;
                        Gizmos.DrawRay(hit.point, hit.normal * 2.0f);
                        Gizmos.DrawSphere(hit.point, 0.05f);

                        if (savedPlanesIdx < 2)
                        {
                            currentVelocity = Vector3.ProjectOnPlane(currentVelocity, savedPlanes[0]);
                        }
                        else if (savedPlanesIdx == 2)
                        {

                            Vector3 creaseDir = Vector3.Cross(savedPlanes[0], savedPlanes[1]).normalized;
                            currentVelocity = Vector3.Project(currentVelocity, creaseDir);
                            //var tempVel = Vector3.ProjectOnPlane(currentVelocity, savedPlanes[1]);

                            //if (Mathf.Abs(Vector3.Dot(savedPlanes[0], savedPlanes[1])) < 1.0f)
                            //{
                            //    Vector3 creaseDir = Vector3.Cross(savedPlanes[0], savedPlanes[1]).normalized;
                            //    currentVelocity = Vector3.Project(currentVelocity, creaseDir);
                            //}
                            //else
                            //{
                            //    currentVelocity = tempVel;
                            //    savedPlanesIdx = 0;
                            //}
                        }
                        else
                        {
                            currentVelocity = Vector3.zero;
                            break;
                        }
                    }
                    else if(hitCount == 0)
                    {
                        currentInternalPos += currentVelocity;
                        Gizmos.DrawWireCube(currentInternalPos, playerCollider.size);
                        Gizmos.DrawLine(lastTmpPos, currentInternalPos);
                        break;
                    }
                }


                tmpPosition = currentInternalPos;
                Gizmos.color = tmpPosition != constantWishPos ? Color.magenta : Color.green;
                Gizmos.DrawCube(tmpPosition, playerCollider.size);
                break;
            case CCDebugDrawMode.Overlap:
                _savedOverlapPos.Clear();

                Vector3 afterPos = transform.position;

                int currentPBIteration = 0;
                bool finishedPB = false;
                Vector3 halfext = (playerCollider.size * 0.5f) + Vector3.one;

                int setIteration = Mathf.Min(MAX_PUSHBACK_ITERATION, debugOverlapCount);

                while (currentPBIteration < setIteration && !finishedPB)
                {
                    int overlaps = Physics.OverlapBoxNonAlloc(afterPos + playerCollider.center, halfext, _overlapCols, Quaternion.identity, -1, QueryTriggerInteraction.Ignore);

                    if (overlaps > 0)
                    {
                        for (int i = 0; i < overlaps; i++)
                        {
                            var other = _overlapCols[i];
                            if (other != playerCollider
                                && Physics.ComputePenetration(
                                        playerCollider,
                                        afterPos,
                                        Quaternion.identity,
                                        other,
                                        other.gameObject.transform.position,
                                        other.gameObject.transform.rotation,
                                    out Vector3 pbDir, out float pbDist))
                            {
                                afterPos += pbDir * (pbDist + COLLISION_OFFSET);
                                _savedOverlapPos.Add(afterPos);
                            }
                        }
                    }
                    else
                        finishedPB = true;

                    currentPBIteration++;
                }

                float maxColor = _savedOverlapPos.Count + 1.0f;
                for (int i = 0; i < _savedOverlapPos.Count; i++)
                {
                    Gizmos.color = Color.Lerp(Color.yellow, Color.magenta, (i + 1) / maxColor);
                    Gizmos.DrawCube(_savedOverlapPos[i], playerCollider.size);
                }
                Gizmos.color = Color.magenta - new Color { a = 0.5f } ;
                Gizmos.DrawCube(afterPos, playerCollider.size);
                break;
        }
    }
    private List<Vector3> _savedOverlapPos = new List<Vector3>(MAX_PUSHBACK_ITERATION);
}
