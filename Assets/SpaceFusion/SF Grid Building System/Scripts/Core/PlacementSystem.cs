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

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core {
    public class PlacementSystem : MonoBehaviour {
        public static PlacementSystem Instance;

        [SerializeField]
        private PreviewSystem previewSystem;

        [SerializeField]
        private PlacementHandler placementHandler;


        // EVENTS
        public event Action OnPlacementStateStart;
        public event Action OnPlacementStateEnd;

        private readonly Dictionary<GridDataType, GridData> _gridDataMap = new();
        private Vector3Int _lastDetectedPosition = Vector3Int.zero;
        private IPlacementState _stateHandler;
        private InputManager _inputManager;
        private GameConfig _gameConfig;
        private PlaceableObjectDatabase _database;

        // ---  在这里添加新代码  ---
        // 使用 AssetIdentifier (string) 作为键，来跟踪已建造的独特建筑
        private readonly HashSet<string> _uniqueBuildingsBuilt = new HashSet<string>();
        // --- 新代码结束  ---


        private PlacementGrid _grid;
        // !!!!some additional triggers to handle specific cases !!!
        // for moving state we want to stop immediately after action is executed
        // for remove and place we allow to have multi actions, so we don't need to select if after each action again
        private bool _stopStateAfterAction;

        /// <summary>
        /// Make singleton, we only need to have 1 placement system active at a time
        /// </summary>
        private void Awake() {
            if (Instance != null) {
                Destroy(this);
            }

            Instance = this;
        }


        public void Initialize(PlacementGrid grid) {
            _grid = grid;
            _gameConfig = GameConfig.Instance;
            _database = _gameConfig.PlaceableObjectDatabase;
            _inputManager = InputManager.Instance;
            foreach (GridDataType gridType in Enum.GetValues(typeof(GridDataType))) {
                // creates GridData for every possible gridType
                _gridDataMap[gridType] = new GridData();
            }

            StopState();

            // --- ADD THIS ---
            // 在系统初始化完毕后，扫描场景中已有的建筑
            ScanAndRegisterSceneObjects();
            // --- END ADD ---
        }

        // --- 在 PlacementSystem 类中添加此新方法 ---
        private void ScanAndRegisterSceneObjects()
        {
            // 1. 找到场景中所有挂载了 BuildingEffect 的物体
            BuildingEffect[] sceneBuildings = FindObjectsOfType<BuildingEffect>();

            foreach (var building in sceneBuildings)
            {
                // 如果这个物体已经有 GUID 且在字典里了，说明已经被注册过（防止重复）
                PlacedObject existingPO = building.GetComponent<PlacedObject>();
                if (existingPO != null && !string.IsNullOrEmpty(existingPO.data.guid)) continue;

                // 2. 尝试通过物体名称在数据库中找到对应的 Placeable 数据
                // 注意：Unity 生成的实例通常叫 "House(Clone)" 或者手动放的叫 "House"
                // 我们需要清理名称来匹配数据库的 ID
                string cleanName = building.gameObject.name.Replace("(Clone)", "").Trim();

                // 有时候手动放的可能叫 "House (1)"，如果你有这种情况，可能需要更复杂的正则去名
                // 这里假设你的场景物体命名规范，和 Database 里的 ID 一致
                Placeable data = _database.GetPlaceable(cleanName);

                if (data == null)
                {
                    Debug.LogWarning($"场景中存在建筑 '{building.name}'，但在数据库中找不到 ID 为 '{cleanName}' 的数据。无法自动注册。请检查物体名称是否与 ScriptableObject ID 一致。");
                    continue;
                }

                // 3. 计算它在网格上的位置
                Vector3Int gridPos = _grid.WorldToCell(building.transform.position);

                // 4. 调用 Handler 进行物体层面的注册 (添加组件、GUID、字典)
                string guid = placementHandler.RegisterPrePlacedObject(building.gameObject, gridPos, data);

                // 5. 更新 GridData (数据层面的注册，标记格子被占用)
                // 这一步非常重要，否则你可以在旧建筑上重叠建造
                if (_gridDataMap.ContainsKey(data.GridType))
                {
                    try
                    {
                        // 这里默认 size 使用 data.Size。如果场景里的物体被缩放过，可能会有偏差，但通常建筑游戏里这保持一致。
                        _gridDataMap[data.GridType].Add(gridPos, Vector2Int.RoundToInt(data.Size), data.GetAssetIdentifier(), guid);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"注册预制建筑 {cleanName} 时出错，位置 {gridPos} 可能重叠或越界: {e.Message}");
                    }
                }
            }
        }


        /// <summary>
        /// LoadedObjectPlacementState
        /// based on the loaded podata instantiates the according object and initializes it with all the loaded values
        /// </summary>
        public void InitializeLoadedObject(PlaceableObjectData podata) {
            _stateHandler = new LoadedObjectPlacementState(podata, _grid, _database, _gridDataMap, placementHandler);
            _stateHandler.OnAction(podata.gridPosition);
            _stateHandler = null;
        }

        /// <summary>
        /// initializes the PlacementState and adds all trigger functions
        /// </summary>
        public void StartPlacement(string assetIdentifier) {
            // 1. 从数据库中获取建筑数据 (你已经提供了 _database 字段)
            Placeable placeableData = _database.GetPlaceable(assetIdentifier);

            // 2. 检查它是否是独特的 (我们刚添加的字段)，并且是否已经被建造
            if (placeableData != null && placeableData.IsUnique)
            {
                if (_uniqueBuildingsBuilt.Contains(assetIdentifier))
                {
                    Debug.LogWarning($"无法开始放置 {assetIdentifier}: 该独特建筑已被建造。");

                    // (可选) 在这里触发一个UI提示，告诉玩家“已达建造上限”
                    // UIManager.Instance.ShowNotification("已达建造上限");

                    return; // 立即停止，不允许进入放置状态
                }
            }
            StopState();
            _grid.SetVisualizationState(true);
            _stateHandler = new PlacementState(assetIdentifier, _grid, previewSystem, _database, _gridDataMap, placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnRotate += RotateStructure;
            OnPlacementStateStart?.Invoke();
        }

        /// <summary>
        /// initializes the RemoveState and adds all trigger functions
        /// </summary>
        public void StartRemoving(GridDataType gridType) {
            StopState();
            _grid.SetVisualizationState(true);
            _stateHandler = new RemoveState(_grid, previewSystem, _gridDataMap[gridType], placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnExit += ObjectGrouper.Instance.DisplayAll;
            ObjectGrouper.Instance.DisplayOnlyObjectsOfSelectedGridType(gridType);
        }

        /// <summary>
        /// In the remove state if clicked on a grid cell, all objects across all gridData that have this position will be removed 
        /// </summary>
        public void StartRemovingAll() {
            StopState();
            _grid.SetVisualizationState(true);
            _stateHandler = new RemoveAllState(_grid, previewSystem, _gridDataMap, placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnExit += ObjectGrouper.Instance.DisplayAll;
            ObjectGrouper.Instance.DisplayAll();
        }

        /// <summary>
        /// initializes the RemoveState, directly removes the object at the given gridPosition, and  sets the state to null again
        /// </summary>
        public void Remove(PlacedObject placedObject) {
            var gridType = placedObject.placeable.GridType;
            StopState();
            _stateHandler = new RemoveState(_grid, previewSystem, _gridDataMap[gridType], placementHandler);
            _stateHandler.OnAction(placedObject.data.gridPosition);
            _stateHandler.EndState();
            _stateHandler = null;

        }

        /// <summary>
        /// initializes the MoveState and adds all trigger functions
        /// </summary>
        public void StartMoving(PlacedObject target) {
            StopState();
            _stopStateAfterAction = true;
            _grid.SetVisualizationState(true);
            _stateHandler = new MovingState(target, _grid, previewSystem, _gridDataMap, placementHandler);
            _inputManager.OnClicked += StateAction;
            _inputManager.OnExit += StopState;
            _inputManager.OnRotate += RotateStructure;
            OnPlacementStateStart?.Invoke();
        }

        public void StopState() {
            //we should still disable the visualization even if there is no state available
            _grid.SetVisualizationState(false);
            if (_stateHandler == null) {
                return;
            }

            // reset stop trigger;
            _stopStateAfterAction = false;

            _stateHandler.EndState();
            _inputManager.OnClicked -= StateAction;
            _inputManager.OnExit -= StopState;
            _inputManager.OnExit -= ObjectGrouper.Instance.DisplayAll;
            _inputManager.OnRotate -= RotateStructure;
            _lastDetectedPosition = Vector3Int.zero;
            // very Important: reset the placement state when we stop the placement
            _stateHandler = null;
            ObjectGrouper.Instance.DisplayAll();
            OnPlacementStateEnd?.Invoke();
        }


        /// <summary>
        /// additional check if our pointer is over UI --> ignore action
        /// calculates the current gridPosition and triggers Action of the selected state,
        /// which will e.g. either place or remove the object (based on the state that we are currently in)
        /// </summary>
        private void StateAction() {
            if (InputManager.IsPointerOverUIObject()) {
                return;
            }

            var mousePosition = _inputManager.GetSelectedMapPosition();
            var gridPosition = _grid.WorldToCell(mousePosition);

            _stateHandler.OnAction(gridPosition);
            if (_stopStateAfterAction) {
                StopState();
            }
        }

        /// <summary>
        ///  triggers OnRotation function of the current state
        /// </summary>
        private void RotateStructure() {
            _stateHandler.OnRotation();
        }


        /// <summary>
        /// tracks the mousePosition and calculates the up to date gridPosition
        /// if the gridPosition would change, we update the stateHandler state and the last detected position
        /// </summary>
        private void Update() {
            if (_stateHandler == null) {
                return;
            }

            // actual raycasted position on the grid floor
            var mousePosition = _inputManager.GetSelectedMapPosition();
            var gridPosition = _grid.WorldToCell(mousePosition);
            // if nothing has changed for the grid position, we do not need to waste resources to calculate all the other stuff 
            if (_lastDetectedPosition == gridPosition) {
                return;
            }

            _stateHandler.UpdateState(gridPosition);
            _lastDetectedPosition = gridPosition;
        }
        /// <summary>
        /// (新函数) 检查一个独特建筑是否已被建造
        /// (这个函数主要给UI按钮使用，用来禁用按钮)
        /// </summary>
        public bool IsUniqueBuildingBuilt(string assetIdentifier)
        {
            return _uniqueBuildingsBuilt.Contains(assetIdentifier);
        }

        /// <summary>
        /// (新函数) 注册一个新放置的独特建筑
        /// (将由 PlacementHandler 在放置时调用)
        /// </summary>
        public void RegisterUniqueBuilding(string assetIdentifier)
        {
            // Add 方法如果成功添加 (即之前不存在) 会返回 true
            if (_uniqueBuildingsBuilt.Add(assetIdentifier))
            {
                /*Debug.Log($"独特建筑已注册: {assetIdentifier}");*/
            }
        }

        /// <summary>
        /// (新函数) 注销一个被摧毁的独特建筑
        /// (将由 PlacementHandler 在移除时调用)
        /// </summary>
        public void UnregisterUniqueBuilding(string assetIdentifier)
        {
            // Remove 方法如果成功移除 (即之前存在) 会返回 true
            if (_uniqueBuildingsBuilt.Remove(assetIdentifier))
            {
                /*Debug.Log($"独特建筑已注销: {assetIdentifier}");*/
            }
        }
    }
}