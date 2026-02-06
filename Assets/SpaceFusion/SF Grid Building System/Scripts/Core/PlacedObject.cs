using System;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.SaveSystem;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.UI;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public class PlacedObject : MonoBehaviour
    {
        public static Action<PlacedObject> holdComplete;
        public Placeable placeable;
        public BuildingEffect buildingEffect;

        private Vector3 _lastMousePosition;
        private Camera _sceneCamera;

        [ReadOnly()]
        public PlaceableObjectData data = new();

        private void OnEnable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLmbPress += HandleMousePress;
                InputManager.Instance.OnLmbHold += HandleMouseHold;
            }
            GetCamera();
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLmbPress -= HandleMousePress;
                InputManager.Instance.OnLmbHold -= HandleMouseHold;
            }
        }

        private void GetCamera()
        {
            if (_sceneCamera != null) return;
            if (GameManager.Instance != null && GameManager.Instance.SceneCamera != null)
            {
                _sceneCamera = GameManager.Instance.SceneCamera;
            }
            else
            {
                _sceneCamera = Camera.main;
            }
        }

        public void Initialize(Placeable scriptable, Vector3Int gridPosition)
        {
            placeable = scriptable;
            data.assetIdentifier = scriptable.GetAssetIdentifier();
            data.gridPosition = gridPosition;
            data.guid = Guid.NewGuid().ToString();
        }

        public void InitializeLoadedData(Placeable scriptable, PlaceableObjectData podata)
        {
            placeable = scriptable;
            data = podata;
        }

        private void HandleMousePress(Vector2 mousePosition)
        {
            _lastMousePosition = mousePosition;
        }

        private void HandleMouseHold(Vector2 mousePosition)
        {
            if (_sceneCamera == null) GetCamera();
            if (IsRaycastOnObject(_lastMousePosition) && IsRaycastOnObject(mousePosition))
            {
                OnObjectHoldComplete();
            }
        }

        private bool IsRaycastOnObject(Vector3 mousePosition)
        {
            if (_sceneCamera == null) return false;
            var ray = _sceneCamera.ScreenPointToRay(mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnObjectHoldComplete()
        {
            holdComplete?.Invoke(this);
        }

        private void OnMouseEnter()
        {
            if (InputManager.IsPointerOverUIObject() || PlacementSystem.Instance == null) return;
            if (BuildingInfoUI.Instance != null)
            {
                string info = GetBuildingStats();
                BuildingInfoUI.Instance.Show(placeable.GetAssetIdentifier(), info);
            }
        }

        private void OnMouseExit()
        {
            if (BuildingInfoUI.Instance != null) BuildingInfoUI.Instance.Hide();
        }

        private string GetBuildingStats()
        {
            StringBuilder sb = new StringBuilder();

            // 1. 处理普通建筑逻辑
            if (buildingEffect != null)
            {
                // 使用你新定义的通用变量名
                float energy = buildingEffect.electricityChange;
                if (Mathf.Abs(energy) > 0.01f)
                {
                    bool isProduction = energy < 0; // 负数发电
                    string color = isProduction ? "<color=green>" : "<color=red>";
                    string label = isProduction ? "Energy Production" : "Energy Consumption";
                    sb.AppendLine($"{color}{label}: {Mathf.Abs(energy):F1}</color>");
                }

                float co2 = buildingEffect.co2Change;
                if (Mathf.Abs(co2) > 0.01f)
                {
                    bool isEmission = co2 > 0; // 正数排放
                    string color = isEmission ? "<color=red>" : "<color=green>";
                    string label = isEmission ? "CO2 Emission" : "CO2 Absorption";
                    sb.AppendLine($"{color}{label}: {Mathf.Abs(co2):F1}</color>");
                }

                // 保留其他特定职能显示
                if (buildingEffect.type == BuildingType.Farm)
                    sb.AppendLine($"<color=green>Food Production: {buildingEffect.foodProduction:F1}</color>");

                if (buildingEffect.type == BuildingType.House)
                    sb.AppendLine($"Population: {buildingEffect.initialPopulation} / {buildingEffect.populationCapacity}");
            }

            // 2. 处理教学建筑逻辑
            var tutorialEffect = GetComponent<TutorialBuildingEffect>();
            if (tutorialEffect != null)
            {
                // 同样适配新的变量名 electricityChange 和 co2Change
                if (Mathf.Abs(tutorialEffect.electricityChange) > 0.01f)
                {
                    bool isGen = tutorialEffect.electricityChange < 0;
                    string prefix = isGen ? "+" : "-";
                    sb.AppendLine($"<color=yellow>Tutorial Energy: {prefix}{Mathf.Abs(tutorialEffect.electricityChange):F1}</color>");
                }

                if (Mathf.Abs(tutorialEffect.co2Change) > 0.01f)
                {
                    bool isEmit = tutorialEffect.co2Change > 0;
                    string label = isEmit ? "Emit" : "Absorb";
                    sb.AppendLine($"<color=#FFA500>Tutorial CO2 {label}: {Mathf.Abs(tutorialEffect.co2Change):F1}</color>");
                }
            }

            if (sb.Length == 0) return "No Effect";
            return sb.ToString();
        }

        public void RemoveFromSaveData()
        {
            GameManager.Instance?.saveData.RemoveData(data);
        }

        private void OnApplicationQuit()
        {
            GameManager.Instance?.saveData.AddData(data);
        }
    }
}