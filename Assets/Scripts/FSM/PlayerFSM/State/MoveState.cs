using UnityEngine;
using GameInput;

public class MoveState : StateBase
{
    private FSMControl fsm;
    private Animator animator;
    private PlayerControl playerControl;
    private TestFSM testFsm;
    private float rotationVelocity;

    private float walkSpeed = 1f;
    private float runSpeed = 1.5f;
    private TestFSM_test testFSM_test;


    public MoveState(Animator animator, PlayerControl playerControl, FSMControl fsm, TestFSM testFsm)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testFsm = testFsm;
    }

    public MoveState(Animator animator, PlayerControl playerControl, FSMControl fsm, TestFSM_test testFSM_test)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testFSM_test = testFSM_test;
    }


    public override void OnEnter()
    {
        testFsm.ClearMoveAnimation();
    }

    public override void OnUpdate()
    {
        Vector2 moveInput = testFsm.GetMoveInput();

        if (moveInput == Vector2.zero)
        {
            fsm.SetState(testFsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
            return;
        }

        // 根据奔跑状态设置 root motion 速度倍率
        float speedMultiplier = testFsm.IsRunning ? runSpeed : walkSpeed;
        testFsm.CurrentMoveSpeedMultiplier = speedMultiplier;

        Transform lookRoot = testFsm.lookRoot;
        Vector3 forward, right;
        if (lookRoot != null)
        {
            forward = lookRoot.forward;
            right = lookRoot.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
        }
        else
        {
            forward = testFsm.transform.forward;
            right = testFsm.transform.right;
        }

        Vector3 moveDir = forward * moveInput.y + right * moveInput.x;
        if (moveDir.sqrMagnitude > 1)
            moveDir.Normalize();

        if (testFsm.IsLockOn)
        {
            animator.SetFloat("LockOn", 1f);
            Transform lockTarget = testFsm.LockOnTarget;
            if (lockTarget != null)
            {
                Vector3 toTarget = lockTarget.position - testFsm.transform.position;
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    testFsm.transform.rotation = Quaternion.Slerp(
                        testFsm.transform.rotation,
                        Quaternion.LookRotation(toTarget.normalized),
                        12f * Time.deltaTime
                    );
                }
            }
        }
        else
        {
            animator.SetFloat("LockOn", 0f);
            float targetRotation = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float rotation = Mathf.SmoothDampAngle(testFsm.transform.eulerAngles.y, targetRotation, ref rotationVelocity, 0.08f);
            testFsm.transform.rotation = Quaternion.Euler(0, rotation, 0);
        }
        if (testFsm.IsLockOn)
            testFsm.ApplyLockOnMove(moveInput);
        else
            testFsm.ApplyFreeMove(moveInput);
    }

    public override void OnExit()
    {
        testFsm.CurrentMoveSpeedMultiplier = 1f;
        testFsm.ClearMoveAnimation();
    }
}