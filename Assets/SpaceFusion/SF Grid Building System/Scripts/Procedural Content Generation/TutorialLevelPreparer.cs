using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class TutorialLevelPreparer : MonoBehaviour
{
    public static TutorialLevelPreparer Instance;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void PrepareLayoutForEvent(int eventIndex)
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        // 清理当前所有区域
        foreach (var zone in MultiZoneCityGenerator.Instance.zones)
        {
            zone.isOccupied = false;
            foreach (Transform child in zone.originPoint)
            {
                if (child.name != "RingOutline" && child.name != "StatusLabel" && child.name != "ArrowIndicator")
                    Destroy(child.gameObject);
            }
        }

        switch (eventIndex)
        {
            case 1: // Event 1: Energy Balance (60 + 20 - 40) [cite: 70-75]
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, BuildingType.PowerPlant); // Power Station (+60)
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, BuildingType.Co2Storage); // Battery/Storage (+20)
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(2, BuildingType.House);      // Residential Area (-40)
            break;

            case 2: // Event 2: State Accumulation (400 + 50 - 10) [cite: 76-80]
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, BuildingType.PowerPlant); // Emissions +50
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, BuildingType.Institute);  // Removals -10
            break;
            }
        }
    }