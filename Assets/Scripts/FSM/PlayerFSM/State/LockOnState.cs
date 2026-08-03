using UnityEngine;
using GameInput;
public class LockOnState : StateBase
{
    private Animator animator;
    private TestFSM testFSM;
    private FSMControl fsm;
    private TestFSM_test testFSM_test;


    public LockOnState(Animator animator, FSMControl fsm ,TestFSM testFSM)
    {
        this.animator = animator;
        this.testFSM = testFSM;
        this.fsm = fsm;
    }

    public LockOnState(Animator animator, FSMControl fsm, TestFSM_test testFSM_test)
    {
        this.animator = animator;
        this.fsm = fsm;
        this.testFSM_test = testFSM_test;
    }


    public override void OnEnter()
    {
        Debug.Log("进入锁定状态");
        animator.CrossFade("BaseMotion", 0.02f);
        animator.SetFloat("LockOn", 1f);
    }
    public override void OnUpdate()
    {
        Transform locktarget = testFSM._lockOnTarget;
        if(locktarget == null|| Vector3.SqrMagnitude(locktarget.position - testFSM.transform.position) > testFSM.lockOnMaxRange)
         {
            Debug.Log("锁定目标丢失，退出锁定");
            fsm.SetState(StateType.IDlE);
             return;
         }

         Vector3 toTarget  = locktarget.position - testFSM.transform.position;
        toTarget.y = 0;
        if(toTarget.sqrMagnitude > 0.01f)
        {
            testFSM.transform.rotation = Quaternion.Slerp(testFSM.transform.rotation, 
            Quaternion.LookRotation(toTarget), Time.deltaTime * 10f);
        }
    }

    public override void OnExit()
    {
    }



}
