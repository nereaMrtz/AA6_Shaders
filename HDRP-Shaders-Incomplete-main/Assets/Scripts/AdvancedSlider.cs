using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class AdvancedSlider : MonoBehaviour
{
    public Slider slider;
    public TMP_InputField input;
    public float value { get
        {
            return slider.value;
        } 
    }

    public UnityEvent<float> OnChange;
    public UnityEvent<int> OnChangeInt;
    private void OnValidate()
    {
        Setup();
    }
    private void Reset()
    {
        Setup();
    }
    private void Start()
    {
        Setup();
        input?.onValueChanged.AddListener(InputChange);
        slider?.onValueChanged.AddListener(SliderChange);
    }
    void Setup()
    {
        if(input == null)
        {
            input = GetComponentInChildren<TMP_InputField>();
        }
        if(slider == null)
        {
            slider = GetComponentInChildren<Slider>();
        }
    }
    public void SetValue(float value, bool fire = true)
    {
        if (slider)
            slider.value = value;
        if (input)
            input.text = value.ToString();
        if(fire)
        OnChange?.Invoke(value);
        OnChangeInt?.Invoke((int)value);
    }
    void InputChange(string value)
    {
        float valuef = float.Parse(value);
        if (slider)
            slider.value = valuef;
        OnChange?.Invoke(valuef);
        OnChangeInt?.Invoke((int)valuef);
    }
    void SliderChange(float value)
    {
        if (input)
            input.text = value.ToString();
        OnChange?.Invoke(value);
        OnChangeInt?.Invoke((int)value);
    }
}
