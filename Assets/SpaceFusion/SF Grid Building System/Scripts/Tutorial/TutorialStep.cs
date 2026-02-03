using UnityEngine;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    [System.Serializable]
    public class BuildingRequirement
    {
        public bool isTutorialBuilding;
        [Tooltip("如果是普通建筑，请选择类型")]
        public BuildingType normalType;
        [Tooltip("如果是教程建筑，请选择类型")]
        public TutorialBuildingType tutorialType;
        [Tooltip("该种类建筑需要达到的目标数量")]
        public int requiredCount = 1;
    }
}

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

    [Header("Completion - Building (Flexible)")]
    public bool requireBuilding = false;
    [Tooltip("如果勾选：只要列表内所有建筑的总数达标即可。如果不勾选：列表内每一项都必须分别达标。")]
    public bool allowAnyCombination = false;
    [Tooltip("支持混合配置多种建筑及其数量要求")]
    public List<BuildingRequirement> targetBuildings;

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