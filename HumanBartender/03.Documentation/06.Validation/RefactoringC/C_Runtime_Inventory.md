# C 런타임 조사 목록

구조·호출·상태 소유권 점검 대상 목록. 모든 파일의 모든 분기를 실행했다는 의미는 아닙니다.

상세 결과: [RefactoringC_SSOT.md](../../RefactoringC_SSOT.md)

| 영역 | 파일 수 |
|---|---:|
| Story | 12 |
| Dialogue | 14 |
| Outside | 36 |
| Cutscene | 61 |
| Camera | 5 |
| Sound | 2 |

별도 C 공용 지점: LogoFade.cs, Tool/ConditionUtil.cs, C 소유 DataNew DTO와 Editor 도구.

## Story

- `Assets/03.Scripts/Story/BarStoryPresenter.cs`
- `Assets/03.Scripts/Story/IStoryCraftGate.cs`
- `Assets/03.Scripts/Story/IStoryPresenter.cs`
- `Assets/03.Scripts/Story/StoryCraftGate.cs`
- `Assets/03.Scripts/Story/StoryExecutionResult.cs`
- `Assets/03.Scripts/Story/StoryFlow.cs`
- `Assets/03.Scripts/Story/StoryGrade.cs`
- `Assets/03.Scripts/Story/StoryOrder.cs`
- `Assets/03.Scripts/Story/StoryResultContext.cs`
- `Assets/03.Scripts/Story/StorySceneCursor.cs`
- `Assets/03.Scripts/Story/StoryScriptRunner.cs`
- `Assets/03.Scripts/Story/StoryServeDropTarget.cs`

## Dialogue

- `Assets/03.Scripts/Dialogue/CharacterAnimTypes.cs`
- `Assets/03.Scripts/Dialogue/CharacterLoader.cs`
- `Assets/03.Scripts/Dialogue/CharacterPart.cs`
- `Assets/03.Scripts/Dialogue/ChoicePanel.cs`
- `Assets/03.Scripts/Dialogue/DialogueCharacterManager.cs`
- `Assets/03.Scripts/Dialogue/DialogueRunner.cs`
- `Assets/03.Scripts/Dialogue/DialogueTypingService.cs`
- `Assets/03.Scripts/Dialogue/ICharacterSetter.cs`
- `Assets/03.Scripts/Dialogue/IDialogueFader.cs`
- `Assets/03.Scripts/Dialogue/IDialoguePresenter.cs`
- `Assets/03.Scripts/Dialogue/IFade.cs`
- `Assets/03.Scripts/Dialogue/IPlaybackPolicy.cs`
- `Assets/03.Scripts/Dialogue/UIDialogueChoiceView.cs`
- `Assets/03.Scripts/Dialogue/UIDialogueTextView.cs`

## Outside

- `Assets/03.Scripts/Outside/Camera/OutsideCamera.cs`
- `Assets/03.Scripts/Outside/Entity/Action/InteractiveTriggerEntity.cs`
- `Assets/03.Scripts/Outside/Entity/Bubi.cs`
- `Assets/03.Scripts/Outside/Entity/InteractEntrance.cs`
- `Assets/03.Scripts/Outside/Entity/InteractionAction.cs`
- `Assets/03.Scripts/Outside/Entity/InteractiveActionEntity.cs`
- `Assets/03.Scripts/Outside/Entity/InteractiveEntity.cs`
- `Assets/03.Scripts/Outside/Entity/InteractiveEntityManager.cs`
- `Assets/03.Scripts/Outside/Entity/InteractiveNPCEntity.cs`
- `Assets/03.Scripts/Outside/Entity/NPC_Samho.cs`
- `Assets/03.Scripts/Outside/Entity/OutsideElevator.cs`
- `Assets/03.Scripts/Outside/Entity/OutsideElevatorRadio.cs`
- `Assets/03.Scripts/Outside/Entity/OutsideEntity.cs`
- `Assets/03.Scripts/Outside/IEntity.cs`
- `Assets/03.Scripts/Outside/IInteractable.cs`
- `Assets/03.Scripts/Outside/IInteractor.cs`
- `Assets/03.Scripts/Outside/IOutsideTimeliner.cs`
- `Assets/03.Scripts/Outside/ITrackedble.cs`
- `Assets/03.Scripts/Outside/ITrackedbleEvent.cs`
- `Assets/03.Scripts/Outside/InteractableEvent.cs`
- `Assets/03.Scripts/Outside/InteractionDetector.cs`
- `Assets/03.Scripts/Outside/InteractionStateLease.cs`
- `Assets/03.Scripts/Outside/InteractorEvent.cs`
- `Assets/03.Scripts/Outside/NpcInteractable.cs`
- `Assets/03.Scripts/Outside/OustideTimelineManager.cs`
- `Assets/03.Scripts/Outside/OutSideEventSO.cs`
- `Assets/03.Scripts/Outside/OutsideActions.cs`
- `Assets/03.Scripts/Outside/OutsideDialoguePresenter.cs`
- `Assets/03.Scripts/Outside/OutsideSpotManager.cs`
- `Assets/03.Scripts/Outside/SpotPoint.cs`
- `Assets/03.Scripts/Outside/UIInteractableButton.cs`
- `Assets/03.Scripts/Outside/UIOutsideTextview.cs`
- `Assets/03.Scripts/Outside/UIOutsideTracker.cs`
- `Assets/03.Scripts/Outside/gimmic/OutsidPhonePlayer.cs`
- `Assets/03.Scripts/Outside/gimmic/OutsidePhoneButton.cs`
- `Assets/03.Scripts/Outside/gimmic/OutsidePhoneGimmick.cs`

## Cutscene

- `Assets/03.Scripts/Cutscene/CutSceneAnchor.cs`
- `Assets/03.Scripts/Cutscene/CutSceneTimelineTypes.cs`
- `Assets/03.Scripts/Cutscene/CutsceneManager.cs`
- `Assets/03.Scripts/Cutscene/ICutScenePlayer.cs`
- `Assets/03.Scripts/Cutscene/IEffectPlayer.cs`
- `Assets/03.Scripts/Cutscene/ITimeLinePlayer.cs`
- `Assets/03.Scripts/Cutscene/Outside Timeline/CutsceneDialogueHandler.cs`
- `Assets/03.Scripts/Cutscene/Outside Timeline/Outside/SpriteRevealBinder.cs`
- `Assets/03.Scripts/Cutscene/Outside Timeline/SpriteRevealBehaviour.cs`
- `Assets/03.Scripts/Cutscene/Outside Timeline/SpriteRevealClip.cs`
- `Assets/03.Scripts/Cutscene/Outside Timeline/SpriteRevealMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/Outside Timeline/SpriteRevealTrack.cs`
- `Assets/03.Scripts/Cutscene/SpineAnimationManager.cs`
- `Assets/03.Scripts/Cutscene/SpriteAnimationManager.cs`
- `Assets/03.Scripts/Cutscene/TimelinePlayback.cs`
- `Assets/03.Scripts/Cutscene/TimelineTransitionScope.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneBGBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneBGClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneBGMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneBGTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneDialogueBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneDialogueClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneDialogueMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneDialogueTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneEffectBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneEffectClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneEffectMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneEffectTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneImageBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneImageClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneImageMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneImageTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneMoveBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneMoveClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneMoveMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneMoveTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneResolutionBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneResolutionClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneResolutionMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneResolutionTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRootMoveBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRootMoveClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRootMoveMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRootMoveTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRotateYBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRotateYClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRotateYMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneRotateYTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneShakeBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneShakeClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneShakeMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneShakeTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneSpriteAnimBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneSpriteAnimClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneSpriteAnimMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneSpriteAnimTrack.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneTimelineManager.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneZoomBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneZoomClip.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneZoomMixerBehaviour.cs`
- `Assets/03.Scripts/Cutscene/UI Timeline/CutSceneZoomTrack.cs`

## Camera

- `Assets/03.Scripts/Camera/CameraControllerNew.cs`
- `Assets/03.Scripts/Camera/ICameraControlNew.cs`
- `Assets/03.Scripts/Camera/ISlotCamera.cs`
- `Assets/03.Scripts/Camera/PlayCamera.cs`
- `Assets/03.Scripts/Camera/ResolutionZoomTest.cs`

## Sound

- `Assets/03.Scripts/Sound/ISoundManager.cs`
- `Assets/03.Scripts/Sound/SoundManager.cs`
