using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HorrorLand.MenuSystem;

[RequireComponent(typeof(CharacterController))]

public class SC_FPSController : MonoBehaviour
{
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

    CharacterController characterController;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    [HideInInspector]
    public bool canMove = true;
    public bool allowSprinting = true;
    public bool IsSprinting { get; private set; }

    private float currentLookSpeed;
    private float mouseYMultiplier;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        ApplySavedLookSettings();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        bool isRunning = allowSprinting && Input.GetKey(KeyCode.LeftShift);
        IsSprinting = isRunning && canMove && (Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f || Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f);
        float curSpeedX = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpSpeed;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        characterController.Move(moveDirection * Time.deltaTime);

        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * currentLookSpeed * mouseYMultiplier;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * currentLookSpeed, 0);
        }
    }

    private void ApplySavedLookSettings()
    {
        GameSettingsStore.EnsureDefaults();

        currentLookSpeed = GameSettingsStore.GetFloat(MenuPrefsKeys.MouseSensitivity, lookSpeed);
        bool invertMouse = GameSettingsStore.GetInt(MenuPrefsKeys.InvertMouse, 0) == 1;
        mouseYMultiplier = invertMouse ? -1f : 1f;
    }
}
