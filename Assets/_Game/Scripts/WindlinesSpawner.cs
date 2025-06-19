using UnityEngine;
using System.Collections;

public class WindlinesSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] windVFXPrefabs;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float spawnRadius = 8f;
    [SerializeField] private int maxWindVFX = 10;

    private Transform playerTransform;
    private float timer = 0f;
    private int currentVFXCount = 0;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                Debug.LogWarning("Player with tag 'Player' not found.");
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        transform.position = playerTransform.position;

        timer += Time.deltaTime;
        if (timer >= spawnInterval && currentVFXCount < maxWindVFX)
        {
            SpawnWindVFX();
            timer = 0f;
        }
    }

    void SpawnWindVFX()
    {
        if (windVFXPrefabs.Length == 0) return;

        Vector3 randomOffset = Random.onUnitSphere * spawnRadius;
        randomOffset.y = Mathf.Abs(randomOffset.y);

        Vector3 spawnPos = playerTransform.position + randomOffset;
        GameObject prefab = windVFXPrefabs[Random.Range(0, windVFXPrefabs.Length)];
        GameObject vfx = Instantiate(prefab, spawnPos, Quaternion.identity);

        currentVFXCount++;

        ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = vfx.GetComponentInChildren<ParticleSystem>();
        }

        if (ps != null)
        {
            StartCoroutine(WaitForVFXToFinish(ps, vfx));
        }
        else
        {
            Debug.LogWarning("No ParticleSystem found on VFX prefab. Destroying after 5 seconds.");
            Destroy(vfx, 5f);
            StartCoroutine(DelayedCountDecrease(5f));
        }
    }

    IEnumerator WaitForVFXToFinish(ParticleSystem ps, GameObject vfx)
    {
        yield return new WaitUntil(() => !ps.IsAlive(true));
        Destroy(vfx);
        currentVFXCount--;
    }

    IEnumerator DelayedCountDecrease(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentVFXCount--;
    }
}