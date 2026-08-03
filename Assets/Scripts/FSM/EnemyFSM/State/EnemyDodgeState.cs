using UnityEngine;

public class EnemyDodgeState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private EnemyFSM enemyFSM;
    private Transform attacker;
    private Transform transform;
    private CharacterController controller;
    private float duration = 0.8f;
    private float timer;
    private Vector3 dodgeDirection;
    private EnemyFSM_test enemyFSM_test;


    public EnemyDodgeState(Animator animator, EnemyFSMControl fsm,
        EnemyFSM enemyFSM, Transform transform, CharacterController controller)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM = enemyFSM;
        this.transform = transform;
        this.controller = controller;
    }

    public EnemyDodgeState(Animator animator, EnemyFSMControl fsm, EnemyFSM_test enemyFSM_test, Transform transform, CharacterController controller)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.enemyFSM_test = enemyFSM_test;
        this.transform = transform;
        this.controller = controller;
    }


    public void SetAttacker(Transform attackerTransform)
    {
        attacker = attackerTransform;
    }

    public void Rehit()
    {
        animator.CrossFadeInFixedTime("Dodge", 0f, 0);
        timer = duration;

        if (attacker != null)
        {
            Vector3 awayFromAttacker = transform.position - attacker.position;
            awayFromAttacker.y = 0;
            dodgeDirection = awayFromAttacker.normalized;

            Vector3 faceAttacker = attacker.position - transform.position;
            faceAttacker.y = 0;
            if (faceAttacker != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(faceAttacker);
        }
        else
        {
            dodgeDirection = -transform.forward;
        }
    }

    public override void OnEnter()
    {
        animator.CrossFadeInFixedTime("Dodge", 0f, 0);
        timer = duration;

        if (attacker != null)
        {
            Vector3 awayFromAttacker = transform.position - attacker.position;
            awayFromAttacker.y = 0;
            dodgeDirection = awayFromAttacker.normalized;

            Vector3 faceAttacker = attacker.position - transform.position;
            faceAttacker.y = 0;
            if (faceAttacker != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(faceAttacker);
        }
        else
        {
            dodgeDirection = -transform.forward;
        }
    }

    public override void OnUpdate()
    {
        if (controller != null && controller.enabled)
        {
            float dodgeSpeed = 0.8f;
            controller.Move(dodgeDirection * dodgeSpeed * Time.deltaTime);
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
