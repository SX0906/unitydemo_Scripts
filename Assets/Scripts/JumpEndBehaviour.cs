using UnityEngine;

public class JumpEndBehaviour : StateMachineBehaviour
{
    // 当 JumpEnd 动画播放结束时自动调用
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        TestFSM player = animator.GetComponent<TestFSM>();
        if (player != null && player.IsJumping)
        {
            player.OnJumpLandingFinished();
        }
    }
}
