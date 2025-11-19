using System;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.SaveSystem;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
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