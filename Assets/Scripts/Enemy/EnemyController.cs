using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class EnemyController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float rollForce = 6f;
    [SerializeField] float attackRange = 1.5f;
    [SerializeField] float rollCooldown = 2f;
    [SerializeField] float attackCooldown = 0.7f; 
    [SerializeField] float jumpForce = 7f;
    [SerializeField] float jumpCooldown = 2f;
    [SerializeField] Collider2D attackHitbox;
    [SerializeField] float attackHitboxActiveTime = 0.2f;
    [SerializeField] SpriteRenderer hitboxVisual;
    [SerializeField] Transform player;
    [SerializeField] int attackDamage = 1;
    [SerializeField] int maxHealth = 3;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool rolling = false;
    private float rollTimer = 0f;
    private float attackTimer = 0f;
    private float jumpTimer = 0f;
    private int facingDirection = 1;
    private bool grounded = false;
    private bool isFacingRight = true;
    private int currentHealth;
    private Coroutine flashCoroutine;
    private bool isDead = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (attackHitbox != null)
            attackHitbox.enabled = false;
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead) return;
        if (player == null) return;

        rollTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        jumpTimer -= Time.deltaTime;

        float distance = Vector2.Distance(transform.position, player.position);
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        
        if (dir > 0 && !isFacingRight)
        {
            Flip(true);
        }
        else if (dir < 0 && isFacingRight)
        {
            Flip(false);
        }
        facingDirection = isFacingRight ? 1 : -1;

        if (!rolling)
        {
            if (distance < attackRange * 1.5f && grounded && jumpTimer <= 0f && Random.value > 0.8f)
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

           
                if (attackTimer <= 0f && Random.value > 0.2f)
                {
                    attackTimer = attackCooldown + Random.Range(0f, 0.3f);
                    int attackType = Random.Range(1, 3); // 1 or 2
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
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRed());
        if (currentHealth <= 0)
        {
            Die();
        }
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
        spriteRenderer.color = Color.white;
        animator.SetTrigger("Death");
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;
        Destroy(gameObject, 2f);
    }
}