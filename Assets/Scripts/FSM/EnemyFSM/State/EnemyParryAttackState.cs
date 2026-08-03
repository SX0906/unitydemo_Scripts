using UnityEngine;
using Unity.Cinemachine;
public class EnemyParryAttackState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;
    private Transform transform;
    private CombatAudioPlayer audioPlayer;
    private Transform attacker;
    private float duration = 1.8f;
    private float timer;
    private float parrySoundDelay = 0.1f;
    private bool parrySoundPlayed;

    [Header("弹刀伤害")]
    public float parryDamage = 15f;
    private EnemyFSM_test enemyFSM_test;


    public EnemyParryAttackState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, Transform transform, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
        this.transform = transform;
        this.audioPlayer = audioPlayer;
    }

    public EnemyParryAttackState(Animator animator, EnemyFSMControl fsm, EnemyFSM_test enemyFSM_test, Transform transform, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM_test = enemyFSM_test;
        this.transform = transform;
        this.audioPlayer = audioPlayer;
    }


    public void SetAttacker(Transform attackerTransform)
    {
        attacker = attackerTransform;
    }

    public override void OnEnter()
    {
        // 面朝攻击者
        if (attacker != null)
        {
            Vector3 dirToAttacker = attacker.position - transform.position;
            dirToAttacker.y = 0;
            if (dirToAttacker != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(dirToAttacker);
            }
        }

        // 播放弹刀动画
        animator.CrossFadeInFixedTime("Parry_Attack", 0f, 0);
        
        CinemachineImpulseSource impulseSource = enemyFSM.GetComponent<CinemachineImpulseSource>();
        impulseSource?.GenerateImpulse();

        // 攒架势
        enemyFSM.GetComponent<EnemyVitals>()?.GainPostureOnBlock(0f);

        // 预设伤害值（实际命中窗口由动画帧事件开启）
        enemyFSM.SetEnemyWeaponDamage(parryDamage);

        parrySoundPlayed = false;
        timer = duration;
    }

    public override void OnUpdate()
    {
        // 面朝攻击者
        if (attacker != null)
        {
            Vector3 dirToAttacker = attacker.position - transform.position;
            dirToAttacker.y = 0;
            if (dirToAttacker != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToAttacker);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }

        // 延迟 0.1 秒播放格挡音效
        float elapsed = duration - timer;
        if (!parrySoundPlayed && elapsed >= parrySoundDelay)
        {
            audioPlayer?.PlayParrySound();
            parrySoundPlayed = true;
        }

        timer -= Time.deltaTime;

        // 动画播完 → 回到待机
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Parry_Attack") && stateInfo.normalizedTime >= 0.9f)
        {
            fsm.SetState(EnemyStateType.IDLE);
            return;
        }

        // 兜底计时
        if (timer <= 0f)
        {
            fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public override void OnExit()
    {
        enemyFSM.OnEnemyHitWindowClose();
        attacker = null;
    }
}
