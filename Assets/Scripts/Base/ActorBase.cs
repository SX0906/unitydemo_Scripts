using UnityEngine;
using UnityEngine.AI;

public interface IHitReceiver
{
    void ReceiveHit(string hitStateName, float damage);
}

[System.Serializable]
public class ActorReferenceGroup
{
    public Animator animator;
    public CharacterController characterController;
    public NavMeshAgent navAgent;
}

[System.Serializable]
public class ActorMoveConfig
{
    public float walkSpeed = 2.5f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;
    public float gravity = -20f;
    public float groundedForce = -2f;
}

[System.Serializable]
public class ActorRuntimeState
{
    public bool isGrounded;
    public bool isRunning;
    public bool isAttacking;
    public bool isHit;
    public bool isGuarding;
}

public class ActorBase : MonoBehaviour
{
    [Header("基础引用")]
    public ActorReferenceGroup refs = new ActorReferenceGroup();

    [Header("基础移动参数")]
    public ActorMoveConfig moveConfig = new ActorMoveConfig();

    [Header("基础运行状态")]
    public ActorRuntimeState actorState = new ActorRuntimeState();

    protected Vector3 verticalVelocity;

    public Animator Animator => refs.animator;
    public CharacterController CharacterController => refs.characterController;
    public NavMeshAgent NavAgent => refs.navAgent;

    public Vector3 VerticalVelocity
    {
        get => verticalVelocity;
        set => verticalVelocity = value;
    }

    protected virtual void Reset()
    {
        AutoFindReferences();
    }

    protected virtual void Awake()
    {
        AutoFindReferences();
    }

    protected virtual void AutoFindReferences()
    {
        if (!refs.animator)
            refs.animator = GetComponentInChildren<Animator>();

        if (!refs.characterController)
            refs.characterController = GetComponent<CharacterController>();

        if (!refs.navAgent)
            refs.navAgent = GetComponent<NavMeshAgent>();
    }

    public virtual bool CanUseNavAgent()
    {
        return refs.navAgent && refs.navAgent.enabled && refs.navAgent.isOnNavMesh;
    }
}
