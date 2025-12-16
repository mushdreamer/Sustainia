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
            if (!_selectedObject) throw new Exception($"No placeable with identifier '{assetIdentifier}' found");

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
            if (!IsPlacementValid(gridPosition, worldPosition)) return;

            if (!ResourceManager.Instance.SpendMoney(_selectedObject.Cost))
            {
                Debug.Log("金钱不足!");
                return;
            }

            var guid = _placementHandler.PlaceObject(_selectedObject, worldPosition, gridPosition, _currentDirection, _placeablePivotOffset, _grid.CellSize);
            _selectedGridData.Add(gridPosition, _occupiedCells, _selectedObject.GetAssetIdentifier(), guid);

            if (MultiZoneCityGenerator.Instance != null)
            {
                MultiZoneCityGenerator.Instance.SetZoneOccupiedState(worldPosition, true);
            }

            // --- 修改点：通知系统物体已放置（用于教程严格检查）---
            PlacementSystem.Instance.InvokeBuildingPlaced(_selectedObject);

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
            _previewSystem.UpdatePosition(worldPos, IsPlacementValid(gridPosition, worldPos), _selectedObject, _currentDirection);
            _currentGridPosition = gridPosition;
        }

        private bool IsPlacementValid(Vector3Int gridPosition, Vector3 worldPos)
        {
            bool gridValid = _selectedGridData.IsPlaceable(gridPosition, _occupiedCells) && _grid.IsWithinBounds(gridPosition, _occupiedCells);
            bool zoneValid = true;
            if (MultiZoneCityGenerator.Instance != null)
            {
                zoneValid = MultiZoneCityGenerator.Instance.IsZoneAvailableForBuilding(worldPos);
            }
            return gridValid && zoneValid;
        }
    }
}