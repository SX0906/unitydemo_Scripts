using UnityEngine;
using GameInput;
using Unity.Entities.UniversalDelegates;
public class DodgeState : StateBase
{
    private Animator animator;
    private PlayerControl playerControl;
    private FSMControl fsm;
    private TestFSM testfsm;
    private PlayerVitals playerVitals;
    private bool currentAnimStarted;
    private const string DodgeAnimName = "Dodge";

    public DodgeState(Animator animator, PlayerControl playerControl, FSMControl fsm, TestFSM testfsm,PlayerVitals playerVitals)
    {
        this.animator = animator;
        this.playerControl = playerControl;
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.playerVitals = playerVitals;
    }

    public override void OnEnter()
    {
        testfsm.ForceCloseWeaponHitbox();

        if (playerVitals != null)                    
            playerVitals.isInvincible = true;

        Vector2 input = playerControl.Player.Move.ReadValue<Vector2>();

        // 将原始输入转为镜头相对的世界方向
        Transform lookRoot = testfsm.lookRoot;
        if (lookRoot != null && input != Vector2.zero)
        {
            // 1. 获取镜头的前方和右方（忽略垂直分量）
            Vector3 forward = lookRoot.forward;
            Vector3 right = lookRoot.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // 2. 计算镜头相对的世界方向
            Vector3 worldDir = forward * input.y + right * input.x;
            if (worldDir.sqrMagnitude > 1f)
                worldDir.Normalize();

            // 3. 将世界方向转换到角色的本地空间
            Vector3 localDir = testfsm.transform.InverseTransformDirection(worldDir);

            animator.SetFloat("DodgeX", localDir.x);
            animator.SetFloat("DodgeY", localDir.z);
        }
        else
        {
            // 没有镜头或输入为零时，fallback 到原始输入
            animator.SetFloat("DodgeX", input.x);
            animator.SetFloat("DodgeY", input.y);
        }

        animator.Play(DodgeAnimName);
        currentAnimStarted = false;
    }
    public override void OnUpdate()
    {
        if (!currentAnimStarted)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(DodgeAnimName) && state.normalizedTime > 0f)
                currentAnimStarted = true;
            else
                return;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.IsName(DodgeAnimName) && currentState.normalizedTime >= 0.75f)
        {
            Vector2 move = testfsm.GetMoveInput();
            if (move == Vector2.zero)
                fsm.SetState(testfsm.IsLockOn ? StateType.LockOn : StateType.IDlE);
            else
                fsm.SetState(StateType.MOVE);
        }
    }
    public override void OnExit()
    {
        if (playerVitals != null)                     
            playerVitals.isInvincible = false;
        currentAnimStarted = false;
    }


}
