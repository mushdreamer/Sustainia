using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    [TextArea(3, 10)]
    public string instructionText;

    [Header("Clean Slate Logic")]
    [Tooltip("进入此步骤前是否清空场景所有建筑")]
    public bool clearSceneBeforeStart = false;
    [Tooltip("清空后加载的布局索引 (对应 Preparer 中的 eventIndex)")]
    public int layoutToLoad = -1;

    [Header("Camera Control")]
    [Tooltip("镜头聚焦的目标建筑或位置")]
    public GameObject focusTarget;
    [Tooltip("镜头距离目标的远近")]
    public float cameraDistance = 15f;
    [Tooltip("镜头的俯视角度")]
    public float cameraAngle = 45f;

    [Header("Visual Hint")]
    [Tooltip("是否在该步骤显示视觉指引标志")]
    public bool showIndicator = true;
    [Tooltip("视觉指引标志的颜色")]
    public Color indicatorColor = Color.yellow;

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
    [Tooltip("是否为教程专用建筑类型")]
    public bool isTutorialBuilding = false;
    public BuildingType targetBuildingType;
    [Tooltip("如果勾选了 isTutorialBuilding，请在此选择教程建筑类型")]
    public TutorialBuildingType targetTutorialType;

    [Header("Completion - Removal")]
    public bool requireRemoval = false;

    [Header("Completion - Simulation")]
    public bool requirePositiveEnergyBalance = false;

    [Header("Completion - Optimization (New)")]
    [Tooltip("如果勾选，将检查 LevelScenarioLoader 中的真实目标是否达成")]
    public bool requireOptimizationGoal = false;

    [Header("Completion - Interaction")]
    public bool requireInput = false;
}