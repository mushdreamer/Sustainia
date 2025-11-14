using System;
using System.Collections;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core {
    /// <summary>
    /// This script should be assigned directly to the camera!
    /// This camera controller handles camera movement by dragging the mouse and zooming via the mouse scroll wheel
    /// It has also the option to trigger a Focus method to focus on a given world position, and center it at the middle of the screen
    /// </summary>
    public class CameraController : MonoBehaviour {
        private Vector3 _groundCamOffset;
        private Vector3 _camSmoothDampV;
        private Vector3 _lastMousePositionL;
        private Vector3 _lastMousePositionR;
        private Vector3 _offset;
        private Camera _sceneCamera;

        private bool _startedHoldingOverUI;
        private bool _isInPlacementState;

        private GameConfig _config;

        private void Start() {
            _config = GameConfig.Instance;
            _sceneCamera = GetComponent<Camera>();
            var groundPos = GetWorldPosAtViewportPoint(0.5f, 0.5f);
            _groundCamOffset = _sceneCamera.transform.position - groundPos;

            InputManager.Instance.OnLmbRelease += ResetUIHold;
            InputManager.Instance.OnMmbDrag += HandleDrag;
            InputManager.Instance.OnRmbDrag += HandleRotation;
            InputManager.Instance.OnScroll += HandleZoom;
            InputManager.Instance.OnMouseAtScreenCorner += HandleMouseAtScreenCorner;
            PlacementSystem.Instance.OnPlacementStateStart += PlacementStateActivated;
            PlacementSystem.Instance.OnPlacementStateEnd += PlacementStateEnded;
        }


        private Vector3 GetWorldPosAtViewportPoint(float vx, float vy) {
            var worldRay = _sceneCamera.ViewportPointToRay(new Vector3(vx, vy, 0));
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            groundPlane.Raycast(worldRay, out var distanceToGround);
            // Debug.Log("distance to ground:" + distanceToGround);
            return worldRay.GetPoint(distanceToGround);
        }


        private void OnDestroy() {
            InputManager.Instance.OnLmbRelease -= ResetUIHold;
            InputManager.Instance.OnMmbDrag -= HandleDrag;
            InputManager.Instance.OnRmbDrag -= HandleRotation;
            InputManager.Instance.OnScroll -= HandleZoom;
            InputManager.Instance.OnMouseAtScreenCorner -= HandleMouseAtScreenCorner;
            PlacementSystem.Instance.OnPlacementStateStart -= PlacementStateActivated;
            PlacementSystem.Instance.OnPlacementStateEnd -= PlacementStateEnded;
        }

        private void ResetUIHold() {
            _startedHoldingOverUI = false;
        }


        private void HandleDrag(Vector2 mouseDelta) {
            if (_startedHoldingOverUI) {
                return;
            }

            var xBoundary = _config.XBoundary;
            var zBoundary = _config.ZBoundary;
            // Convert movement to world space, and also consider camera rotation for the drag
            var moveDirection = transform.right * mouseDelta.x + transform.forward * mouseDelta.y;
            moveDirection.y = 0;
            var newPosition = transform.position - moveDirection * (_config.DragSpeed * Time.deltaTime);
            // Clamp position within bounds
            //newPosition.x = Mathf.Clamp(newPosition.x, xBoundary.x, xBoundary.y);
            //newPosition.z = Mathf.Clamp(newPosition.z, zBoundary.x, zBoundary.y);
            transform.position = newPosition;
        }


        private void HandleRotation(Vector2 mouseDelta) {
            var rotationSpeed = _config.RotationSpeed;
            // Rotate around the Y-axis
            transform.Rotate(Vector3.up, mouseDelta.x * rotationSpeed * Time.deltaTime, Space.World);

            // Rotate around the X-axis
            var angle = -mouseDelta.y * rotationSpeed * Time.deltaTime;
            var rightAxis = transform.right; // Local right direction
            transform.Rotate(rightAxis, angle, Space.World);
        }

        private void HandleMouseAtScreenCorner(Vector2 direction) {
            if (_config.EnableAutoMove == false) {
                return;
            }

            var xBoundary = _config.XBoundary;
            var zBoundary = _config.ZBoundary;
            // if either not restricted or in placement state
            if (_config.RestrictAutoMoveForPlacement && !_isInPlacementState) {
                return;
            }

            var newPosition = transform.position + new Vector3(direction.x, 0, direction.y) * (_config.DragSpeed * Time.deltaTime);
            //newPosition.x = Mathf.Clamp(newPosition.x, xBoundary.x, xBoundary.y);
            //newPosition.z = Mathf.Clamp(newPosition.z, zBoundary.x, zBoundary.y);
            transform.position = newPosition;
        }

        private void HandleZoom(float scrollDelta) {
            if (scrollDelta == 0) {
                return;
            }

            if (!_sceneCamera) {
                return;
            }

            //switch (scrollDelta) {
                // Stop zooming further if min or max Y reached
                //case > 0 when _sceneCamera.transform.position.y <= _config.YBoundary.x:
                //case < 0 when _sceneCamera.transform.position.y >= _config.YBoundary.y:
                    //return;
            //}

            var mouseRay = _sceneCamera.ScreenPointToRay(Input.mousePosition);
            // Flat ground at y = 0
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(mouseRay, out var distance)) {
                var mouseWorldPos = mouseRay.GetPoint(distance);
                var zoomDirection = (mouseWorldPos - _sceneCamera.transform.position).normalized;
                var newPosition = _sceneCamera.transform.position + zoomDirection * (scrollDelta * _config.ZoomSpeed);

                _sceneCamera.transform.position = newPosition;
            }
        }

        private void PlacementStateActivated() {
            _isInPlacementState = true;
        }

        private void PlacementStateEnded() {
            _isInPlacementState = false;
        }

        public void FocusOnPosition(Vector3 target, float duration) {
            StartCoroutine(CameraUpdateCoroutine(target, duration));

        }

        private IEnumerator CameraUpdateCoroutine(Vector3 target, float duration) {
            var startPosition = _sceneCamera.transform.position;
            var endPosition = target + _groundCamOffset;
            var elapsedTime = 0f;

            while (elapsedTime < duration) {
                // Calculate how much time has passed and use it to interpolate the position
                elapsedTime += Time.deltaTime;
                var t = Mathf.Clamp01(elapsedTime / duration);
                t = Mathf.SmoothStep(0f, 1f, t);
                // Move the camera smoothly using Lerp
                _sceneCamera.transform.position = Vector3.Lerp(startPosition, endPosition, t);
                // Wait for the next frame before continuing the loop
                yield return null;
            }
        }
    }
}