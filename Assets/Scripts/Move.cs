using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Move : MonoBehaviour
{
    public Transform camHolder;
    public BoxCollider playerCollider;
    public Rigidbody playerRigidbody;
    public bool showDebugMovement;
    public Vector3 debugMoveVector;
    public float debugMoveDeltaTime = 0.02f;
    public Vector3 Velocity => _velocity;

    private RaycastHit[] _moveHits = new RaycastHit[5];
    private Vector3 _internalPosition = Vector3.zero;
    private Vector3 _lastTickPostion = Vector3.zero;
    private Collider[] _overlapCols = new Collider[MAX_OVERLAP_COLS];
    private Vector3 _velocity;

    public Text velocityText;

    public bool AutoJumping;
    public float speed;

    const float SWEEP_TEST_EPSILON = 0.002f;
    const float COLLISION_OFFSET = 0.01f;
    const int MAX_OVERLAP_COLS = 3;
    const int MAX_MOVE_ITERATION = 4;
    const int MAX_CLIP_PLANES = 5;
    const float MIN_PUSHBACK_DIST = 0.0005f;
    const float NON_JUMP_VELOCITY = 2.667f;

    private Vector3 _scaledMoveInput;
    private float _inputX, _inputY, _inputZ;
    private bool _jumpButtonPressed;
    private float _fallVelocity;
    private float _surfaceFriction = 1.0f;

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


        //자리 잡기
        CategorizePosition();
        if (!ReferenceEquals(_ground,null))
            StayOnGround();

    }

    private void Update()
    {
        velocityText.text = Mathf.RoundToInt(Velocity.magnitude * 52.5f).ToString();

        if (!_jumpButtonPressed)
            _jumpButtonPressed = AutoJumping ? Input.GetKey(KeyCode.Space) : Input.GetKeyDown(KeyCode.Space);

        _inputX = (Input.GetKey(KeyCode.A) ? -1 : 0) + (Input.GetKey(KeyCode.D) ? 1 : 0);
        _inputY = (Input.GetKey(KeyCode.S) ? -1 : 0) + (Input.GetKey(KeyCode.W) ? 1 : 0);
        _inputZ = 0f;//(Input.GetKey(KeyCode.Q) ? -1 : 0) + (Input.GetKey(KeyCode.E) ? 1 : 0);
    }

    float _lastTickTime = 0f;

    public bool interpolation;
    // Update is called once per frame
    void FixedUpdate()
    {
        _lastTickPostion = _internalPosition;

        PlayerMove();
        //TryPlayerMove(Quaternion.Euler(0, camHolder.eulerAngles.y, 0) * (isLerpMotion ? currentVelocity : targetVelocity), Time.deltaTime);

        _lastTickTime = Time.time;

        if(!interpolation)
            transform.position = _internalPosition;
    }

    private void LateUpdate()
    {
        if (!interpolation)
            return;

        float interpolationFactor = (Time.time - _lastTickTime) / (Time.fixedDeltaTime);
        transform.position = Vector3.Lerp(_lastTickPostion,_internalPosition, interpolationFactor);
    }


    void CategorizePosition()
    {
        float zvel = _velocity.y;
        //bool bMovingUp = zvel > 0.0f;
        bool bMobingUpRapidly = zvel > NON_JUMP_VELOCITY;

        _surfaceFriction = 1.0f;

        if(bMobingUpRapidly)
        {
            SetGroundEntity(new RaycastHit());
        }
        else
        {
            var start = _internalPosition;
            var end = _internalPosition + (Vector3.down * 0.0381f);
            var dest = end - start;
            var pm = BoxTrace(dest.normalized, dest.magnitude + COLLISION_OFFSET, _internalPosition, playerCollider, false);
            pm.distance = Mathf.Max(0.0f, pm.distance - COLLISION_OFFSET);
            //var fraction = pm.distance / dest.magnitude;

            if (ReferenceEquals(pm.transform, null) || pm.normal.y < 0.7f)
            {
                SetGroundEntity(new RaycastHit());
                if (_velocity.y > 0.0f)
                    _surfaceFriction = 0.25f;
            }
            else 
            {
                SetGroundEntity(pm);
            }
        }
    }

    void SetGroundEntity(RaycastHit hit)
    {
        GameObject newGround = hit.transform != null ? hit.transform.gameObject : null;
        GameObject oldGround = _ground;

        //Vector3 vecBaseVelocity = BaseVelocity;

        if(!ReferenceEquals(newGround, null) && ReferenceEquals(oldGround, null))
        {
            //vecBaseVelocity -= newGround.GetComponent<xxx>().Velocity;
            //vecBaseVelocity.y =  newGround.GetComponent<xxx>().Velocity.y;
        }

        else if(!ReferenceEquals(oldGround, null) && ReferenceEquals(newGround, null))
        {
            //vecBaseVelocity += oldGround.GetComponent<xxx>().Velocity;
            //vecBaseVelocity.y =  oldGround.GetComponent<xxx>().Velocity.y;
        }

        //BaseVelocity = vecBaseVelocity;

        //수영관련 코드
        _ground = newGround;

        if(!ReferenceEquals(newGround, null))
        {
            _surfaceFriction = Mathf.Min(1.0f,_surfaceFriction * 1.25f);

            _velocity.y = 0f;
        }
    }

    GameObject _ground;

    void CheckVelocity()
    {
        var sv_maxvelocity = 66.675f;
        var correctVel = _velocity;
        for(int i = 0; i < 3; i++)
        {
            if (float.IsNaN(_velocity[i]))
                correctVel[i] = 0.0f;

            if (correctVel[i] > sv_maxvelocity)
                correctVel[i] = sv_maxvelocity;
            else if (correctVel[i] < -sv_maxvelocity)
                correctVel[i] = -sv_maxvelocity;
        }

        _velocity = correctVel;
    }

    void CheckParameters()
    {
        var sv_maxspeed = 4.7625f;
        float spd;

        var cl_forwardspeed = 8.5725f;
        var cl_sidespeed = 8.5725f;

        _inputX *= cl_forwardspeed;
        _inputY *= cl_sidespeed;
        //_inputZ *= cl_forwardspeed;

        spd = (_inputX * _inputX) +
            (_inputY * _inputY);// + 
            //(_inputZ * _inputZ);

        if(spd != 0.0f && spd > sv_maxspeed * sv_maxspeed)
        {
            float fRatio = sv_maxspeed / Mathf.Sqrt(spd);
            _inputX *= fRatio;
            _inputY *= fRatio;
            _inputZ *= fRatio;
        }
    }

    void PlayerMove()
    {
        CheckParameters();

        if(_velocity.y > 4.7625f)
        {
            SetGroundEntity(new RaycastHit()); //null 셋팅
        }

        if (ReferenceEquals(_ground, null))
        {
            _fallVelocity = -_velocity.y;
        }

        //Duck(); //앉기 관련 코드

        FullWalkMove();
    }

    void FullWalkMove()
    {
        var sv_gravity = 15.24f;
        _velocity -= Vector3.up * (sv_gravity * 0.5f * Time.deltaTime);
        CheckVelocity();
        //BaseVelocity = new Vector3(BaseVelocity.x,0f,BaseVelocity.z);
        //속도 NaN 체크

        if(_jumpButtonPressed)
        {
            CheckJumpButton();
        }

        if(!ReferenceEquals(_ground, null))
        {
            _velocity.y = 0f;
            Friction();
        }

        CheckVelocity();

        if (!ReferenceEquals(_ground, null))
        {
            WalkMove();
        }
        else
        {
            AirMove();
        }

        CategorizePosition();

        CheckVelocity();

        _velocity -= Vector3.up * (sv_gravity * 0.5f * Time.deltaTime);
        CheckVelocity();

        if (!ReferenceEquals(_ground,null))
        {
            _velocity.y = 0f;
        }

        //CheckFalling();
    }

    void AirMove()
    {
        Vector3 wishvel = default;
        float fmove, smove;
        Vector3 wishdir;
        float wishspeed;

        Vector3 forward = camHolder.transform.GetChild(0).forward;
        Vector3 right = camHolder.transform.GetChild(0).right;
        
        forward = new Vector3(forward.x,0f,forward.z);
        right = new Vector3(right.x,0f,right.z);

        forward.Normalize();
        right.Normalize();

        fmove = _inputY;
        smove = _inputX;

        for (int i = 0; i < 3; i++)
            wishvel[i] = (forward[i] * fmove) + (right[i] * smove);

        wishvel.y = 0.0f;

        wishdir = wishvel;
        wishspeed = wishdir.magnitude;
        wishdir.Normalize();

        var sv_maxspeed = 4.7625f;
        if (wishspeed != 0.0f && (wishspeed > sv_maxspeed))
        {
            wishvel *= sv_maxspeed / wishspeed;
            wishspeed = sv_maxspeed;
        }

        var sv_airaccelerate = 12.0f;

        AirAccelerate(ref wishdir, wishspeed, sv_airaccelerate);

        TryPlayerMove(ref _velocity, Time.deltaTime);
    }

    void WalkMove()
    {
        Vector3 wishvel;
        float spd;
        float fmove, smove;
        Vector3 wishdir;
        float wishspeed;

        Vector3 dest;

        GameObject oldGround = _ground;


        fmove = _inputY;
        smove = _inputX;

        wishvel = Quaternion.Euler(0, camHolder.eulerAngles.y, 0) * new Vector3(smove, 0f, fmove);
        wishdir = wishvel.normalized;

        wishspeed = wishvel.magnitude;

        var sv_maxspeed = 4.7625f;
        if (wishspeed != 0.0f && wishspeed > sv_maxspeed)
        {
            wishvel *= sv_maxspeed / wishspeed;
            wishspeed = sv_maxspeed;
        }

        var sv_accelerate = 5.6f; //계수인지 유닛인지 몰?루 <- 계수로 추정
        _velocity.y = 0f;
        Accelerate(ref wishdir, wishspeed, sv_accelerate);
        _velocity.y = 0f;

        //Velocity += BaseVelocity;

        spd = _velocity.magnitude;

        if (spd < 0.01905f)
        {
            _velocity = Vector3.zero;
            //Velocity -= Basevelocity;
            return;
        }

        dest = new Vector3(_velocity.x, 0f, _velocity.z) * Time.deltaTime;
        //mv->m_outWishVel += wishdir * wishspeed;


        var pm = BoxTrace(dest.normalized, dest.magnitude + COLLISION_OFFSET, _internalPosition, playerCollider);
        pm.distance = Mathf.Max(0.0f, pm.distance - COLLISION_OFFSET);

        var fraction = pm.distance / dest.magnitude;

        if(fraction == 1.0f)
        {
            _internalPosition += dest;

            StayOnGround();
            return;
        }

        if(ReferenceEquals(oldGround,null))
        {
            return;
        }

        StepMove();

        //Velocity -= BaseVelocity;

        StayOnGround();
    }

    void StepMove()
    {
        var initPos = _internalPosition;
        var initVel = _velocity;

        TryPlayerMove(ref _velocity, Time.deltaTime);

        var downPos = _internalPosition;
        var downVel = _velocity;

        _internalPosition = initPos;
        _velocity = initVel;

        var sv_stepsize = 0.3429f;
        var dest = Vector3.up * sv_stepsize;
        var pm = BoxTrace(dest.normalized, dest.magnitude + COLLISION_OFFSET, _internalPosition, playerCollider);
        pm.distance = Mathf.Max(0.0f, pm.distance - COLLISION_OFFSET);

        var fraction = pm.distance / dest.magnitude;

        if(fraction > 0.0f)
        {
            _internalPosition += pm.distance * dest.normalized;
        }

        TryPlayerMove(ref _velocity, Time.deltaTime);

        dest = Vector3.down * sv_stepsize;
        pm = BoxTrace(dest.normalized, dest.magnitude + COLLISION_OFFSET, _internalPosition, playerCollider);
        pm.distance = Mathf.Max(0.0f, pm.distance - COLLISION_OFFSET);

        fraction = pm.distance / dest.magnitude;

        if(!ReferenceEquals(pm.transform,null)
            && pm.normal.y < 0.7f)
        {
            _internalPosition = downPos;
            _velocity = downVel;
            return;
        }

        if(fraction > 0.0f)
        {
            _internalPosition += dest.normalized * (pm.distance);
        }

        var updist = (_internalPosition.x - initPos.x) * (_internalPosition.x - initPos.x) + (_internalPosition.z - initPos.z) * (_internalPosition.z - initPos.z);
        var downdist = (downPos.x - initPos.x) * (downPos.x - initPos.x) + (downPos.y - initPos.y) * (downPos.y - initPos.y);

        if(downdist > updist)
        {
            _internalPosition = downPos;
            _velocity = downVel;
        }
        else
        {
            _velocity.y = downVel.y;
        }
    }

    void StayOnGround()
    {
        var sv_stepsize = 0.3429f;
        var start = _internalPosition + (Vector3.up * 0.0381f);
        var end = _internalPosition + (Vector3.down * sv_stepsize);
        var dest = start - _internalPosition;
        var pm = BoxTrace(dest.normalized, dest.magnitude + COLLISION_OFFSET, _internalPosition, playerCollider);
        pm.distance = Mathf.Max(0.0f, pm.distance - COLLISION_OFFSET);

        start = _internalPosition + dest.normalized * pm.distance;

        dest = end - start;

        pm = BoxTrace(dest.normalized, dest.magnitude + COLLISION_OFFSET, start, playerCollider);
        pm.distance = Mathf.Max(0.0f, pm.distance - COLLISION_OFFSET);

        var fraction = pm.distance / dest.magnitude;

        if(fraction > 0.0f 
            && fraction < 1.0f
            && pm.normal.y > 0.7f)
        {
            _internalPosition = start + dest.normalized * (pm.distance);
        }
        var pushBacks = BoxPushBack(_internalPosition, playerCollider, out var pbVec);

        if (pushBacks > 0)
        {
            if (pbVec.sqrMagnitude > 0)
            {
                _internalPosition += pbVec.normalized * (pbVec.magnitude + MIN_PUSHBACK_DIST);
            }
        }
    }

    void Accelerate(ref Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed, accelspeed, currentspeed;

        currentspeed = Vector3.Dot(_velocity, wishdir);

        addspeed = wishspeed - currentspeed;

        if (addspeed <= 0)
            return;

        accelspeed = accel * Time.deltaTime * wishspeed * _surfaceFriction;

        if(accelspeed >addspeed)
            accelspeed = addspeed;

        _velocity += wishdir * accelspeed;
    }

    void AirAccelerate(ref Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed, accelspeed, currentspeed;
        float wishspd;

        wishspd = wishspeed;

        if (wishspd > 0.5715f) //GetAirSpeedCap() => 30.0f 하드코딩
            wishspd = 0.5715f;

        currentspeed = Vector3.Dot(_velocity, wishdir);

        addspeed = wishspd - currentspeed;

        if (addspeed <= 0f)
            return;

        accelspeed = accel * wishspeed * Time.deltaTime * _surfaceFriction;

        if(accelspeed > addspeed)
            accelspeed = addspeed;

        _velocity += accelspeed * wishdir;
    }

    bool CheckJumpButton()
    {
        if (_jumpButtonPressed) _jumpButtonPressed = false;
        else return false;
        if (ReferenceEquals(_ground, null)) return false;

        //if (ducking) return false;

        SetGroundEntity(new RaycastHit());

        var flMul = 5.111651396564518f;
        var sv_gravity = 15.24f;

        _velocity.y = flMul;
        _velocity -= Vector3.up * (sv_gravity * 0.5f * Time.deltaTime);
        //duckjump 시간 설정

        return true;
    }

    void Friction()
    {
        float speed, newspeed, control;
        float friction;
        float drop;

        var sv_friction = 4f; //계수인지 유닛인지 몰?루 -> 계수로 추정됨, 유닛인 경우 0.01905f 곱하기
        var sv_stopspeed = 1.905f;

        speed = _velocity.magnitude;
        if (speed < 0.001905f)
        {
            return;
        }
        drop = 0;

        if (!ReferenceEquals(_ground, null))
        {
            friction = sv_friction * _surfaceFriction; // * _ground.GetComponent<xxx>().friction;

            control = (speed < sv_stopspeed) ? sv_stopspeed : speed;

            drop += control * friction * Time.deltaTime;
        }

        newspeed = speed - drop;
        if (newspeed < 0.0f)
            newspeed = 0.0f;

        if(newspeed != speed)
        {
            newspeed /= speed;

            _velocity *= newspeed;
        }

        //outWishVel -= (0.01905f - newspeed) * Velocity;
    }

    /// <summary>
    /// 충돌 미끌어짐 알고리즘으로 플레이어를 움직임
    /// </summary>
    /// <param name="moveVector"></param>
    /// <param name="deltaTime"></param>
    int TryPlayerMove(ref Vector3 moveVector, float deltaTime)
    {
        int blocked = 0;
        int numPlanes = 0;
        int numBumps;
        int bumpCount = MAX_MOVE_ITERATION;

        float allFraction = 0;
        float timeleft = deltaTime;

        Vector3 originVector = moveVector;
        Vector3 primalVector = moveVector;
        Vector3 newVector = Vector3.zero;
        Vector3[] planes = new Vector3[MAX_CLIP_PLANES];
        RaycastHit currentHit;

        int pi, pj;

        //푸쉬 백(오버랩핑)
        int pushBacks = BoxPushBack(_internalPosition, playerCollider, out Vector3 pbVec);
        //bool hasPushBackPlane = false;

        if (pushBacks > 0)
        {
            if (pbVec.sqrMagnitude > 0)
            {
                _internalPosition += pbVec.normalized * (pbVec.magnitude + MIN_PUSHBACK_DIST);

                moveVector -= Vector3.Project(moveVector, -pbVec.normalized);
                planes[numPlanes++] = pbVec.normalized;
                //hasPushBackPlane = true;
            }
        }

        //스윕
        for (numBumps = 0; numBumps < bumpCount; numBumps++)
        {
            if (moveVector.magnitude == 0.0f)
                break;

            var nextVec = moveVector * timeleft;
            currentHit = BoxTrace(nextVec.normalized, nextVec.magnitude + COLLISION_OFFSET, _internalPosition, playerCollider);
            currentHit.distance = Mathf.Max(0.0f, currentHit.distance - COLLISION_OFFSET);
            
            var fraction = currentHit.distance / nextVec.magnitude;
            allFraction += fraction;

            if (fraction > 0.0f)
            {
                _internalPosition += currentHit.distance * nextVec.normalized;
                originVector = moveVector;
                //numPlanes = 0;
                //numPlanes = hasPushBackPlane ? numPlanes : 0;
                //hasPushBackPlane = false;
            }

            if (fraction == 1.0f)
                break;

            if (currentHit.normal.y > 0.7f)
            {
                blocked |= 1;    //바닥
            }

            if (currentHit.normal.y == 0.0f)
            {
                blocked |= 2;    //벽 혹은 계단
            }

            timeleft -= timeleft * fraction;

            if (numPlanes >= MAX_CLIP_PLANES)
            {
                moveVector = Vector3.zero;
                break;
            }

            planes[numPlanes++] = currentHit.normal;

            //var sv_bounce = 0f;

            //if (numPlanes == 1
            //    //&& GetMoveType() == MOVETYPE_WALK
            //    && ReferenceEquals(_ground, null))
            //{
            //    Debug.Log("나시행함");
            //    var q = 0;
            //    for (pi = 0; pi < numPlanes; pi++)
            //    {
            //        if (planes[pi].y > 0.7f)
            //        {
            //            q = 1;
            //            ClipVelocity(originVector, planes[pi], out newVector);
            //            originVector = newVector;
            //        }
            //        else
            //        {
            //            q = 2;
            //            ClipVelocity(originVector, planes[pi], out newVector,1.0f + sv_bounce * (1 - _surfaceFriction));
            //        }
            //    }
            //    Debug.Log(q);
            //    moveVector = newVector;
            //    originVector = newVector;
            //}
            //else
            {
                for (pi = 0; pi < numPlanes; pi++)
                {
                    //Debug.Log("클립함" + numPlanes);
                    ClipVelocity(originVector, planes[pi], out moveVector);
                    for (pj = 0; pj < numPlanes; pj++)
                        if (pj != pi)
                        {
                            if (Vector3.Dot(moveVector, planes[pj]) < 0)
                                break;
                        }
                    if (pj == numPlanes)
                        break;
                }
            }
            

            if (pi != numPlanes)
            {
                ;
            }
            else
            {
                //if (numPlanes != 2)
                //{
                //    //moveVector = Vector3.zero;
                //    break;
                //}
                var creaseDir = Vector3.Cross(planes[0], planes[1]);
                creaseDir.Normalize();
                moveVector = creaseDir * Vector3.Dot(creaseDir, moveVector);
            }

            if (Vector3.Dot(moveVector, primalVector) <= 0)
            {
                moveVector = Vector3.zero;
                break;
            }
        }

        //푸쉬 백(오버랩핑)
        pushBacks = BoxPushBack(_internalPosition, playerCollider, out pbVec);

        if (pushBacks > 0)
        {
            if (pbVec.sqrMagnitude > 0)
            {
                _internalPosition += pbVec.normalized * (pbVec.magnitude + MIN_PUSHBACK_DIST);
            }
        }

        if (allFraction == 0)
        {
            moveVector = Vector3.zero;
        }

        return blocked;
    }

    /// <summary>
    /// 박스캐스트 후 가장 가까운 히트의 정보와 박스캐스트의 모든 히트 수를 반환한다.
    /// </summary>
    /// <param name="wishDir">방향</param>
    /// <param name="wishDist">거리</param>
    /// <param name="initialPos">초기 위치</param>
    /// <param name="box">콜라이더</param>
    /// <param name="closestHit">가장 가까운 히트</param>
    /// <returns>유효한 충돌 수</returns>
    int BoxSweepTest(Vector3 wishDir, float wishDist, Vector3 initialPos, BoxCollider box,out RaycastHit closestHit)
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
                if (_moveHits[i].collider == box
                    || _moveHits[i].collider.isTrigger
                    || _moveHits[i].distance < 0f)
                {
                    validHitCount--;
                    continue;
                }

                closestDistInLoop = _moveHits[i].distance;
                closestHitInLoop = _moveHits[i];
            }
        }

        closestHit = closestHitInLoop;
        closestHit.distance = Mathf.Max(0.0f, closestHit.distance);

        return validHitCount;
    }

    /// <summary>
    /// 퀘이크 엔진 TraceBBox용 메소드
    /// </summary>
    /// <param name="wishDir">방향</param>
    /// <param name="wishDist">거리</param>
    /// <param name="pos">위치</param>
    /// <param name="box">콜라이더</param>
    /// <returns></returns>
    RaycastHit BoxTrace(Vector3 wishDir, float wishDist, Vector3 pos, BoxCollider box, bool filtZeroDist = true)
    {
        RaycastHit closestHit = new RaycastHit();
        closestHit.distance = wishDist;

        var hitCount = Physics.BoxCastNonAlloc(
            pos + box.center + (wishDir * -SWEEP_TEST_EPSILON),
            box.size * 0.5f, wishDir,
            _moveHits, Quaternion.identity,
            wishDist + SWEEP_TEST_EPSILON,
            -1,
            QueryTriggerInteraction.Ignore);

        var closestDistInLoop = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            _moveHits[i].distance -= SWEEP_TEST_EPSILON;
            if (_moveHits[i].distance < closestDistInLoop)
            {
                if(_moveHits[i].collider == box
                   || _moveHits[i].collider.isTrigger
                   || (filtZeroDist && _moveHits[i].distance <= 0f))
                {
                    continue;
                }

                closestDistInLoop = _moveHits[i].distance;
                closestHit = _moveHits[i];
            }
        }

        return closestHit;
    }

    RaycastHit BoxTraceNonFilter(Vector3 wishDir, float wishDist, Vector3 pos, BoxCollider box)
    {
        RaycastHit closestHit = new RaycastHit();
        closestHit.distance = wishDist;

        var hitCount = Physics.BoxCastNonAlloc(
            pos + box.center + (wishDir * -SWEEP_TEST_EPSILON),
            box.size * 0.5f, wishDir,
            _moveHits, Quaternion.identity,
            wishDist + SWEEP_TEST_EPSILON,
            -1,
            QueryTriggerInteraction.Ignore);

        var closestDistInLoop = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            _moveHits[i].distance -= SWEEP_TEST_EPSILON;
            if (_moveHits[i].distance < closestDistInLoop)
            {
                if (_moveHits[i].collider == box
                   || _moveHits[i].collider.isTrigger)
                {
                    continue;
                }

                closestDistInLoop = _moveHits[i].distance;
                closestHit = _moveHits[i];
            }
        }

        return closestHit;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="initialPos"></param>
    /// <param name="box"></param>
    /// <param name="pbVec"></param>
    /// <param name="skinWidth"></param>
    /// <returns>유효한 겹침상태 수</returns>
    int BoxPushBack(Vector3 initialPos, BoxCollider box, out Vector3 pbVec, float skinWidth = 0f)
    {
        pbVec = Vector3.zero;

        var savedVectors = new Vector3[MAX_OVERLAP_COLS];

        var overlaps = Physics.OverlapBoxNonAlloc(initialPos + box.center, (box.size * 0.5f) + Vector3.one * skinWidth, _overlapCols, Quaternion.identity, -1, QueryTriggerInteraction.Ignore);

        var validOverlaps = overlaps;

        if (overlaps > 0)
        {
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
                        out var pbDir, out var pbDist))
                {
                    savedVectors[elmentCount++] = pbDir * pbDist;
                }
                else
                {
                    validOverlaps--;
                }
            }

            var farthestDist = -1f;
            
            for (int i = 0; i < elmentCount; i++)
            {
                if (savedVectors[i].sqrMagnitude > farthestDist)
                {
                    farthestDist = savedVectors[i].sqrMagnitude;
                    pbVec = savedVectors[i];
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
            //if (outputVelocity[i] > -0.1f && outputVelocity[i] < 0.1f)
            //{
            //    outputVelocity[i] = 0;
            //}
        }

        float adjust = Vector3.Dot(outputVelocity, normal);
        if (adjust < 0.0f)
        {
            outputVelocity -= normal * adjust;
        }

        return blocked;
    }

    //디버깅용 가시화
    private void OnDrawGizmos()
    {
        if (!showDebugMovement) return;

        var initMoveVector = debugMoveVector; //camHolder.TransformVector(new Vector3(_inputX, _inputZ, _inputY).normalized * speed * Time.fixedDeltaTime);


        var tmpPosition = transform.position;

        int blocked = 0;
        int numPlanes = 0;
        int numBumps = 0;
        int bumpCount = MAX_MOVE_ITERATION;

        float timeleft = debugMoveDeltaTime;

        Vector3 originVector = initMoveVector;
        Vector3[] planes = new Vector3[MAX_CLIP_PLANES];
        RaycastHit currentHit = new RaycastHit();

        int pi, pj;

        int pushBacks = BoxPushBack(tmpPosition, playerCollider, out Vector3 pbVec);
        bool isPush = false;

        if (pushBacks > 0)
        {
            if (pbVec.sqrMagnitude > 0)
            {
                tmpPosition += pbVec.normalized * (pbVec.magnitude + MIN_PUSHBACK_DIST);

                initMoveVector -= Vector3.Project(initMoveVector, -pbVec.normalized);
                //planes[numPlanes++] = pbVec.normalized;
                isPush = true;
            }
        }
        if (isPush)
            Gizmos.DrawWireCube(tmpPosition + playerCollider.center, playerCollider.size);

        var constantWishPos = tmpPosition + (initMoveVector * debugMoveDeltaTime);



        for (numBumps = 0; numBumps < bumpCount; numBumps++)
        {
            Gizmos.color = Color.yellow + new Color(-numBumps / 4f, -numBumps / 4f, 0f);

            if (initMoveVector.magnitude == 0.0f)
                break;

            var nextVec = initMoveVector * timeleft;
            var lastTmpPos = tmpPosition;
            currentHit = BoxTrace(nextVec.normalized, nextVec.magnitude + COLLISION_OFFSET, tmpPosition, playerCollider);
            currentHit.distance = Mathf.Max(0.0f, currentHit.distance - COLLISION_OFFSET);
            var fraction = currentHit.distance / nextVec.magnitude;

            if (fraction > 0.0f)
            {
                tmpPosition += currentHit.distance * nextVec.normalized;
                Gizmos.DrawWireCube(tmpPosition + playerCollider.center, playerCollider.size);
                Gizmos.DrawLine(lastTmpPos + playerCollider.center, tmpPosition + playerCollider.center);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(currentHit.point, currentHit.normal);
                Gizmos.DrawSphere(currentHit.point, 0.05f);
                numPlanes = 0;
                //numPlanes = isPush ? numPlanes : 0;
                //isPush = false;
            }

            if (fraction == 1.0f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(tmpPosition + playerCollider.center, playerCollider.size);
                Gizmos.DrawLine(lastTmpPos + playerCollider.center, tmpPosition + playerCollider.center);
                break;
            }
               

            if (currentHit.normal.y > 0.7f)
            {
                blocked |= 1;    //바닥
            }

            if (currentHit.normal.y == 0.0f)
            {
                blocked |= 2;    //벽 혹은 계단
            }

            timeleft -= timeleft * fraction;

            if (numPlanes >= MAX_CLIP_PLANES)
            {
                //Velocity = Vector3.zero;
                break;
            }

            planes[numPlanes++] = currentHit.normal;

            for (pi = 0; pi < numPlanes; pi++)
            {
                ClipVelocity(originVector, planes[pi], out initMoveVector);
                for (pj = 0; pj < numPlanes; pj++)
                    if (pj != pi)
                    {
                        if (Vector3.Dot(initMoveVector, planes[pj]) < 0)
                            break;
                    }
                if (pj == numPlanes)
                    break;
            }

            if (pi != numPlanes)
            {

            }
            else
            {
                //if (numPlanes != 2)
                //{
                //    //Velocity = Vector3.zero;
                //    break;
                //}
                var creaseDir = Vector3.Cross(planes[0], planes[1]);
                initMoveVector = creaseDir * Vector3.Dot(creaseDir, initMoveVector);
            }

            if (Vector3.Dot(initMoveVector, originVector) <= 0)
            {
                //Velocity = Vector3.zero;
                break;
            }
        }

        Gizmos.color = tmpPosition != constantWishPos ? Color.magenta : Color.green;

        Gizmos.color -= new Color(0, 0, 0, 0.3f);

        
        Gizmos.DrawCube(tmpPosition + playerCollider.center, playerCollider.size - Vector3.one * 0.002f);

        Gizmos.color = tmpPosition != constantWishPos ? Color.yellow : Color.green;

        Gizmos.DrawCube(constantWishPos + playerCollider.center, playerCollider.size);
        Gizmos.DrawLine(transform.position + playerCollider.center, constantWishPos + playerCollider.center);

    }

}
