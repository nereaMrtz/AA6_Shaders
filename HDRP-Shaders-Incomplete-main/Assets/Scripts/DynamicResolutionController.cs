using System.Collections;
using System.Collections.Generic;
using Tayx.Graphy.Advanced;
using UnityEngine;
using UnityEngine.Rendering;

public class DynamicResolutionController : MonoBehaviour
{
    public float minResolution = .25f;
    float nextStepUp;
    public float stepUpWait;
    public float step;
    public int overheadFps = 10;
    public float targetFramerate = 60;
    public bool dynamic = true;
    public float currentScale = 1.0f;
    float lastDeltaTime;

    public static Camera cam;

    public float SetDynamicResolutionScale()
    {
        if (dynamic)
        {
            float current = 1f / Time.deltaTime;
            if (current < targetFramerate - 1)
            {
                currentScale -= step;
            }
            if (Time.time > nextStepUp && current > targetFramerate + overheadFps)
            {
                currentScale += step;
                nextStepUp = Time.time + stepUpWait;
            }
        }
        lastDeltaTime = Time.deltaTime;
        currentScale = Mathf.Clamp(currentScale, minResolution, 1f);
        G_AdvancedData.dynamicResolutionScale = currentScale;
        return currentScale * 100f;
    }

    void Start()
    {
        DynamicResolutionHandler.SetDynamicResScaler(SetDynamicResolutionScale, DynamicResScalePolicyType.ReturnsPercentage);
        cam = GetComponent<Camera>();
    }
}
