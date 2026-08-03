using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFSMBT_test
{
    public interface IEnemyFsmAccess
    {
        Transform Transform { get; }
        bool IsGrounded { get; }
        bool HasTarget { get; }
        Transform TargetPlayer { get; }
        LayerMask PlayerLayer { get; }
        void SetDesiredState(EnemyStateType state);
        void DirectSetState(EnemyStateType state);
        void SetWeaponDamage(float dmg);
        EnemyAttackState_test GetAttackState();
    }

    public BehaviorTree BuildTree(IEnemyFsmAccess fsm, EnemySkillManager_test skillMgr)
    {
        BTActionNode actionIdle = new BTActionNode(() => { fsm.SetDesiredState(EnemyStateType.IDLE); return BTNodeState.Success; });

        BTSequenceNode attackSequence = new BTSequenceNode();
        attackSequence.AddChild(new BTConditionNode(() => fsm.IsGrounded));
        attackSequence.AddChild(new BTConditionNode(() => skillMgr != null && fsm.HasTarget && fsm.TargetPlayer != null && skillMgr.HasAvailableSkill(fsm.Transform, fsm.TargetPlayer)));
        attackSequence.AddChild(new BTActionNode(() => {
            EnemySkillData skill = skillMgr.GetAvailableSkill(fsm.Transform, fsm.TargetPlayer);
            if (skill == null) return BTNodeState.Failure;
            skillMgr.StartCast(skill, fsm.TargetPlayer);
            fsm.GetAttackState()?.SetAttackerAndSkill(fsm.TargetPlayer, skill);
            fsm.DirectSetState(EnemyStateType.ATTACK);
            return BTNodeState.Success;
        }));

        BTSequenceNode chaseSequence = new BTSequenceNode();
        chaseSequence.AddChild(new BTConditionNode(() => fsm.IsGrounded));
        chaseSequence.AddChild(new BTConditionNode(() => skillMgr != null && fsm.HasTarget && fsm.TargetPlayer != null && skillMgr.HasAnySkillReadyIgnoreDistance(fsm.Transform, fsm.TargetPlayer)));
        chaseSequence.AddChild(new BTActionNode(() => { if (fsm.HasTarget) { fsm.SetDesiredState(EnemyStateType.MOVE); return BTNodeState.Success; } return BTNodeState.Failure; }));

        BTSequenceNode lockMoveSequence = new BTSequenceNode();
        lockMoveSequence.AddChild(new BTConditionNode(() => fsm.IsGrounded));
        lockMoveSequence.AddChild(new BTActionNode(() => { if (fsm.HasTarget) { fsm.SetDesiredState(EnemyStateType.LOCK_MOVE); return BTNodeState.Success; } return BTNodeState.Failure; }));

        BTSelectorNode root = new BTSelectorNode(attackSequence, chaseSequence, lockMoveSequence, actionIdle);
        return new BehaviorTree(root);
    }
}
