using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    /// <summary>
    /// handles showing the cell indicators and the placeable object preview on the grid
    /// </summary>
    public class PreviewSystem : MonoBehaviour
    {
        private GameObject _cellIndicatorPrefab;
        private Material _previewMaterialPrefab;
        private Material _previewMaterialInstance;
        private Renderer _cellIndicatorRenderer;
        private GameObject _previewObject;
        private Vector3 _pivotOffset;
        private GameObject _cellIndicator;
        private GameConfig _config;
        private float _cellSize;

        private bool _isDynamicSize;

        private void Start()
        {
            _config = GameConfig.Instance;
            _previewMaterialPrefab = _config.PreviewMaterialPrefab;
            _cellIndicatorPrefab = _config.CellIndicatorPrefab;
            _previewMaterialInstance = new Material(_previewMaterialPrefab);
            _cellIndicator = Instantiate(_cellIndicatorPrefab, transform);
            _cellIndicator.SetActive(false);
            _cellIndicatorRenderer = _cellIndicator.GetComponentInChildren<Renderer>();
        }

        /// <summary>
        /// Initializes the placement preview
        /// </summary>
        public Vector3 StartShowingPlacementPreview(Placeable selectedObject, float gridCellSize)
        {
            _previewObject = Instantiate(selectedObject.Prefab);

            // --- 核心修改：蓝图模式下，销毁物体上的逻辑脚本，防止扣费/产出 ---
            // 这样它就只是一个纯粹的视觉模型，Start() 方法永远不会有机会执行
            var effects = _previewObject.GetComponentsInChildren<BuildingEffect>();
            foreach (var effect in effects)
            {
                Destroy(effect);
            }
            // 顺便把 PlacedObject 也清理掉，防止产生干扰
            var placedObjects = _previewObject.GetComponentsInChildren<PlacedObject>();
            foreach (var po in placedObjects)
            {
                Destroy(po);
            }
            // -------------------------------------------------------------

            _isDynamicSize = selectedObject.DynamicSize;
            if (_isDynamicSize)
            {
                _previewObject.transform.localScale = new Vector3(gridCellSize, gridCellSize, gridCellSize);
            }

            _pivotOffset = PlaceableUtils.CalculateOffset(_previewObject, gridCellSize);
            _cellSize = gridCellSize;
            if (_config.UsePreviewMaterial)
            {
                PreparePreview(_previewObject);
            }

            _cellIndicator.SetActive(true);
            return _pivotOffset;
        }


        /// <summary>
        /// Initializes the remove preview
        /// </summary>
        public void StartShowingRemovePreview(float gridCellSize)
        {
            _cellIndicator.SetActive(true);
            _cellSize = gridCellSize;
            PrepareCellIndicator(new Vector2(_cellSize, _cellSize));
            UpdateCellIndicator(false, true);
        }

        private void PrepareCellIndicator(Vector2 size)
        {
            if (size is { x: <= 0, y: <= 0 })
            {
                return;
            }
            _cellIndicator.transform.localScale = new Vector3(size.x, _cellIndicator.transform.localScale.y, size.y);
            _cellIndicatorRenderer.material.mainTextureScale = size;
        }

        private void PreparePreview(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var materials = r.materials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = _previewMaterialInstance;
                }

                r.materials = materials;
            }
        }

        public void StopShowingPreview()
        {
            _cellIndicator.SetActive(false);
            if (_previewObject != null)
            {
                Destroy(_previewObject);
            }
        }

        public void UpdatePosition(Vector3 position, bool isValid, Placeable placeable, ObjectDirection direction = ObjectDirection.Down)
        {
            if (_previewObject)
            {
                if (placeable)
                {
                    _previewObject.transform.position =
                        position + new Vector3(0, _config.PreviewYOffset, 0) + PlaceableUtils.GetTotalOffset(_pivotOffset, direction);
                    _previewObject.transform.rotation = Quaternion.Euler(0, PlaceableUtils.GetRotationAngle(direction), 0);
                    PrepareCellIndicator(PlaceableUtils.GetCorrectedObjectSize(placeable, direction, _cellSize));
                }

                if (_config.UsePreviewMaterial)
                {
                    UpdatePreviewMaterial(isValid);
                }
            }

            MoveCellIndicator(position);
            UpdateCellIndicator(isValid);
        }

        public void UpdateRemovalPosition(Vector3 position, bool isValid)
        {
            MoveCellIndicator(position);
            UpdateCellIndicator(isValid, true);
        }

        private void UpdatePreviewMaterial(bool isValid)
        {
            var color = isValid ? _config.ValidPlacementColor : _config.InValidPlacementColor;
            _previewMaterialInstance.color = color;
        }

        private void UpdateCellIndicator(bool isValid, bool removing = false)
        {
            Color color;
            if (removing)
            {
                color = isValid ? _config.ValidRemovalColor : _config.InValidRemovalColor;
            }
            else
            {
                color = isValid ? _config.ValidPlacementColor : _config.InValidPlacementColor;
            }

            color.a = 0.5f;
            _cellIndicatorRenderer.material.color = color;
        }

        private void MoveCellIndicator(Vector3 position)
        {
            _cellIndicator.transform.position = position;
        }
    }
}