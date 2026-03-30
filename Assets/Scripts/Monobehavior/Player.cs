using UnityEngine;
using UnityEngine.UI;



public class Player : BaseEntity
{

    [Header("Hammer Settings")]
    public float maxChargeTime = 0.5f;
    public float hammerAOE = 2.5f;
    public float hammerDamageMultiplier = 2f;

    public Transform hammerPivot;

    public Transform attackPoint;
    public LayerMask enemyLayers;


    [Header("UI Reference")]
    public Slider chargeMeter;
    public GameObject meterCanvas;

    private float _currentCharge = 0f;
    private bool _isCharging = false;

    [Header("Loot Settings")]
    public float goldCount = 0;
    public float experienceCount = 0;


    protected override void Awake()
    {
        base.Awake();
        
    }

    void Update()
    {
        Move();
        HandleHammerCharge();
    }

    protected override void Move()
    {
        float moveX = Input.GetAxis("Horizontal"); 
		float moveY = Input.GetAxis("Vertical");   

		Vector3 direction = new Vector3(moveX, moveY, 0).normalized;
        float currentSpeed = _isCharging ? stats.moveSpeed * 0.3f : stats.moveSpeed;
		transform.Translate(direction * stats.moveSpeed * Time.deltaTime);

    }



    private void HandleHammerCharge()
{
    if (Input.GetButton("Fire1"))
    {
        _isCharging = true;
        meterCanvas.SetActive(true);

        // 1. ADIM: Zamanı biriktir (Asla maxChargeTime'ı geçemez)
        _currentCharge += Time.deltaTime;
        _currentCharge = Mathf.Clamp(_currentCharge, 0f, maxChargeTime);

        // 2. ADIM: ORAN (Bütün sihir burada. 0 ile 1 arası tertemiz bir sayı)
        // Şarj başladığında 0, bittiğinde tam 1 olur.
        float progress = _currentCharge / maxChargeTime;

        // 3. ADIM: AYNI ANDA ÇALIŞTIR
        // Slider 1'den (Dolu) 0'a (Boş) düşerken...
        chargeMeter.value = 1f - progress; 

        // Çekiç 0 dereceden (Dik) 90 dereceye (Yatık) aynı oranda gelir.
        hammerPivot.localRotation = Quaternion.Euler(0, 0, progress * 90f);
    }

    if (Input.GetButtonUp("Fire1"))
    {
        // Şarj TAMAMLANDI MI? (progress tam 1 oldu mu?)
        if (_currentCharge >= maxChargeTime)
        {
            // Bıraktığın an çekiç ışınlanarak dikey (0) olur ve vurur
            hammerPivot.localRotation = Quaternion.identity;
            HammerSlam(); 
        }
        else
        {
            // Şarj bitmediyse her şeyi eski haline çek
            hammerPivot.localRotation = Quaternion.identity;
        }

        ResetCharge();
    }
}
    private void HammerSlam()
    {
        
        Debug.Log("ÇEKİÇ YERE VURULDU!");

        // Alan hasarı ve Knockback
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, hammerAOE, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable target = enemy.GetComponent<IDamageable>();
            if (target != null)
            {
                // Normal atağın 2 katı hasar ver
                target.TakeDamage(stats.attackPower * hammerDamageMultiplier);
            }
        }

        // Juicing: Ekranı salla ve anlık yavaşlat (Opsiyonel)
        // StartCoroutine(CombatJuice.HitStop(0.1f));
    }


    private void ResetCharge()
    {
        _isCharging = false;
        _currentCharge = 0f;
        chargeMeter.value = 0f;
        meterCanvas.SetActive(false); // Barı gizles
    }

    



    /*private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Enemy enemyScript = collision.gameObject.GetComponent<Enemy>();
        
            if (enemyScript != null)
            {
                TakeDamage(enemyScript.stats.attackPower);
            }
        }

                //buradaki collision mantigi attack function gelince silinecek.

    }
    */

    protected override void Die()
    {
        base.Die();
        Destroy(gameObject);
    }

}
