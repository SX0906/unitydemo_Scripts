using System.Collections.Generic;
using UnityEngine;

public class EnemySkillManager_test : MonoBehaviour
{
    public List<EnemySkillData> skillPool = new();
    private readonly Dictionary<string, float> cooldownEndTimes = new();

    public EnemySkillData CurrentSkill { get; private set; }
    public Transform CurrentTarget { get; private set; }
    public bool IsCasting => CurrentSkill != null;

    public EnemySkillData GetAvailableSkill(Transform self, Transform target)
    {
        if (target == null || skillPool == null) return null;
        var available = new List<EnemySkillData>();
        foreach (var s in skillPool)
            if (IsSkillReady(s, self, target)) available.Add(s);
        return available.Count > 0 ? available[Random.Range(0, available.Count)] : null;
    }

    public bool IsSkillReady(EnemySkillData skill, Transform self, Transform target)
    {
        if (skill == null || !skill.isAvailable || target == null) return false;
        ICombatTarget t = target.GetComponentInParent<ICombatTarget>();
        if (t != null && !t.IsAlive) return false;
        if (cooldownEndTimes.TryGetValue(skill.skillID, out float end) && Time.time < end) return false;
        float dist = Vector3.Distance(self.position, target.position);
        return dist <= skill.useDistance;
    }

    public bool HasAvailableSkill(Transform self, Transform target) { return GetAvailableSkill(self, target) != null; }
    public void StartCast(EnemySkillData skill, Transform target) { CurrentSkill = skill; CurrentTarget = target; }

    public void FinishCast()
    {
        if (CurrentSkill != null)
        {
            float cd = CurrentSkill.coolDown;
            ICombatant self = GetComponent<ICombatant>();
            if (self?.Vitals is EnemyVitals ev && ev.RagePercent >= 1f) cd *= 0.9f;
            cooldownEndTimes[CurrentSkill.skillID] = Time.time + cd;
        }
        CurrentSkill = null; CurrentTarget = null;
    }

    public float GetCooldownRemain(EnemySkillData skill)
    {
        if (skill == null) return 0f;
        if (cooldownEndTimes.TryGetValue(skill.skillID, out float end)) return Mathf.Max(0f, end - Time.time);
        return 0f;
    }

    public bool HasAnySkillReadyIgnoreDistance(Transform self, Transform target)
    {
        if (target == null || skillPool == null) return false;
        ICombatTarget t = target.GetComponentInParent<ICombatTarget>();
        if (t != null && !t.IsAlive) return false;
        foreach (var s in skillPool)
        {
            if (s == null || !s.isAvailable) continue;
            if (cooldownEndTimes.TryGetValue(s.skillID, out float end) && Time.time < end) continue;
            return true;
        }
        return false;
    }
}
