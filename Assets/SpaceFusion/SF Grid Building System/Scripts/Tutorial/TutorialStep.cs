using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    [TextArea(3, 10)]
    public string instructionText;

    public enum StartCondition
    {
        Immediate,
        WaitForElectricityDeficit
    }

    [Header("Start Conditions")]
    public StartCondition startCondition = StartCondition.Immediate;

    [Header("Game State Control")]
    public bool shouldPauseGame = true;

    [Header("Completion - Building")]
    public bool requireBuilding = false;
    public bool allowAnyBuilding = false;
    public BuildingType targetBuildingType;

    [Header("Completion - Removal")]
    public bool requireRemoval = false;

    [Header("Completion - Simulation")]
    public bool requirePositiveEnergyBalance = false;

    [Header("Completion - Optimization (New)")]
    [Tooltip("如果勾选，将检查 LevelScenarioLoader 中的真实目标是否达成")]
    public bool requireOptimizationGoal = false; // <--- 新增字段

    [Header("Completion - Interaction")]
    public bool requireInput = false;
}