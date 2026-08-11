using UnityEngine;

public class EnemyHitState : EnemyStateBase
{
    private Animator animator;
    private EnemyFSMControl fsm;
    private Transform transform;
    private CharacterController controller;
    private string hitDirTag = "F";
    private float duration = 0.45f;
    private float timer;
    private Transform attacker;
    private CombatAudioPlayer audioPlayer;

    // ===== 击退参数 =====
    private Vector3 knockbackVelocity;
    private float knockbackTimer;
    private bool hasKnockback;
    private float knockbackDecel = 8f;
    private float verticalVelocity;
    private bool isLaunched;
    private float gravity = -20f;
    private float launchFallbackTimer;
    private float launchMaxFallbackTime = 1f;

    public EnemyHitState(Animator animator, EnemyFSMControl fsm, Transform transform,
        CharacterController controller, CombatAudioPlayer audioPlayer)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.transform = transform;
        this.controller = controller;
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

    public void StartKnockback(Vector3 direction, float force, float duration, float upForce = 0f)
    {
        if (force <= 0f || duration <= 0f)
        {
            hasKnockback = false;
            knockbackVelocity = Vector3.zero;
            verticalVelocity = 0f;
            isLaunched = false;
            return;
        }

        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.magnitude < 0.01f) dir = -transform.forward;

        knockbackVelocity = dir.normalized * force;
        knockbackTimer = duration;
        hasKnockback = true;

        verticalVelocity = upForce;
        isLaunched = upForce > 0f;
        launchFallbackTimer = launchMaxFallbackTime;

        // 根据击退方向选择受击动画方向
        Vector3 localDir = transform.InverseTransformDirection(dir.normalized);
        float forward = Vector3.Dot(localDir, Vector3.forward);
        float right = Vector3.Dot(localDir, Vector3.right);
        hitDirTag = Mathf.Abs(forward) > Mathf.Abs(right)
            ? (forward > 0f ? "F" : "B")
            : (right > 0f ? "R" : "L");

        animator.CrossFadeInFixedTime("Hit_" + hitDirTag, 0f, 0);
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
        hasKnockback = false;
        knockbackVelocity = Vector3.zero;
        knockbackTimer = 0f;
        verticalVelocity = 0f;
        isLaunched = false;
        launchFallbackTimer = 0f;
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

        Vector3 totalMove = Vector3.zero;

        if (hasKnockback && knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
            totalMove += knockbackVelocity * Time.deltaTime;

            knockbackVelocity = Vector3.MoveTowards(
                knockbackVelocity, Vector3.zero, knockbackDecel * Time.deltaTime);

            if (knockbackTimer <= 0f) hasKnockback = false;
        }

        if (isLaunched)
        {
            verticalVelocity += gravity * Time.deltaTime;
            totalMove += Vector3.up * verticalVelocity * Time.deltaTime;
        }

        if (totalMove != Vector3.zero)
        {
            if (controller != null && controller.enabled)
                controller.Move(totalMove);
            else
                transform.position += totalMove;
        }

        if (isLaunched)
        {
            if (controller != null && controller.enabled)
            {
                if (controller.isGrounded && verticalVelocity <= 0f)
                {
                    verticalVelocity = 0f;
                    isLaunched = false;
                }
            }
            else
            {
                launchFallbackTimer -= Time.deltaTime;
                if (launchFallbackTimer <= 0f)
                {
                    verticalVelocity = 0f;
                    isLaunched = false;
                }
            }
        }

        timer -= Time.deltaTime;
        if (timer <= 0f && !isLaunched)
        {
            fsm.SetState(EnemyStateType.IDLE);
        }
    }

    public override void OnExit()
    {
        attacker = null;
        hasKnockback = false;
        knockbackVelocity = Vector3.zero;
        knockbackTimer = 0f;
        verticalVelocity = 0f;
        isLaunched = false;
        launchFallbackTimer = 0f;
    }
}
