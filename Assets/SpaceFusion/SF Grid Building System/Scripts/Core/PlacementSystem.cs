using System;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using SpaceFusion.SF_Grid_Building_System.Scripts.Interfaces;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.PlacementStates;
using SpaceFusion.SF_Grid_Building_System.Scripts.SaveSystem;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public class PlacementSystem : MonoBehaviour
    {
        public static PlacementSystem Instance;

        [SerializeField]
        private PreviewSystem previewSystem;

        [SerializeField]
        private PlacementHandler placementHandler;

        public event Action OnPlacementStateStart;
        public event Action OnPlacementStateEnd;

        private readonly Dictionary<GridDataType, GridData> _gridDataMap = new();
        private Vector3Int _lastDetectedPosition = Vector3Int.zero;
        private IPlacementState _stateHandler;
        private InputManager _inputManager;
        private GameConfig _gameConfig;
        private PlaceableObjectDatabase _database;

        private PlacementGrid _grid;
        private bool _stopStateAfterAction;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); }
            Instance = this;
        }

        public void Initialize(PlacementGrid grid)
        {
            _grid = grid;
            _gameConfig = GameConfig.Instance;
            _database = _gameConfig.PlaceableObjectDatabase;
            _inputManager = InputManager.Instance;
            foreach (GridDataType gridType in Enum.GetValues(typeof(GridDataType)))
            {
                _gridDataMap[gridType] = new GridData();
            }

            StopState();
            // 扫描场景中的现有建筑（可选，用于Reload）
            ScanAndRegisterSceneObjects();
        }

        private void ScanAndRegisterSceneObjects()
        {
            BuildingEffect[] sceneBuildings = FindObjectsOfType<BuildingEffect>();
            foreach (var building in sceneBuildings)
            {
                PlacedObject existingPO = building.GetComponent<PlacedObject>();
                if (existingPO != null && !string.IsNullOrEmpty(existingPO.data.guid)) continue;

                string cleanName = building.gameObject.name.Replace("(Clone)", "").Trim();
                Placeable data = _database.GetPlaceable(cleanName);

                if (data == null) continue;

                Vector3Int gridPos = _grid.WorldToCell(building.transform.position);
                RegisterExternalObject(building.gameObject, data, gridPos);
            }
        }

        /// <summary>
        /// 供 PCG 调用：将物体注册进网格数据，使其可被选中/移除
        /// </summary>
        public void RegisterExternalObject(GameObject obj, Placeable data, Vector3Int gridPos)
        {
            // 1. 生成 GUID 并注册到 Handler
            string guid = placementHandler.RegisterPrePlacedObject(obj, gridPos, data);

            // 2. 标记网格占用
            if (_gridDataMap.ContainsKey(data.GridType))
            {
                try
                {
                    Vector2Int occupiedCells = Vector2Int.RoundToInt(data.Size);
                    _gridDataMap[data.GridType].Add(gridPos, occupiedCells, data.GetAssetIdentifier(), guid);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Grid occupied warning at {gridPos}: {e.Message}");
                }
            }
        }

        public void InitializeLoadedObject(PlaceableObjectData podata)
        {
            _stateHandler = new LoadedObjectPlacementState(podata, _grid, _database, _gridDataMap, placementHandler);
            _stateHandler.OnAction(podata.gridPosition);
            _stateHandler = null;
        }

        public void StartPlacement(string assetIdentifier)
        {
            StopState();
            _grid.SetVisualizationState(true);
            _stateHandler = new PlacementState(assetIdentifier, _grid, previewSystem, _database, _gridDataMap, placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnRotate += RotateStructure;
            OnPlacementStateStart?.Invoke();
        }

        public void StartRemoving(GridDataType gridType)
        {
            StopState();
            _grid.SetVisualizationState(true);
            _stateHandler = new RemoveState(_grid, previewSystem, _gridDataMap[gridType], placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnExit += ObjectGrouper.Instance.DisplayAll;
            ObjectGrouper.Instance.DisplayOnlyObjectsOfSelectedGridType(gridType);
        }

        public void StartRemovingAll()
        {
            StopState();
            _grid.SetVisualizationState(true);
            _stateHandler = new RemoveAllState(_grid, previewSystem, _gridDataMap, placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnExit += ObjectGrouper.Instance.DisplayAll;
            ObjectGrouper.Instance.DisplayAll();
        }

        public void Remove(PlacedObject placedObject)
        {
            var gridType = placedObject.placeable.GridType;
            StopState();
            _stateHandler = new RemoveState(_grid, previewSystem, _gridDataMap[gridType], placementHandler);
            _stateHandler.OnAction(placedObject.data.gridPosition);
            _stateHandler.EndState();
            _stateHandler = null;
        }

        public void StartMoving(PlacedObject target)
        {
            StopState();
            _stopStateAfterAction = true;
            _grid.SetVisualizationState(true);
            _stateHandler = new MovingState(target, _grid, previewSystem, _gridDataMap, placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnRotate += RotateStructure;
            OnPlacementStateStart?.Invoke();
        }

        public void StopState()
        {
            _grid.SetVisualizationState(false);
            if (_stateHandler == null) return;
            _stopStateAfterAction = false;
            _stateHandler.EndState();
            _inputManager.OnClicked -= StateAction;
            _inputManager.OnExit -= StopState;
            _inputManager.OnExit -= ObjectGrouper.Instance.DisplayAll;
            _inputManager.OnRotate -= RotateStructure;
            _lastDetectedPosition = Vector3Int.zero;
            _stateHandler = null;
            ObjectGrouper.Instance.DisplayAll();
            OnPlacementStateEnd?.Invoke();
        }

        private void StateAction()
        {
            if (InputManager.IsPointerOverUIObject()) return;
            var mousePosition = _inputManager.GetSelectedMapPosition();
            var gridPosition = _grid.WorldToCell(mousePosition);
            _stateHandler.OnAction(gridPosition);
            if (_stopStateAfterAction) StopState();
        }

        private void RotateStructure()
        {
            _stateHandler.OnRotation();
        }

        private void Update()
        {
            if (_stateHandler == null) return;
            var mousePosition = _inputManager.GetSelectedMapPosition();
            var gridPosition = _grid.WorldToCell(mousePosition);
            if (_lastDetectedPosition == gridPosition) return;
            _stateHandler.UpdateState(gridPosition);
            _lastDetectedPosition = gridPosition;
        }
    }
}