using UnityEngine;
using System.Collections.Generic; // 引入List需要的命名空间

public class MultiZoneCityGenerator : MonoBehaviour
{
    // --- 定义一个简单的配置类，让设计师在Inspector里填 ---
    [System.Serializable]
    public class GenerationZone
    {
        public string zoneName = "Area 1"; // 给区域起个名，方便管理
        public Transform originPoint;      // 【核心】拖入一个空物体，用它的位置作为起点
        public int width = 20;             // 这个特定区域的宽
        public int height = 20;            // 这个特定区域的高
    }

    [Header("Global Settings")]
    public float cellSize = 10.0f;     // 格子大小（所有区域通用）
    public LayerMask roadLayer;        // 道路层级
    public GameObject[] buildingPrefabs; // 建筑列表
    public float buildingYOffset = 0.0f;

    [Header("Zones Configuration")]
    // 这里就是设计师填“想要几个框”的地方
    public List<GenerationZone> zones;

    void Start()
    {
        // 遍历设计师填的所有区域，一个一个处理
        foreach (var zone in zones)
        {
            if (zone.originPoint != null)
            {
                GenerateOneZone(zone);
            }
        }
    }

    // 将原本的逻辑封装成一个独立的函数，传入具体的区域配置
    void GenerateOneZone(GenerationZone zone)
    {
        // 1. 为当前这个区域创建独立的地图数据
        int[,] mapData = new int[zone.width, zone.height];
        Vector3 startPos = zone.originPoint.position; // 使用该区域锚点的位置

        // --- 扫描阶段 ---
        for (int x = 0; x < zone.width; x++)
        {
            for (int z = 0; z < zone.height; z++)
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

        // --- 生成阶段 ---
        bool[,] visited = new bool[zone.width, zone.height];

        // 安全检查
        if (buildingPrefabs == null || buildingPrefabs.Length == 0) return;

        for (int x = 0; x < zone.width; x++)
        {
            for (int z = 0; z < zone.height; z++)
            {
                if (mapData[x, z] == 1 || visited[x, z]) continue;

                // 计算Block大小
                int blockW = 0;
                while ((x + blockW) < zone.width && mapData[x + blockW, z] == 0) blockW++;

                int blockH = 0;
                while ((z + blockH) < zone.height && mapData[x, z + blockH] == 0) blockH++;

                // 标记已访问
                for (int i = 0; i < blockW; i++)
                {
                    for (int j = 0; j < blockH; j++)
                    {
                        visited[x + i, z + j] = true;
                    }
                }

                // 计算中心并生成
                float centerXIndex = x + blockW / 2.0f;
                float centerZIndex = z + blockH / 2.0f;

                float worldX = startPos.x + centerXIndex * cellSize;
                float worldZ = startPos.z + centerZIndex * cellSize;

                Vector3 spawnPos = new Vector3(worldX, startPos.y + buildingYOffset, worldZ);

                int randomIndex = Random.Range(0, buildingPrefabs.Length);
                GameObject selectedPrefab = buildingPrefabs[randomIndex];

                if (selectedPrefab != null)
                {
                    // 把生成的建筑设为对应“锚点”的子物体，这样结构更清晰
                    Instantiate(selectedPrefab, spawnPos, Quaternion.identity, zone.originPoint);
                }
            }
        }
    }

    // --- 可视化：把每个区域的黄框都画出来 ---
    void OnDrawGizmos()
    {
        if (zones == null) return;

        Gizmos.color = Color.yellow;
        foreach (var zone in zones)
        {
            if (zone.originPoint != null)
            {
                Vector3 center = zone.originPoint.position +
                                 new Vector3(zone.width * cellSize * 0.5f, 0, zone.height * cellSize * 0.5f);
                Vector3 size = new Vector3(zone.width * cellSize, 1f, zone.height * cellSize);

                Gizmos.DrawWireCube(center, size);

                // 顺便在左下角画个小球，标记这是起点
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(zone.originPoint.position, 1f);
                Gizmos.color = Color.yellow; // 还原颜色
            }
        }
    }
}