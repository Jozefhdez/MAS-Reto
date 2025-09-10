using UnityEngine;
using System.Collections.Generic;

public class ParkSpawner : MonoBehaviour
{
    [Header("Exclusion Zone")]
    public Vector3 excludeCenter = Vector3.zero;
    public float excludeRadius = 6f;
    public bool useExclusion = true;

    [Header("Spawn Area (centered on this object)")]
    public int size = 100;
    public float yGround = 0f;

    [Header("Ground")]
    public GameObject planePrefab;
    private GameObject groundPlane;

    [Header("Trees")]
    public GameObject[] treePrefabs;
    public Vector2 treeScaleRange = new Vector2(1f, 3f);
    public float treeMinDistance = 2.0f;

    [Header("Bushes")]
    public GameObject[] bushPrefabs;
    public Vector2 bushScaleRange = new Vector2(1f, 2f);
    public float bushMinDistance = 1.2f;

    [Header("People (standing)")]
    public GameObject[] peoplePrefabs;
    public GameObject[] specialPeoplePrefabs;
    public Vector2 peopleScaleRange = new Vector2(1f, 1f);
    public float peopleMinDistance = 1.2f;

    [Header("General")]
    public int seed = -1; // -1 = to use random seed
    public int spawnedLayer = 0;
    public LayerMask overlapMask;
    public int maxTriesPerSpawn = 40;
    public Transform parent;

    [Header("Generated References")]
    public List<GameObject> generatedPeople = new List<GameObject>();

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
                groundPlane.AddComponent<MeshCollider>();
            }
            
            groundPlane.transform.localScale = new Vector3((size + 5) / 10f, 1f, (size + 5) / 10f);
        }

        // Inicializar semilla aleatoria
        if (seed == -1)
        {
            seed = System.DateTime.Now.Millisecond;
            Debug.Log($"Using random seed: {seed}");
        }
        else
        {
            Debug.Log($"Using static seed: {seed}");
        }
        Random.InitState(seed);
        
        if (parent == null) parent = this.transform;

        SpawnGroup(treePrefabs, treeCount, treeMinDistance, treeScaleRange, randomYRotation: true);
        SpawnGroup(bushPrefabs, bushCount, bushMinDistance, bushScaleRange, randomYRotation: true);
        SpawnGroup(peoplePrefabs, peopleCount, peopleMinDistance, peopleScaleRange, randomYRotation: true);
        SpawnPeople(specialPeoplePrefabs, specialPeoplePrefabs.Length, peopleMinDistance, peopleScaleRange, randomYRotation: true);

        // Assign people to drones
        AssignPeopleToDrones();
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

                if (useExclusion && InExclusion(pos))
                {
                    continue;
                }

                bool blocked = Physics.CheckSphere(pos, minDistance, overlapMask, QueryTriggerInteraction.Ignore);
                if (!blocked)
                {
                    var prefab = prefabs[Random.Range(0, prefabs.Length)];
                    Quaternion rot = randomYRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
                    var go = Instantiate(prefab, pos, rot, parent);

                    SetLayerRecursive(go, LayerMask.NameToLayer("Obstacles"));

                    float scale = Random.Range(scaleRange.x, scaleRange.y);
                    go.transform.localScale = go.transform.localScale * scale;

                    // If it has no collider
                    if (!go.GetComponentInChildren<Collider>())
                    {
                        // Add a non-convex MeshCollider to every MeshFilter under this prefab (static obstacles)
                        var mfs = go.GetComponentsInChildren<MeshFilter>(includeInactive: true);
                        foreach (var mf in mfs)
                        {
                            var mc = mf.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = false;
                        }
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

    void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    Vector3 RandomPointInArea()
    {
        float halfX = size * 0.5f;
        float halfZ = size * 0.5f;
        float x = Random.Range(-halfX, halfX);
        float z = Random.Range(-halfZ, halfZ);
        return new Vector3(transform.position.x + x, yGround, transform.position.z + z);
    }

    bool InExclusion(Vector3 pos)
    {
        Vector2 p = new Vector2(pos.x, pos.z);
        Vector2 c = new Vector2(excludeCenter.x, excludeCenter.z);
        return Vector2.Distance(p, c) < excludeRadius;
    }

    Vector3 RandomPointInOpenArea()
    {
        // Generate people on more open zones
        float halfX = size * 0.3f;
        float halfZ = size * 0.3f;
        
        // Create more "open" zones
        Vector3[] openZones = new Vector3[]
        {
            new Vector3(transform.position.x - size * 0.2f, yGround, transform.position.z - size * 0.2f),
            new Vector3(transform.position.x + size * 0.2f, yGround, transform.position.z - size * 0.2f),
            new Vector3(transform.position.x, yGround, transform.position.z + size * 0.2f)
        };
        
        Vector3 basePos = openZones[Random.Range(0, openZones.Length)];
        float x = Random.Range(-halfX, halfX);
        float z = Random.Range(-halfZ, halfZ);
        
        return new Vector3(basePos.x + x, yGround, basePos.z + z);
    }

    void SpawnPeople(GameObject[] prefabs, int count, float minDistance, Vector2 scaleRange, bool randomYRotation)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0) return;

        generatedPeople.Clear();

        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int tries = 0; tries < maxTriesPerSpawn; tries++)
            {
                Vector3 pos = RandomPointInOpenArea(); // Usar función mejorada
                if (useExclusion && InExclusion(pos))
                {
                    continue;
                }
                bool blocked = Physics.CheckSphere(pos, minDistance, overlapMask, QueryTriggerInteraction.Ignore);
                if (!blocked)
                {
                    var prefab = prefabs[i];
                    Quaternion rot = randomYRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
                    var go = Instantiate(prefab, pos, rot, parent);

                    SetLayerRecursive(go, LayerMask.NameToLayer("Person"));

                    float scale = Random.Range(scaleRange.x, scaleRange.y);
                    go.transform.localScale = go.transform.localScale * scale;

                    go.tag = "Person";

                    // If it has no collider, add a small trigger sphere so future placements can "see" it
                    if (!go.GetComponent<Collider>())
                    {
                        var sc = go.AddComponent<SphereCollider>();
                        sc.isTrigger = true;
                        sc.radius = minDistance * 0.5f;
                    }

                    generatedPeople.Add(go);
                    Debug.Log($"Person {i+1} generated at: {pos}");

                    placed = true;
                    break;
                }
            }
            if (!placed)
            {
                Debug.LogWarning($"Could not place a person after {maxTriesPerSpawn} tries.");
            }
        }
        
        Debug.Log($"Total of generated people: {generatedPeople.Count}");
    }

    void AssignPeopleToDrones()
    {
        DroneTargetAssigner assigner = FindFirstObjectByType<DroneTargetAssigner>();
        if (assigner != null)
        {
            assigner.AssignTargets(generatedPeople);
        }
    }
}