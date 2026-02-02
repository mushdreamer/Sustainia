using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core {
    /// <summary>
    /// Stores positions for a specific Grid.
    /// You can have multiple GriData overlapping in one placementGrid.
    /// E.g. terrainData and buildableData.
    /// We only validate against the objects that are saved in the same GridData class
    /// </summary>
    public class GridData {
        private readonly Dictionary<Vector3Int, PlacementInfo> instantiatedObjects = new();

        /// <summary>
        /// Adds the object to the grid --> each occupied position will be mapped to the instantiatedObjects, so that we can track all the used positions on the grid
        /// </summary>
        public void Add(Vector3Int gridPosition, Vector2Int size, string assetIdentifier, string guid) {
            // calculate used positions
            var newUsedPositions = GetNeededPositions(gridPosition, size);
            var data = new PlacementInfo(newUsedPositions, assetIdentifier, guid);
            foreach (var position in newUsedPositions.Where(position => !instantiatedObjects.TryAdd(position, data))) {
                throw new Exception($"Dictionary already contains this position {position}");
            }

        }

        public void Move(Vector3Int oldGridPosition, Vector3Int newGridPosition, Vector2Int size) {
            // calculate used positions
            var oldReference = instantiatedObjects[oldGridPosition];
            var identifier = oldReference.AssetIdentifier;
            var guid = oldReference.Guid;
            RemoveObjectPositions(oldGridPosition);
            Add(newGridPosition, size, identifier, guid);
        }


        /// <summary>
        /// checks if all the needed positions are available
        /// returns true if all positions are free, otherwise returns false
        /// </summary>
        public bool IsPlaceable(Vector3Int gridPosition, Vector2Int size) {
            var positions = GetNeededPositions(gridPosition, size);
            return positions.All(position => !instantiatedObjects.ContainsKey(position));
        }

        /// <summary>
        /// Similar as IsPlaceable just that it additionally "whitelists" the old occupied positions
        /// Since if we move the object (e.g. 2x3) one field to the right it still occupies some of its old positions
        /// but in this case it's ok since the old positions will be cleared before placing it to the new position
        /// </summary>
        public bool IsMoveable(Vector3Int oldPosition, Vector3Int newPosition, Vector2Int size) {
            var oldPositions = instantiatedObjects[oldPosition].UsedPositions;
            var positions = GetNeededPositions(newPosition, size);
            return positions.All(position =>
                !instantiatedObjects.ContainsKey(position) || oldPositions.Contains(position));
        }


        /// <summary>
        /// Calculates all positions that the selected object would need to use, in order to be placed
        /// </summary>
        private static List<Vector3Int> GetNeededPositions(Vector3Int gridPosition, Vector2Int size) {
            List<Vector3Int> positions = new();
            for (var x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    positions.Add(gridPosition + new Vector3Int(x, 0, y));
                }
            }

            return positions;
        }

        /// <summary>
        /// returns the placedObjectIndex of the instantiatedObject for the given gridPosition
        /// </summary>
        public string GetGuid(Vector3Int gridPosition) {
            return !instantiatedObjects.TryGetValue(gridPosition, out var val) ? null : val.Guid;
        }

        /// <summary>
        /// free all positions that the removed instantiated object had used
        /// </summary>
        public void RemoveObjectPositions(Vector3Int gridPosition) {
            foreach (var pos in instantiatedObjects[gridPosition].UsedPositions) {
                instantiatedObjects.Remove(pos);
            }
        }
        public void ClearAll()
        {
            instantiatedObjects.Clear();
        }
    }


    public class PlacementInfo {
        // positions that will be occupied by this object
        public readonly List<Vector3Int> UsedPositions;
        public string AssetIdentifier { get; }
        public string Guid { get; }

        public PlacementInfo(List<Vector3Int> usedPositions, string assetIdentifier, string guid) {
            this.UsedPositions = usedPositions;
            AssetIdentifier = assetIdentifier;
            Guid = guid;
        }
    }
}