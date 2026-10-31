using System;
using System.Collections.Generic;
using UnityEngine;

public class ControlsDisplayer : MonoBehaviour
{
    [SerializeField] private List<InputInfoData> data;
    [SerializeField] private InputInfo inputInfoPrefab;
    [SerializeField] private RectTransform inputInfoHolder;

    private void Start()
    {
        DisplayInputIcons();
    }

    private void DisplayInputIcons()
    {
        bool isDark = true;
        foreach (InputInfoData infoData in data)
        {
            InputInfo inputInfo = Instantiate(inputInfoPrefab, inputInfoHolder);
            inputInfo.Setup(infoData, isDark);
            isDark = !isDark;
        }
    }
}
