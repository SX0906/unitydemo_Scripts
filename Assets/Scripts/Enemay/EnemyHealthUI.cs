using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("血量条设置")]
    public Slider healthSlider;
    public Image healthFillImage;
    public Color healthFullColor = Color.red;
    public Color healthEmptyColor = Color.gray;

    [Header("UI设置")]
    public float uiHeightOffset = 2f;
    public bool alwaysFaceCamera = true;
    public float losePlayerHideDelay = 5f; // 丢失玩家后5秒隐藏

    [Header("引用")]
    public HealthSystem healthSystem;
    public Transform targetTransform;
    private Camera mainCamera;
    private EnemyCombatController combatController;

    private float losePlayerTimer;
    private bool isVisible;
    private bool lastSawPlayer;

    private void Start()
    {
        mainCamera = Camera.main;

        if (healthSystem == null)
        {
            healthSystem = GetComponentInParent<HealthSystem>();
        }

        if (targetTransform == null)
        {
            targetTransform = transform.parent;
        }

        // 获取敌人战斗控制器
        combatController = GetComponentInParent<EnemyCombatController>();

        // 初始化UI
        UpdateHealthUI();
        
        // 默认隐藏
        HideUI();

        // 订阅事件
        if (healthSystem != null)
        {
            healthSystem.OnHealthChanged += OnHealthChanged;
            healthSystem.OnDeath += OnDeath;
        }
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
        {
            healthSystem.OnHealthChanged -= OnHealthChanged;
            healthSystem.OnDeath -= OnDeath;
        }
    }

    private void Update()
    {
        // 更新位置
        if (targetTransform != null)
        {
            Vector3 targetPosition = targetTransform.position + Vector3.up * uiHeightOffset;
            transform.position = targetPosition;
        }

        // 面向摄像机
        if (alwaysFaceCamera && mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward, mainCamera.transform.rotation * Vector3.up);
        }

        // 处理可见性逻辑
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool canSeePlayer = combatController != null && combatController.HasTarget;

        // 看到玩家，显示血条
        if (canSeePlayer)
        {
            ShowUI();
            lastSawPlayer = true;
            losePlayerTimer = losePlayerHideDelay;
        }
        // 看不到玩家
        else
        {
            // 如果之前看到过，开始计时
            if (lastSawPlayer)
            {
                losePlayerTimer -= Time.deltaTime;
                if (losePlayerTimer <= 0f)
                {
                    HideUI();
                    lastSawPlayer = false;
                }
            }
        }
    }

    private void OnHealthChanged()
    {
        UpdateHealthUI();
        // 受伤时立即显示
        ShowUI();
        lastSawPlayer = true;
        losePlayerTimer = losePlayerHideDelay;
    }

    private void OnDeath()
    {
        // 死亡时隐藏UI
        HideUI();
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null && healthSystem != null)
        {
            float healthPercent = healthSystem.GetHealthPercent();
            healthSlider.value = healthPercent;
            
            // 不修改颜色，保持图片原本的颜色
        }
    }

    private void ShowUI()
    {
        if (!isVisible)
        {
            isVisible = true;
            gameObject.SetActive(true);
        }
    }

    private void HideUI()
    {
        if (isVisible)
        {
            isVisible = false;
            gameObject.SetActive(false);
        }
    }
}
