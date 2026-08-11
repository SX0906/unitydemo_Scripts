using UnityEngine;
using System.Collections.Generic;
using GameInput;
using UnityEngine.Playables;

public class TestFSM : MonoBehaviour
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private CharacterController controller;
    private WeaponHitDetector weaponHitDetector;
    private PlayerVitals playerVitals;
    private bool _isRunning;
    private bool _isLockOn;
    private List<Transform> _lockSwitchOrder = new List<Transform>();
    private int _lockSwitchIndex = -1;
    private Transform _lockSwitchBest;
    private float _softLockIdleTimer;
    // 空中蓄力下砸
    private bool isChargingAirToFloor;
    private float airToFloorChargeTime;
    public int currentComboSet { get; set; } = 1;
    public bool JumpSoftEnter { get; set; }
    public float AirAttackEnterY { get; set; }

    // === 反击（BackAttack） ===
    public bool backAttackAvailable;
    public float backAttackTimer;
    public float backAttackDuration = 5f;

    // === 公开属性（供 State 类访问） ===
    public bool IsLockOn => _isLockOn;
    public bool IsRunning => _isRunning;
    public float CurrentMoveSpeedMultiplier { get; set; } = 1f;
    public bool IsJumping => fsm != null && fsm.stateType == StateType.JUMP;
    public Transform LockOnTarget => _lockOnTarget;
    public float VerticalVelocity { get; set; }

    [Header("相机控制")]
    public FSMCamera cameraController;
    public Transform lookRoot;
    public Transform _lockOnTarget;

    [Header("锁定标记")]
    public LockOnTargetMarker lockOnMarker;

    [Header("Timeline")]
    public PlayableDirector powerDirector;
    public LayerMask targetLayers;
    public float lockOnSearchRadius = 10f;
    public float lockOnActivationRange = 6f;
    public float lockOnMaxRange = 10f;

    [Header("软锁定")]
    [Tooltip("软锁定目标与玩家高度差超过该值时不生效")]
    public float softLockMaxHeightDiff = 0.8f;
    [Tooltip("攻击结束后多少秒没有新攻击就自动清除软锁定目标")]
    public float softLockIdleDuration = 1.8f;

    [Header("攻击吸附")]
    public float attackSnapDistance = 2.5f;
    [Range(0f, 180f)] public float attackSnapAngle = 100f;
    public float attackSnapRotateSpeed = 720f;

    [Header("体力消耗")]
    public float dodgeStaminaCost = 15f;
    public float attackUpStaminaCost = 20f;
    public float jumpStaminaCost = 10f;
    public float airAttackStaminaCost = 3f;       // 空中攻击每次消耗体力

    [Header("空中蓄力下砸")]
    public float airToFloorChargeDuration = 1f;   // 长按多久触发
    public float airToFloorStaminaCost = 18f;       // 体力消耗（可调）

    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private float fallEnterDelay = 0.08f;
    private float notGroundedTime;
    
    [Header("顿帧效果")]
    //public float hitStopTimeScale = 0.1f;       // 命中时时间缩放
    //public float hitStopDuration = 0.2f;        // 命中顿帧持续秒数
    public int hitStopFrameCount = 8;           //命中顿帧停止帧数
    public float dodgeSlowTimeScale = 0.5f;     // 闪避攻击时慢动作缩放
    public float dodgeSlowDuration = 0.2f;      // 闪避慢动作持续秒数

    [Header("Power防御")]
    [Range(0f, 360f)] public float powerBlockAngle = 160f;  // Power状态下正面格挡角度

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerControl = new PlayerControl();
        controller = GetComponent<CharacterController>();
        weaponHitDetector = GetComponentInChildren<WeaponHitDetector>();
        playerVitals = GetComponent<PlayerVitals>();

        fsm = new FSMControl();

        // 顿帧管理器宿主注册
        HitStopManager.EnsureHost(this);
        fsm.AddState(StateType.IDlE, new IdleState(animator, fsm));
        fsm.AddState(StateType.MOVE, new MoveState(animator, playerControl, fsm, this));
        Collider weaponCol = GetComponentInChildren<WeaponHitDetector>()?.GetComponent<Collider>();
        fsm.AddState(StateType.ATTACK_01, new AttackState(animator, playerControl, fsm, this, "LAtk-1", weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.ATTACK_02, new AttackState(animator, playerControl, fsm, this, "LAtk-2", weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.LockOn, new LockOnState(animator, fsm, this));
        fsm.AddState(StateType.JUMP, new JumpState(animator, playerControl, fsm, this, controller));
        fsm.AddState(StateType.ATTACK_UP, new AttackUpState(animator, playerControl, fsm, this, controller, weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.AIR_ATTACK, new AirAttackState(animator, playerControl, fsm, this, controller, weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.DODGE, new DodgeState(animator, playerControl, fsm, this,playerVitals));
        fsm.AddState(StateType.HIT, new HitState(fsm, this));
        fsm.AddState(StateType.AIRTOFLOORATTACK, new AirtoFloopAttackState(animator, playerControl, fsm, this, controller));
        fsm.AddState(StateType.POWER, new PowerState(animator, playerControl, fsm, this, playerVitals));
        fsm.AddState(StateType.BACKATTACK, new BackAttackState(animator, playerControl, fsm, this, weaponCol,
            attackSnapDistance, attackSnapAngle, attackSnapRotateSpeed));
        fsm.AddState(StateType.DEATH,new DeathState(animator,fsm,this));
        
        fsm.SetState(StateType.IDlE);

        if (lockOnMarker == null)
        {
            var markerGo = new GameObject("LockOnTargetMarker");
            lockOnMarker = markerGo.AddComponent<LockOnTargetMarker>();
        }

        if (FindFirstObjectByType<PauseMenu>() == null)
        {
            var pauseGo = new GameObject("PauseMenu");
            var pauseMenu = pauseGo.AddComponent<PauseMenu>();
            pauseMenu.Initialize(this);
        }
    }

    private void OnEnable()
    {
        playerControl.Player.Enable();
    }

    private void OnDisable()
    {
        playerControl.Player.Disable();
    }

    private void OnDestroy()
    {
        if (lockOnMarker != null)
            Destroy(lockOnMarker.gameObject);
    }

    private void Update()
    {
        ActorCollisionEscape.Tick(controller);

        // 全局兜底：任何状态下只要脚底有其它角色，忽略碰撞并斜向弹开下落
        if (ActorCollisionEscape.IsOverlappingActor(controller, out int otherLayer))
            ActorCollisionEscape.ResolveOverlap(controller, otherLayer);

        if (fsm.stateType == StateType.HIT || fsm.stateType == StateType.DEATH)
        {
            fsm.OnTick();
            return;
        }

        // Power状态下只跑Tick，不处理任何其他输入
        if (fsm.stateType == StateType.POWER)
        {
            fsm.OnTick();
            return;
        }

        Vector2 moveInput = playerControl.Player.Move.ReadValue<Vector2>();

        FollowGroundOnSlope();

        bool isGrounded = IsGrounded;
        if (isGrounded)
        {
            notGroundedTime = 0f;
        }
        else
        {
            notGroundedTime += Time.deltaTime;
        }

        // 地面移动/待机/锁定状态离开平台后，进入空中下落动画，避免继续播移动或待机动画
        if (!isGrounded && notGroundedTime >= fallEnterDelay &&
            (fsm.stateType == StateType.IDlE ||
             fsm.stateType == StateType.MOVE ||
             fsm.stateType == StateType.LockOn))
        {
            JumpSoftEnter = true;
            fsm.SetState(StateType.JUMP);
        }

        // 反击计时器倒计时
        if (backAttackAvailable)
        {
            backAttackTimer -= Time.deltaTime;
            if (backAttackTimer <= 0f)
            {
                backAttackAvailable = false;
                backAttackTimer = 0f;
            }
        }

        CheckLockOnRange();

        UpdateSoftLockIdleTimer();

        if (playerControl.Player.ComboSet1.WasPressedThisFrame())
        {
            currentComboSet = 1;
            Debug.Log("切换到连招1");
        }
        if (playerControl.Player.ComboSet2.WasPressedThisFrame())
        {
            currentComboSet = 2;
            Debug.Log("切换到连招2");
        }

        // === Power技能：R键，怒气满时释放 ===
        if (playerControl.Player.Power.WasPressedThisFrame())
        {
            if (playerVitals != null && playerVitals.currentRage >= playerVitals.maxRage)
            {
                if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02
                    && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK
                    && fsm.stateType != StateType.AIRTOFLOORATTACK
                    && fsm.stateType != StateType.JUMP)
                {
                    fsm.SetState(StateType.POWER);
                    return;
                }
            }
            else
            {
                Debug.Log("怒气不足，无法释放Power技能");
            }
        }

        // === Attack输入处理 ===
        if (fsm.stateType != StateType.JUMP && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK
            &&fsm.stateType!= StateType.AIRTOFLOORATTACK && fsm.stateType != StateType.BACKATTACK)
        {
            if (playerControl.Player.Attack.WasPressedThisFrame())
            {
                // 反击可用时优先触发反击
                if (backAttackAvailable && backAttackTimer > 0f)
                {
                    fsm.SetState(StateType.BACKATTACK);
                    Debug.Log("触发反击");
                    return;
                }

                if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02
                    && fsm.stateType != StateType.BACKATTACK)
                {
                    switch (currentComboSet)
                    {
                        case 1:
                            fsm.SetState(StateType.ATTACK_01);
                            break;
                        case 2:
                            fsm.SetState(StateType.ATTACK_02);
                            break;
                        default:
                            fsm.SetState(StateType.ATTACK_01);
                            break;
                    }
                }
            }
        }

        // 空中攻击蓄力检测 —— 长按1.5秒触发AirtoFloorAttack，松开则普通AirAttack
        if (!IsGrounded || fsm.stateType == StateType.JUMP || fsm.stateType == StateType.ATTACK_UP || fsm.stateType == StateType.AIR_ATTACK)
        {
            if (playerControl.Player.Attack.WasPressedThisFrame())
            {
                // 反击可用时优先触发反击
                if (backAttackAvailable && backAttackTimer > 0f)
                {
                    fsm.SetState(StateType.BACKATTACK);
                    return;
                }

                isChargingAirToFloor = true;
                airToFloorChargeTime = 0f;
                Debug.Log("时间不足，无法释放下落攻击");
            }

            if (isChargingAirToFloor)
            {
                if (playerControl.Player.Attack.IsPressed())
                {
                    airToFloorChargeTime += Time.deltaTime;
                    if (airToFloorChargeTime >= airToFloorChargeDuration)
                    {
                        Debug.Log("时间满足，是否下落攻击");
                        isChargingAirToFloor = false;
                        if (playerVitals == null || playerVitals.UseStamina(airToFloorStaminaCost))
                        {
                            fsm.SetState(StateType.AIRTOFLOORATTACK);
                        }
                        else
                        {
                            Debug.Log("体力不足，无法使用空中下砸");
                        }
                    }
                }
                else
                {
                    // 提前松开 → 普通空中攻击（消耗体力）
                    isChargingAirToFloor = false;
                    if (playerVitals == null || playerVitals.UseStamina(airAttackStaminaCost))
                    {
                        fsm.SetState(StateType.AIR_ATTACK);
                    }
                    else
                    {
                        // 体力不足 → 进入下落动画
                        JumpSoftEnter = true;
                        fsm.SetState(StateType.JUMP);
                    }
                }
            }
        }
        else
        {
            // 离开跳跃/升龙状态时重置蓄力
            isChargingAirToFloor = false;
        }

        // 闪避 —— 消耗体力 20
        if (playerControl.Player.Dodge.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK
                && fsm.stateType != StateType.AIRTOFLOORATTACK)
            {
                if (playerVitals == null || playerVitals.UseStamina(dodgeStaminaCost))
                {
                    fsm.SetState(StateType.DODGE);
                }
                else
                {
                }
            }
        }

        if (playerControl.Player.Run.WasPressedThisFrame())
        {
            _isRunning = !_isRunning;
        }
        animator.SetFloat("Run", _isRunning ? 1f : 0f);

        if (playerControl.Player.LockOn.WasPressedThisFrame())
        {
            if (_isLockOn)
            {
                ExitLockOn();
            }
            else
            {
                Transform target = _lockOnTarget != null ? _lockOnTarget : FindNearestEnemy();
                if (target != null)
                {
                    float dist = Vector3.Distance(transform.position, target.position);
                    if (dist > lockOnActivationRange)
                    {
                        Debug.Log($"锁定失败：目标距离 {dist:F1}m，超过锁定范围 {lockOnActivationRange}m");
                    }
                    else
                    {
                        _isLockOn = true;
                        _lockOnTarget = target;
                        animator.SetFloat("LockOn", 1f);
                        if (cameraController != null)
                        {
                            cameraController.isLockOn = true;
                            cameraController.lockOnTarget = target;
                        }
                        lockOnMarker?.Show(target);
                        StopSoftLockCameraAssist();
                        ResetLockSwitchState();
                        fsm.SetState(StateType.LockOn);
                    }
                }
            }
        }

        if (playerControl.Player.ChangeLockON.WasPressedThisFrame())
        {
            if (_lockOnTarget == null)
                SelectSoftLockTarget();
            else
                SwitchLockOnTarget();
        }

        // 跳跃 —— 消耗体力 15
        if (playerControl.Player.Jump.WasPressedThisFrame())
        {
            if (fsm.stateType == StateType.IDlE ||
                fsm.stateType == StateType.MOVE ||
                fsm.stateType == StateType.LockOn ||
                fsm.stateType == StateType.ATTACK_01 ||
                fsm.stateType == StateType.ATTACK_02)
            {
                if (IsGrounded)
                {
                    if (playerVitals == null || playerVitals.UseStamina(jumpStaminaCost))
                    {
                        fsm.SetState(StateType.JUMP);
                    }
                    else
                    {
                        Debug.Log("体力不足，无法跳跃");
                    }
                }
            }
        }

        // 升龙 —— 消耗体力 30
        if (playerControl.Player.RAtk.WasPressedThisFrame())
        {
            if (fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIRTOFLOORATTACK)
            {
                if (playerVitals == null || playerVitals.UseStamina(attackUpStaminaCost))
                {
                    fsm.SetState(StateType.ATTACK_UP);
                }
                else
                {
                    Debug.Log("体力不足，无法使用升龙");
                }
            }
        }

        if (fsm.stateType != StateType.ATTACK_01 && fsm.stateType != StateType.ATTACK_02
            && fsm.stateType != StateType.ATTACK_UP && fsm.stateType != StateType.AIR_ATTACK
            && fsm.stateType != StateType.DODGE && fsm.stateType != StateType.JUMP
            && fsm.stateType != StateType.AIRTOFLOORATTACK
            && fsm.stateType != StateType.BACKATTACK)
        {
            if (moveInput == Vector2.zero)
            {
                if (fsm.stateType == StateType.MOVE)
                    fsm.SetState(StateType.IDlE);
            }
            else
            {
                if (fsm.stateType != StateType.MOVE)
                    fsm.SetState(StateType.MOVE);
            }
        }

        fsm.OnTick();
    }

    private void CheckLockOnRange()
    {
        if (_lockOnTarget == null) return;

        var vitals = _lockOnTarget.GetComponentInParent<EnemyVitals>();
        if (vitals != null && vitals.IsDead)
        {
            if (_isLockOn)
            {
                Debug.Log("自动退出锁定：目标已死亡");
                ExitLockOn();
            }
            else
            {
                ClearSoftLockTarget();
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, _lockOnTarget.position);
        if (dist > lockOnMaxRange)
        {
            if (_isLockOn)
            {
                Debug.Log($"自动退出锁定：目标距离 {dist:F1}m，超过最大范围 {lockOnMaxRange}m");
                ExitLockOn();
            }
            else
            {
                ClearSoftLockTarget();
            }
        }
    }

    private void ExitLockOn()
    {
        _isLockOn = false;
        _lockOnTarget = null;
        lockOnMarker?.Hide();
        animator.SetFloat("LockOn", 0f);
        if (cameraController != null)
        {
            cameraController.isLockOn = false;
            cameraController.lockOnTarget = null;
        }
        StopSoftLockCameraAssist();
        ResetLockSwitchState();
        if (fsm.stateType == StateType.LockOn)
            fsm.SetState(StateType.IDlE);
    }

    private Transform FindNearestEnemy()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, lockOnSearchRadius, targetLayers);
        Transform best = null;
        float bestDist = float.MaxValue;
        float bestHealth = float.MaxValue;
        foreach (var col in cols)
        {
            var vitals = col.GetComponentInParent<EnemyVitals>();
            if (vitals == null || vitals.IsDead) continue;

            float d = Vector3.Distance(transform.position, vitals.transform.position);
            float hp = vitals.HealthPercent;
            if (best == null ||
                d < bestDist ||
                (Mathf.Approximately(d, bestDist) && hp < bestHealth))
            {
                bestDist = d;
                bestHealth = hp;
                best = vitals.transform;
            }
        }
        return best;
    }

    private void ClearSoftLockTarget()
    {
        _lockOnTarget = null;
        lockOnMarker?.Hide();
        StopSoftLockCameraAssist();
        ResetLockSwitchState();
    }

    private void SelectSoftLockTarget()
    {
        Transform target = FindBestLockTarget();
        if (target == null) return;

        ResetLockSwitchState();
        _lockOnTarget = target;
        ResetSoftLockIdleTimer();
        lockOnMarker?.Show(target);
    }

    public void StartSoftLockCameraAssist()
    {
        if (_isLockOn || _lockOnTarget == null || !IsValidSoftLockTarget(_lockOnTarget))
        {
            StopSoftLockCameraAssist();
            return;
        }

        cameraController?.StartSoftLockAssist(_lockOnTarget);
    }

    public void StopSoftLockCameraAssist()
    {
        cameraController?.StopSoftLockAssist();
    }

    public void ResetSoftLockIdleTimer()
    {
        _softLockIdleTimer = softLockIdleDuration;
    }

    private void UpdateSoftLockIdleTimer()
    {
        if (_isLockOn || _lockOnTarget == null) return;

        _softLockIdleTimer -= Time.deltaTime;
        if (_softLockIdleTimer <= 0f)
            ClearSoftLockTarget();
    }

    private void SwitchLockOnTarget()
    {
        List<Transform> ordered = CollectLockTargets();
        if (!_isLockOn)
            ordered = FilterLockTargetsByHeight(ordered);

        Camera cam = Camera.main;
        List<Transform> visible = cam != null
            ? FilterVisibleLockTargets(ordered, cam)
            : ordered;
        if (visible.Count > 0)
            ordered = visible;

        if (ordered.Count == 0)
        {
            Debug.Log("没有其它可切换的锁定目标");
            return;
        }

        ordered.Sort((a, b) => CompareLockPriority(a, b));

        bool setChanged = !SameLockSet(_lockSwitchOrder, ordered);
        if (setChanged)
        {
            _lockSwitchOrder = ordered;
            _lockSwitchIndex = -1;
            _lockSwitchBest = ordered[0];
        }
        else if (_lockSwitchBest != ordered[0])
        {
            _lockSwitchBest = ordered[0];
            _lockSwitchIndex = -1;
        }

        int count = ordered.Count;
        int currentIndex = ordered.IndexOf(_lockOnTarget);
        int nextIndex;

        if (currentIndex < 0 || _lockSwitchIndex == -1)
        {
            // 重新锚定：当前不是最优则先切到最优，当前已是最优则切到下一个
            nextIndex = currentIndex == 0 ? 1 % count : 0;
        }
        else
        {
            nextIndex = (_lockSwitchIndex + 1) % count;
        }

        Transform next = ordered[nextIndex];
        _lockSwitchIndex = nextIndex;

        _lockOnTarget = next;
        ResetSoftLockIdleTimer();
        lockOnMarker?.Show(next);

        if (_isLockOn && cameraController != null)
            cameraController.lockOnTarget = next;
    }

    private bool SameLockSet(List<Transform> a, List<Transform> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var t in a)
        {
            if (!b.Contains(t)) return false;
        }
        return true;
    }

    private void ResetLockSwitchState()
    {
        _lockSwitchOrder.Clear();
        _lockSwitchIndex = -1;
        _lockSwitchBest = null;
    }

    private Transform FindBestLockTarget()
    {
        List<Transform> all = CollectLockTargets();
        all = FilterLockTargetsByHeight(all);
        if (all.Count == 0) return null;

        Camera cam = Camera.main;
        List<Transform> visible = cam != null
            ? FilterVisibleLockTargets(all, cam)
            : all;

        if (visible.Count == 0)
            visible = all;

        visible.Sort((a, b) => CompareLockPriority(a, b));
        return visible[0];
    }

    private List<Transform> CollectLockTargets()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, lockOnSearchRadius, targetLayers);
        List<Transform> all = new List<Transform>();

        foreach (var col in cols)
        {
            var vitals = col.GetComponentInParent<EnemyVitals>();
            if (vitals == null || vitals.IsDead) continue;
            if (Vector3.Distance(transform.position, vitals.transform.position) > lockOnMaxRange) continue;
            if (!all.Contains(vitals.transform))
                all.Add(vitals.transform);
        }

        return all;
    }

    private List<Transform> FilterLockTargetsByHeight(List<Transform> targets)
    {
        List<Transform> result = new List<Transform>();

        foreach (var t in targets)
        {
            if (Mathf.Abs(t.position.y - transform.position.y) <= softLockMaxHeightDiff)
                result.Add(t);
        }

        return result;
    }

    private List<Transform> FilterVisibleLockTargets(List<Transform> targets, Camera cam)
    {
        List<Transform> visible = new List<Transform>();

        foreach (var t in targets)
        {
            Vector3 viewport = cam.WorldToViewportPoint(t.position + Vector3.up * 1f);
            if (viewport.z <= 0f ||
                viewport.x < 0f || viewport.x > 1f ||
                viewport.y < 0f || viewport.y > 1f)
                continue;
            visible.Add(t);
        }

        return visible;
    }

    public bool IsValidLockTarget(Transform target)
    {
        if (target == null) return false;

        var vitals = target.GetComponentInParent<EnemyVitals>();
        if (vitals != null && vitals.IsDead) return false;

        return Vector3.Distance(transform.position, target.position) <= lockOnMaxRange;
    }

    public bool IsValidSoftLockTarget(Transform target)
    {
        if (!IsValidLockTarget(target)) return false;
        return Mathf.Abs(target.position.y - transform.position.y) <= softLockMaxHeightDiff;
    }

    public Transform RefreshSoftLockTarget()
    {
        if (_isLockOn && _lockOnTarget != null && IsValidLockTarget(_lockOnTarget))
        {
            ResetSoftLockIdleTimer();
            return _lockOnTarget;
        }

        if (_lockOnTarget != null && IsValidSoftLockTarget(_lockOnTarget))
        {
            ResetSoftLockIdleTimer();
            return _lockOnTarget;
        }

        Transform best = FindBestLockTarget();
        _lockOnTarget = best;

        if (best != null)
        {
            ResetSoftLockIdleTimer();
            lockOnMarker?.Show(best);
        }
        else
            lockOnMarker?.Hide();

        return best;
    }

    private int CompareLockPriority(Transform a, Transform b)
    {
        float distA = Vector3.Distance(transform.position, a.position);
        float distB = Vector3.Distance(transform.position, b.position);
        if (distA < distB) return -1;
        if (distB < distA) return 1;

        var vitalsA = a.GetComponentInParent<EnemyVitals>();
        var vitalsB = b.GetComponentInParent<EnemyVitals>();
        float hpA = vitalsA != null ? vitalsA.HealthPercent : 1f;
        float hpB = vitalsB != null ? vitalsB.HealthPercent : 1f;
        return hpA.CompareTo(hpB);
    }

    private void FollowGroundOnSlope()
    {
        if (controller == null || !controller.enabled) return;
        if (fsm.stateType != StateType.IDlE &&
            fsm.stateType != StateType.MOVE &&
            fsm.stateType != StateType.LockOn) return;

        float maxGap = controller.stepOffset + groundCheckDistance;
        float probeHeight = maxGap + 0.02f;
        Vector3 origin = transform.position + Vector3.up * probeHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeHeight + maxGap, groundLayer))
        {
            float gap = transform.position.y - hit.point.y;
            if (gap > 0.001f)
            {
                controller.Move(Vector3.down * Mathf.Min(gap, maxGap));
            }
        }
    }

    public bool IsGrounded
    {
        get
        {
            if (controller == null) return false;
            Vector3 origin = transform.position + controller.center;
            float radius = controller.radius * 0.8f;
            float checkDist = controller.height / 2f - controller.radius + groundCheckDistance;
            return Physics.SphereCast(origin, radius, Vector3.down, out _, checkDist, groundLayer);
        }
    }

    public Vector2 GetMoveInput()
    {
        return playerControl.Player.Move.ReadValue<Vector2>();
    }

    public void SetInputActive(bool active)
    {
        if (playerControl == null) return;
        if (active)
            playerControl.Player.Enable();
        else
            playerControl.Player.Disable();
    }

    // === 公开方法：受击 ===

    /// <summary>
    /// 玩家受到伤害。外部（敌人攻击检测等）调用此方法。
    /// </summary>
    public void TakeDamage(float damage, Transform attacker)
    {
        if (playerVitals == null || playerVitals.IsDead) return;

        // 按模式调整敌人对玩家的伤害
        damage *= GameModeSettings.EnemyDamageMultiplier;

        // 闪避无敌时 → 触发慢动作 + 不吃伤害 + 获得反击机会
        if (playerVitals.isInvincible && fsm.stateType == StateType.DODGE)
        {
            HitStopManager.Request(dodgeSlowTimeScale, dodgeSlowDuration);
            // 闪避成功 → 获得反击机会
            backAttackAvailable = true;
            backAttackTimer = backAttackDuration;
            return;
        }

        // === Power状态下防御处理 ===
        if (fsm.stateType == StateType.POWER)
        {
            // Power 状态下玩家无敌，直接忽略伤害
            return;
        }

        if (fsm.stateType == StateType.BACKATTACK)
        {
            playerVitals.TakeDamage(damage);
            
            if (playerVitals.IsDead)
            {
                fsm.SetState(StateType.DEATH);
            }
            return;
        }

        playerVitals.TakeDamage(damage);

        if (playerVitals.IsDead)
        {
            fsm.SetState(StateType.DEATH);
            return;
        }

        // 命中顿帧——双方停止
        HitStopManager.FreezeAnimator(animator, hitStopFrameCount);
        if (attacker != null)
        {
            Animator attackerAnim = attacker.GetComponentInChildren<Animator>();
            if (attackerAnim != null)
                HitStopManager.FreezeAnimator(attackerAnim, hitStopFrameCount);
        }

        var hitState = fsm.GetState<HitState>(StateType.HIT);
        if (hitState != null)
        {
            if (fsm.stateType == StateType.HIT)
            {
                hitState.Rehit(attacker);
            }
            else
            {
                hitState.SetHitInfo(attacker);
                fsm.SetState(StateType.HIT);
            }
        }
    }

    // === MoveState 使用的公开方法 ===

    public void ClearMoveAnimation()
    {
        if (animator != null)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("speed", 0f);
        }
    }

    public void ApplyLockOnMove(Vector2 input)
    {
        if (animator != null)
        {
            animator.SetFloat("MoveX", input.x);
            animator.SetFloat("MoveY", input.y);
            float speed = _isRunning ? 2f : 1f;
            animator.SetFloat("speed", speed);
        }
    }

    public void ApplyFreeMove(Vector2 input)
    {
        if (animator != null)
        {
            animator.SetFloat("MoveX", input.x);
            animator.SetFloat("MoveY", input.y);
            float speed = _isRunning ? 2f : 1f;
            animator.SetFloat("speed", speed);
        }
    }

    private System.Collections.IEnumerator HitStopCoroutine(float timeScale, float duration)
    {
        yield break;
    }

    // === JumpEndBehaviour 使用的公开方法 ===

    public void OnJumpLandingFinished()
    {
        fsm?.SetState(_isLockOn ? StateType.LockOn : StateType.IDlE);
    }

    // === AnimationEvent 转发（调用方为根物体 Animator） ===

    public void OnHitWindowOpen(string dirTag)
    {
        weaponHitDetector?.OnHitWindowOpen(dirTag);

        StateBase currentState = fsm.GetCurrentState();
        if (currentState is AttackState attackState)
        {
            attackState.OnComboWindowOpen();
        }
        else if (currentState is AirAttackState airAttackState)
        {
            airAttackState.OnComboWindowOpen();
        }
    }

    public void OnHitWindowClose()
    {
        weaponHitDetector?.OnHitWindowClose();

        StateBase currentState = fsm.GetCurrentState();
        if (currentState is AttackState attackState)
        {
            attackState.OnComboWindowClose();
        }
        else if (currentState is AirAttackState airAttackState)
        {
            airAttackState.OnComboWindowClose();
        }
    }

    /// <summary>任何中断攻击的路径都能安全地立刻关闭武器碰撞盒</summary>
    public void ForceCloseWeaponHitbox()
    {
        weaponHitDetector?.OnHitWindowClose();
    }

    // === Timeline Signal 调用：Power 技能的每次范围伤害 ===
    public void DealPowerDamage()
    {
        Transform t = transform;
        // 伤害球：前方1.8米，半径1.8米
        Vector3 center = t.position + t.forward * 1.8f + Vector3.up * 0.5f;

        Collider[] hits = Physics.OverlapSphere(center, 1.8f, targetLayers);

        foreach (Collider col in hits)
        {
            EnemyFSM enemy = col.GetComponentInParent<EnemyFSM>();
            if (enemy == null) continue;

            Vector3 dir = enemy.transform.position - t.position;
            dir.y = 0;
            if (dir.magnitude < 0.01f) dir = t.forward;
            dir.Normalize();

            enemy.TakeDamage("F", dir, false, t, 25f, true);
        }
    }

    // === Timeline Signal 调用：Power 技能终结伤害（2倍） ===
    public void DealPowerDamageFinal()
    {
        Transform t = transform;
        Vector3 center = t.position + t.forward * 1.8f + Vector3.up * 0.5f;

        Collider[] hits = Physics.OverlapSphere(center, 1.8f, targetLayers);

        foreach (Collider col in hits)
        {
            EnemyFSM enemy = col.GetComponentInParent<EnemyFSM>();
            if (enemy == null) continue;

            Vector3 dir = enemy.transform.position - t.position;
            dir.y = 0;
            if (dir.magnitude < 0.01f) dir = t.forward;
            dir.Normalize();

            enemy.TakeDamage("F", dir, false, t, 50f, true);
        }
    }
}
