using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public Vector2 sensitivity;
    public Vector2 limit;
    public float speed;
    public float runSpeedMultiplier;
    float rot_x;

    private void OnEnable()
    {
        rot_x = transform.eulerAngles.x;
    }
    void Update()
    {
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            rot_x = Mathf.Clamp(rot_x + Input.GetAxis("Mouse Y") * sensitivity.y, limit.x, limit.y);

            transform.localEulerAngles = new Vector3(rot_x, transform.localEulerAngles.y + Input.GetAxis("Mouse X") * sensitivity.x, 0);
        }


        float nSpeed = speed;

        if (Input.GetButton("Run"))
        {
            nSpeed = speed * runSpeedMultiplier;
        }

        transform.position += (transform.forward * Input.GetAxis("Forward") + transform.right * Input.GetAxis("Horizontal") + transform.up * Input.GetAxis("Vertical")) * nSpeed * Time.deltaTime;

    }

    public void SetSpeed(float speed)
    {
        this.speed = speed;
    }

    public void SetSensitivityX(float sensitivity)
    {
        this.sensitivity.x = sensitivity;
    }
    public void SetSensitivityY(float sensitivity)
    {
        this.sensitivity.y = sensitivity;
    }
}
