using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Play 씬의 제조 신호에 맞춰 준비 화면 프리팹을 만들고 제거한다.</summary>
public sealed class CraftPrepStageHost : MonoBehaviour
{
    [SerializeField] private CraftFlowController craftFlow;
    [SerializeField] private CraftPrepStageScreen stagePrefab;
    [Tooltip("바와 기믹 무대에서 떨어진 제조 준비 프리팹의 생성 위치.")]
    [SerializeField] private Transform stageRoot;
    [SerializeField] private PlayerInput playerInput;
    [Tooltip("준비 중 잠시 숨길 바 UI. 종료 시 원래 켜져 있던 캔버스만 복구한다.")]
    [SerializeField] private Canvas[] barCanvases;

    private CraftPrepStageScreen activeStage;
    private readonly List<Canvas> hiddenBarCanvases = new List<Canvas>();
    private string returnActionMap;

    public bool IsOpen { get { return activeStage != null && activeStage.IsOpen; } }

    private void OnEnable()
    {
        if (craftFlow == null) return;
        craftFlow.CraftBegan += OnCraftBegan;
        craftFlow.PreparationChanged += OnPreparationChanged;
        if (craftFlow.Preparation != null) Show(craftFlow.Preparation);
    }

    private void OnDisable()
    {
        if (craftFlow != null)
        {
            craftFlow.CraftBegan -= OnCraftBegan;
            craftFlow.PreparationChanged -= OnPreparationChanged;
        }
        Hide();
    }

    private void OnCraftBegan(CraftSession session)
    {
        Show(craftFlow.Preparation);
    }

    private void OnPreparationChanged(CraftPreparation preparation)
    {
        if (preparation == null) Hide();
    }

    private void Show(CraftPreparation preparation)
    {
        if (preparation == null) return;
        if (stagePrefab == null || stageRoot == null || playerInput == null)
            throw new InvalidOperationException("[CraftPrep] Stage Prefab, Stage Root, Player Input을 연결해 주세요.");
        Hide();
        returnActionMap = playerInput.currentActionMap != null ? playerInput.currentActionMap.name : null;
        try
        {
            foreach (Canvas canvas in barCanvases ?? Array.Empty<Canvas>())
            {
                if (canvas == null || !canvas.enabled) continue;
                canvas.enabled = false;
                hiddenBarCanvases.Add(canvas);
            }
            activeStage = Instantiate(stagePrefab, stageRoot, false);
            activeStage.Open(preparation, craftFlow);
            playerInput.SwitchCurrentActionMap("CraftPrep");
        }
        catch
        {
            Hide();
            throw;
        }
    }

    private void Hide()
    {
        if (activeStage != null)
        {
            activeStage.Close();
            // Destroy는 프레임 끝에 적용되므로 전용 카메라와 입력을 먼저 끈다.
            activeStage.gameObject.SetActive(false);
            Destroy(activeStage.gameObject);
            activeStage = null;
        }
        if (playerInput != null && !string.IsNullOrEmpty(returnActionMap))
            playerInput.SwitchCurrentActionMap(returnActionMap);
        returnActionMap = null;
        foreach (Canvas canvas in hiddenBarCanvases)
            if (canvas != null) canvas.enabled = true;
        hiddenBarCanvases.Clear();
    }

    public void Previous() { if (activeStage != null) activeStage.Previous(); }
    public void Next() { if (activeStage != null) activeStage.Next(); }
}
