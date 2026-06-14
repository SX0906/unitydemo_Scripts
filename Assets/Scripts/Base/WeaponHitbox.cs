using System;
using System.Collections.Generic;
using UnityEngine;

public class WeaponHitbox : MonoBehaviour
{
    [Header("攻击范围挂载点")]
    public GameObject owner;

    [Header("攻击对象的目标 Layer")]
    public LayerMask targetMask;

    [Header("每次攻击窗口同一目标只能打一次")]
    public bool hitSameTargetOncePerWindow = true;

    [Header("攻击日志")]
    public bool debugLog;

    public event Action<GameObject> OnHitEnemy;

    private Collider _hitboxCollider;
    private string _currentHitState;
    private float _currentDamage;
    private bool _isHitboxActive;

    private readonly HashSet<object> _hitTargetSet = new HashSet<object>();

    private void Awake()
    {
        _hitboxCollider = GetComponent<Collider>();
        if (_hitboxCollider == null)
        {
            Debug.LogError($"[{gameObject.name}] 未找到Collider组件，WeaponHitbox已禁用", this);
            enabled = false;
            return;
        }

        _hitboxCollider.isTrigger = true;
        _hitboxCollider.enabled = false;
    }

    public void BeginHitbox(string hitStateName, float damage = 10f)
    {
        if (string.IsNullOrEmpty(hitStateName) || _isHitboxActive) return;

        _currentHitState = hitStateName;
        _currentDamage = damage;
        _isHitboxActive = true;
        _hitTargetSet.Clear();

        if (_hitboxCollider != null)
            _hitboxCollider.enabled = true;

        if (debugLog)
            Debug.Log($"[{gameObject.name}] 开启Hitbox | 攻击状态：{hitStateName} | 伤害：{damage}");
    }

    public void EndHitbox()
    {
        if (!_isHitboxActive) return;

        _isHitboxActive = false;
        _currentHitState = string.Empty;
        _currentDamage = 0f;
        _hitTargetSet.Clear();

        if (_hitboxCollider != null)
            _hitboxCollider.enabled = false;

        if (debugLog)
            Debug.Log($"[{gameObject.name}] 关闭Hitbox");
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    private void ProcessHit(Collider other)
    {
        if (!_isHitboxActive || string.IsNullOrEmpty(_currentHitState)) return;

        if (((1 << other.gameObject.layer) & targetMask) == 0) return;

        if (IsSelfOrChild(other.transform)) return;

        IHitReceiver hitReceiver = other.GetComponentInParent<IHitReceiver>();
        if (hitReceiver == null) return;

        if (hitSameTargetOncePerWindow && _hitTargetSet.Contains(hitReceiver)) return;

        _hitTargetSet.Add(hitReceiver);

        // 先尝试处决
        GameObject victim = other.gameObject;
        if (owner != null)
        {
            if (ExecutionManager.TryStartExecution(owner, victim))
            {
                // 处决成功，不做普通伤害
                EndHitbox();
                return;
            }
        }

        // 普通伤害
        hitReceiver.ReceiveHit(_currentHitState, _currentDamage);

        OnHitEnemy?.Invoke(other.gameObject);

        if (debugLog)
            Debug.Log($"[{gameObject.name}] 击中目标：{other.name} | 攻击状态：{_currentHitState} | 伤害：{_currentDamage}");
    }

    private bool IsSelfOrChild(Transform targetTrans)
    {
        if (owner == null) return false;
        return targetTrans == owner.transform || targetTrans.IsChildOf(owner.transform);
    }
}
