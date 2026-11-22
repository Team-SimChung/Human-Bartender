using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal interface IStudioFileHost : IStudioPanelHost
    {
        StudioSequence Sequence { get; }
        void Load(StudioSequence sequence);
        void Run(Action action);
        void SetStatus(string message);
    }

    // 컷씬 생성·저장·내보내기 경로와 편집기 파일 작업을 처리함.
    internal sealed class StudioFileCommands
    {
        private readonly IStudioFileHost host;
        private readonly StudioSaveCoordinator saves;
        private CutsceneStudioWindow Window => host.Window;
        private StudioSequence sequence => host.Sequence;

        internal StudioFileCommands(IStudioFileHost host, StudioSaveCoordinator saves)
        {
            this.host = host;
            this.saves = saves;
        }

        internal void Create()
        {
            StudioEditorAssets.EnsureFolder(StudioEditorAssets.Root);
            string path = EditorUtility.SaveFilePanelInProject("새 컷씬", "new_cutscene", "asset", "컷씬 편집 원본 저장 위치", StudioEditorAssets.Root);
            if (string.IsNullOrEmpty(path))
                return;
            host.Load(StudioEditorAssets.Create(path));
        }

        internal void ShowSaveMenu(Rect anchor)
        {
            var menu = new UnityEngine.UIElements.GenericDropdownMenu();
            menu.AddItem("중간 저장  Ctrl+S", false, Save);
            menu.AddItem("컷씬으로 저장 / Timeline / 게임에 등록", false, RegisterCurrentSequence);
            menu.AddItem("재생용 씬 생성 / 갱신", false, ChooseSceneExportPath);
            menu.AddSeparator("");
            menu.AddItem("다른 이름으로 저장", false, ChooseDuplicatePath);
            menu.AddItem("Unity Timeline 에셋 보기", false, RevealTimelineAsset);
            StudioGUI.ShowMenu(menu, anchor, Window);
            void RegisterCurrentSequence()
            {
                host.Run(ExportCurrentPrefab);
                void ExportCurrentPrefab()
                {
                    StudioEditorAssets.ExportPrefab(sequence);
                    saves.AcknowledgeSave();
                    host.SetStatus("게임에 등록됨 / ID: " + sequence.Id);
                }
            }

            void ChooseSceneExportPath()
            {
                string path = EditorUtility.SaveFilePanelInProject("재생용 씬 저장", sequence.name, "unity", "원본 Timeline을 공유하는 재생용 씬", Path.GetDirectoryName(AssetDatabase.GetAssetPath(sequence)));
                if (!string.IsNullOrEmpty(path))
                    host.Run(ExportSceneToPath);
                void ExportSceneToPath()
                {
                    StudioEditorAssets.ExportScene(sequence, path);
                    saves.AcknowledgeSave();
                    host.SetStatus("씬 저장됨 / " + path);
                }
            }

            void ChooseDuplicatePath()
            {
                string path = EditorUtility.SaveFilePanelInProject("컷씬 복제", sequence.name + "_copy", "asset", "새 컷씬 원본을 만듭니다.", StudioEditorAssets.Root);
                if (!string.IsNullOrEmpty(path))
                    host.Run(DuplicateToPath);
                void DuplicateToPath()
                {
                    host.Load(StudioEditorAssets.Duplicate(sequence, path));
                }
            }

            void RevealTimelineAsset()
            {
                Selection.activeObject = sequence.Timeline;
                EditorGUIUtility.PingObject(sequence.Timeline);
            }
        }

        internal void Save() => host.Run(SaveCurrentSequence);

        private void SaveCurrentSequence()
        {
            saves.Save();
            host.SetStatus("저장됨");
        }
    }
}
