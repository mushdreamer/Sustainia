using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    [TextArea(3, 10)]
    public string instructionText;

    // --- 新增：触发条件 ---
    public enum StartCondition
    {
        Immediate,
        WaitForElectricityDeficit
    }

    [Header("Start Conditions")]
    public StartCondition startCondition = StartCondition.Immediate;

    [Header("Game State Control")]
    [Tooltip("此步骤是否需要暂停游戏资源模拟？(默认True，对于需要观察数值变化的步骤可设为False)")]
    public bool shouldPauseGame = true; // <--- 新增字段

    [Header("Completion - Building")]
    public bool requireBuilding = false;
    public bool allowAnyBuilding = false;
    public BuildingType targetBuildingType;

    [Header("Completion - Removal")]
    public bool requireRemoval = false;

    [Header("Completion - Simulation")]
    public bool requirePositiveEnergyBalance = false;

    [Header("Completion - Interaction")]
    public bool requireInput = false;
}