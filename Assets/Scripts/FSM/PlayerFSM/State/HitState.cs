using UnityEngine;

/// <summary>
/// 玩家受击硬直状态 —— 无受击动画，仅硬直 + 面向攻击者 + 受击音效。
/// </summary>
public class HitState : StateBase
{
    private FSMControl fsm;
    private TestFSM testfsm;
    private CombatAudioPlayer audioPlayer;

    private float duration = 0.01f;
    private float timer;
    private Transform attacker;
    private TestFSM_test testFSM_test;


    public HitState(FSMControl fsm, TestFSM testfsm)
    {
        this.fsm = fsm;
        this.testfsm = testfsm;
        this.audioPlayer = testfsm.GetComponent<CombatAudioPlayer>();
    }

    public HitState(FSMControl fsm, TestFSM_test testFSM_test)
    {
        this.fsm = fsm;
        this.testFSM_test = testFSM_test;
    }


    public void SetHitInfo(Transform attackerTransform)
    {
        attacker = attackerTransform;
    }

    public void Rehit(Transform attackerTransform)
    {
        attacker = attackerTransform;
        timer = duration;
        audioPlayer?.PlayHitSound();
    }

    public override void OnEnter()
    {
        Debug.Log("进入受击状态");
        timer = duration;
        audioPlayer?.PlayHitSound();
    }

    public override void OnUpdate()
    {
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