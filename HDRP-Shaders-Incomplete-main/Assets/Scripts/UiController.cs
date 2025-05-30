using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class UiController : MonoBehaviour
{
    public GameObject[] options;
    [Header("Camera options")]
    public Animator cameraAnim;
    public CameraController cameraController;
    public GameObject[] cameraSettings;
    public AdvancedSlider cameraSensitivityY;
    public AdvancedSlider cameraSensitivityX;
    public AdvancedSlider cameraSpeed;
    public AdvancedSlider cameraFOV;
    [Header("Graphics Settings")]
    public Camera main;
    public DynamicResolutionController resolutionController;
    public Camera noEffects;
    public TMP_Dropdown quality;
    public AdvancedSlider renderScale;
    public AdvancedSlider targetFramerate;
    // Start is called before the first frame update
    void Start()
    {
        cameraSensitivityY.SetValue(cameraController.sensitivity.y);
        cameraSensitivityX.SetValue(cameraController.sensitivity.x);
        cameraSpeed.SetValue(cameraController.speed);
        cameraFOV.SetValue(main.fieldOfView, false);
        targetFramerate.SetValue(60, false);
        FreeCamera(false);

        quality.AddOptions(QualitySettings.names.ToList());
        quality.value = QualitySettings.GetQualityLevel();
        quality.onValueChanged.AddListener(QualitySettings.SetQualityLevel);
    }

    // Update is called once per frame
    void Update()
    {
        if (cameraAnim.enabled)
        {
            cameraFOV.SetValue(main.fieldOfView, false);
        }
        if (resolutionController.dynamic)
        {
            renderScale.SetValue(resolutionController.currentScale * 100f, false);
        }
    }
    #region CAMERA OPTIONS
    public void ToggleOptions(bool enable)
    {
        foreach (var option in options)
        {
            option.SetActive(enable);
        }
        if (enable)
            FreeCamera(cameraController.enabled);
    }
    public void FreeCamera(bool enable)
    {
        cameraAnim.enabled = !enable;
        cameraController.enabled = enable;
        foreach (var camera in cameraSettings)
        {
            camera.SetActive(enable);
        }
    }
    public void SetFOV(float fov)
    {
        main.fieldOfView = fov;
        noEffects.fieldOfView = fov;
    }
    #endregion
    #region GRAPHICS SETTINGS
    public void ToggleNoEffects(bool noEffects)
    {
        main.enabled = !noEffects;
        this.noEffects.enabled = noEffects;
    }
    public void ToggleDynamicResolution(bool enabled)
    {
        resolutionController.dynamic = enabled;
    }
    public void TargetFramerate(int target)
    {
        if (target <= 0) target = 1;
        resolutionController.targetFramerate = target;
    }
    public void ResolutionScaling(float scale)
    {
        resolutionController.currentScale = scale / 100f;
    }
    #endregion

}
