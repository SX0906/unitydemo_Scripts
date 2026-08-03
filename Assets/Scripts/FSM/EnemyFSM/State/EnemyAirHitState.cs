using UnityEngine;

public class EnemyAirHitState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private Transform transform;
    private CharacterController controller;
    private EnemyFSM enemyFSM;
    private CombatAudioPlayer audioPlayer;
    private Vector3 hitDirection;
    
    private float airTimer;
    [SerializeField] private float maxAirTime = 1.2f;
    private float gravity = -20f;
    private float fallSpeed;
    private float noHitTimer = 1f;
    private bool isFalling;
    private float hoverStartY;
    
    private Transform playerTransform;
    private float yOffsetFromPlayer;
    private bool isFollowingPlayer;
    private EnemyFSM_test enemyFSM_test;


    public EnemyAirHitState(Animator animator, EnemyFSMControl fsm,
        Transform transform, CharacterController controller, EnemyFSM enemyFSM, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.transform = transform;
        this.controller = controller;
        this.enemyFSM = enemyFSM;
        this.audioPlayer = audioPlayer;
    }

    public EnemyAirHitState(Animator animator, EnemyFSMControl fsm, Transform transform, CharacterController controller, EnemyFSM_test enemyFSM_test, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.transform = transform;
        this.controller = controller;
        this.enemyFSM_test = enemyFSM_test;
        this.audioPlayer = audioPlayer;
    }


    public void SetHitDirection(Vector3 worldDir)
    {
        hitDirection = worldDir;
    }

    public void RefreshAirTime()
    {
        airTimer = maxAirTime;
        isFalling = false;
        fallSpeed = 0f;
        noHitTimer = 1f;
    }

    public void StartFollowPlayerRootMotion(Transform player)
    {
        playerTransform = player;
        yOffsetFromPlayer = transform.position.y - player.position.y;
        isFollowingPlayer = true;
    }

    public void Rehit()
    {
        Vector3 localDir = transform.InverseTransformDirection(hitDirection.normalized);
        float forward = Vector3.Dot(localDir, Vector3.forward);
        float right = Vector3.Dot(localDir, Vector3.right);

        string trigger;
        if (Mathf.Abs(forward) > Mathf.Abs(right))
            trigger = forward > 0 ? "Hit_Air_F" : "Hit_Air_B";
        else
            trigger = right > 0 ? "Hit_Air_R" : "Hit_Air_L";

        animator.CrossFadeInFixedTime(trigger, 0.02f, 0);
        audioPlayer.PlayHitSound();
        noHitTimer = 1f;
    }

    public override void OnEnter()
    {
        Vector3 localDir = transform.InverseTransformDirection(hitDirection.normalized);
        float forward = Vector3.Dot(localDir, Vector3.forward);
        float right = Vector3.Dot(localDir, Vector3.right);

        string trigger;
        if (Mathf.Abs(forward) > Mathf.Abs(right))
            trigger = forward > 0 ? "Hit_Air_F" : "Hit_Air_B";
        else
            trigger = right > 0 ? "Hit_Air_R" : "Hit_Air_L";

        animator.CrossFadeInFixedTime(trigger, 0.02f, 0);
        audioPlayer.PlayHitSound();

        airTimer = maxAirTime;
        isFalling = false;
        fallSpeed = 0f;
        noHitTimer = 1f;
    }

    public override void OnUpdate()
    {
        // 浮空超过1秒未被追击 → 坠地
        if (!isFollowingPlayer)
        {
            noHitTimer -= Time.deltaTime;
            if (noHitTimer <= 0f)
            {
                fsm.SetState(EnemyStateType.FALLTOFLOOR);
                return;
            }
        }

        float deltaY = 0f;

        if (isFollowingPlayer && playerTransform != null)
        {
            Animator playerAnim = playerTransform.GetComponentInChildren<Animator>();
            bool playerStillInUpAttack = false;
            if (playerAnim != null)
            {
                AnimatorStateInfo state = playerAnim.GetCurrentAnimatorStateInfo(0);
                playerStillInUpAttack = state.IsName("Attack_Up_Floor_To_Air") 
                                      || state.IsName("Attack_Up_Air_To_Air");
            }

            if (!playerStillInUpAttack)
            {
                isFollowingPlayer = false;
                hoverStartY = transform.position.y;
                animator.applyRootMotion = false;
                Debug.Log($"[AirHit] 退出升龙跟随，悬停Y={hoverStartY:F2}，airTimer={airTimer:F2}");
            }
            else
            {
                float targetY = playerTransform.position.y + yOffsetFromPlayer;
                deltaY = targetY - transform.position.y;
            }
        }

        if (!isFollowingPlayer)
        {
            airTimer -= Time.deltaTime;

            if (airTimer > 0)
            {
                float drift = transform.position.y - hoverStartY;
                deltaY = -drift;
                fallSpeed = 0f;
            }
            else
            {
                fallSpeed += gravity * Time.deltaTime;
                fallSpeed =Mathf.Max(fallSpeed,-30f);
                deltaY = fallSpeed * Time.deltaTime;
                isFalling = true;
            }
        }

        if (controller != null && controller.enabled)
        {
            controller.Move(new Vector3(0, deltaY, 0));
        }
        else
        {
            transform.position += new Vector3(0, deltaY, 0);
        }

        if (isFalling && enemyFSM != null && enemyFSM.IsGrounded)
        {
            fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public override void OnExit()
    {
        isFollowingPlayer = false;
        playerTransform = null;
        fallSpeed = 0f;
        animator.applyRootMotion = true;
    }
}
