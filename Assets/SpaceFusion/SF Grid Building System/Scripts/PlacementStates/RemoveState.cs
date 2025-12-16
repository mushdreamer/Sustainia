using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Interfaces;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.PlacementStates
{

    public class RemoveState : IPlacementState
    {
        private string _guid;
        private readonly IPlacementGrid _grid;
        private readonly PreviewSystem _previewSystem;
        private readonly GridData _gridData;
        private readonly PlacementHandler _placementHandler;

        public RemoveState(IPlacementGrid grid, PreviewSystem previewSystem, GridData gridData, PlacementHandler placementHandler)
        {
            _grid = grid;
            _previewSystem = previewSystem;
            _gridData = gridData;
            _placementHandler = placementHandler;
            previewSystem.StartShowingRemovePreview(_grid.CellSize);
        }

        public void EndState()
        {
            _previewSystem.StopShowingPreview();
        }

        public void OnAction(Vector3Int gridPosition)
        {
            _guid = _gridData.GetGuid(gridPosition);
            if (_guid == null) return;

            // 获取位置用于解锁 Zone
            var worldPosition = _grid.CellToWorld(gridPosition);

            _gridData.RemoveObjectPositions(gridPosition);
            _placementHandler.RemoveObjectPositions(_guid);

            // --- 关键：解锁 Zone ---
            if (MultiZoneCityGenerator.Instance != null)
            {
                MultiZoneCityGenerator.Instance.SetZoneOccupiedState(worldPosition, false);
            }

            var cellPosition = _grid.CellToWorld(gridPosition);
            _previewSystem.UpdateRemovalPosition(cellPosition, !IsPositionEmpty(gridPosition));
        }

        private bool IsPositionEmpty(Vector3Int gridPosition)
        {
            return _gridData.IsPlaceable(gridPosition, Vector2Int.one);
        }

        public void UpdateState(Vector3Int gridPosition)
        {
            var validity = !IsPositionEmpty(gridPosition);
            _previewSystem.UpdateRemovalPosition(_grid.CellToWorld(gridPosition), validity);
        }

        public void OnRotation() { }
    }
}