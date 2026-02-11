using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    public class PlaceableActionTooltip : MonoBehaviour
    {
        [SerializeField]
        private Button removeButton;

        [SerializeField]
        private GameObject tooltipUI;

        [SerializeField]
        private GameObject blockerUI;

        [SerializeField]
        [Tooltip("Margin in pixels for the screen edges")]
        private int margin;

        private static PlaceableActionTooltip _instance;
        private PlacementSystem _placementSystem;
        private PlacedObject _placedObject;
        private Vector2 _tooltipSize;
        private Camera _targetCamera;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
            }
            else
            {
                _instance = this;
                _tooltipSize = GetComponent<RectTransform>().sizeDelta;

                // 仅保留删除按钮的监听
                if (removeButton != null)
                {
                    removeButton.onClick.AddListener(RemoveObject);
                }

                tooltipUI.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            _placementSystem = PlacementSystem.Instance;
            _targetCamera = GameManager.Instance.SceneCamera;

            // 绑定退出/关闭逻辑
            InputManager.Instance.OnExit += CloseTooltip;
            PlacedObject.holdComplete += ShowTooltip;
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnExit -= CloseTooltip;

            PlacedObject.holdComplete -= ShowTooltip;
        }

        private void CloseTooltip()
        {
            tooltipUI.gameObject.SetActive(false);
            blockerUI.SetActive(false);
        }

        private void Show(PlacedObject caller)
        {
            _placedObject = caller;

            // 仅显示删除按钮，不再根据建筑类型切换其他按钮
            removeButton.gameObject.SetActive(true);

            var screenPosition = _targetCamera.WorldToScreenPoint(caller.transform.position);
            tooltipUI.gameObject.SetActive(true);
            tooltipUI.transform.position = RecalculatePositionWithinBounds(screenPosition);
            blockerUI.SetActive(true);
        }

        private Vector3 RecalculatePositionWithinBounds(Vector3 screenPosition)
        {
            var newPosition = new Vector3(screenPosition.x, screenPosition.y, screenPosition.z);
            if (screenPosition.x < 0)
            {
                newPosition.x = margin;
            }
            else if (screenPosition.x + _tooltipSize.x > Screen.width)
            {
                newPosition.x = Screen.width - _tooltipSize.x - margin;
            }

            if (screenPosition.y < 0)
            {
                newPosition.y = margin;
            }
            else if (screenPosition.y + _tooltipSize.y > Screen.height)
            {
                newPosition.y = Screen.height - _tooltipSize.y - margin;
            }

            return newPosition;
        }

        private void RemoveObject()
        {
            if (_placedObject != null)
            {
                _placementSystem.Remove(_placedObject);
            }
            HideTooltip();
        }

        private void HideTooltip()
        {
            tooltipUI.gameObject.SetActive(false);
            blockerUI.SetActive(false);
        }

        private static void ShowTooltip(PlacedObject caller)
        {
            if (_instance != null)
            {
                _instance.Show(caller);
            }
        }
    }
}