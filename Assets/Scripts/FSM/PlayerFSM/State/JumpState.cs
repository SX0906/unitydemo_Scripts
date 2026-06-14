using UnityEngine;
using GameInput;
public class JumpState : StateBase
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private CharacterController controller;
    private float jumpForce = 8f;
    private float gravity = -20f;
    private bool jumpHeld;
    private bool isLanding;
    private bool allowAirMove;

    private float airMoveSpeed = 4f;
    private float airRunSpeed = 6f;
    private float rotationVelocity;

    public JumpState(Animator animator, PlayerControl playerControl, FSMControl fsm, TestFSM testfsm, CharacterController controller)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.controller = controller;
    }

    public override void OnEnter()
    {
        if (testfsm.JumpSoftEnter)
        {
            testfsm.VerticalVelocity = 0f;
            testfsm.JumpSoftEnter = false;
            jumpHeld = false;
            isLanding = false;
            allowAirMove = false;
            animator.Play("Jump_Loop", 0, 0f);
        }
        else
        {
            animator.SetTrigger("Jump");
            testfsm.VerticalVelocity = jumpForce;
            jumpHeld = true;
            isLanding = false;
            allowAirMove = true;
        }
    }

    public override void OnUpdate()
    {
        if(jumpHeld && !playerControl.Player.Jump.IsPressed())
        {
            if(testfsm.VerticalVelocity > 0)
            {
                testfsm.VerticalVelocity *= 0.5f;
            }
            jumpHeld = false;
        }

        if(!isLanding)
        {
            testfsm.VerticalVelocity += gravity * Time.deltaTime;

            Vector3 horizontalMove = Vector3.zero;
            if (allowAirMove)
                horizontalMove = GetAirMovement();

            Vector3 move = new Vector3(horizontalMove.x, testfsm.VerticalVelocity, horizontalMove.z) * Time.deltaTime;
            controller.Move(move);

            if(testfsm.IsGrounded && testfsm.VerticalVelocity <= 0f)
            {
                testfsm.VerticalVelocity = 0f;
                isLanding = true;
                animator.SetTrigger("Land");
            }
        }
    }

    private Vector3 GetAirMovement()
    {
        Vector2 moveInput = testfsm.GetMoveInput();
        if (moveInput == Vector2.zero)
            return Vector3.zero;

        float speedMultiplier = testfsm.IsRunning ? airRunSpeed : airMoveSpeed;

        Transform lookRoot = testfsm.lookRoot;
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
            forward = testfsm.transform.forward;
            right = testfsm.transform.right;
        }

        Vector3 moveDir = forward * moveInput.y + right * moveInput.x;
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        if (testfsm.IsLockOn)
        {
            Transform lockTarget = testfsm.LockOnTarget;
            if (lockTarget != null)
            {
                Vector3 toTarget = lockTarget.position - testfsm.transform.position;
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    testfsm.transform.rotation = Quaternion.Slerp(
                        testfsm.transform.rotation,
                        Quaternion.LookRotation(toTarget.normalized),
                        12f * Time.deltaTime
                    );
                }
            }
        }
        else
        {
            float targetRotation = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float rotation = Mathf.SmoothDampAngle(testfsm.transform.eulerAngles.y, targetRotation, ref rotationVelocity, 0.08f);
            testfsm.transform.rotation = Quaternion.Euler(0, rotation, 0);
        }

        return moveDir * speedMultiplier;
    }

    public override void OnExit()
    {
        isLanding = false;
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("Land");
    }

    public bool IsLanding => isLanding;
}