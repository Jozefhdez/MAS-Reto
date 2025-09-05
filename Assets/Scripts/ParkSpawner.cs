using UnityEngine;

public class ParkSpawner : MonoBehaviour
{
    [Header("Spawn Area (centered on this object)")]
    public int size = 100;
    public float yGround = 0f;                   // ground height

    [Header("Ground")]
    public GameObject planePrefab;
    private GameObject groundPlane;

    [Header("Trees")]
    public GameObject[] treePrefabs;
    public Vector2 treeScaleRange = new Vector2(0.9f, 1.3f);
    public float treeMinDistance = 2.0f;

    [Header("Bushes")]
    public GameObject[] bushPrefabs;
    public Vector2 bushScaleRange = new Vector2(0.8f, 1.2f);
    public float bushMinDistance = 1.2f;

    [Header("People (standing)")]
    public GameObject[] peoplePrefabs;
    public Vector2 peopleScaleRange = new Vector2(0.9f, 1.3f);
    public float peopleMinDistance = 1.2f;

    [Header("General")]
    public int seed = 12345;
    public int spawnedLayer = 0;          // set to your "Scenery" layer index if you made one
    public LayerMask overlapMask;         // include the same layer here to avoid overlaps
    public int maxTriesPerSpawn = 40;
    public Transform parent;              // where to organize spawned objects

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        var center = new Vector3(transform.position.x, yGround, transform.position.z);
        Gizmos.DrawWireCube(center, new Vector3(size, 0.1f, size));
    }

    void Start()
    {
        int treeCount = Mathf.RoundToInt(0.8f * size);
        int bushCount = Mathf.RoundToInt(0.7f * size);
        int peopleCount = Mathf.RoundToInt(0.3f * size);

        // Create ground plane automatically
        if (planePrefab != null)
        {
            if (groundPlane != null) Destroy(groundPlane);

            groundPlane = Instantiate(
                planePrefab,
                new Vector3(transform.position.x, yGround, transform.position.z),
                Quaternion.identity,
                transform
            );
            groundPlane.name = "GroundPlane";
            groundPlane.layer = LayerMask.NameToLayer("Ground");

            if (!groundPlane.GetComponent<Collider>())
            {
                groundPlane.AddComponent<MeshCollider>(); // works for Unity's default Plane
            }
            
            groundPlane.transform.localScale = new Vector3((size + 1) / 10f, 1f, (size + 1) / 10f);
        }

        Random.InitState(seed);
        if (parent == null) parent = this.transform;

        SpawnGroup(treePrefabs, treeCount, treeMinDistance, treeScaleRange, randomYRotation: true);
        SpawnGroup(bushPrefabs, bushCount, bushMinDistance, bushScaleRange, randomYRotation: true);
        SpawnGroup(peoplePrefabs, peopleCount, peopleMinDistance, peopleScaleRange, randomYRotation: true);
    }

    void SpawnGroup(GameObject[] prefabs, int count, float minDistance, Vector2 scaleRange, bool randomYRotation)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int tries = 0; tries < maxTriesPerSpawn; tries++)
            {
                Vector3 pos = RandomPointInArea();
                bool blocked = Physics.CheckSphere(pos, minDistance, overlapMask, QueryTriggerInteraction.Ignore);
                if (!blocked)
                {
                    var prefab = prefabs[Random.Range(0, prefabs.Length)];
                    Quaternion rot = randomYRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
                    var go = Instantiate(prefab, pos, rot, parent);

                    float scale = Random.Range(scaleRange.x, scaleRange.y);
                    go.transform.localScale = go.transform.localScale * scale;

                    if (spawnedLayer >= 0) go.layer = spawnedLayer;

                    // If it has no collider, add a small trigger sphere so future placements can "see" it
                    if (!go.GetComponent<Collider>())
                    {
                        var sc = go.AddComponent<SphereCollider>();
                        sc.isTrigger = true;
                        sc.radius = minDistance * 0.5f;
                    }

                    placed = true;
                    break;
                }
            }
            if (!placed)
            {
                Debug.LogWarning($"Could not place an object after {maxTriesPerSpawn} tries (group).");
            }
        }
    }

    Vector3 RandomPointInArea()
    {
        float halfX = size * 0.5f;
        float halfZ = size * 0.5f;
        float x = Random.Range(-halfX, halfX);
        float z = Random.Range(-halfZ, halfZ);
        return new Vector3(transform.position.x + x, yGround, transform.position.z + z);
    }
}