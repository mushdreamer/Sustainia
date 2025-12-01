using UnityEngine;

public class ScannedCityGenerator : MonoBehaviour
{
    [Header("Scanning Settings")]
    public int gridWidth = 20;       // 扫描多少个格子宽
    public int gridHeight = 20;      // 扫描多少个格子高
    public float cellSize = 10.0f;   // 每个格子代表世界坐标多少米
    public LayerMask roadLayer;      // 务必把你的道路物体设置到一个专门的Layer

    [Header("Generation Settings")]
    // *** 修改点 1: 这里从单个GameObject变成了数组，允许你拖入多个建筑 ***
    public GameObject[] buildingPrefabs;

    public float buildingYOffset = 0.0f; // 建筑生成的高度偏移

    // 内存中的地图数据：0=空地, 1=道路
    private int[,] mapData;

    void Start()
    {
        ScanMap();
        GenerateBuildingsInCenters();
    }

    void ScanMap()
    {
        mapData = new int[gridWidth, gridHeight];
        Vector3 startPos = transform.position;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                float worldX = startPos.x + x * cellSize + cellSize * 0.5f;
                float worldZ = startPos.z + z * cellSize + cellSize * 0.5f;

                Vector3 rayOrigin = new Vector3(worldX, 100f, worldZ);
                if (Physics.Raycast(rayOrigin, Vector3.down, 200f, roadLayer))
                {
                    mapData[x, z] = 1;
                }
                else
                {
                    mapData[x, z] = 0;
                }
            }
        }
    }

    void GenerateBuildingsInCenters()
    {
        bool[,] visited = new bool[gridWidth, gridHeight];
        Vector3 startPos = transform.position;

        // 安全检查：防止你忘了在Inspector里拖建筑导致报错
        if (buildingPrefabs == null || buildingPrefabs.Length == 0)
        {
            Debug.LogError("请在Inspector面板的 Building Prefabs 数组中添加至少一个建筑预制体！");
            return;
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                if (mapData[x, z] == 1 || visited[x, z]) continue;

                // --- 发现新街区 ---

                int blockW = 0;
                while ((x + blockW) < gridWidth && mapData[x + blockW, z] == 0)
                {
                    blockW++;
                }

                int blockH = 0;
                while ((z + blockH) < gridHeight && mapData[x, z + blockH] == 0)
                {
                    blockH++;
                }

                for (int i = 0; i < blockW; i++)
                {
                    for (int j = 0; j < blockH; j++)
                    {
                        visited[x + i, z + j] = true;
                    }
                }

                // 计算中心
                float centerXIndex = x + blockW / 2.0f;
                float centerZIndex = z + blockH / 2.0f;

                float worldX = startPos.x + centerXIndex * cellSize;
                float worldZ = startPos.z + centerZIndex * cellSize;

                Vector3 spawnPos = new Vector3(worldX, startPos.y + buildingYOffset, worldZ);

                // *** 修改点 2: 随机抽取逻辑 ***
                // Random.Range(min, max) 对于整数来说是“包头不包尾”的，所以用 Length 是安全的
                int randomIndex = Random.Range(0, buildingPrefabs.Length);
                GameObject selectedPrefab = buildingPrefabs[randomIndex];

                if (selectedPrefab != null)
                {
                    Instantiate(selectedPrefab, spawnPos, Quaternion.identity, transform);
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 startPos = transform.position;
        Gizmos.DrawWireCube(
            new Vector3(startPos.x + gridWidth * cellSize * 0.5f, startPos.y, startPos.z + gridHeight * cellSize * 0.5f),
            new Vector3(gridWidth * cellSize, 1f, gridHeight * cellSize)
        );
    }
}