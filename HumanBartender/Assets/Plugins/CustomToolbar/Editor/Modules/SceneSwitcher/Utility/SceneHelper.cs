using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static class SceneHelper
    {
        private static string _sceneToOpen;
        private static bool _isAutoPlay;

        private const string TargetSceneFolderPath = "Assets/00.Scenes"; // 여기롤 변경해주세요.

        /// <summary>
        /// 지정된 씬을 시작하고 선택적으로 플레이 모드를 시작합니다.
        /// </summary>
        /// <param name="sceneName">열려는 씬의 이름입니다.</param>
        /// <param name="isPlay">true이면 씬이 플레이 모드에서 시작됩니다.</param>
        public static void StartScene(string sceneName, bool isPlay)
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            _sceneToOpen = sceneName;
            _isAutoPlay = isPlay;

            EditorApplication.update += OnUpdate;
        }

        /// <summary>
        /// 모든 씬을 찾습니다.
        /// </summary>
        /// <returns>씬 경로와 씬 이름의 딕셔너리를 반환합니다.</returns>
        public static Dictionary<string, string> FindAllScenes()
        {
            // ScenePath-SceneName
            Dictionary<string, string> result = new();

            string[] guids = AssetDatabase.FindAssets("t:scene ", new[] { TargetSceneFolderPath });

            foreach (string guid in guids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                string filterSceneName = scenePath.Replace(".unity", "");

                try
                {
                    string resultPath = filterSceneName.Replace($"{TargetSceneFolderPath}/", "");
                    string resultSceneName = scenePath;

                    result.Add(resultPath, resultSceneName);
                }
                catch (Exception)
                {
                }
            }

            return result;
        }

        /// <summary>
        /// 타겟 씬 경로를 반환합니다.
        /// </summary>
        public static string TargetScenePath
        {
            get
            {
                // 첫번째 씬 이름을 가져옵니다.
                string[] allLevelName = GetAllScenesInBuildSettings();

                if (allLevelName == null)
                    return "";

                return allLevelName[0];
            }
        }
        
        /// <summary>
        /// 에디터 업데이트 콜백입니다.
        /// 씬을 열고 자동 재생 모드를 설정합니다.
        /// </summary>
        private static void OnUpdate()
        {
            if (_sceneToOpen == null ||
                EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= OnUpdate;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(_sceneToOpen);
                EditorApplication.isPlaying = _isAutoPlay;
            }

            _isAutoPlay = false;
            _sceneToOpen = null;
        }
        
        /// <summary>
        /// 빌드 세팅스에 등록된 씬 중 첫번째 씬의 이름을 반환합니다.
        /// </summary>
        /// <returns>빌드 세팅스에 등록된 씬 중 첫번째 씬의 이름을 반환합니다. 등록된 씬이 없으면 "None" 문자열을 반환합니다.</returns>
        public static string FirstSceneName
        {
            get
            {
                // 첫번째 씬 이름을 가져옵니다.
                string[] allLevelName = GetAllScenesInBuildSettings();
                return allLevelName == null ? "None" : Path.GetFileNameWithoutExtension(allLevelName[0]);
            }
        }
        
        /// <summary>
        /// 빌드 세팅스에 등록된 모든 씬들의 경로를 배열로 반환합니다.
        /// </summary>
        /// <returns>빌드 세팅스에 등록된 씬들의 경로 배열입니다. 등록된 씬이 없으면 null을 반환합니다.</returns>
        private static string[] GetAllScenesInBuildSettings()
        {
            List<string> list = new List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    list.Add(scene.path);

            return list.Count == 0 ? null : list.ToArray();
        }
        
#if !UNITY_6000_3_OR_NEWER
        /// <summary>
        /// 드롭다운 메뉴를 표시합니다.
        /// </summary>
        /// <param name="dropdown">드롭다운 메뉴를 표시할 요소입니다.</param>
        public static void ShowMenuItem(VisualElement dropdown)
        {
            var menu = new GenericMenu();
            Dictionary<string, string> allScenes = FindAllScenes();
        
            foreach ((string scenePath, string sceneAllPath) in allScenes)
                menu.AddItem(new GUIContent($"{scenePath}"), false, OnClickDropdown, sceneAllPath);
        
            menu.DropDown(dropdown.worldBound);
        }
#else
        /// <summary>
        /// 드롭다운 메뉴를 표시합니다.
        /// </summary>
        /// <param name="obj">드롭다운 메뉴를 표시할 요소입니다.</param>
        public static void ShowMenuItem(Rect obj)
        {
            var menu = new GenericMenu();
            Dictionary<string, string> allScenes = FindAllScenes();
        
            foreach ((string scenePath, string sceneAllPath) in allScenes)
                menu.AddItem(new GUIContent($"{scenePath}"), false, OnClickDropdown, sceneAllPath);
        
            menu.DropDown(obj);
        }
#endif
        
        /// <summary>
        /// 드롭다운 메뉴 항목을 클릭했을 때 호출되는 메서드입니다.
        /// </summary>
        /// <param name="parameter">클릭된 항목의 경로입니다.</param>
        private static void OnClickDropdown(object parameter)
        {
            StartScene((string)parameter, false);
        }

        public static void MoveFirstScene()
        {
            StartScene(TargetScenePath, true);
        }
    }
}