using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.SaveSystem;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.UI;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using System;
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
            // 1. 安全检查：确保 InputManager 存在
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLmbPress += HandleMousePress;
                InputManager.Instance.OnLmbHold += HandleMouseHold;
                Debug.Log($"[PlacedObject] 已启用并订阅输入事件: {gameObject.name}");
            }
            else
            {
                Debug.LogError($"[PlacedObject] InputManager.Instance 为空！无法订阅事件: {gameObject.name}");
            }

            // 尝试获取相机
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

        // 增强的获取相机方法
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

            if (_sceneCamera == null)
            {
                Debug.LogWarning($"[PlacedObject] 警告: {gameObject.name} 找不到 SceneCamera 或 MainCamera！射线检测将失败。");
            }
        }

        public void Initialize(Placeable scriptable, Vector3Int gridPosition)
        {
            placeable = scriptable;
            data.assetIdentifier = scriptable.GetAssetIdentifier();
            data.gridPosition = gridPosition;
            data.guid = Guid.NewGuid().ToString();
        }

        public void Initialize(PlaceableObjectData podata)
        {
            data = podata;
        }

        private void OnMouseEnter()
        {
            // 如果处于放置状态（手上正拿着东西准备造），或者是点击了UI，就不显示建筑信息
            if (InputManager.IsPointerOverUIObject() || PlacementSystem.Instance == null) return;

            // 只有当 UI 单例存在时才显示
            if (BuildingInfoUI.Instance != null)
            {
                string info = GetBuildingStats();
                BuildingInfoUI.Instance.Show(placeable.GetAssetIdentifier(), info);
            }
        }

        private void OnMouseExit()
        {
            if (BuildingInfoUI.Instance != null)
            {
                BuildingInfoUI.Instance.Hide();
            }
        }

        private string GetBuildingStats()
        {
            if (buildingEffect == null) return "No Effect";

            StringBuilder sb = new StringBuilder();
            BuildingType type = buildingEffect.type; // 获取当前建筑类型

            // --- 1. 电力显示逻辑 ---
            // 几乎所有建筑都耗电，或者产电，所以这部分通常可以保留
            // 但如果你希望有些装饰性建筑不显示，也可以加 if 限制
            float energy = buildingEffect.GetCurrentElectricity();
            if (Mathf.Abs(energy) > 0.01f) // 使用微小阈值防止浮点数误差
            {
                // 负数代表产电（在 BuildingEffect.cs 的 PowerPlant 逻辑中是负数）
                // 正数代表耗电
                bool isProduction = energy < 0;
                string color = isProduction ? "<color=green>" : "<color=red>";
                string label = isProduction ? "Energy Production" : "Energy Consumption";
                sb.AppendLine($"{color}{label}: {Mathf.Abs(energy):F1}</color>");
            }

            // --- 2. CO2 显示逻辑 ---
            // 只有特定建筑会产生或吸收 CO2
            float co2 = buildingEffect.GetCurrentCo2Change();
            if (Mathf.Abs(co2) > 0.01f)
            {
                bool isEmission = co2 > 0;
                string color = isEmission ? "<color=red>" : "<color=green>";
                string label = isEmission ? "CO2 Emission" : "CO2 Absorption";
                sb.AppendLine($"{color}{label}: {Mathf.Abs(co2):F1}</color>");
            }

            // --- 3. 食物 (仅限 Farm) ---
            if (type == BuildingType.Farm)
            {
                float food = buildingEffect.GetCurrentFood();
                sb.AppendLine($"<color=green>Food Production: {food:F1}</color>");
            }

            // --- 4. 人口 (仅限 House) ---
            if (type == BuildingType.House)
            {
                sb.AppendLine($"Population: {buildingEffect.initialPopulation} / {buildingEffect.populationCapacity}");
            }

            // --- 5. 科研 (仅限 Institute) ---
            if (type == BuildingType.Institute)
            {
                sb.AppendLine($"<color=#00FFFF>Research: Active</color>"); // 使用青色高亮
            }

            // --- 6. 银行 (仅限 Bank) ---
            if (type == BuildingType.Bank)
            {
                sb.AppendLine($"<color=#FFD700>Economy: Trade Center</color>"); // 使用金色高亮
            }

            // --- 7. CCUS (仅限 Co2Storage) ---
            // 虽然上面有 CO2 显示逻辑，但这里可以加个额外说明
            if (type == BuildingType.Co2Storage)
            {
                sb.AppendLine($"<color=green>Status: Capturing</color>");
            }

            return sb.ToString();
        }

        private void HandleMousePress(Vector2 mousePosition)
        {
            _lastMousePosition = mousePosition;
            // Debug.Log($"[PlacedObject] 按下: {gameObject.name}"); // 调试用，如果太吵可以注释掉
        }

        private void HandleMouseHold(Vector2 mousePosition)
        {
            // 每次操作前确保有相机
            if (_sceneCamera == null) GetCamera();

            // 检查按下位置和当前位置是否都在物体上
            bool startOnObject = IsRaycastOnObject(_lastMousePosition);
            bool currentOnObject = IsRaycastOnObject(mousePosition);

            if (startOnObject && currentOnObject)
            {
                Debug.Log($"[PlacedObject] 长按成功触发: {gameObject.name}");
                OnObjectHoldComplete();
            }
        }

        private bool IsRaycastOnObject(Vector3 mousePosition)
        {
            if (_sceneCamera == null) return false;

            var ray = _sceneCamera.ScreenPointToRay(mousePosition);

            // 使用 RaycastAll 穿透地板
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