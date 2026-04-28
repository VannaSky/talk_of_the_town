using UnityEngine;
using UnityEngine.Serialization;

namespace Cameras
{
    [RequireComponent(typeof(Camera))]
    public class FreeFlyCamera : MonoBehaviour
    {
        #region UI

        [FormerlySerializedAs("_toggleActivationKey")]
        [Space, Header("General Activation")]

        [SerializeField, Tooltip("Press this key to toggle the script's active state.")]
        private KeyCode toggleActivationKey = KeyCode.F;  // <-- ADDED SECTION

        [FormerlySerializedAs("_active")] [SerializeField, Tooltip("The script is currently active")]
        private bool active = true;

        [FormerlySerializedAs("_enableRotation")]
        [Space, Header("Rotation Settings")]

        [SerializeField, Tooltip("Camera rotation by mouse movement is active")]
        private bool enableRotation = true;

        [FormerlySerializedAs("_mouseSense")] [SerializeField, Tooltip("Sensitivity of mouse rotation")]
        private float mouseSense = 1.8f;

        [FormerlySerializedAs("_enableTranslation")]
        [Space, Header("Zoom/Translation Settings")]

        [SerializeField, Tooltip("Camera zooming in/out by 'Mouse Scroll Wheel' is active")]
        private bool enableTranslation = true;

        [FormerlySerializedAs("_translationSpeed")] [SerializeField, Tooltip("Velocity of camera zooming in/out")]
        private float translationSpeed = 55f;

        [FormerlySerializedAs("_enableMovement")]
        [Space, Header("Movement Settings")]

        [SerializeField, Tooltip("Camera movement by 'W','A','S','D','Q','E' keys is active")]
        private bool enableMovement = true;

        [FormerlySerializedAs("_movementSpeed")] [SerializeField, Tooltip("Camera movement speed")]
        private float movementSpeed = 10f;

        [FormerlySerializedAs("_boostedSpeed")] [SerializeField, Tooltip("Speed of the quick camera movement when holding the 'Left Shift' key")]
        private float boostedSpeed = 50f;

        [FormerlySerializedAs("_boostSpeed")] [SerializeField, Tooltip("Boost speed key")]
        private KeyCode boostSpeed = KeyCode.LeftShift;

        [FormerlySerializedAs("_moveUp")] [SerializeField, Tooltip("Move up key")]
        private KeyCode moveUp = KeyCode.E;

        [FormerlySerializedAs("_moveDown")] [SerializeField, Tooltip("Move down key")]
        private KeyCode moveDown = KeyCode.Q;

        [FormerlySerializedAs("_enableSpeedAcceleration")]
        [Space, Header("Acceleration Settings")]

        [SerializeField, Tooltip("Acceleration at camera movement is active")]
        private bool enableSpeedAcceleration = true;

        [FormerlySerializedAs("_speedAccelerationFactor")] [SerializeField, Tooltip("Rate which is applied during camera movement")]
        private float speedAccelerationFactor = 1.5f;

        [FormerlySerializedAs("_initPositonButton")]
        [Space, Header("Reset to Initial")]

        [SerializeField, Tooltip("This keypress will move the camera to its initialization position")]
        private KeyCode initPositonButton = KeyCode.R;

        #endregion UI

        // Internal fields
        private CursorLockMode _wantedMode;
        private float _currentIncrease = 1;
        private float _currentIncreaseMem = 0;

        private Vector3 _initPosition;
        private Vector3 _initRotation;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure boosted speed is not less than base movement speed
            if (boostedSpeed < movementSpeed)
                boostedSpeed = movementSpeed;
        }
#endif

        private void Start()
        {
            _initPosition = transform.position;
            _initRotation = transform.eulerAngles;
        }

        private void OnEnable()
        {
            if (active)
            {
                _wantedMode = CursorLockMode.Locked;
            }
        }

        private void SetCursorState()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = _wantedMode = CursorLockMode.None;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _wantedMode = CursorLockMode.Locked;
            }

            // Apply cursor state
            Cursor.lockState = _wantedMode;
            // Hide cursor when locking
            Cursor.visible = (CursorLockMode.Locked != _wantedMode);
        }

        private void CalculateCurrentIncrease(bool moving)
        {
            _currentIncrease = Time.deltaTime;

            if (!enableSpeedAcceleration || (enableSpeedAcceleration && !moving))
            {
                _currentIncreaseMem = 0;
                return;
            }

            _currentIncreaseMem += Time.deltaTime * (speedAccelerationFactor - 1);
            _currentIncrease = Time.deltaTime + Mathf.Pow(_currentIncreaseMem, 3) * Time.deltaTime;
        }

        private void Update()
        {
            // -----------------------------------------------------------------
            // ADDED SECTION: Toggle activation on key press
            // -----------------------------------------------------------------
            if (Input.GetKeyDown(toggleActivationKey))
            {
                active = !active;  // flip the bool

                if (active)
                {
                    // Reactivate => lock cursor again
                    _wantedMode = CursorLockMode.Locked;
                    Cursor.lockState = _wantedMode;
                    Cursor.visible = false;
                }
                else
                {
                    // Deactivate => unlock cursor
                    _wantedMode = CursorLockMode.None;
                    Cursor.lockState = _wantedMode;
                    Cursor.visible = true;
                }
            }
            // -----------------------------------------------------------------

            if (!active)
            {
                // If inactive, do nothing else
                return;
            }

            // If we're active, proceed as usual
            SetCursorState();

            // If the cursor is visible, don't move camera
            if (Cursor.visible)
                return;

            // Translation (zoom in/out)
            if (enableTranslation)
            {
                transform.Translate(Vector3.forward * Input.mouseScrollDelta.y * Time.deltaTime * translationSpeed);
            }

            // Movement
            if (enableMovement)
            {
                Vector3 deltaPosition = Vector3.zero;
                float currentSpeed = movementSpeed;

                if (Input.GetKey(boostSpeed))
                    currentSpeed = boostedSpeed;

                if (Input.GetKey(KeyCode.W))
                    deltaPosition += transform.forward;

                if (Input.GetKey(KeyCode.S))
                    deltaPosition -= transform.forward;

                if (Input.GetKey(KeyCode.A))
                    deltaPosition -= transform.right;

                if (Input.GetKey(KeyCode.D))
                    deltaPosition += transform.right;

                if (Input.GetKey(moveUp))
                    deltaPosition += transform.up;

                if (Input.GetKey(moveDown))
                    deltaPosition -= transform.up;

                // Calc acceleration
                CalculateCurrentIncrease(deltaPosition != Vector3.zero);

                transform.position += deltaPosition * currentSpeed * _currentIncrease;
            }

            // Rotation
            if (enableRotation)
            {
                // Pitch
                transform.rotation *= Quaternion.AngleAxis(
                    -Input.GetAxis("Mouse Y") * mouseSense,
                    Vector3.right
                );

                // Yaw
                transform.rotation = Quaternion.Euler(
                    transform.eulerAngles.x,
                    transform.eulerAngles.y + Input.GetAxis("Mouse X") * mouseSense,
                    transform.eulerAngles.z
                );
            }

            // Return to init position
            if (Input.GetKeyDown(initPositonButton))
            {
                transform.position = _initPosition;
                transform.eulerAngles = _initRotation;
            }
        }
    }
}
