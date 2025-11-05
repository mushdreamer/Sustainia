using System;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using SpaceFusion.SF_Grid_Building_System.Scripts.Interfaces;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.PlacementStates {
    /// <summary>
    /// State handler for placements
    /// </summary>
    public class PlacementState : IPlacementState {
        private readonly IPlacementGrid _grid;
        private readonly PreviewSystem _previewSystem;
        private readonly PlacementHandler _placementHandler;
        private readonly GridData _selectedGridData;
        private readonly Placeable _selectedObject;
        private ObjectDirection _currentDirection = ObjectDirection.Down;
        private Vector3Int _currentGridPosition;

        // corrected object size based on actual rotation and grid cell size, for placement and validation
        private Vector2Int _occupiedCells;

        private readonly Vector3 _placeablePivotOffset;

        public PlacementState(string assetIdentifier, IPlacementGrid grid, PreviewSystem previewSystem,
            PlaceableObjectDatabase database, Dictionary<GridDataType, GridData> gridDataMap, PlacementHandler placementHandler) {
            _grid = grid;
            _previewSystem = previewSystem;
            _placementHandler = placementHandler;
            _selectedObject = database.GetPlaceable(assetIdentifier);
            _selectedGridData = gridDataMap[_selectedObject.GridType];
            if (!_selectedObject) {
                throw new Exception($"No placeable with identifier '{assetIdentifier}' found");
            }

            _occupiedCells = PlaceableUtils.GetOccupiedCells(_selectedObject, _currentDirection, _grid.CellSize);
            // for the preview we don't need correctedObject size because we don't really care about the actual cell size for the preview
            // the system also already needs to calculate the placeable object offset so we can reuse it instead of calculating it again when placing

            _placeablePivotOffset = previewSystem.StartShowingPlacementPreview(_selectedObject, grid.CellSize);
        }

        public void EndState() {
            _previewSystem.StopShowingPreview();
        }

        public void OnAction(Vector3Int gridPosition) {
            var isValidPlacement = IsPlacementValid(gridPosition);
            if (!isValidPlacement) {
                // wrong placement
                return;
            }

            // --- MODIFICATION START ---
            // 检查是否有足够的钱
            if (!ResourceManager.Instance.SpendMoney(_selectedObject.Cost))
            {
                Debug.Log("金钱不足!");
                // 这里可以添加一个UI提示告诉玩家钱不够
                return;
            }
            // --- MODIFICATION END ---

            var worldPosition = _grid.CellToWorld(gridPosition);
            var guid = _placementHandler.PlaceObject(_selectedObject, worldPosition, gridPosition, _currentDirection, _placeablePivotOffset, _grid.CellSize);

            _selectedGridData.Add(gridPosition, _occupiedCells, _selectedObject.GetAssetIdentifier(), guid);
            // after we placed the object, this position becomes invalid --> we do not want to put a second object over it
            // 检查我们刚刚放置的建筑是否是独特的
            // (这是基于我们之前在 Placeable.cs 中添加的 IsUnique 字段)
            if (_selectedObject.IsUnique)
            {
                // 如果是，立即调用 PlacementSystem 的 StopState() 来退出放置模式。
                // 这将阻止用户在不退出模式的情况下连续点击放置。
                PlacementSystem.Instance.StopState();
            }
            else
            {
                // 如果不是独特的 (例如道路、普通房屋)，
                // 则像以前一样更新预览，允许连续放置
                _previewSystem.UpdatePosition(worldPosition, false, _selectedObject, _currentDirection);
            }
            //_previewSystem.UpdatePosition(worldPosition, false, _selectedObject, _currentDirection);
        }

        public void OnRotation() {
            _currentDirection = PlaceableUtils.GetNextDir(_currentDirection);
            _occupiedCells = PlaceableUtils.GetOccupiedCells(_selectedObject, _currentDirection, _grid.CellSize);
            UpdateState(_currentGridPosition);
        }

        public void UpdateState(Vector3Int gridPosition) {
            var isValidPlacement = IsPlacementValid(gridPosition);

            _previewSystem.UpdatePosition(_grid.CellToWorld(gridPosition), isValidPlacement, _selectedObject, _currentDirection);
            // update the currentGridPosition, so we always have the up-to-date position to call UpdateState when Rotation is triggered
            _currentGridPosition = gridPosition;
        }


        /// <summary>
        /// Checks if placement is valid for the selected grid data
        /// </summary>
        private bool IsPlacementValid(Vector3Int gridPosition) {
            return _selectedGridData.IsPlaceable(gridPosition, _occupiedCells) && _grid.IsWithinBounds(gridPosition, _occupiedCells);
        }
    }
}