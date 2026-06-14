using UnityEngine;

public class PlayerSkillRootMotionRelay : MonoBehaviour
{
    private PlayerSkillPlayer skillPlayer;
    private Animator animator;

    public void Initialize(PlayerSkillPlayer owner, Animator sourceAnimator)
    {
        skillPlayer = owner;
        animator = sourceAnimator;
    }

    private void OnAnimatorMove()
    {
        if (skillPlayer != null && animator != null)
            skillPlayer.ApplyRootMotion(animator);
    }
}
