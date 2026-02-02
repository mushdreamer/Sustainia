using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Configuration")]
    public bool enableTutorial = true;
    public List<TutorialStep> steps;

    [Header("References")]
    public TutorialUI tutorialUI;

    // 如果你有外部相机控制脚本，请将其拖入此处以在教程期间自动禁用冲突
    public MonoBehaviour externalCameraController;

    private int _currentStepIndex = 0;
    private bool _isTutorialActive = false;
    private bool _isWaitingForStartCondition = false;

    private GameObject _proceduralIndicator;
    private MeshRenderer _indicatorRenderer;
    private Coroutine _cameraCoroutine;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        CreateProceduralIndicator();
    }

    private void Start()
    {
        if (!enableTutorial)
        {
            if (tutorialUI) tutorialUI.Hide();
            return;
        }
        if (tutorialUI) tutorialUI.Initialize(this);
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced += CheckBuildingProgress;
            PlacementSystem.Instance.OnBuildingRemoved += CheckRemovalProgress;
        }
        StartTutorial();
    }

    private void CreateProceduralIndicator()
    {
        _proceduralIndicator = new GameObject("TutorialIndicator");
        MeshFilter meshFilter = _proceduralIndicator.AddComponent<MeshFilter>();
        _indicatorRenderer = _proceduralIndicator.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] { new Vector3(0, 1.5f, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, -1.5f, 0) };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
        _indicatorRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _proceduralIndicator.SetActive(false);
    }

    private void Update()
    {
        if (Debug.isDebugBuild || Application.isEditor)
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
                SkipCurrentStep();
            }
        }

        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        if (_proceduralIndicator.activeSelf)
        {
            float bounce = Mathf.Sin(Time.unscaledTime * 3f) * 0.5f;
            _proceduralIndicator.transform.Rotate(Vector3.up, 90f * Time.deltaTime);
            if (currentStep.focusTarget != null)
                _proceduralIndicator.transform.position = currentStep.focusTarget.transform.position + Vector3.up * (5f + bounce);
        }

        if (_isWaitingForStartCondition)
        {
            bool conditionMet = false;
            switch (currentStep.startCondition)
            {
                case TutorialStep.StartCondition.WaitForElectricityDeficit:
                    if (ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < -0.1f) conditionMet = true;
                    break;
                case TutorialStep.StartCondition.Immediate:
                default:
                    conditionMet = true;
                    break;
            }
            if (conditionMet) ActivateStepUI(currentStep);
        }
        else
        {
            bool stepComplete = false;
            if (currentStep.requirePositiveEnergyBalance && ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance >= 0f) stepComplete = true;
            if (currentStep.requireOptimizationGoal && LevelScenarioLoader.Instance != null && LevelScenarioLoader.Instance.IsOptimizationGoalMet()) stepComplete = true;
            if (stepComplete) NextStep();
        }
    }

    public void StartTutorial()
    {
        _isTutorialActive = true;
        _currentStepIndex = 0;
        PrepareStep(_currentStepIndex);
    }

    private void PrepareStep(int index)
    {
        if (index >= steps.Count) { CompleteTutorial(); return; }
        if (index == 0) TutorialLevelPreparer.Instance.PrepareLayoutForEvent(1);
        else if (index == 4) TutorialLevelPreparer.Instance.PrepareLayoutForEvent(2);
        TutorialStep step = steps[index];
        if (step.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
        {
            _isWaitingForStartCondition = true;
            tutorialUI.Hide();
            _proceduralIndicator.SetActive(false);
            if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        }
        else { ActivateStepUI(step); }
    }

    private void ActivateStepUI(TutorialStep step)
    {
        _isWaitingForStartCondition = false;
        tutorialUI.ShowStep(step);

        if (step.showIndicator && step.focusTarget != null)
        {
            _proceduralIndicator.SetActive(true);
            _indicatorRenderer.material.color = step.indicatorColor;
        }
        else { _proceduralIndicator.SetActive(false); }

        if (step.focusTarget != null)
        {
            if (_cameraCoroutine != null) StopCoroutine(_cameraCoroutine);
            _cameraCoroutine = StartCoroutine(MoveCameraSmoothly(step));
        }

        if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = step.shouldPauseGame;
    }

    private IEnumerator MoveCameraSmoothly(TutorialStep step)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) yield break;

        // 1. 锁定外部控制，防止抢镜头
        if (externalCameraController != null) externalCameraController.enabled = false;

        Vector3 targetPos = step.focusTarget.transform.position;
        float rad = step.cameraAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(0, Mathf.Sin(rad), -Mathf.Cos(rad)) * step.cameraDistance;
        Vector3 finalCamPos = targetPos + offset;

        float elapsed = 0;
        float duration = 1.0f;
        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;
        Quaternion finalRot = Quaternion.LookRotation(targetPos - finalCamPos);

        while (elapsed < duration)
        {
            mainCam.transform.position = Vector3.Lerp(startPos, finalCamPos, elapsed / duration);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, finalRot, elapsed / duration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        mainCam.transform.position = finalCamPos;
        mainCam.transform.rotation = finalRot;

        // 2. 如果这一步不需要玩家操作（只是纯文本交互），我们可以保持锁定。
        // 如果这一步需要玩家操作（如 Event 3 拆除建筑），可以尝试把当前的 finalCamPos 
        // 同步回你的外部控制器，这样玩家在接管时就不会产生坐标跳变。 [cite: 12-16]
}

    public void NextStep()
    {
        _currentStepIndex++;
        PrepareStep(_currentStepIndex);
    }

    // --- 需求功能：跳过当前步骤 ---
    public void SkipCurrentStep()
    {
        if (!_isTutorialActive) return;
        Debug.Log($"Skipping Step: {steps[_currentStepIndex].stepName}");
        NextStep();
    }

    private void CheckBuildingProgress(Placeable placedData)
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;
        TutorialStep currentStep = steps[_currentStepIndex];
        if (currentStep.requireBuilding && currentStep.allowAnyBuilding) Invoke(nameof(NextStep), 0.5f);
        else if (currentStep.requireBuilding && placedData?.Prefab?.GetComponent<BuildingEffect>()?.type == currentStep.targetBuildingType) Invoke(nameof(NextStep), 0.5f);
    }

    private void CheckRemovalProgress()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;
        if (steps[_currentStepIndex].requireRemoval) Invoke(nameof(NextStep), 0.5f);
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false;
        _proceduralIndicator.SetActive(false);
        tutorialUI.Hide();
        if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        if (externalCameraController != null) externalCameraController.enabled = true;
        Debug.Log("Tutorial Completed!");
    }

    private void OnDestroy()
    {
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced -= CheckBuildingProgress;
            PlacementSystem.Instance.OnBuildingRemoved -= CheckRemovalProgress;
        }
    }
}