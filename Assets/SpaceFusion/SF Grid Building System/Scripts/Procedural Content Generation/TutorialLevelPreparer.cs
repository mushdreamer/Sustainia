using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class TutorialLevelPreparer : MonoBehaviour
{
    public static TutorialLevelPreparer Instance;
    private void Awake() { Instance = this; }

    public void PrepareLayoutForEvent(int eventIndex)
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        if (eventIndex == 1)
        {
            // 1. 生成电网输入 (Grid Input: +60)
            // 这里的名称必须与你 Inspector 里的 Placeable 资产名称完全一致
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, "PowerPlant");

            // 2. 生成本地发电 (Local Generation: +20)
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, "LocalGenration");

            // 3. 生成住宅消耗 (Demand: -40)
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(2, "House");

            // 4. 生成储能电池 (Battery Storage)
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(3, "Battery");
        }
    }
}