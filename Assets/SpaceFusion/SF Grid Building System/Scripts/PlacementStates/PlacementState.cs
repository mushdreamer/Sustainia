using System;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using SpaceFusion.SF_Grid_Building_System.Scripts.Interfaces;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.PlacementStates
{
    /// <summary>
    /// State handler for placements
    /// </summary>
    public class PlacementState : IPlacementState
    {
        private readonly IPlacementGrid _grid;
        private readonly PreviewSystem _previewSystem;
        private readonly PlacementHandler _placementHandler;
        private readonly GridData _selectedGridData;
        private readonly Placeable _selectedObject;
        private ObjectDirection _currentDirection = ObjectDirection.Down;
        private Vector3Int _currentGridPosition;
        private Vector2Int _occupiedCells;
        private readonly Vector3 _placeablePivotOffset;

        public PlacementState(string assetIdentifier, IPlacementGrid grid, PreviewSystem previewSystem,
            PlaceableObjectDatabase database, Dictionary<GridDataType, GridData> gridDataMap, PlacementHandler placementHandler)
        {
            _grid = grid;
            _previewSystem = previewSystem;
            _placementHandler = placementHandler;
            _selectedObject = database.GetPlaceable(assetIdentifier);
            _selectedGridData = gridDataMap[_selectedObject.GridType];
            if (!_selectedObject)
            {
                throw new Exception($"No placeable with identifier '{assetIdentifier}' found");
            }

            _occupiedCells = PlaceableUtils.GetOccupiedCells(_selectedObject, _currentDirection, _grid.CellSize);
            _placeablePivotOffset = previewSystem.StartShowingPlacementPreview(_selectedObject, grid.CellSize);
        }

        public void EndState()
        {
            _previewSystem.StopShowingPreview();
        }

        public void OnAction(Vector3Int gridPosition)
        {
            var worldPosition = _grid.CellToWorld(gridPosition);
            var isValidPlacement = IsPlacementValid(gridPosition, worldPosition);

            if (!isValidPlacement)
            {
                // 如果区域不可用（比如已经被占用了），播放一个错误音效或Debug
                // Debug.Log("Invalid Placement: Zone occupied or out of bounds.");
                return;
            }

            // 检查是否有足够的钱
            if (!ResourceManager.Instance.SpendMoney(_selectedObject.Cost))
            {
                Debug.Log("金钱不足!");
                return;
            }

            var guid = _placementHandler.PlaceObject(_selectedObject, worldPosition, gridPosition, _currentDirection, _placeablePivotOffset, _grid.CellSize);
            _selectedGridData.Add(gridPosition, _occupiedCells, _selectedObject.GetAssetIdentifier(), guid);

            // --- 核心修改：锁定 Zone ---
            if (MultiZoneCityGenerator.Instance != null)
            {
                MultiZoneCityGenerator.Instance.SetZoneOccupiedState(worldPosition, true);
            }

            _previewSystem.UpdatePosition(worldPosition, false, _selectedObject, _currentDirection);
        }

        public void OnRotation()
        {
            _currentDirection = PlaceableUtils.GetNextDir(_currentDirection);
            _occupiedCells = PlaceableUtils.GetOccupiedCells(_selectedObject, _currentDirection, _grid.CellSize);
            UpdateState(_currentGridPosition);
        }

        public void UpdateState(Vector3Int gridPosition)
        {
            var worldPos = _grid.CellToWorld(gridPosition);
            var isValidPlacement = IsPlacementValid(gridPosition, worldPos);
            _previewSystem.UpdatePosition(worldPos, isValidPlacement, _selectedObject, _currentDirection);
            _currentGridPosition = gridPosition;
        }

        private bool IsPlacementValid(Vector3Int gridPosition, Vector3 worldPosition)
        {
            // 1. 基础的 GridData 检查（是否和其他物体碰撞）
            bool gridValid = _selectedGridData.IsPlaceable(gridPosition, _occupiedCells) && _grid.IsWithinBounds(gridPosition, _occupiedCells);

            // 2. --- 核心修改：Zone 独占性检查 ---
            // 必须在某个 Zone 内，且该 Zone 必须为空
            bool zoneValid = false;
            if (MultiZoneCityGenerator.Instance != null)
            {
                zoneValid = MultiZoneCityGenerator.Instance.IsZoneAvailableForBuilding(worldPosition);
            }
            else
            {
                // 如果没有生成器（比如在测试场景），默认允许，或者你可以根据需求改为 false
                zoneValid = true;
            }

            return gridValid && zoneValid;
        }
    }
}