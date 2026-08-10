using UnityEngine;

public enum LockMovePhase
{
    React,
    LockMove
}

public class EnemyLockMoveState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private Transform transform;
    private CharacterController controller;
    private EnemyFSM enemyFSM;

    private LockMovePhase phase;
    private bool reactIsRetreat;
    private float dodgeDuration = 0.8f;
    private float reactTimer;
    private Transform attacker;

    private float retreatTargetDistance = 4f;
    private bool wasTooClose;

    private float lockRotateSpeed = 12f;
    private float minDistance = 5f;
    private float minDistanceBuffer = 0.5f;

    private float gravity = -20f;
    private float verticalVelocity;

    private float strafeDirTimer;
    private float strafeDirChangeInterval = 2f;
    private float strafeSign = 1f;

    private Vector3 smoothWorldMoveDir = Vector3.zero;
    private float moveDirSmoothSpeed = 8f;

    public EnemyLockMoveState(Animator animator, EnemyFSMControl fsm,
        Transform transform, CharacterController controller, EnemyFSM enemyFSM)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.transform = transform;
        this.controller = controller;
        this.enemyFSM = enemyFSM;
    }

    public override void OnEnter()
    {
        wasTooClose = false;
        EnterLockMovePhase();
    }

    private void StartReact()
    {
        phase = LockMovePhase.React;
        reactIsRetreat = Random.value < 0.6f;

        if (reactIsRetreat)
        {
            animator.CrossFade("BaseMotion", 0.05f);
            animator.SetFloat("LockOn", 1f);
            animator.SetFloat("Run", 1f);
            animator.SetFloat("speed", 1.2f);
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -1f);
        }
        else
        {
            StartDodge();
        }
    }

    private void StartDodge()
    {
        animator.CrossFadeInFixedTime("Dodge", 0f, 0);
        reactTimer = dodgeDuration;

        attacker = enemyFSM.targetPlayer;
        if (attacker != null)
        {
            Vector3 faceAttacker = attacker.position - transform.position;
            faceAttacker.y = 0;
            if (faceAttacker != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(faceAttacker);
        }
    }

    private void EnterLockMovePhase()
    {
        phase = LockMovePhase.LockMove;
        animator.CrossFade("BaseMotion", 0.05f);
        animator.SetFloat("LockOn", 1f);
        animator.SetFloat("Run", 1f);
        animator.SetFloat("speed", 1.2f);
        strafeDirTimer = 0f;
        strafeSign = Random.value < 0.5f ? 1f : -1f;
        smoothWorldMoveDir = Vector3.zero;
    }

    public override void OnUpdate()
    {
        if (!enemyFSM.IsGrounded)
            return;

        Transform target = enemyFSM.targetPlayer;
        if (target == null)
        {
            fsm.SetState(EnemyStateType.IDLE);
            return;
        }

        if (enemyFSM.IsGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        if (phase == LockMovePhase.React)
        {
            if (reactIsRetreat)
                UpdateRetreat(target);
            else
                UpdateDodge(target);
        }
        else
        {
            UpdateLockMove(target);
        }

        controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

    private void UpdateRetreat(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0;
        float distance = toTarget.magnitude;

        FaceTarget(toTarget);

        if (distance >= retreatTargetDistance)
            EnterLockMovePhase();
    }

    private void UpdateDodge(Transform target)
    {
        if (target != null)
        {
            Vector3 faceAttacker = target.position - transform.position;
            faceAttacker.y = 0;
            if (faceAttacker != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(faceAttacker);
        }

        reactTimer -= Time.deltaTime;
        if (reactTimer <= 0f)
            EnterLockMovePhase();
    }

    private void UpdateLockMove(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0;

        if (toTarget.sqrMagnitude < 0.001f)
            return;

        float distance = toTarget.magnitude;
        Vector3 dirToTarget = toTarget.normalized;

        FaceTarget(toTarget);

        bool tooClose = distance < minDistance - minDistanceBuffer;
        if (tooClose && !wasTooClose)
        {
            wasTooClose = true;
            StartReact();
            return;
        }
        wasTooClose = tooClose;

        Vector3 targetWorldMoveDir;
        if (tooClose)
        {
            targetWorldMoveDir = -dirToTarget;
        }
        else if (distance > minDistance + minDistanceBuffer)
        {
            targetWorldMoveDir = dirToTarget;
        }
        else
        {
            Vector3 right = Vector3.Cross(Vector3.up, dirToTarget).normalized;
            strafeDirTimer += Time.deltaTime;
            if (strafeDirTimer >= strafeDirChangeInterval)
            {
                strafeDirTimer = 0f;
                strafeSign = Random.value < 0.5f ? 1f : -1f;
            }
            targetWorldMoveDir = right * strafeSign;
        }

        smoothWorldMoveDir = Vector3.Lerp(smoothWorldMoveDir, targetWorldMoveDir, moveDirSmoothSpeed * Time.deltaTime);

        Vector3 localMove = transform.InverseTransformDirection(smoothWorldMoveDir);
        animator.SetFloat("MoveX", localMove.x);
        animator.SetFloat("MoveY", localMove.z);
    }

    private void FaceTarget(Vector3 toTarget)
    {
        if (toTarget.sqrMagnitude < 0.0001f)
            return;
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, lockRotateSpeed * Time.deltaTime);
    }

    public override void OnExit()
    {
        animator.SetFloat("LockOn", 0f);
        animator.SetFloat("Run", 0f);
        animator.SetFloat("speed", 0f);
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
        attacker = null;
    }
}
