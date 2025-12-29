using System;
using System.Collections;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    /// <summary>
    /// 相机控制器
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Smooth Settings")]
        public float positionLerpTime = 10f;
        public float rotationLerpTime = 10f;

        [Header("Movement Settings")]
        public float keyboardMoveSpeed = 50f;
        public float sprintMultiplier = 2.5f;
        public float keyboardRotateSpeed = 100f;

        [Header("Adaptive Speed")]
        public bool useAdaptiveSpeed = true;
        public float referenceHeight = 20f;
        public float maxSpeedMultiplier = 4f;

        [Header("Limits (可调节的限制)")]
        [Tooltip("最小高度 (防止钻地)")]
        public float minHeight = 2.0f;
        [Tooltip("最大高度")]
        public float maxHeight = 500.0f;

        [Tooltip("最小俯仰角 (防止平视穿模)")]
        public float minPitchAngle = 10f;
        [Tooltip("最大俯仰角 (防止甚至翻转，建议不超过89)")]
        public float maxPitchAngle = 85f;

        [Tooltip("是否启用 X/Z 轴的地图边界限制")]
        public bool enableMapBounds = false;
        [Tooltip("地图边界范围 (MinX, MinZ, MaxX, MaxZ)")]
        public Vector4 mapBounds = new Vector4(-500, -500, 500, 500);

        // 内部变量
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _groundCamOffset;
        private Camera _sceneCamera;
        private bool _startedHoldingOverUI;
        private bool _isInPlacementState;
        private GameConfig _config;

        private void Start()
        {
            _config = GameConfig.Instance;
            _sceneCamera = GetComponent<Camera>();

            _targetPosition = transform.position;
            _targetRotation = transform.rotation;

            var groundPos = GetWorldPosAtViewportPoint(0.5f, 0.5f);
            _groundCamOffset = _sceneCamera.transform.position - groundPos;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLmbRelease += ResetUIHold;
                InputManager.Instance.OnMmbDrag += HandleDrag;
                InputManager.Instance.OnRmbDrag += HandleRotation;
                InputManager.Instance.OnScroll += HandleZoom;
                InputManager.Instance.OnMouseAtScreenCorner += HandleMouseAtScreenCorner;
            }
            if (PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.OnPlacementStateStart += PlacementStateActivated;
                PlacementSystem.Instance.OnPlacementStateEnd += PlacementStateEnded;
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLmbRelease -= ResetUIHold;
                InputManager.Instance.OnMmbDrag -= HandleDrag;
                InputManager.Instance.OnRmbDrag -= HandleRotation;
                InputManager.Instance.OnScroll -= HandleZoom;
                InputManager.Instance.OnMouseAtScreenCorner -= HandleMouseAtScreenCorner;
            }
            if (PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.OnPlacementStateStart -= PlacementStateActivated;
                PlacementSystem.Instance.OnPlacementStateEnd -= PlacementStateEnded;
            }
        }

        private void Update()
        {
            HandleKeyboardInput();
            UpdateCameraTransform();
        }

        private void HandleKeyboardInput()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            float rotate = 0f;

            // --- 修复：Q键设为负值(向左转)，E键设为正值(向右转) ---
            if (Input.GetKey(KeyCode.Q)) rotate = -1f;
            if (Input.GetKey(KeyCode.E)) rotate = 1f;
            // ----------------------------------------------------

            float speedMult = 1f;
            if (Input.GetKey(KeyCode.LeftShift)) speedMult = sprintMultiplier;
            speedMult *= GetHeightSpeedMultiplier();

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * v + right * h).normalized;

            if (moveDir.sqrMagnitude > 0.01f)
            {
                _targetPosition += moveDir * (keyboardMoveSpeed * speedMult * Time.deltaTime);
                ClampTargetPosition();
            }

            if (Mathf.Abs(rotate) > 0.01f)
            {
                float angle = rotate * keyboardRotateSpeed * Time.deltaTime;
                Quaternion yRotation = Quaternion.AngleAxis(angle, Vector3.up);
                _targetRotation = yRotation * _targetRotation;
            }
        }

        private void UpdateCameraTransform()
        {
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, _targetPosition, positionLerpTime * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, rotationLerpTime * dt);
        }

        private float GetHeightSpeedMultiplier()
        {
            if (!useAdaptiveSpeed) return 1f;
            float currentHeight = transform.position.y;
            float multiplier = currentHeight / Mathf.Max(1f, referenceHeight);
            return Mathf.Clamp(multiplier, 0.5f, maxSpeedMultiplier);
        }

        private Vector3 GetWorldPosAtViewportPoint(float vx, float vy)
        {
            var worldRay = _sceneCamera.ViewportPointToRay(new Vector3(vx, vy, 0));
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            groundPlane.Raycast(worldRay, out var distanceToGround);
            return worldRay.GetPoint(distanceToGround);
        }

        private void ResetUIHold()
        {
            _startedHoldingOverUI = false;
        }

        private void HandleDrag(Vector2 mouseDelta)
        {
            if (_startedHoldingOverUI) return;

            float adaptiveMult = GetHeightSpeedMultiplier();
            var moveDirection = transform.right * mouseDelta.x + transform.forward * mouseDelta.y;
            moveDirection.y = 0;

            _targetPosition -= moveDirection * (_config.DragSpeed * adaptiveMult * Time.deltaTime);
            ClampTargetPosition();
        }

        private void HandleRotation(Vector2 mouseDelta)
        {
            var rotationSpeed = _config.RotationSpeed;

            // Y轴旋转 (左右)
            float yAngle = mouseDelta.x * rotationSpeed * Time.deltaTime;
            Quaternion yRot = Quaternion.AngleAxis(yAngle, Vector3.up);
            _targetRotation = yRot * _targetRotation;

            // X轴旋转 (俯仰)
            float xAngle = -mouseDelta.y * rotationSpeed * Time.deltaTime;
            Quaternion xRot = Quaternion.AngleAxis(xAngle, transform.right);
            Quaternion potentialRot = xRot * _targetRotation;

            // --- 限制逻辑 ---
            float angleX = potentialRot.eulerAngles.x;
            if (angleX > 180) angleX -= 360;

            // 使用面板上的变量进行限制
            if (angleX > minPitchAngle && angleX < maxPitchAngle)
            {
                _targetRotation = potentialRot;
            }
        }

        private void HandleMouseAtScreenCorner(Vector2 direction)
        {
            if (_config.EnableAutoMove == false) return;
            if (_config.RestrictAutoMoveForPlacement && !_isInPlacementState) return;

            float adaptiveMult = GetHeightSpeedMultiplier();
            Vector3 moveVec = new Vector3(direction.x, 0, direction.y);

            _targetPosition += moveVec * (_config.DragSpeed * adaptiveMult * Time.deltaTime);
            ClampTargetPosition();
        }

        private void HandleZoom(float scrollDelta)
        {
            if (scrollDelta == 0 || !_sceneCamera) return;

            var mouseRay = _sceneCamera.ScreenPointToRay(Input.mousePosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(mouseRay, out var distance))
            {
                var mouseWorldPos = mouseRay.GetPoint(distance);
                var zoomDirection = (mouseWorldPos - _targetPosition).normalized;

                float adaptiveMult = GetHeightSpeedMultiplier();
                Vector3 diff = zoomDirection * (scrollDelta * _config.ZoomSpeed * adaptiveMult);

                float predictedY = _targetPosition.y + diff.y;

                if (predictedY > minHeight && predictedY < maxHeight)
                {
                    _targetPosition += diff;
                }
            }
        }

        private void ClampTargetPosition()
        {
            if (!enableMapBounds) return;

            float x = Mathf.Clamp(_targetPosition.x, mapBounds.x, mapBounds.z);
            float z = Mathf.Clamp(_targetPosition.z, mapBounds.y, mapBounds.w);

            _targetPosition = new Vector3(x, _targetPosition.y, z);
        }

        // ------------------------------
        private void PlacementStateActivated()
        {
            _isInPlacementState = true;
        }

        private void PlacementStateEnded()
        {
            _isInPlacementState = false;
        }
        // ------------------------------

        public void FocusOnPosition(Vector3 target, float duration)
        {
            _targetPosition = target + _groundCamOffset;
        }

        private void OnDrawGizmosSelected()
        {
            if (enableMapBounds)
            {
                Gizmos.color = Color.yellow;
                float width = mapBounds.z - mapBounds.x;
                float height = mapBounds.w - mapBounds.y;
                float centerX = (mapBounds.x + mapBounds.z) / 2;
                float centerZ = (mapBounds.y + mapBounds.w) / 2;

                Vector3 center = new Vector3(centerX, 0, centerZ);
                Vector3 size = new Vector3(width, 100, height);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}