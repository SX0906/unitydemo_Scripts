using UnityEngine;

public class HitState : StateBase
{
    private Animator animator;
    private FSMControl fsm;
    private TestFSM testfsm;

    private string hitDirTag = "F";
    private float duration = 0.5f;
    private float timer;
    private Transform attacker;

    public HitState(Animator animator, FSMControl fsm, TestFSM testfsm)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.testfsm = testfsm;
    }

    public void SetHitInfo(string dirTag, Transform attackerTransform)
    {
        hitDirTag = dirTag;
        attacker = attackerTransform;
    }

    public void Rehit(string dirTag, Transform attackerTransform)
    {
        SetHitInfo(dirTag, attackerTransform);
        string animName = "Hit_" + hitDirTag;
        animator.CrossFadeInFixedTime(animName, 0f, 0);
        timer = duration;
    }

    public override void OnEnter()
    {
        string animName = "Hit_" + hitDirTag;
        animator.CrossFadeInFixedTime(animName, 0f, 0);
        timer = duration;
    }

    public override void OnUpdate()
    {
        // 面向攻击者
        if (attacker != null)
        {
            Vector3 dirToAttacker = attacker.position - testfsm.transform.position;
            dirToAttacker.y = 0;
            if (dirToAttacker != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToAttacker);
                testfsm.transform.rotation = Quaternion.Slerp(
                    testfsm.transform.rotation, targetRot, 8f * Time.deltaTime);
            }
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
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
        attacker = null;
    }
}
