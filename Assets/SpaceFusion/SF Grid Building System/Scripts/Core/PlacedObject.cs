using System;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.SaveSystem;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.UI; // 引入 UI 命名空间 (Tooltip用)
using SpaceFusion.SF_Grid_Building_System.Scripts.Core; // 引入 BuildingType (BuildingEffect用)
using System.Text; // StringBuilder
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

        // --- 修复：添加此方法以支持存档加载 ---
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

        // --- 鼠标悬停显示信息逻辑 (Turn 13/14 Added) ---
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
            if (buildingEffect == null) return "No Effect";

            StringBuilder sb = new StringBuilder();
            BuildingType type = buildingEffect.type;

            float energy = buildingEffect.GetCurrentElectricity();
            if (Mathf.Abs(energy) > 0.01f)
            {
                // 注意：这里负数是产电，正数是耗电
                bool isProduction = energy < 0;
                string color = isProduction ? "<color=green>" : "<color=red>";
                string label = isProduction ? "Energy Production" : "Energy Consumption";
                sb.AppendLine($"{color}{label}: {Mathf.Abs(energy):F1}</color>");
            }

            float co2 = buildingEffect.GetCurrentCo2Change();
            if (Mathf.Abs(co2) > 0.01f)
            {
                bool isEmission = co2 > 0;
                string color = isEmission ? "<color=red>" : "<color=green>";
                string label = isEmission ? "CO2 Emission" : "CO2 Absorption";
                sb.AppendLine($"{color}{label}: {Mathf.Abs(co2):F1}</color>");
            }

            if (type == BuildingType.Farm)
            {
                float food = buildingEffect.GetCurrentFood();
                sb.AppendLine($"<color=green>Food Production: {food:F1}</color>");
            }
            if (type == BuildingType.House)
            {
                sb.AppendLine($"Population: {buildingEffect.initialPopulation} / {buildingEffect.populationCapacity}");
            }
            if (type == BuildingType.Institute)
            {
                sb.AppendLine($"<color=#00FFFF>Research: Active</color>");
            }
            if (type == BuildingType.Bank)
            {
                sb.AppendLine($"<color=#FFD700>Economy: Trade Center</color>");
            }
            if (type == BuildingType.Co2Storage)
            {
                sb.AppendLine($"<color=green>Status: Capturing</color>");
            }

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