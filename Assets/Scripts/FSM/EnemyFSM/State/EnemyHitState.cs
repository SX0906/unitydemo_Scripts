using UnityEngine;

public class EnemyHitState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private Transform transform;
    private string hitDirTag = "F";
    private float duration = 0.45f;
    private float timer;
    private Transform attacker;
    private CombatAudioPlayer audioPlayer;

    public EnemyHitState(Animator animator, EnemyFSMControl fsm, Transform transform, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.transform = transform;
        this.audioPlayer = audioPlayer;
    }

    public void SetHitDirectionTag(string tag)
    {
        hitDirTag = tag;
    }

    public void SetAttacker(Transform attackerTransform)
    {
        attacker = attackerTransform;
    }

    public void Rehit()
    {
        string animName = "Hit_" + hitDirTag;
        animator.CrossFadeInFixedTime(animName, 0f, 0);
        audioPlayer?.PlayHitSound();
        timer = duration;
    }

    public override void OnEnter()
    {
        string animName = "Hit_" + hitDirTag;
        animator.CrossFadeInFixedTime(animName, 0f, 0);
        audioPlayer?.PlayHitSound();
        timer = duration;
    }

    public override void OnUpdate()
    {
        if (attacker != null)
        {
            Vector3 dirToAttacker = attacker.position - transform.position;
            dirToAttacker.y = 0;
            if (dirToAttacker != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToAttacker);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 8f * Time.deltaTime);
            }
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public override void OnExit()
    {
        
        attacker = null;
    }
}
