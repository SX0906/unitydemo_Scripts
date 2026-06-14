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
            testfsm.JumpSoftEnter = false;
            jumpHeld = false;          // 软进入不处理短跳逻辑
            isLanding = false;
            // 直接播放下落循环动画，跳过 Jump Start
            animator.Play("Jump_Loop", 0, 0f);
        }
        else
        {
            // 正常从地面起跳
            animator.SetTrigger("Jump");
            testfsm.VerticalVelocity = jumpForce;
            jumpHeld = true;
            isLanding = false;
        }
    }

    public override void OnUpdate()
    {

        if(jumpHeld && !playerControl.Player.Jump.IsPressed())
        {
            if(testfsm.VerticalVelocity > 0)
            {
                testfsm.VerticalVelocity *= 0.5f; // 如果跳跃键被松开且角色仍在上升，减少跳跃力以实现短跳
            }
            jumpHeld = false; // 跳跃键被松开
        }

        if(!isLanding)
        {
            testfsm.VerticalVelocity += gravity * Time.deltaTime; // 应用重力

            // 实际移动角色
            Vector3 verticalMove = new Vector3(0, testfsm.VerticalVelocity * Time.deltaTime, 0);
            controller.Move(verticalMove);
            if(testfsm.IsGrounded && testfsm.VerticalVelocity <= 0f)
            {
                testfsm.VerticalVelocity = 0f; // 确保在地面上时垂直速度为0
                isLanding = true; // 标记为正在落地
                animator.SetTrigger("Land"); // 触发落地动画
            }
        }


    }

    public override void OnExit()
    {
        //testfsm.VerticalVelocity = 0; // 离开跳跃状态时重置垂直速度
        isLanding = false; // 重置落地标志
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("Land");
    }

    public bool IsLanding => isLanding; 


}
