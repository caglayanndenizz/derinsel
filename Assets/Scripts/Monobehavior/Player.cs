using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Unity.Cinemachine;

public class Player : BaseEntity
{
    [Header("External References")]
    public DungeonGenerator generator;
    
    [Header("Hammer Settings (Heavy)")]
    public float maxChargeTime = 0.5f;
    public float hammerAOE = 2.5f;

    [Header("Light Attack Settings (Spammable)")]
    public float lightAttackRate = 0.2f; 
    public float lightAttackRange = 1.5f; 
    public float lightAttackDuration = 0.1f; 
    private float _nextAttackTime = 0f;

    [Header("References")]
    public Transform hammerPivot;
    public Transform attackPoint;
    public LayerMask enemyLayers;
    public float goldCount;
    public float experienceCount;

    [Header("UI Reference")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    private float _currentCharge = 0f;
    private bool _isCharging = false;
    private Rigidbody2D _rb; // FİZİK İÇİN ŞART

    protected override void Awake()
    {
        base.Awake();
        // Rigidbody referansını al ve ayarla
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; // 2D Top-down olduğu için yerçekimini kapat
        _rb.freezeRotation = true; // Karakterin devrilmesini engelle
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Duvar delmeyi engeller
    }

    void Update()
    {
        HandleHammerCharge(); // Sağ Tık
        HandleLightAttack();  // Sol Tık
    }

    // FİZİK HAREKETİ BURADA OLMALI
    void FixedUpdate()
    {
        Move();
    }

    protected override void Move()
    {
        float moveX = Input.GetAxisRaw("Horizontal"); 
        float moveY = Input.GetAxisRaw("Vertical");   

        Vector2 direction = new Vector2(moveX, moveY).normalized;
        
        // Şarj olurken yavaşla, normalde tam hız git
        float currentSpeed = _isCharging ? stats.moveSpeed * 0.3f : stats.moveSpeed;
        
        // KRİTİK DÜZELTME: transform.Translate SİLİNDİ, Rigidbody Velocity GELDİ!
        _rb.linearVelocity = direction * currentSpeed;

        // --- FLIP MANTIĞI ---
        if (moveX > 0) transform.localScale = new Vector3(1f, 1f, 1f);
        else if (moveX < 0) transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    private void HandleLightAttack()
    {
        if (_isCharging) return;
        if (Input.GetButtonDown("Fire1") && Time.time >= _nextAttackTime)
        {
            LightAttack();
            _nextAttackTime = Time.time + lightAttackRate;
        }
    }

    private void LightAttack()
    {
        StartCoroutine(LightSwingRoutine());
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, lightAttackRange, enemyLayers);
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target != null) target.TakeDamage(stats.lightAttackDamage, false);
        }
    }

    private IEnumerator LightSwingRoutine()
    {
        hammerPivot.localRotation = Quaternion.Euler(0, 0, -45f);
        yield return new WaitForSeconds(lightAttackDuration);
        hammerPivot.localRotation = Quaternion.identity;
    }

    private void HandleHammerCharge()
    {
        if (Input.GetButton("Fire2"))
        {
            _isCharging = true;
            meterCanvas.SetActive(true);
            _currentCharge += Time.deltaTime;
            _currentCharge = Mathf.Clamp(_currentCharge, 0f, maxChargeTime);
            chargeMeter.value = _currentCharge / maxChargeTime; 
            hammerPivot.localRotation = Quaternion.Euler(0, 0, (_currentCharge / maxChargeTime) * 90f);
        }

        if (Input.GetButtonUp("Fire2"))
        {
            if (_currentCharge >= maxChargeTime)
            {
                hammerPivot.localRotation = Quaternion.identity;
                HammerSlam(); 
            }
            ResetCharge();
        }
    }

    private void HammerSlam()
    {
        CinemachineImpulseSource source = GetComponent<CinemachineImpulseSource>();
        if (source != null) source.GenerateImpulse(); 
        
        if (generator != null) generator.BreakWallsInArea(attackPoint.position, hammerAOE);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, hammerAOE, enemyLayers);
        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target != null) target.TakeDamage(stats.heavyAttackDamage, true);
        }
    }

    private void ResetCharge()
    {
        _isCharging = false;
        _currentCharge = 0f;
        chargeMeter.value = 0f;
        meterCanvas.SetActive(false);
        hammerPivot.localRotation = Quaternion.identity;
    }
}