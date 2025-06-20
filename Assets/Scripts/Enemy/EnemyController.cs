using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float rollForce = 6f;
    [SerializeField] float jumpForce = 7f;
    [SerializeField] float jumpCooldown = 2f;

    [Header("Combat")]
    [SerializeField] float attackRange = 1.5f;
    [SerializeField] float rollCooldown = 2f;
    [SerializeField] float attackCooldown = 0.7f;
    [SerializeField] Collider2D attackHitbox;
    [SerializeField] float attackHitboxActiveTime = 0.2f;
    [SerializeField] SpriteRenderer hitboxVisual;
    [SerializeField] int attackDamage = 1;
    [SerializeField] int maxHealth = 5;
    [SerializeField] private GameObject[] hearts;

    [Header("Blocking")]
    [SerializeField] Collider2D blockHitbox;
    [SerializeField] SpriteRenderer blockHitboxVisual;
    [SerializeField] bool blockNegatesDamage = true;
    [SerializeField] int maxBlockHits = 3;
    [SerializeField] float blockResetCooldown = 2f;
    [SerializeField] float minBlockDuration = 1.5f;

    [Header("References")]
    [SerializeField] Transform player;

    [Header("Impact VFX")]
    [SerializeField] private GameObject hitImpactVFX;
    [SerializeField] private GameObject blockImpactVFX;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip blockSound;
    [SerializeField] private AudioClip hitBlockSound;
    [SerializeField] private AudioClip blockBreakSound;
    
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool grounded = false;
    private bool isFacingRight = true;
    private int facingDirection = 1;
    private float rollTimer = 0f;
    private float attackTimer = 0f;
    private float jumpTimer = 0f;
    private int currentHealth;
    private Coroutine flashCoroutine;
    private bool isDead = false;
    private bool rolling = false;

    private bool isBlocking = false;
    private int currentBlockHits;
    private Coroutine blockResetCoroutine;
    private bool isStunned = false;
    private Coroutine blockDurationCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (attackHitbox != null)
            attackHitbox.enabled = false;
        if (blockHitbox != null)
            blockHitbox.enabled = false;
        if (blockHitboxVisual != null)
            blockHitboxVisual.enabled = false;

        currentHealth = maxHealth;
        currentBlockHits = maxBlockHits;
    }

    void UpdateHearts()
    {
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].SetActive(i < currentHealth);
    }

    void Update()
    {
        if (isDead || isStunned) return;
        if (player == null) return;

        rollTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        jumpTimer -= Time.deltaTime;

        float distance = Vector2.Distance(transform.position, player.position);
        float dir = Mathf.Sign(player.position.x - transform.position.x);

        if (dir > 0 && !isFacingRight)
            Flip(true);
        else if (dir < 0 && isFacingRight)
            Flip(false);

        facingDirection = isFacingRight ? 1 : -1;

        if (!rolling)
        {
            if (isBlocking)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                animator.SetInteger("AnimState", 0);
                return;
            }

            if (!isBlocking && Random.value > 0.995f && distance < attackRange * 1.5f)
                StartBlocking();

            if (distance < attackRange * 1.5f && grounded && jumpTimer <= 0f && Random.value > 0.9f)
            {
                float jumpDir = -facingDirection;
                rb.linearVelocity = new Vector2(jumpDir * moveSpeed * 1.2f, jumpForce);
                animator.SetTrigger("Jump");
                grounded = false;
                jumpTimer = jumpCooldown;
            }
            else if (distance > attackRange)
            {
                rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
                animator.SetInteger("AnimState", 1);
            }
            else
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                animator.SetInteger("AnimState", 0);

                if (attackTimer <= 0f && Random.value > 0.3f)
                {
                    attackTimer = attackCooldown + Random.Range(0f, 0.3f);
                    int attackType = Random.Range(1, 3);
                    animator.SetTrigger("Attack" + attackType);
                    if (attackHitbox != null)
                        StartCoroutine(EnableHitboxTemporarily());
                }
                else if (rollTimer <= 0f && Random.value > 0.7f)
                {
                    rolling = true;
                    animator.SetTrigger("Roll");
                    rb.linearVelocity = new Vector2(facingDirection * rollForce, rb.linearVelocity.y);
                    rollTimer = rollCooldown;
                    StartCoroutine(EndRoll());
                }
            }
        }
    }

    void StartBlocking()
    {
        if (isBlocking) return;

        isBlocking = true;
        animator.SetTrigger("Block");
        animator.SetBool("IdleBlock", true);

        if (blockHitbox != null)
            blockHitbox.enabled = true;
        if (blockHitboxVisual != null)
            blockHitboxVisual.enabled = true;

        if (audioSource != null && blockSound != null)
            audioSource.PlayOneShot(blockSound);
        
        if (blockDurationCoroutine != null)
            StopCoroutine(blockDurationCoroutine);
        blockDurationCoroutine = StartCoroutine(BlockForMinimumTime(minBlockDuration));
    }

    void StopBlocking()
    {
        isBlocking = false;
        animator.SetBool("IdleBlock", false);

        if (blockHitbox != null)
            blockHitbox.enabled = false;
        if (blockHitboxVisual != null)
            blockHitboxVisual.enabled = false;

        if (blockDurationCoroutine != null)
        {
            StopCoroutine(blockDurationCoroutine);
            blockDurationCoroutine = null;
        }
    }

    IEnumerator BlockForMinimumTime(float duration)
    {
        yield return new WaitForSeconds(duration);
        StopBlocking();
    }

    void Flip(bool faceRight)
    {
        isFacingRight = faceRight;
        spriteRenderer.flipX = !faceRight;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.contacts[0].normal.y > 0.5f)
        {
            grounded = true;
            animator.SetBool("Grounded", true);
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        grounded = false;
        animator.SetBool("Grounded", false);
    }

    IEnumerator EndRoll()
    {
        yield return new WaitForSeconds(0.5f);
        rolling = false;
    }

    IEnumerator EnableHitboxTemporarily()
    {
        attackHitbox.enabled = true;
        if (hitboxVisual != null)
            hitboxVisual.enabled = true;
        yield return new WaitForSeconds(attackHitboxActiveTime);
        attackHitbox.enabled = false;
        if (hitboxVisual != null)
            hitboxVisual.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (attackHitbox.enabled && other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(attackDamage);
                Debug.Log("Enemy attacked player for " + attackDamage + " damage.");

                if (hitImpactVFX != null)
                    Instantiate(blockImpactVFX, transform.position + new Vector3(0, 2f, 0), Quaternion.identity);
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        if (isBlocking && blockNegatesDamage && currentBlockHits > 0)
        {
            currentBlockHits--;
            Debug.Log("Enemy blocked damage. Remaining blocks: " + currentBlockHits);

            if (blockImpactVFX != null)
                Instantiate(blockImpactVFX, transform.position + new Vector3(0, 2f, 0), Quaternion.identity);

            if (audioSource != null && hitBlockSound != null)
                audioSource.PlayOneShot(hitBlockSound);

            
            if (currentBlockHits > 0)
            {
                if (blockResetCoroutine != null)
                    StopCoroutine(blockResetCoroutine);
                blockResetCoroutine = StartCoroutine(ResetBlockHitsAfterDelay());
            }
            else
            {
                if (audioSource != null && blockBreakSound != null)
                    audioSource.PlayOneShot(blockBreakSound);
                
                Debug.Log("Enemy shield broken!");
                StopBlocking();
                StartCoroutine(StunAndRecoverBlock(1.0f));
            }

            return;
        }

        currentHealth -= amount;
        UpdateHearts();
        animator.SetTrigger("Hurt");

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRed());

        if (currentHealth <= 0)
            Die();
    }

    IEnumerator ResetBlockHitsAfterDelay()
    {
        yield return new WaitForSeconds(blockResetCooldown);
        currentBlockHits = maxBlockHits;
        Debug.Log("Enemy shield recovered.");
    }

    IEnumerator StunAndRecoverBlock(float duration)
    {
        isStunned = true;
        animator.SetTrigger("Hurt");
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(duration);

        isStunned = false;

        if (blockResetCoroutine != null)
            StopCoroutine(blockResetCoroutine);
        blockResetCoroutine = StartCoroutine(ResetBlockHitsAfterDelay());
    }

    IEnumerator FlashRed()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.3f);
        spriteRenderer.color = Color.white;
        flashCoroutine = null;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        animator.SetTrigger("Death");
        spriteRenderer.color = Color.white;
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;
        StopBlocking();
        Destroy(gameObject, 2f);
    }
}
