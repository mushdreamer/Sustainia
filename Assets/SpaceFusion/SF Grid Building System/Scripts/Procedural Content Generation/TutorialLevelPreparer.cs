using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class TutorialLevelPreparer : MonoBehaviour
{
    public static TutorialLevelPreparer Instance;
    private void Awake() { Instance = this; }

    public void ClearAllBuildings()
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        // 停止协程防止清理时产生竞态条件
        MultiZoneCityGenerator.Instance.StopAllCoroutines();

        // --- 核心修复：清理系统逻辑层数据 ---
        // 1. 清理网格占用数据
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.ResetAllGridData();
        }

        // 2. 清理 ResourceManager 里的所有建筑实例引用（包括普通和教程建筑）
        if (ResourceManager.Instance != null)
        {
            // 清理普通建筑数值
            var allBuildings = ResourceManager.Instance.GetAllPlacedBuildings();
            foreach (var b in allBuildings)
            {
                if (b != null) b.RemoveEffect();
            }

            // --- 新增：清理教程专用建筑数值 ---
            // 假设你的 ResourceManager 有提供获取教程建筑列表的方法，或者我们通过下面的物理清理触发 OnDestroy
        }

        // 3. 遍历并重置所有 Zone 的状态和物理物体
        foreach (var zone in MultiZoneCityGenerator.Instance.zones)
        {
            zone.isOccupied = false;
            if (zone.originPoint == null) continue;

            foreach (Transform child in zone.originPoint)
            {
                // 排除 UI 装饰物，销毁所有建筑物体
                if (child.name != "RingOutline" && child.name != "StatusLabel" && child.name != "ArrowIndicator")
                {
                    // 物理销毁会触发 BuildingEffect 或 TutorialBuildingEffect 的 OnDestroy
                    Destroy(child.gameObject);
                }
            }
        }

        // 4. 彻底清除残留（防止有建筑不在 Zone 层级下）
        // 清理普通建筑
        BuildingEffect[] leftovers = Object.FindObjectsByType<BuildingEffect>(FindObjectsSortMode.None);
        foreach (var b in leftovers) { b.RemoveEffect(); Destroy(b.gameObject); }

        // --- 核心修复点：显式清理教程建筑残留 ---
        TutorialBuildingEffect[] tutorialLeftovers = Object.FindObjectsByType<TutorialBuildingEffect>(FindObjectsSortMode.None);
        foreach (var tb in tutorialLeftovers)
        {
            // 物理销毁，触发其内部的 Remove 逻辑
            Destroy(tb.gameObject);
        }

        Debug.Log("<color=red>[Tutorial]</color> Scene cleared: Both normal and tutorial buildings removed.");
    }

    public void PrepareLayoutForEvent(int eventIndex)
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        // 在生成前确保上一个状态被完全清理
        ClearAllBuildings();

        if (eventIndex == 1)
        {
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, "PowerPlant");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, "LocalGeneration");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(2, "House T1");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(3, "Battery");
        }
        else if (eventIndex == 2)
        {
            // 生成全住宅场景 (Zone 0-15)
            int maxZones = Mathf.Min(16, MultiZoneCityGenerator.Instance.zones.Count);
            for (int i = 0; i < maxZones; i++)
            {
                MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(i, "House T1");
            }
            Debug.Log($"<color=cyan>[Tutorial]</color> Event 2: Generated {maxZones} Houses.");
        }
    }
}