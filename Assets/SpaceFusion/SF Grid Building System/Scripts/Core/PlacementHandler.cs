using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using SpaceFusion.SF_Grid_Building_System.Scripts.SaveSystem;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core {
    /// <summary>
    /// Handles placing and removing objects and keeps a list of all object references by guid
    /// </summary>
    public class PlacementHandler : MonoBehaviour {
        /// <summary>
        /// keeps track of all objects that are already placed on the grid
        /// dictionary is easier for removing & tracking objects than a simple list
        /// </summary>
        private readonly Dictionary<string, GameObject> _placedObjectDictionary = new();


        /// <summary>
        /// Handles placing a new object to the grid
        /// Initializes the PlacedObject data, which creates a new guid for unique identification of this object
        /// </summary>
        public string PlaceObject(Placeable placeableObj, Vector3 worldPosition, Vector3Int gridPosition, ObjectDirection direction,
            Vector3 offset, float cellSize) {
            var obj = Instantiate(placeableObj.Prefab);
            obj.AddComponent<PlacedObject>();
            var placedObject = obj.GetComponent<PlacedObject>();
            placedObject.buildingEffect = obj.GetComponent<BuildingEffect>(); // <<< --- 添加这行 ---
            placedObject.Initialize(placeableObj, gridPosition);
            placedObject.data.direction = direction;

            obj.transform.position = worldPosition + PlaceableUtils.GetTotalOffset(offset, direction);
            obj.transform.rotation = Quaternion.Euler(0, PlaceableUtils.GetRotationAngle(direction), 0);
            if (placeableObj.DynamicSize) {
                obj.transform.localScale = new Vector3(cellSize, cellSize, cellSize);
            }

            ObjectGrouper.Instance.AddToGroup(obj, placeableObj.GridType);
            _placedObjectDictionary.Add(placedObject.data.guid, obj);

            // 检查这是否是一个独特建筑 (我们刚添加的字段)，如果是，则注册它
            if (placeableObj.IsUnique)
            {
                // (你的 Placeable.cs 已经有 GetAssetIdentifier() 方法)
                PlacementSystem.Instance.RegisterUniqueBuilding(placeableObj.GetAssetIdentifier());
            }

            // --- ADD THIS ---
            // 放置后应用建筑效果
            obj.GetComponent<BuildingEffect>()?.ApplyEffect();
            // --- END ADD ---

            return placedObject.data.guid;
        }

        /// <summary>
        /// Handles placing an object that is loaded from the saveFile (handling is a little bit different from placing a new object)
        /// instead of the gridPosition we have podata as last parameter where we can Initialize the newly placed object with the previously saved guid, grid pos etc...
        /// </summary>
        public string PlaceLoadedObject(Placeable placeableObj, Vector3 worldPosition, PlaceableObjectData podata, float cellSize) {
            var obj = Instantiate(placeableObj.Prefab);
            obj.AddComponent<PlacedObject>();
            var placedObject = obj.GetComponent<PlacedObject>();
            placedObject.buildingEffect = obj.GetComponent<BuildingEffect>(); // <<< --- 添加这行 ---
            placedObject.data.gridPosition = podata.gridPosition;
            placedObject.placeable = placeableObj;
            placedObject.Initialize(podata);

            var offset = PlaceableUtils.CalculateOffset(obj, cellSize);
            obj.transform.position = worldPosition + PlaceableUtils.GetTotalOffset(offset, podata.direction);
            obj.transform.rotation = Quaternion.Euler(0, PlaceableUtils.GetRotationAngle(podata.direction), 0);
            if (placeableObj.DynamicSize) {
                obj.transform.localScale = new Vector3(cellSize, cellSize, cellSize);
            }

            _placedObjectDictionary.Add(placedObject.data.guid, obj);
            ObjectGrouper.Instance.AddToGroup(obj, placeableObj.GridType);

            // !!! 重要：加载游戏时，也必须注册独特建筑
            if (placeableObj.IsUnique)
            {
                PlacementSystem.Instance.RegisterUniqueBuilding(placeableObj.GetAssetIdentifier());
            }

            // --- ADD THIS ---
            // 加载的物体也需要应用效果
            obj.GetComponent<BuildingEffect>()?.ApplyEffect();
            // --- END ADD ---

            return podata.guid;
        }

        /// <summary>
        /// handles moving a placed object to his new position
        /// </summary>
        public void PlaceMovedObject(GameObject obj, Vector3 worldPosition, Vector3Int gridPosition, ObjectDirection direction, float cellSize) {
            var placedObject = obj.GetComponent<PlacedObject>();
            var offset = PlaceableUtils.CalculateOffset(obj, cellSize);
            obj.transform.position = worldPosition + PlaceableUtils.GetTotalOffset(offset, direction);
            obj.transform.rotation = Quaternion.Euler(0, PlaceableUtils.GetRotationAngle(direction), 0);
            placedObject.data.gridPosition = gridPosition;
            placedObject.data.direction = direction;
            // no need to update reference for moving object, since guid still stays the same 
        }

        /// <summary>
        /// Based on the guid, we load the placed object from our dictionary and remove it from the SaveData and from the dictionary
        /// then we destroy the instantiated object in the scene
        /// </summary>
        public void RemoveObjectPositions(string guid) {
            var obj = _placedObjectDictionary[guid];
            if (!obj) {
                Debug.LogError($"Removing object error: {guid} is not saved in dictionary");
                return;
            }

            // !!! 重要：在销毁对象之前，检查它是否是独特建筑，并将其注销
            PlacedObject placedObject = obj.GetComponent<PlacedObject>();
            if (placedObject != null && placedObject.placeable.IsUnique)
            {
                PlacementSystem.Instance.UnregisterUniqueBuilding(placedObject.placeable.GetAssetIdentifier());
            }

            // --- ADD THIS ---
            // 移除前移除建筑效果
            obj.GetComponent<BuildingEffect>()?.RemoveEffect();
            // --- END ADD ---

            obj.GetComponent<PlacedObject>().RemoveFromSaveData();
            _placedObjectDictionary.Remove(guid);
            // destroy the object and set the reference of the list at the proper index to null
            Destroy(obj);
        }
    }
}