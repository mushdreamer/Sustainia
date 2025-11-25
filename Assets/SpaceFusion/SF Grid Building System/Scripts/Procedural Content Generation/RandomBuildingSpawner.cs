using UnityEngine;

public class RandomBuildingSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject[] buildingPrefabs; // 把你的建筑Prefab拖进去
    public int spawnCount = 10;          // 生成数量
    public Vector2 spawnAreaSize = new Vector2(20, 20); // 这里的数值代表框的长宽

    [Header("Overlap Detector")]
    public float checkRadius = 3.0f;     // 检测半径
    public LayerMask obstacleLayer;      // 记得设置层级
    public int maxAttempts = 10;         // 最大尝试次数

    void Start()
    {
        SpawnBuildings();
    }

    void SpawnBuildings()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            // 初始化一个随机位置变量
            Vector3 randomPos = Vector3.zero;
            bool validPositionFound = false;

            // 尝试寻找空位
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 核心修改：基于当前物体的位置 (transform.position) 进行偏移
                // 这样你拖动这个物体，生成区域就会跟着跑
                float randomX = transform.position.x + Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2);
                float randomZ = transform.position.z + Random.Range(-spawnAreaSize.y / 2, spawnAreaSize.y / 2);

                // 假设建筑生成在和当前物体一样的高度 (transform.position.y)
                Vector3 candidatePos = new Vector3(randomX, transform.position.y, randomZ);

                // 防重叠检测
                if (!Physics.CheckSphere(candidatePos, checkRadius, obstacleLayer))
                {
                    randomPos = candidatePos;
                    validPositionFound = true;
                    break;
                }
            }

            if (validPositionFound)
            {
                GameObject prefabToSpawn = buildingPrefabs[Random.Range(0, buildingPrefabs.Length)];
                GameObject newBuilding = Instantiate(prefabToSpawn, randomPos, Quaternion.identity);

                // 依然把生成的建筑设为子物体，方便管理
                newBuilding.transform.SetParent(this.transform);
            }
        }
    }

    // 可视化辅助框：能在Scene窗口看到绿色的生成范围
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f); // 半透明绿色
        // 绘制一个以当前物体为中心的立方体框
        Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize.x, 1, spawnAreaSize.y));
    }
}