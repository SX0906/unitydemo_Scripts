using UnityEngine;

public readonly struct HitContext
{
    public readonly string DirTag;
    public readonly Vector3 Direction;
    public readonly bool IsLauncher;
    public readonly Transform Attacker;
    public readonly float Damage;
    public readonly bool IgnoreBlock;

    public HitContext(
        string dirTag,
        Vector3 direction,
        bool isLauncher,
        Transform attacker,
        float damage,
        bool ignoreBlock = false)
    {
        DirTag = dirTag;
        Direction = direction;
        IsLauncher = isLauncher;
        Attacker = attacker;
        Damage = damage;
        IgnoreBlock = ignoreBlock;
    }
}

public interface ICombatTarget
{
    Transform Transform { get; }
    bool IsAlive { get; }
    bool TakeHit(HitContext hit);
}

public interface ICombatant
{
    Transform Transform { get; }
    ActorVitals Vitals { get; }
}
