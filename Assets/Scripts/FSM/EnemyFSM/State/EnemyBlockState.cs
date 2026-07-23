using UnityEngine;
using Unity.Cinemachine;
public class EnemyBlockState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;
    private CombatAudioPlayer audioPlayer;
    private Transform attacker;
    private Transform transform;
    private float duration = 0.3f;
    private float timer;

    public EnemyBlockState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, Transform transform,CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
        this.transform = transform;
        this.audioPlayer = audioPlayer;
    }

    public void SetAttacker(Transform attackerTransform)
    {
        attacker = attackerTransform;
    }

    public void Rehit()
    {
        if (attacker != null)
        {
            Vector3 dirToAttacker = attacker.position - transform.position;
            dirToAttacker.y = 0;
            if (dirToAttacker != Vector3.zero)
            {
                Vector3 localDir = transform.InverseTransformDirection(dirToAttacker);
                string parryAnim = localDir.x > 0 ? "Parry_R" : "Parry_L";
                animator.CrossFadeInFixedTime(parryAnim, 0f, 0);
                audioPlayer.PlayParrySound();

            }
        }
        else
        {
            animator.CrossFadeInFixedTime("Parry_L", 0f, 0);
            audioPlayer.PlayParrySound();
        }
        enemyFSM.GetComponent<EnemyVitals>()?.GainPostureOnBlock(0f);
        timer = duration;
    }

    public override void OnEnter()
    {
        if (attacker != null)
        {
            Vector3 dirToAttacker = attacker.position - transform.position;
            dirToAttacker.y = 0;
            if (dirToAttacker != Vector3.zero)
            {
                Vector3 localDir = transform.InverseTransformDirection(dirToAttacker);
                string parryAnim = localDir.x > 0 ? "Parry_R" : "Parry_L";
                animator.CrossFadeInFixedTime(parryAnim, 0f, 0);
                audioPlayer.PlayParrySound();
            }
        }
        else
        {
            animator.CrossFadeInFixedTime("Parry_L", 0f, 0);
            audioPlayer.PlayParrySound();
        }
        CinemachineImpulseSource impulseSource = enemyFSM.GetComponent<CinemachineImpulseSource>();
        impulseSource?.GenerateImpulse();
        enemyFSM.GetComponent<EnemyVitals>()?.GainPostureOnBlock(0f);
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
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
