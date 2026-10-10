using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using System.Linq;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.IO;
using HumanBartender.CutsceneStudio;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 패널의 표시와 입력을 처리하며 다른 패널은 호스트 계약으로 연결함.
    [Serializable]
    internal sealed class StudioSequencePanel
    {
        [NonSerialized] private IStudioSequenceHost host;
        private CutsceneStudioWindow Window => host.Window;
        internal void Initialize(IStudioSequenceHost value) { host = value; }
        [SerializeField]
        private bool objectsOpen = true;

        [SerializeField]
        private bool sequenceInfoOpen = true, paletteOpen = true, assetsOpen = true;

        [SerializeField]
        private string assetSearch = "", assetFolder = "Assets";

        [SerializeField]
        private StudioAssetType assetFilter;

        [NonSerialized] private Vector2 assetScroll;

        [NonSerialized] private Vector2 sceneObjectScroll;

        [SerializeField]
        private string sceneObjectSearch = "";

        [NonSerialized] private StudioLibraryItem[] libraryItems = Array.Empty<StudioLibraryItem>();

        [NonSerialized] private bool libraryQueued;



        // 씬 오브젝트 행에서 타임라인에 표시한 같은 이름의 클립으로 이동함.
        private StudioEntry[] SceneObjectEntries()
        {
            var clips = StudioEvaluation.Clips(host.Sequence, true);
            var rows = new System.Collections.Generic.List<StudioEntry>();
            var camera = clips.FirstOrDefault(MatchesClipsFirstOrDefault);
            if (camera.Clip != null)
                rows.Add(camera);
            foreach (var actor in host.Sequence.Actors)
            {
                var owned = clips.Where(MatchesClipsWhere).ToArray();
                var entry = owned.FirstOrDefault(MatchesOwnedFirstOrDefault);
                if (entry.Clip == null)
                    entry = owned.FirstOrDefault(MatchesOwnedFirstOrDefault2);
                if (entry.Clip == null)
                    entry = owned.FirstOrDefault();
                if (entry.Clip != null)
                    rows.Add(entry);
                bool MatchesClipsWhere(StudioEntry e)
                {
                    return StudioEvaluation.OwnsActor(e.Asset) && e.Asset.ActorId == actor.Id;
                }

                bool MatchesOwnedFirstOrDefault(StudioEntry e)
                {
                    return e.Asset == host.Selected;
                }

                bool MatchesOwnedFirstOrDefault2(StudioEntry e)
                {
                    return e.Asset.Kind == StudioKind.Visual;
                }
            }

            rows.AddRange(clips.Where(MatchesClipsWhere2));
            return rows.ToArray();
            bool MatchesClipsFirstOrDefault(StudioEntry e)
            {
                return StudioKeyframes.IsCamera(e.Asset);
            }

            bool MatchesClipsWhere2(StudioEntry e)
            {
                return e.Asset.Kind == StudioKind.Background || e.Asset.Kind == StudioKind.Audio;
            }
        }


        private static string SceneObjectType(StudioClip data) => StudioKeyframes.IsCamera(data) ? "카메라" : StudioEditorAssets.Label(data.Kind);


        internal void ChooseActor(int index)
        {
            var entries = StudioEvaluation.Clips(host.Sequence, true).Where(MatchesTrueWhere).ToArray();
            var entry = entries.LastOrDefault(MatchesEntriesLastOrDefault);
            if (entry.Clip == null)
                entry = entries.OrderBy(SelectEntriesOrderBy).FirstOrDefault();
            if (entry.Clip != null)
                host.Select(entry.Clip);
            bool MatchesTrueWhere(StudioEntry e)
            {
                return StudioEvaluation.OwnsActor(e.Asset) && e.Asset.ActorId == host.Sequence.Actors[index].Id;
            }

            bool MatchesEntriesLastOrDefault(StudioEntry e)
            {
                return e.Contains(host.Clock.Time);
            }

            double SelectEntriesOrderBy(StudioEntry e)
            {
                return Math.Abs(e.Clip.start - host.Clock.Time);
            }
        }


        private void SelectCamera()
        {
            var entries = StudioEvaluation.Clips(host.Sequence, true).Where(MatchesTrueWhere2).ToArray();
            var entry = entries.LastOrDefault(MatchesEntriesLastOrDefault2);
            if (entry.Clip == null)
                entry = entries.OrderBy(SelectEntriesOrderBy2).FirstOrDefault();
            if (entry.Clip != null)
                host.Select(entry.Clip);
            bool MatchesTrueWhere2(StudioEntry e)
            {
                return e.Asset.Kind == StudioKind.Camera || e.Asset.Kind == StudioKind.State && e.Asset.StateCamera;
            }

            bool MatchesEntriesLastOrDefault2(StudioEntry e)
            {
                return e.Contains(host.Clock.Time);
            }

            double SelectEntriesOrderBy2(StudioEntry e)
            {
                return Math.Abs(e.Clip.start - host.Clock.Time);
            }
        }


        internal void HandleLibraryDrop(Rect frame, bool timeline)
        {
            host.ProcessDrop(frame, false, host.Clock.Time, null);
        }

        [NonSerialized] private ListView objectList, assetList;

        [NonSerialized] private Label assetCount;

        [NonSerialized] private TextField sequenceTitle;

        [NonSerialized] private StudioEntry[] visibleObjects = Array.Empty<StudioEntry>();

        [NonSerialized] private StudioLibraryItem[] visibleAssets = Array.Empty<StudioLibraryItem>();

        // 드래그 검증 시 운영체제 드래그 시작만 대체하고 전달 데이터와 포인터 처리는 유지함.
        [NonSerialized] private Action<string> beginNativeDrag = DragAndDrop.StartDrag;


        internal void BuildSequencePanel()
        {
            var scroll = sequenceBody.scrollOffset;
            sequenceBody.Clear();
            objectList = null;
            sequenceTitle = null;
            if (host.Sequence != null)
            {
                var info = StudioGUI.Section(foldouts, sequenceBody, "시퀀스 설정", "sequence-info", sequenceInfoOpen, StoreSequenceFoldout);
                info.Add(new Label("시퀀스 이름"));
                sequenceTitle = new TextField
                {
                    name = "sequence-title",
                    isDelayed = true,
                    value = host.Sequence.Title
                };
                sequenceTitle.AddToClassList("studio-title-input");
                sequenceTitle.RegisterValueChangedCallback(OnSequencetitleRegisterValueChangedCallback);
                info.Add(sequenceTitle);
                var palette = StudioGUI.Section(foldouts, sequenceBody, "클립 추가", "palette", paletteOpen, StorePaletteFoldout);
                var definitions = StudioClipSchema.Definitions;
                for (int i = 0; i < definitions.Count; i += 2)
                {
                    var row = new VisualElement();
                    row.AddToClassList("studio-palette-row");
                    palette.Add(row);
                    for (int j = i; j < Mathf.Min(i + 2, definitions.Count); j++)
                    {
                        var definition = definitions[j];
                        var kind = definition.Kind;
                        var button = StudioGUI.ActionButton(definition.Icon + "  " + definition.Label, AddPaletteClip, "add-" + kind);
                        button.AddToClassList("studio-palette-button");
                        button.tooltip = "클릭: 현재 시간에 추가 / 드래그: 타임라인에 배치";
                        var stripe = new VisualElement
                        {
                            pickingMode = PickingMode.Ignore
                        };
                        stripe.AddToClassList("studio-palette-stripe");
                        stripe.style.backgroundColor = definition.Color;
                        button.Add(stripe);
                        RegisterNativeDrag(button, NoAsset, GetPaletteKind);
                        if (j == i + 1)
                            button.AddToClassList("second-column");
                        row.Add(button);
                        void AddPaletteClip()
                        {
                            host.AddPalette(kind, host.Clock.Time);
                        }

                        UnityEngine.Object NoAsset()
                        {
                            return null;
                        }

                        StudioKind? GetPaletteKind()
                        {
                            return kind;
                        }
                    }

                    if (i + 1 == definitions.Count)
                    {
                        var spacer = new VisualElement();
                        spacer.style.flexGrow = 1;
                        spacer.style.flexBasis = 0;
                        spacer.style.marginLeft = 4;
                        row.Add(spacer);
                    }
                }

                var objects = StudioGUI.Section(foldouts, sequenceBody, "씬 오브젝트", "scene-objects", objectsOpen, StoreObjectsFoldout);
                var search = new ToolbarSearchField
                {
                    name = "scene-object-search",
                    value = sceneObjectSearch
                };
                search.RegisterValueChangedCallback(SearchSceneObjects);
                objects.Add(search);
                objectList = MakeCardList("scene-object-list", false);
                objectList.bindItem = BindSceneObject;
                objects.Add(objectList);
                objectList.Q<ScrollView>().verticalScroller.valueChanged += StoreObjectScroll;
                RefreshObjectList();
                var objectOffset = sceneObjectScroll;
                objectList.schedule.Execute(RestoreObjectScroll);
                void StoreSequenceFoldout(bool v)
                {
                    sequenceInfoOpen = v;
                }

                void OnSequencetitleRegisterValueChangedCallback(ChangeEvent<string> e)
                {
                    Undo.RecordObject(host.Sequence, "시퀀스 이름 변경");
                    host.Sequence.DisplayName = e.newValue;
                    host.Changed();
                }

                void StorePaletteFoldout(bool v)
                {
                    paletteOpen = v;
                }

                void StoreObjectsFoldout(bool v)
                {
                    objectsOpen = v;
                }

                void SearchSceneObjects(ChangeEvent<string> e)
                {
                    sceneObjectSearch = e.newValue;
                    sceneObjectScroll = Vector2.zero;
                    RefreshObjectList();
                    if (visibleObjects.Length > 0)
                        objectList.ScrollToItem(0);
                }

                void StoreObjectScroll(float v)
                {
                    sceneObjectScroll.y = v;
                }

                void RestoreObjectScroll()
                {
                    if (objectList != null)
                        objectList.Q<ScrollView>().scrollOffset = objectOffset;
                }
            }
            else
                sequenceBody.Add(new HelpBox("새 컷씬을 만들거나 상단에서 저장된 시퀀스를 선택하세요.", HelpBoxMessageType.None));
            BuildAssetPanel();
            sequenceBody.scrollOffset = scroll;
        }


        private ListView MakeCardList(string name, bool assets)
        {
            var list = new ListView
            {
                name = name,
                fixedItemHeight = 56,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                reorderable = false
            };
            list.AddToClassList("studio-card-list");
            list.makeItem = CreateCard;
            return list;
            VisualElement CreateCard()
            {
                var row = new VisualElement();
                row.AddToClassList("studio-card");
                row.Add(new Image { name = "thumbnail", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
                row.Add(new Label { name = "thumbnail-symbol", pickingMode = PickingMode.Ignore });
                var labels = new VisualElement();
                labels.AddToClassList("studio-card-labels");
                labels.Add(new Label { name = "title" });
                labels.Add(new Label { name = "type" });
                row.Add(labels);
                if (assets)
                {
                    var plus = StudioGUI.ActionButton("＋", PlaceCardAsset);
                    plus.name = "asset-add";
                    row.Add(plus);
                    RegisterNativeDrag(row, GetCardAsset, NoClipKind);
                    row.RegisterCallback<MouseDownEvent>(RevealCardAsset);
                    void PlaceCardAsset()
                    {
                        if (row.userData is StudioLibraryItem item && item.Asset != null && host.Sequence != null)
                            host.PlaceAsset(item.Asset, host.Clock.Time, host.Preview?.Stage.CameraCenter ?? Vector2.zero);
                    }

                    UnityEngine.Object GetCardAsset()
                    {
                        return (row.userData as StudioLibraryItem)?.Asset;
                    }

                    StudioKind? NoClipKind()
                    {
                        return null;
                    }

                    void RevealCardAsset(MouseDownEvent e)
                    {
                        if (e.clickCount == 2 && row.userData is StudioLibraryItem item)
                            EditorGUIUtility.PingObject(item.Asset);
                    }
                }
                else
                {
                    row.focusable = true;
                    row.RegisterCallback<PointerDownEvent>(SelectCardClip);
                    void SelectCardClip(PointerDownEvent e)
                    {
                        if (e.button == 0 && row.userData is StudioEntry entry)
                        {
                            host.Select(entry.Clip);
                            row.Focus();
                        }
                    }
                }

                return row;
            }
        }


        internal void RefreshObjectList()
        {
            if (objectList == null || host.Sequence == null)
                return;
            string search = sceneObjectSearch.Trim();
            visibleObjects = SceneObjectEntries().Where(MatchesObjectSearch).ToArray();
            objectList.style.height = Mathf.Clamp(visibleObjects.Length, 1, 6) * 56;
            objectList.itemsSource = visibleObjects;
            objectList.RefreshItems();
            bool MatchesObjectSearch(StudioEntry e)
            {
                return string.IsNullOrEmpty(search) || e.Clip.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || SceneObjectType(e.Asset).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }


        private void BindSceneObject(VisualElement row, int index)
        {
            var entry = visibleObjects[index];
            row.userData = entry;
            row.Q<Label>("title").text = entry.Clip.displayName;
            row.Q<Label>("type").text = SceneObjectType(entry.Asset);
            row.tooltip = entry.Clip.displayName;
            bool active = entry.Asset == host.Selected || StudioKeyframes.IsCamera(entry.Asset) && host.CameraSelected || StudioEvaluation.OwnsActor(entry.Asset) && host.ActorIndex >= 0 && host.ActorIndex < host.Sequence.Actors.Count && host.Sequence.Actors[host.ActorIndex].Id == entry.Asset.ActorId;
            row.EnableInClassList("selected", active);
            var appearance = StudioClipAppearance.Resolve(host.Sequence, entry.Asset);
            var actor = appearance.Actor;
            var sprite = appearance.Sprite;
            var image = row.Q<Image>("thumbnail");
            image.image = null;
            image.sprite = sprite;
            image.tintColor = actor?.Color ?? Color.white;
            image.style.backgroundColor = sprite == null && actor != null ? actor.Color : Color.clear;
            // 빈 오브젝트도 타임라인 썸네일과 같은 원본 비율로 표시함.
            float aspect = actor != null ? Mathf.Max(1, actor.Size.x) / Mathf.Max(1, actor.Size.y) : 1;
            image.style.width = sprite == null && actor != null ? Mathf.Min(28, 28 * aspect) : 36;
            image.style.height = sprite == null && actor != null ? Mathf.Min(28, 28 / aspect) : 36;
            image.style.marginLeft = sprite == null && actor != null ? (44 - image.style.width.value.value) / 2 : 4;
            row.Q<Label>("thumbnail-symbol").text = sprite != null || actor != null ? "" : entry.Asset.Kind == StudioKind.Audio ? "♫" : StudioKeyframes.IsCamera(entry.Asset) ? "▣" : "◇";
        }


        private void BuildAssetPanel()
        {
            var assets = StudioGUI.Section(foldouts, sequenceBody, "에셋", "assets", assetsOpen, StoreAssetsFoldout);
            var actions = new VisualElement();
            actions.AddToClassList("studio-asset-actions");
            actions.Add(StudioGUI.ActionButton("가져오기", OpenAssetPicker));
            actions.Add(StudioGUI.ActionButton("↻", InvalidateLibrary));
            assets.Add(actions);
            var filter = new PopupField<string>(StudioAssetLibrary.TypeNames.ToList(), (int)assetFilter)
            {
                name = "asset-type"
            };
            filter.RegisterValueChangedCallback(ChangeAssetFilter);
            assets.Add(filter);
            var search = new ToolbarSearchField
            {
                name = "asset-search",
                value = assetSearch
            };
            search.RegisterValueChangedCallback(SearchAssets);
            assets.Add(search);
            var folder = new ObjectField("검색 폴더")
            {
                name = "asset-folder",
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetFolder)
            };
            folder.RegisterValueChangedCallback(ChangeAssetFolder);
            assets.Add(folder);
            assetCount = new Label();
            assetCount.AddToClassList("studio-secondary");
            assets.Add(assetCount);
            assetList = MakeCardList("asset-list", true);
            assetList.style.height = 560;
            assetList.bindItem = BindAssetCard;
            assets.Add(assetList);
            RefreshAssetList();
            var assetOffset = assetScroll;
            assetList.Q<ScrollView>().verticalScroller.valueChanged += StoreAssetScroll;
            assetList.schedule.Execute(RestoreAssetScroll);
            void StoreAssetsFoldout(bool v)
            {
                assetsOpen = v;
            }

            void OpenAssetPicker()
            {
                toolkitPickingAsset = true;
                EditorGUIUtility.ShowObjectPicker<Object>(null, false, "", 48213);
            }

            void ChangeAssetFilter(ChangeEvent<string> _)
            {
                assetFilter = (StudioAssetType)filter.index;
                RefreshAssetList();
            }

            void SearchAssets(ChangeEvent<string> e)
            {
                assetSearch = e.newValue;
                RefreshAssetList();
            }

            void ChangeAssetFolder(ChangeEvent<UnityEngine.Object> e)
            {
                string path = e.newValue == null ? "Assets" : AssetDatabase.GetAssetPath(e.newValue);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    folder.SetValueWithoutNotify(e.previousValue);
                    return;
                }

                assetFolder = path;
                assetScroll = Vector2.zero;
                InvalidateLibrary();
            }

            void BindAssetCard(VisualElement row, int index)
            {
                var item = visibleAssets[index];
                row.userData = item;
                row.Q<Label>("title").text = item.Name;
                row.Q<Label>("type").text = (item.Unsupported == null ? "" : "미지원 / ") + StudioAssetLibrary.TypeNames[(int)item.Type];
                row.tooltip = item.Path + "\n" + item.Unsupported;
                var image = row.Q<Image>("thumbnail");
                image.sprite = null;
                image.image = AssetPreview.GetAssetPreview(item.Asset) ?? AssetPreview.GetMiniThumbnail(item.Asset);
                row.Q<Button>("asset-add").SetEnabled(host.Sequence != null && item.Unsupported == null);
            }

            void StoreAssetScroll(float v)
            {
                assetScroll.y = v;
            }

            void RestoreAssetScroll()
            {
                if (assetList != null)
                    assetList.Q<ScrollView>().scrollOffset = assetOffset;
            }
        }


        internal bool HandlePickedAsset(string command)
        {
            if (!toolkitPickingAsset || command != "ObjectSelectorClosed" || EditorGUIUtility.GetObjectPickerControlID() != 48213)
                return false;
            toolkitPickingAsset = false;
            var picked = EditorGUIUtility.GetObjectPickerObject();
            if (picked != null && host.Sequence != null)
                host.PlaceAsset(picked, host.Clock.Time, Vector2.zero);
            return true;
        }


        private void RefreshAssetList()
        {
            if (assetList == null)
                return;
            visibleAssets = libraryItems.Where(MatchesAssetSearch).ToArray();
            assetCount.text = libraryQueued ? "에셋 목록 갱신 중…" : visibleAssets.Length + "개 에셋";
            assetList.itemsSource = visibleAssets;
            assetList.RefreshItems();
            bool MatchesAssetSearch(StudioLibraryItem i)
            {
                return i.Asset != null && StudioAssetLibrary.Matches(i, assetFilter, assetSearch);
            }
        }


        private void RegisterNativeDrag(VisualElement element, Func<Object> asset, Func<StudioKind?> kind)
        {
            new StudioAssetDrag(element, asset, kind, beginNativeDrag);
        }

        [NonSerialized] private VisualElement root;
        [NonSerialized] private ScrollView sequenceBody;
        [NonSerialized] private StudioSequence displayedSequence;
        [NonSerialized] private bool libraryUIDirty = true, toolkitPickingAsset;
        [NonSerialized] private readonly Dictionary<string, bool> foldouts = new();
        internal StudioSequence DisplayedSequence => displayedSequence;

        internal VisualElement CreateGUI()
        {
            root = StudioGUI.MakePanel("sequence-panel", "시퀀스", out sequenceBody);
            displayedSequence = null;
            libraryUIDirty = true;
            return root;
        }

        internal void Sync(bool contentChanged)
        {
            if (root == null)
                return;
            bool newSequence = displayedSequence != host.Sequence;
            if (newSequence || libraryUIDirty)
            {
                displayedSequence = host.Sequence;
                libraryUIDirty = false;
                BuildSequencePanel();
                contentChanged = true;
            }
            if (sequenceTitle != null && host.Sequence != null && !sequenceTitle.Contains(Window.rootVisualElement.focusController?.focusedElement as VisualElement))
                sequenceTitle.SetValueWithoutNotify(host.Sequence.Title);
            if (contentChanged)
                RefreshObjectList();
            if (AssetPreview.IsLoadingAssetPreviews())
                assetList?.RefreshItems();
        }

        internal void ResetForSequence()
        {
            sceneObjectScroll = Vector2.zero;
            libraryUIDirty = true;
        }

        internal void Dispose()
        {
            EditorApplication.delayCall -= RefreshLibrary;
            libraryQueued = false;
            toolkitPickingAsset = false;
        }

        internal void InvalidateLibrary()
        {
            if (libraryQueued)
                return;
            libraryQueued = true;
            EditorApplication.delayCall += RefreshLibrary;
        }

        private void RefreshLibrary()
        {
            libraryQueued = false;
            if (Window == null)
                return;
            libraryItems = StudioAssetLibrary.Scan(AssetDatabase.IsValidFolder(assetFolder) ? assetFolder : "Assets");
            libraryUIDirty = true;
            Window.Repaint();
        }
    }
}
