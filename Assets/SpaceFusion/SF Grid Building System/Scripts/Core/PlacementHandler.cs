using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using SpaceFusion.SF_Grid_Building_System.Scripts.SaveSystem;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    /// <summary>
    /// Handles placing and removing objects and keeps a list of all object references by guid
    /// </summary>
    public class PlacementHandler : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> _placedObjectDictionary = new();

        public string PlaceObject(Placeable placeableObj, Vector3 worldPosition, Vector3Int gridPosition, ObjectDirection direction,
            Vector3 offset, float cellSize)
        {
            var obj = Instantiate(placeableObj.Prefab);
            obj.AddComponent<PlacedObject>();
            var placedObject = obj.GetComponent<PlacedObject>();
            placedObject.buildingEffect = obj.GetComponent<BuildingEffect>();
            placedObject.Initialize(placeableObj, gridPosition);
            placedObject.data.direction = direction;

            obj.transform.position = worldPosition + PlaceableUtils.GetTotalOffset(offset, direction);
            obj.transform.rotation = Quaternion.Euler(0, PlaceableUtils.GetRotationAngle(direction), 0);
            if (placeableObj.DynamicSize)
            {
                obj.transform.localScale = new Vector3(cellSize, cellSize, cellSize);
            }

            ObjectGrouper.Instance.AddToGroup(obj, placeableObj.GridType);
            _placedObjectDictionary.Add(placedObject.data.guid, obj);

            // [已删除] 手动调用 ApplyEffect
            // 原因：Instantiate 出来的物体会在下一帧自动执行 Start() -> ApplyEffect()
            // 如果在这里手动调用，会抢在 Start() 赋值之前运行，导致数值为 0

            return placedObject.data.guid;
        }

        public string PlaceLoadedObject(Placeable placeableObj, Vector3 worldPosition, PlaceableObjectData podata, float cellSize)
        {
            var obj = Instantiate(placeableObj.Prefab);
            obj.AddComponent<PlacedObject>();
            var placedObject = obj.GetComponent<PlacedObject>();
            placedObject.buildingEffect = obj.GetComponent<BuildingEffect>();

            placedObject.InitializeLoadedData(placeableObj, podata);

            var offset = PlaceableUtils.CalculateOffset(obj, cellSize);
            obj.transform.position = worldPosition + PlaceableUtils.GetTotalOffset(offset, podata.direction);
            obj.transform.rotation = Quaternion.Euler(0, PlaceableUtils.GetRotationAngle(podata.direction), 0);
            if (placeableObj.DynamicSize)
            {
                obj.transform.localScale = new Vector3(cellSize, cellSize, cellSize);
            }

            _placedObjectDictionary.Add(placedObject.data.guid, obj);
            ObjectGrouper.Instance.AddToGroup(obj, placeableObj.GridType);

            // [已删除] 同上，不要手动调用

            return podata.guid;
        }

        public void PlaceMovedObject(GameObject obj, Vector3 worldPosition, Vector3Int gridPosition, ObjectDirection direction, float cellSize)
        {
            var placedObject = obj.GetComponent<PlacedObject>();
            var offset = PlaceableUtils.CalculateOffset(obj, cellSize);
            obj.transform.position = worldPosition + PlaceableUtils.GetTotalOffset(offset, direction);
            obj.transform.rotation = Quaternion.Euler(0, PlaceableUtils.GetRotationAngle(direction), 0);
            placedObject.data.gridPosition = gridPosition;
            placedObject.data.direction = direction;
        }

        public void RemoveObjectPositions(string guid)
        {
            var obj = _placedObjectDictionary[guid];
            if (!obj)
            {
                Debug.LogError($"Removing object error: {guid} is not saved in dictionary");
                return;
            }

            // 移除前移除建筑效果
            obj.GetComponent<BuildingEffect>()?.RemoveEffect();

            obj.GetComponent<PlacedObject>().RemoveFromSaveData();
            _placedObjectDictionary.Remove(guid);
            Destroy(obj);
        }

        public string RegisterPrePlacedObject(GameObject obj, Vector3Int gridPos, Placeable placeableObj)
        {
            var placedObject = obj.GetComponent<PlacedObject>();
            if (placedObject == null) placedObject = obj.AddComponent<PlacedObject>();

            placedObject.buildingEffect = obj.GetComponent<BuildingEffect>();
            placedObject.Initialize(placeableObj, gridPos);
            placedObject.data.direction = ObjectDirection.Down;

            ObjectGrouper.Instance.AddToGroup(obj, placeableObj.GridType);

            if (!_placedObjectDictionary.ContainsKey(placedObject.data.guid))
            {
                _placedObjectDictionary.Add(placedObject.data.guid, obj);
            }

            // [已删除] 对于场景中预置的物体，它们的 Start() 也会在游戏开始时自动运行
            // 不需要手动干预

            return placedObject.data.guid;
        }
    }
}