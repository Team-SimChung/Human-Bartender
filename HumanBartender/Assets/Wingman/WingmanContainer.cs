#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static WingmanInspector.WingmanUtility;

namespace WingmanInspector {

    public class WingmanContainer {

        public static GUIStyle boldLabelStyle;
        public static float searchBarHeight;
        public static WingmanPersistentData persistentData;
        public static Texture textureAtlas;
        public static Texture allIcon;
        public static Texture xIcon;
        
        public static GUIStyle leftToolBarGuiStyle;
        public static GUIContent copyToolBarGuiContent;
        
        public static GUIStyle rightToolBarGuiStyle;
        public static GUIContent pasteToolBarGuiContent;

        private const string AllButtonName = "All";
        private const float DragThreshold = 12f;
        private const float MiniMapMargin = 4f;
        private const float SearchCompListSpace = 4f;
        private const float RowHeight = 25f;
        private const float InspectorScrollBarWidth = 12.666666667f;
        private const float ToolBarButtonWidth = 30f;

        private const string InspectorListClassName = "unity-inspector-editors-list";
        private const string InspectorScrollClassName = "unity-inspector-root-scrollview";
        private const string InspectorNoMultiEditClassName = "unity-inspector-no-multi-edit-warning";
        private const string MainWingmanName = "Wingman Main";
        private const string SearchResultsName = "SearchResults";
        
        private static Vector2 iconSize = new Vector2(12, 12);
        private static Vector2 toolBarIconSize = new Vector2(12, 12);
        
        public readonly EditorWindow inspectorWindow;
        public bool isFocused;
        
        public enum ShortcutOperation { Nothing, ToggleComponent }
        private ShortcutOperation activeShortcutToPerform;
        
        private Object inspectingObject;
        private VisualElement editorListVisual;
        private IMGUIContainer miniMapGuiContainer;
        private IMGUIContainer pinnedHeaderContainer;
        private IMGUIContainer searchResultsGuiContainer;
        private IMGUIContainer pinnedDividerContainer;
        private ScrollView inspectorScrollView;

        private List<WingmanId> selectedCompIds;
        private List<WingmanId> validCompIds = new List<WingmanId>();
        private List<WingmanId> prevValidCompIds = new List<WingmanId>();
        private Dictionary<int, Component> compFromIndex = new Dictionary<int, Component>();
        private HashSet<string> noMultiEditVisualElements = new HashSet<string>();
        
        private Vector2 miniMapScrollPos;
        
        private int lastCompCount;
        private int lastRowCount;

        private enum AssetType { NotImportant, HierarchyGameObject,  HierarchyPrefab, HierarchyModel, ProjectPrefab }
        private AssetType inspectingAssetType;
        
        private List<ComponentSearchResults> searchResults = new List<ComponentSearchResults>();
        private const double TimeAfterLastKeyPressToSearch = 0.15;
        private double timeOfLastSearchUpdate;
        private bool performSearchFlag;
        
        private bool inspectorWasLocked;
        private PropertyInfo lockedPropertyInfo;
        
        private int rangeModifierPivot;

        private const string DragAndDropKey = "WingmansDragAndDrop";
        private bool isDragging;
        private bool dragHandlerSet;
        private bool canStartDrag;
        private WingmanId dragId;
        private Vector2 initialDragMousePos;

        public WingmanContainer(EditorWindow window) {
            inspectorWindow = window;
            lockedPropertyInfo = window.GetType().GetProperty("isLocked", BindingFlags.Public | BindingFlags.Instance);
            inspectorWasLocked = InspectorIsLocked();
            inspectorScrollView = (ScrollView)inspectorWindow.rootVisualElement.Q(null, InspectorScrollClassName);
            SetContainerSelectionToObject(inspectorWasLocked ? persistentData.GetRestoredObjectForInspectorWindow(window) : Selection.activeObject);
        }
        
        public void PerformShortcutOperation(ShortcutOperation shortcut) {
            activeShortcutToPerform = shortcut;
            // Force update, otherwise we wait for mouse movement to trigger gui handler
            miniMapGuiContainer?.MarkDirtyRepaint();
        }
        
        public void RemoveGui() {
            if (!InspectingObjectIsValid()) return;

            if (ShowingWingmanGui()) {
                editorListVisual?.RemoveAt(MiniMapIndex());
            }

            if (ShowingSearchResults()) {
                editorListVisual?.RemoveAt(SearchResultsIndex());
            }
        }

        public void SetContainerSelectionToObject(Object obj) {
            inspectingObject = obj;
            
            if (!inspectingObject) {
                inspectingAssetType = AssetType.NotImportant;
                return;
            }
            
            // Figure out what type of asset we are inspecting
            {
                bool isAsset = AssetDatabase.Contains(inspectingObject);
                PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(inspectingObject);
                
                if (isAsset && prefabType is PrefabAssetType.Regular or PrefabAssetType.Variant) {
                    inspectingAssetType = AssetType.ProjectPrefab;
                }
                else if (!isAsset && prefabType is PrefabAssetType.Model) {
                    inspectingAssetType = AssetType.HierarchyModel;
                }
                else if (!isAsset && prefabType is PrefabAssetType.Regular or PrefabAssetType.Variant) {
                    inspectingAssetType = AssetType.HierarchyPrefab;
                }
                else if (!isAsset && prefabType is PrefabAssetType.NotAPrefab) {
                    inspectingAssetType = AssetType.HierarchyGameObject;
                }
                else {
                    inspectingAssetType = AssetType.NotImportant;
                }
            }
            
            searchResults.Clear();
            RefreshNoMultiInspectVisualsSet();
            persistentData.AddDataForContainer(inspectingObject);
            selectedCompIds = persistentData.SelectedCompIds(inspectingObject);
            
            if (HasTextInSearchField()) {
                PerformSearch();
                if (!HasSearchResults()) {
                    persistentData.SetSearchString(inspectingObject, string.Empty);
                }
            }
        }

        public void Update() {
            CheckForLockStatusChange();
            
            if (!InspectingObjectIsValid()) return;
            if (Settings.TransOnlyDisable && OnlyHasTransform()) return;

            editorListVisual ??= inspectorWindow.rootVisualElement.Q(null, InspectorListClassName);
            if (editorListVisual == null) return;
            
            if (performSearchFlag && EditorApplication.timeSinceStartup - timeOfLastSearchUpdate > TimeAfterLastKeyPressToSearch) {
                PerformSearch();
                performSearchFlag = false;
                searchResultsGuiContainer?.MarkDirtyRepaint();
            }
            
            if (!ShowingWingmanGui() && editorListVisual.childCount > MiniMapIndex()) {
                float miniMapHeight = CalculateMiniMapHeight();
                
                miniMapGuiContainer = new IMGUIContainer();
                miniMapGuiContainer.name = MainWingmanName;
                miniMapGuiContainer.style.width = FullLength();
                miniMapGuiContainer.style.height = miniMapHeight;
                miniMapGuiContainer.style.minHeight = miniMapHeight; 
                miniMapGuiContainer.onGUIHandler = DrawWingmanGui;
                Margin(miniMapGuiContainer.style, MiniMapMargin);
                
                editorListVisual.Insert(MiniMapIndex(), miniMapGuiContainer);
                UpdateComponentVisibility();
            }

            bool searchResultsAreStale = SearchResultsAreStale();
            if (searchResultsAreStale) {
                PerformSearch();
                searchResultsGuiContainer?.MarkDirtyRepaint();
            } 

            bool showingSearchResults = ShowingSearchResults();
            
            if (!showingSearchResults && HasSearchResults() && editorListVisual.childCount > SearchResultsIndex()) {
                searchResultsGuiContainer = new IMGUIContainer();
                searchResultsGuiContainer.name = SearchResultsName;
                searchResultsGuiContainer.style.width = FullLength();
                searchResultsGuiContainer.style.height = FullLength(); 
                searchResultsGuiContainer.onGUIHandler = DrawSearchResultsGui;
                editorListVisual.Insert(SearchResultsIndex(), searchResultsGuiContainer);
                searchResultsGuiContainer?.MarkDirtyRepaint();
            }
            
            if (showingSearchResults && !HasSearchResults()) {
                RemoveSearchGui();
                ToggleAllComponentVisibility(true);
            }
            
#if UNITY_2021
            Fix2021EditorMargins();
#endif
        }
        
#if UNITY_6000_3_OR_NEWER

        public void OnHierarchyGUI() {
            if (DragAndDrop.GetGenericData(DragAndDropKey) is not bool initiatedDrag || !initiatedDrag) return;

            if (Event.current.type == EventType.DragUpdated && !dragHandlerSet) {
                DragAndDrop.AddDropHandlerV2(HierarchyDropHandler);
                dragHandlerSet = true;
                Event.current.Use();
            }

            if (Event.current.type == EventType.DragExited && dragHandlerSet) {
                DragAndDrop.RemoveDropHandlerV2(HierarchyDropHandler);
                dragHandlerSet = false;
                Event.current.Use();
            }
        }
        
#else

        public void OnHierarchyGUI() {
            if (DragAndDrop.GetGenericData(DragAndDropKey) is not bool initiatedDrag || !initiatedDrag) return;

            if (Event.current.type == EventType.DragUpdated && !dragHandlerSet) {
                DragAndDrop.AddDropHandler(HierarchyDropHandler);
                dragHandlerSet = true;
                Event.current.Use();
            }

            if (Event.current.type == EventType.DragExited && dragHandlerSet) {
                DragAndDrop.RemoveDropHandler(HierarchyDropHandler);
                dragHandlerSet = false;
                Event.current.Use();
            }
        }
        
#endif

        private void DrawWingmanGui() {
            Rect reservedRect = miniMapGuiContainer.contentRect;
            isFocused = reservedRect.Contains(Event.current.mousePosition);

            if (!InspectingObjectIsValid()) return;
            
            bool showCopyPasteOnly = Settings.TransOnlyKeepCopyPaste && OnlyHasTransform();
            if (!Settings.HideToolbar || showCopyPasteOnly) {
                DrawToolBar(reservedRect, showCopyPasteOnly);
                reservedRect = ShiftRectStartVertically(reservedRect, searchBarHeight + SearchCompListSpace);
            }

            List<Component> comps = GetAllVisibleComponents();
            float[] buttonWidths = GetButtonWidths(comps);
            
            int newCompCount = comps.Count;
            int newRowCount = GetRowCount(reservedRect.width, buttonWidths);
            
            // Create associated component data
            compFromIndex.Clear();
            validCompIds.Clear();
            for (int i = 0; i < comps.Count; i++) {
                compFromIndex.Add(i, comps[i]);
                validCompIds.Add(GetWingmanId(comps[i]));
            }
            
            // Check for resizing the container
            bool resizeRequired = newCompCount != lastCompCount || newRowCount != lastRowCount;
            if (resizeRequired) {
                ResizeGuiContainer();
            }

            // Remove component from selection if it was removed from gameobject
            if (newCompCount < lastCompCount) {
                for (int i = selectedCompIds.Count - 1; i >= 0; i--) {
                    if (!validCompIds.Contains(selectedCompIds[i])) {
                        selectedCompIds.RemoveAt(i);
                    }
                }
            }
            
            bool compsGotAdjusted = newCompCount < lastCompCount || !CompareComponentIds(validCompIds, prevValidCompIds);
            
            // Set variables for next method call
            prevValidCompIds.Clear();
            foreach (WingmanId validCompId in validCompIds) {
                prevValidCompIds.Add(validCompId);
                
                lastCompCount = newCompCount;
                lastRowCount = newRowCount;
                
                GetScrollViewDimensions(reservedRect, newRowCount, out Rect innerScrollRect, out Rect outerScrollRect);
                List<Rect> buttonPlacements = GetButtonPlacements(innerScrollRect, comps, buttonWidths);

                CheckToShowContextMenu(comps, buttonPlacements);
                CheckForShortcutOperations(comps, buttonPlacements);
                
                if (showCopyPasteOnly) return;
                
                UpdateDragAndDrop();
                
                EditorGUI.BeginChangeCheck();
                DrawPreviewScrollView(buttonPlacements, comps, innerScrollRect, outerScrollRect);
            
                if (EditorGUI.EndChangeCheck() || compsGotAdjusted) {
                    UpdateComponentVisibility();
                }
            }
        }

        private void DrawPreviewScrollView(List<Rect> placementRects, List<Component> comps, Rect innerScrollRect, Rect outerScrollRect) {
            miniMapScrollPos = GUI.BeginScrollView(outerScrollRect, miniMapScrollPos, innerScrollRect, GUIStyle.none, GUIStyle.none);
            
            // Handle the All button
            { 
                bool draggingAll = false;
                bool prevAllButtonToggle = AllIsSelected() && !HasTextInSearchField();
                Rect allButtonRect = placementRects[0];
                
                if (allButtonRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) {
                    canStartDrag = true;
                    dragId = WingmanId.None();
                    draggingAll = !prevAllButtonToggle;
                    ClearSearchOnComponentButtonPress();
                }
                
                if (DrawToggleButton(allButtonRect, allIcon, AllButtonName, prevAllButtonToggle, true, draggingAll)) {
                    selectedCompIds.Clear();
                    rangeModifierPivot = 0;
                }
            }
            
            EventModifiers modifiers = Event.current.modifiers;
            bool multiSelectModifier = modifiers.HasFlag(EventModifiers.Control);
            bool rangeSelectModifier = modifiers.HasFlag(EventModifiers.Shift);
            
            for (int i = 0; i < comps.Count; i++) {
                Component comp = comps[i];
                Rect buttonRect = placementRects[i + 1];
                WingmanId compId = GetWingmanId(comp);
                
                if (buttonRect.Contains(Event.current.mousePosition)) {
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0) {
                        canStartDrag = true;
                        dragId = compId;
                    }
                }
                
                string compName = GetComponentName(comp);
                GUIContent content = EditorGUIUtility.ObjectContent(comp, comp.GetType());
                
                bool displayCompAsEnabled = true;
                if (ComponentIsTogglable(comp)) {
                    displayCompAsEnabled = GetComponentEnabledState(comp);
                }
                
                bool prevToggle = selectedCompIds.Contains(compId);
                bool draggingButton = compId == dragId && !prevToggle;
                
                bool toggled = DrawToggleButton(buttonRect, content.image, compName, prevToggle, displayCompAsEnabled, draggingButton);
                
                if (toggled && !prevToggle) {
                    OnButtonToggleOn(i, multiSelectModifier, rangeSelectModifier);
                    ClearSearchOnComponentButtonPress();
                }
                else if (!toggled && prevToggle) {
                    OnButtonToggleOff(i, multiSelectModifier, rangeSelectModifier);
                    ClearSearchOnComponentButtonPress();
                }
            }
            
            GUI.EndScrollView();
        }

        private void GetScrollViewDimensions(Rect reservedRect, int rowCount, out Rect innerScrollRect, out Rect outerScrollRect) {
            innerScrollRect = new Rect(reservedRect) { height = rowCount * RowHeight };
            outerScrollRect = new Rect(reservedRect) { height = RowHeight * Settings.MaxNumberOfRows };
        }

        private List<Rect> GetButtonPlacements(Rect scrollViewRect, List<Component> comps, float[] buttonWidths) {
            List<Rect> placements = new List<Rect>(); 
            
            Rect placementRect = scrollViewRect;
            
            float usableWidth = scrollViewRect.width;
            if (!ShowingVerticalScrollBar()) {
                usableWidth -= InspectorScrollBarWidth;
            }
            
            Rect allButtonRect = new Rect(placementRect.position, new Vector2(buttonWidths[0], RowHeight));
            placements.Add(allButtonRect);
            
            float curWidth = usableWidth;
            curWidth -= buttonWidths[0];
            placementRect.position += new Vector2(buttonWidths[0], 0f);

            for (int i = 0; i < comps.Count; i++) {
                float buttonWidth = buttonWidths[i + 1];
                
                if (curWidth < buttonWidth) {
                    placementRect.position = new Vector2(scrollViewRect.position.x, placementRect.position.y + RowHeight);
                    curWidth = usableWidth;
                }
                curWidth -= buttonWidth;

                Rect buttonRect = new Rect(placementRect.position, new Vector2(buttonWidth, RowHeight));
                placements.Add(buttonRect);

                placementRect.position += new Vector2(buttonWidth, 0f);
            }

            return placements;
        }
        
        private void ClearSearchOnComponentButtonPress() {
            if (HasTextInSearchField()) {
                persistentData.SetSearchString(inspectingObject, string.Empty);
                searchResults.Clear();
                GUI.changed = true;
                RemoveSearchGui();
                ToggleAllComponentVisibility(true);
            }
        }

        private bool DrawToggleButton(Rect placement, Texture icon, string label, bool toggled, bool compEnabled, bool beingDragged) {
            if (!toggled && isDragging && beingDragged) {
                toggled = true;
                GUI.changed = true;
            }
            else if (Event.current.type == EventType.MouseUp && placement.Contains(Event.current.mousePosition) && Event.current.button == 0) {
                toggled = !toggled;
            }

            GUIStyle style = GUI.skin.button;
            Color restoreGuiColor = GUI.color;
            
            if (!compEnabled) {
                Color dimColor = new Color(0.67f, 0.67f, 0.67f, 1f);
                GUI.color = dimColor; // This tints everything drawn next 
            }
            
            int uniqueControlId = GUIUtility.GetControlID(FocusType.Passive);
            GUI.Toggle(placement, uniqueControlId, toggled, GUIContent.none, style);
            
            GUI.color = restoreGuiColor;
            
            Vector2 iconPos = new Vector2(placement.position.x + boldLabelStyle.margin.right, 0f);
            Rect iconRect = CenterRectVertically(placement, new(iconPos, iconSize));
            GUI.DrawTexture(iconRect, icon);
            
            Vector2 labelSize = boldLabelStyle.CalcSize(new GUIContent(label));
            Vector2 labelPos = new Vector2(iconRect.xMax, 0f);
            Rect labelRect = new Rect(labelPos, labelSize);
            labelRect = CenterRectVertically(placement, labelRect);
            GUI.Label(labelRect, label, boldLabelStyle);

            return toggled;
        }
        
        private void OnButtonToggleOn(int compIndex, bool multiSelectModifier, bool rangeSelectModifier) {
            WingmanId compId = ComponentIdFromIndex(compIndex);
            
            if (multiSelectModifier && !rangeSelectModifier) {
                rangeModifierPivot = compIndex;
                selectedCompIds.Add(compId);
                return;
            }
            
            if (rangeSelectModifier) {
                if (AllIsSelected()) {
                    rangeModifierPivot = compIndex;
                    selectedCompIds.Add(compId);
                    return;
                }
                
                AddRangeToSelected(compIndex);
                return;
            }

            selectedCompIds.Clear();
            selectedCompIds.Add(compId);
            rangeModifierPivot = compIndex;
        }
        
        private void OnButtonToggleOff(int compIndex, bool multiSelectModifier, bool rangeSelectModifier) {
            WingmanId compId = ComponentIdFromIndex(compIndex);
            
            if (rangeSelectModifier && selectedCompIds.Count <= 1) return;
            
            if (!multiSelectModifier && !rangeSelectModifier && selectedCompIds.Count > 1) {
                selectedCompIds.Clear();
                selectedCompIds.Add(compId);
                rangeModifierPivot = compIndex;
                return;
            }
            
            if (rangeSelectModifier) {
                if (compIndex == rangeModifierPivot) {
                    selectedCompIds.Clear();
                    selectedCompIds.Add(compId);
                    return;
                }
                
                AddRangeToSelected(compIndex);

                if (compIndex < rangeModifierPivot) {
                    int islandMin = compIndex;
                    while (selectedCompIds.Contains(ComponentIdFromIndex(islandMin - 1))) {
                        islandMin -= 1;
                    }

                    for (int i = islandMin; i < compIndex; i++) {
                        selectedCompIds.Remove(ComponentIdFromIndex(i));
                    }
                }
                else {
                    int islandMax = compIndex;
                    while (selectedCompIds.Contains(ComponentIdFromIndex(islandMax + 1))) {
                        islandMax += 1;
                    }
                    
                    for (int i = compIndex + 1; i <= islandMax; i++) {
                        selectedCompIds.Remove(ComponentIdFromIndex(i));
                    }
                }
                
                return;
            }
            
            selectedCompIds.Remove(compId);
        }
        
        private void AddRangeToSelected(int compIndex) {
            (int min, int max) = rangeModifierPivot < compIndex ? (rangeModifierPivot, compIndex) : (compIndex, rangeModifierPivot);
            for (int i = min; i <= max; i++) {
                WingmanId id = ComponentIdFromIndex(i);
                if (!selectedCompIds.Contains(id)) {
                    selectedCompIds.Add(id);
                }
            }
        }
        
        private void DrawToolBar(Rect placementRect, bool showCopyPasteOnly) {
            placementRect.height = searchBarHeight;
            
            float fullWidth = placementRect.width;
            float xStartPos = placementRect.position.x;
            
            if (!Settings.HideCopyPaste || showCopyPasteOnly) {
                if (DrawToolBarButton(placementRect, true)) {
                    CopySelectedToClipboard();
                }
                placementRect.position += new Vector2(ToolBarButtonWidth, 0f);
                if (DrawToolBarButton(placementRect, false)) {
                    PasteFromClipboard();
                }
                placementRect.position += new Vector2(ToolBarButtonWidth + MiniMapMargin, 0f);
            }
            
            if (showCopyPasteOnly) return;
            
            placementRect.width = fullWidth - (placementRect.position.x - xStartPos);

            const float crossSize = 11;
            const float crossDistFromEndOfSearch = 16;
            Rect crossPlacement = placementRect;
            crossPlacement.width = crossSize;
            crossPlacement.height = crossSize;
            crossPlacement.position = new Vector2(placementRect.xMax - crossDistFromEndOfSearch, placementRect.position.y);
            crossPlacement = CenterRectVertically(placementRect, crossPlacement);
            
            // Handle X input before drawing search field because it eats the input of overlayed elements
            string searchText = persistentData.SearchString(inspectingObject);
            bool showX = searchText != string.Empty;
            bool pressedX = false;
            if (showX) {
                if (crossPlacement.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseUp) {
                    searchText = string.Empty;
                    searchResults.Clear();
                    pressedX = true;
                }
            }
            
            int prevSearchLen = searchText.Length;
            GUI.SetNextControlName("SearchField");
            searchText = GUI.TextField(placementRect, searchText, EditorStyles.toolbarSearchField);

            // Deselect any selected components when typing in search 
            if (!string.IsNullOrWhiteSpace(searchText)) {
                selectedCompIds.Clear();
            }
            
            // If we click outside of the search bar unfocus it
            if (pressedX || !placementRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown) {
                GUI.FocusControl(null);
                if (string.IsNullOrWhiteSpace(searchText)) {
                    searchText = string.Empty;
                }
            }

            // Draw X after search field so it shows on top
            if (showX) {
                Color prevColor = GUI.color;
                GUI.color = new Vector4(prevColor.r, prevColor.g, prevColor.b, 0.7f);
                GUI.Button(crossPlacement, xIcon, GUIStyle.none);
                GUI.color = prevColor;
            }
            
            if (searchText != null && prevSearchLen != searchText.Length) {
                performSearchFlag = true;
                timeOfLastSearchUpdate = EditorApplication.timeSinceStartup;
            }

            persistentData.SetSearchString(inspectingObject, searchText);
        }
        
        private bool DrawToolBarButton(Rect placement, bool copy) {
            placement.width = ToolBarButtonWidth;
            
            bool pressed = GUI.Button(placement, copy ? copyToolBarGuiContent : pasteToolBarGuiContent, copy ? leftToolBarGuiStyle : rightToolBarGuiStyle);

            Rect iconRect = placement;
            iconRect.size = toolBarIconSize;
            iconRect = CenterRectVertically(placement, iconRect);
            iconRect = CenterRectHorizontally(placement, iconRect);

            if (EditorGUIUtility.isProSkin) {
                Rect uvRect = copy ? new Rect(0f, 0.5f, 0.5f, 0.5f) : new Rect(0f, 0f, 0.5f, 0.5f);
                GUI.DrawTextureWithTexCoords(iconRect, textureAtlas, uvRect);
            }
            else {
                Rect uvRect = copy ? new Rect(0.5f, 0.5f, 0.5f, 0.5f) : new Rect(0.5f, 0f, 0.5f, 0.5f);
                GUI.DrawTextureWithTexCoords(iconRect, textureAtlas, uvRect);
            }

            return pressed;
        }
        
        private List<Component> GetComponentsFromSelection() {
            if (!InspectingObjectIsValid()) {
                return null;
            }
            
            List<Component> allComps = GetAllVisibleComponents();
            
            if (AllIsSelected()) {
                return allComps;
            }
            
            List<Component> selComps = new List<Component>(selectedCompIds.Count);
            foreach (WingmanId compId in selectedCompIds) {
                selComps.Add(ComponentFromId(compId));
            }
            return selComps;
        }
        
        private class ComponentSearchResults {
            public Component comp;
            public SerializedObject serializedComponent;
            public List<List<SearchResult>> searchResultBranches = new();
        }
        
        private class SearchResult { 
            public SearchTransform searchTrans;
            public bool showChildren;
            public int indentLevel;
            public SerializedProperty Property => searchTrans.property;
        }
        
        public class SearchTransform {
            public SerializedProperty property;
            public List<SearchTransform> children; 
        }
        
        private void PerformSearch() {
            string searchText = persistentData.SearchString(inspectingObject);
            if (string.IsNullOrWhiteSpace(searchText)) {
                searchResults.Clear();
                return;
            }

            List<Component> comps = GetAllVisibleComponents();
            if (comps == null) return;
            
            searchResults.Clear();
            
            foreach (Component comp in comps) {
                ComponentSearchResults compResults = null;
                SerializedObject serializedComponent = new SerializedObject(comp);
                
                List<SearchTransform> compSearches = GetComponentSearches(serializedComponent);
                if (compSearches == null) continue;
                
                foreach (SearchTransform search in compSearches) {
                    List<SearchResult> branch = new List<SearchResult>();
                    if (!BuildSearchBranch(search, searchText, branch)) continue;
                    
                    compResults ??= new ComponentSearchResults {
                        comp = comp, 
                        serializedComponent = serializedComponent,
                    };
                    compResults.searchResultBranches.Add(branch);
                }

                if (compResults != null) {
                    searchResults.Add(compResults);
                }
            }
        }
        
        private List<SearchTransform> GetComponentSearches(SerializedObject serializedComponent) {
            SerializedProperty iter = serializedComponent.GetIterator();
            if (iter == null || !iter.NextVisible(true)) {
                return null;
            }
            
            List<SearchTransform> roots = new List<SearchTransform>();
            
            do {
                roots.Add(TraverseProperty(iter));
            }
            while (iter.NextVisible(enterChildren: false));
            
            return roots;
        }
        
        private SearchTransform TraverseProperty(SerializedProperty curProperty) {
            SearchTransform curSearchTransform = new SearchTransform() {
                property = curProperty.Copy(),
                children = new List<SearchTransform>(),
            };
            
            if (PropertyIsPureArray(curProperty)) {
                for (int i = 0; i < curProperty.arraySize; i++) {
                    SerializedProperty elm = curProperty.GetArrayElementAtIndex(i);
                    if (elm.hasVisibleChildren) {
                        SearchTransform child = TraverseProperty(elm);
                        curSearchTransform.children.Add(child);
                    }
                }
                return curSearchTransform;
            }

            if (!curProperty.hasVisibleChildren) {
                return curSearchTransform; 
            }

            // Enter children
            SerializedProperty childIter = curProperty.Copy();
            bool hasChild = childIter.NextVisible(enterChildren: true);

            while (hasChild) {
                // Stop when we've left this property's subtree
                if (childIter.depth <= curProperty.depth) break;

                // Only process direct children
                if (childIter.depth == curProperty.depth + 1) {
                    SearchTransform child = TraverseProperty(childIter);
                    curSearchTransform.children.Add(child);
                }

                hasChild = childIter.NextVisible(enterChildren: false);
            }
            
            return curSearchTransform;
        }
        
        private bool BuildSearchBranch(SearchTransform propTrans, string searchString, List<SearchResult> branch, int indentLevel = 0) {
            string searchOn = searchString.Contains('.') ? propTrans.property.propertyPath : propTrans.property.displayName;
            
            if (FuzzyMatch(searchOn, searchString)) {
                branch.Add(new() {
                    searchTrans = propTrans,
                    showChildren = propTrans.property.hasVisibleChildren,
                    indentLevel = indentLevel,
                });
                return true;
            }
            
            int insertIndex = branch.Count;
            bool addedSelf = false;
            
            foreach (SearchTransform child in propTrans.children) {
                bool matchedOnChild = BuildSearchBranch(child, searchString, branch, indentLevel + 1);
                if (!matchedOnChild || addedSelf) continue;
                
                branch.Insert(insertIndex, new() {
                    searchTrans = propTrans,
                    showChildren = false,
                    indentLevel = indentLevel,
                });
                addedSelf = true;
            } 
            
            return addedSelf;
        }
        
        private bool FuzzyMatch(string stringToSearch, string pattern) {
            const int adjacencyBonus = 5;      
            const int leadingLetterPenalty = -5;
            const int maxLeadingLetterPenalty = -9;
            const int unmatchedLetterPenalty = -1;

            int score = 0;
            int patternIdx = 0;
            int patternLength = pattern.Length;
            int strIdx = 0;
            int strLength = stringToSearch.Length;
            bool prevMatched = false;

            char? bestLetter = null;
            char? bestLower = null;
            int bestLetterScore = 0;

            while (strIdx != strLength) {
                char? patternChar = patternIdx != patternLength ? pattern[patternIdx] : null;
                char strChar = stringToSearch[strIdx];

                char? patternLower = patternChar != null ? char.ToLower((char)patternChar) : null;
                char strLower = char.ToLower(strChar);

                bool nextMatch = patternChar != null && patternLower == strLower;
                bool rematch = bestLetter != null && bestLower == strLower;

                bool advanced = nextMatch && bestLetter != null;
                bool patternRepeat = bestLetter != null && patternChar != null && bestLower == patternLower;
                if (advanced || patternRepeat) {
                    score += bestLetterScore;
                    bestLetter = null;
                    bestLower = null;
                    bestLetterScore = 0;
                }

                if (nextMatch || rematch) {
                    int newScore = 0;

                    if (patternIdx == 0) {
                        int penalty = Math.Max(strIdx * leadingLetterPenalty, maxLeadingLetterPenalty);
                        score += penalty;
                    }

                    if (prevMatched) {
                        newScore += adjacencyBonus;
                    }

                    if (nextMatch) {
                        ++patternIdx;
                    }

                    if (newScore >= bestLetterScore) {
                        if (bestLetter != null) {
                            score += unmatchedLetterPenalty;
                        }

                        bestLetter = strChar;
                        bestLower = char.ToLower((char)bestLetter);
                        bestLetterScore = newScore;
                    }

                    prevMatched = true;
                }
                else {
                    score += unmatchedLetterPenalty;
                    prevMatched = false;
                }

                ++strIdx;
            }

            if (bestLetter != null) {
                score += bestLetterScore;
            }

            const int idealScore = -10;
            return patternIdx == patternLength && score >= idealScore;
        }

#if UNITY_6000_3_OR_NEWER
        private DragAndDropVisualMode HierarchyDropHandler(EntityId dropTargetEntityId, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform) {
            Object dropTargetObject = EditorUtility.EntityIdToObject(dropTargetEntityId);
            return SharedHierarchyDropHandler(dropTargetObject, dropMode, perform);
        }
#else
        private DragAndDropVisualMode HierarchyDropHandler(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform) {
            Object dropTargetObject = EditorUtility.InstanceIDToObject(dropTargetInstanceID);
            return SharedHierarchyDropHandler(dropTargetObject, dropMode, perform);
        }
#endif

        private DragAndDropVisualMode SharedHierarchyDropHandler(Object dropTargetObject, HierarchyDropFlags dropMode, bool perform) {
            bool copying = dropMode == HierarchyDropFlags.DropUpon;
            bool creating = dropMode == HierarchyDropFlags.DropBetween || dropMode == HierarchyDropFlags.None;
            
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;
            if (copying) {
                visualMode = DragAndDropVisualMode.Copy;
            }
            else if (creating) {
                visualMode = DragAndDropVisualMode.Move;
            }

            if (!perform || (!copying && !creating)) {
                return visualMode;
            }
            
            List<Component> comps = GetComponentsFromSelection();
            if (comps == null) {
                return visualMode;
            }
            
            if (copying && dropTargetObject is GameObject gameObject) {
                GroupUndoAction("Copy Components", () => gameObject.PasteComponents(comps));
                EditorApplication.delayCall += () => Selection.activeObject = gameObject;
                return visualMode;
            }
            
            GroupUndoAction("Create Object from Components", () => {
                GameObject newGameObject = new GameObject("GameObject");
                Undo.RegisterCreatedObjectUndo(newGameObject, string.Empty);
                newGameObject.PasteComponentsFromEmpty(comps);
                EditorApplication.delayCall += () => Selection.activeObject = newGameObject;
            });

            return visualMode;
        }

        private void GroupUndoAction(string undoName, Action action) {
            Undo.IncrementCurrentGroup();
            int curUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            action.Invoke();
            Undo.CollapseUndoOperations(curUndoGroup);
        }
        
        private void UpdateDragAndDrop() {
            bool mouseDragEvent = Event.current.type == EventType.MouseDrag;

            if (!isDragging && canStartDrag && mouseDragEvent) {
                initialDragMousePos = Event.current.mousePosition;
                canStartDrag = false;
                return;
            }

            if (initialDragMousePos != Vector2.zero && mouseDragEvent && Vector2.Distance(initialDragMousePos, Event.current.mousePosition) >= DragThreshold) {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragAndDropKey, true);
                DragAndDrop.StartDrag(MainWingmanName);
                isDragging = true;
            }
            
            // DragExited is set when we drag out of the container or stop dragging inside it
            if (Event.current.type == EventType.DragExited) {
                canStartDrag = false;
                isDragging = false;
                initialDragMousePos = Vector2.zero;
                Event.current.Use();
            }
        }

        private bool CompareComponentIds(List<WingmanId> list0, List<WingmanId> list1) {
            if (list0.Count != list1.Count) {
                return false;
            }

            for (int i = 0; i < list0.Count; i++) {
                if (list0[i] != list1[i]) {
                    return false;
                }
            }

            return true;
        }

        private void ResizeGuiContainer() {
            float height = CalculateMiniMapHeight();
            miniMapGuiContainer.style.height = height; 
            miniMapGuiContainer.style.minHeight = height; 
            miniMapGuiContainer.style.width = FullLength();
        }
        
        private void DrawSearchResultsGui() {
            if (!HasSearchResults() || SearchResultsAreStale() || !InspectingObjectIsValid()) return;
            
            ToggleAllComponentVisibility(false);
            
            foreach (ComponentSearchResults componentSearchResult in searchResults) {
                // Refresh the components representation, which refreshes each serialized property
                // we aggregated during the search. This is also needed for undo/redo and prefab reverts
                componentSearchResult.serializedComponent.Update();
                
                EditorGUILayout.InspectorTitlebar(true, componentSearchResult.comp, false);
                EditorGUI.indentLevel++;
                
                EditorGUI.BeginChangeCheck();
                foreach (List<SearchResult> branch in componentSearchResult.searchResultBranches) { 
                    DrawComponentSearchBranch(branch);
                }
                if (EditorGUI.EndChangeCheck()) {
                    componentSearchResult.serializedComponent.ApplyModifiedProperties();
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }
        
        private void DrawComponentSearchBranch(List<SearchResult> searchBranch) {
            int baseIndentLevel = EditorGUI.indentLevel;
            
            foreach (SearchResult searchResult in searchBranch) {
                EditorGUI.indentLevel = baseIndentLevel + searchResult.indentLevel;
                
                SerializedProperty prop = searchResult.Property;
                
                bool prevIsExpanded = prop.isExpanded; 
                if (prop.hasVisibleChildren && !prop.isExpanded) {
                    prop.isExpanded = true;
                }
                
                bool onlyDrawArrayHeader = PropertyIsPureArray(prop) && !searchResult.showChildren;
                if (onlyDrawArrayHeader) {
                    const float arraySizeFieldWidth = 50f;
                    Rect rect = EditorGUILayout.GetControlRect();
                    Rect arrFoldoutRect = new Rect(rect.x, rect.y, rect.width - arraySizeFieldWidth, rect.height);
                    Rect arrSizeRect    = new Rect(rect.x + rect.width - arraySizeFieldWidth, rect.y, arraySizeFieldWidth, rect.height);
                    
                    EditorGUI.Foldout(arrFoldoutRect, true, prop.displayName);
                    
                    // For whatever reason Unity having an indent level messes with the array size position
                    // so we temporarily set it to 0 while we draw our own array size 
                    int postSizeIndent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.IntField(arrSizeRect, prop.arraySize, EditorStyles.numberField);
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel = postSizeIndent;
                }
                else {
                    // Sometimes bitable data types have empty space as children? So this is a work around.
                    bool showChildren = prop.propertyType is SerializedPropertyType.Generic ? searchResult.showChildren : false;
                    EditorGUILayout.PropertyField(prop, showChildren);
                    prop.isExpanded = prevIsExpanded;
                }
            }
            
            EditorGUI.indentLevel = baseIndentLevel;
        }
        
        private void UpdateComponentVisibility() {
            int startIndex = ComponentStartIndex();
            int skippedCount = 0;
            
            for (int i = startIndex; i < editorListVisual.childCount; i++) {
                if (noMultiEditVisualElements.Contains(editorListVisual[i].name)) {
                    skippedCount++;
                    continue;
                }
                
                int compIndex = i - startIndex - skippedCount;
                if (compFromIndex.TryGetValue(compIndex, out Component comp)) {
                    bool showComp = selectedCompIds.Count <= 0 || selectedCompIds.Contains(GetWingmanId(comp));
                    editorListVisual[i].style.display = showComp ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private void ToggleAllComponentVisibility(bool show) {
            int startIndex = ShowingSearchResults() ? SearchResultsIndex() + 1 : MiniMapIndex() + 1;
            for (int i = startIndex; i < editorListVisual.childCount; i++) {
                editorListVisual[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private bool ShowingWingmanGui() {
            int insertIndex = MiniMapIndex();
            
            if (insertIndex >= editorListVisual.childCount) {
                return false;
            }

            VisualElement duplicateContainer = editorListVisual.hierarchy.Children().FirstOrDefault(child => child.name == MainWingmanName);
            if (duplicateContainer != null) {
                bool inCorrectPosition = editorListVisual.hierarchy.IndexOf(duplicateContainer) == insertIndex;
                if (inCorrectPosition) {
                    return true;
                }
                duplicateContainer.RemoveFromHierarchy();
                return false;
            }
            
            VisualElement potentialMiniMap = editorListVisual.hierarchy.ElementAt(insertIndex);
            return potentialMiniMap != null && potentialMiniMap.name == MainWingmanName;
        }

        private bool ShowingSearchResults() {
            int insertIndex = SearchResultsIndex();
            
            if (insertIndex >= editorListVisual.childCount) {
                return false;
            }
            
            VisualElement potentialSearchResults = editorListVisual.hierarchy.ElementAt(insertIndex);
            return potentialSearchResults != null && potentialSearchResults.name == SearchResultsName;
        }
        
        private bool HasSearchResults() {
            return searchResults != null && searchResults.Count > 0;
        }

        private bool SearchResultsAreStale() {
            return searchResults != null && searchResults.Count > 0 && !searchResults[0].comp;
        }

        private bool OnlyHasTransform() {
#if UNITY_6000_0_OR_NEWER
            return ((GameObject)inspectingObject).GetComponentCount() == 1;
#else
            return ((GameObject)inspectingObject).GetComponents<Component>().Length == 1;
#endif
        }

        private int GetRowCount(float rowWidth, float[] buttonWidths) {
            if (!ShowingVerticalScrollBar()) {
                rowWidth -= InspectorScrollBarWidth;
            }
            
            int rowCount = 1;
            float curWidth = rowWidth;

            foreach (float buttonWidth in buttonWidths) {
                if (curWidth < buttonWidth) {
                    curWidth = rowWidth;
                    rowCount++;
                }
                curWidth -= buttonWidth;
            }

            return rowCount;
        }

        private float[] GetButtonWidths(List<Component> comps) {
            float[] buttonWidths = new float[comps.Count + 1];
            buttonWidths[0] = GetButtonWidth(AllButtonName);
            for (int i = 1; i < buttonWidths.Length; i++) {
                buttonWidths[i] = GetButtonWidth(GetComponentName(comps[i - 1]));
            }
            return buttonWidths;
        }
        
        private float GetButtonWidth(string text) {
            float totalPadding = boldLabelStyle.margin.right * 2f;
            Vector2 guiSize = boldLabelStyle.CalcSize(new GUIContent(text));
            return iconSize.x + guiSize.x + totalPadding;
        }
        
        private Rect CenterRectVertically(Rect parent, Rect child) {
            float yDiff = parent.height - child.height;
            float yPos = parent.position.y + (yDiff / 2f);
            child.position = new Vector2(child.position.x, yPos);
            return child;
        }

        private Rect CenterRectHorizontally(Rect parent, Rect child) {
            float xDiff = parent.width - child.width;
            float xPos = parent.position.x + (xDiff / 2f);
            child.position = new Vector2(xPos, child.position.y);
            return child;
        }
        
        private void Margin(IStyle style, float margin) {
            style.marginTop = margin;
            style.marginBottom = margin;
            style.marginLeft = margin;
            style.marginRight = margin;
        }
        
        private bool ShowingVerticalScrollBar() {
            return inspectorScrollView.verticalScroller.resolvedStyle.display == DisplayStyle.Flex;
        }
        
        private List<Component> GetAllVisibleComponents() {
            if (!InspectingObjectIsValid()) {
                return null;
            }

            GameObject selectedGameObject = inspectingObject as GameObject;
            
            if (Selection.gameObjects.Length == 1) {
                return GetAllVisibleComponents(selectedGameObject);
            }

            { // Get all visible components that each selected object shares
                List<Component> comps = GetAllVisibleComponents(selectedGameObject);

                if (InspectorIsLocked()) {
                    return comps;
                }

                foreach (GameObject otherGameObject in Selection.gameObjects) {
                    if (otherGameObject == selectedGameObject) continue;

                    List<Component> otherComps = GetAllVisibleComponents(otherGameObject);

                    for (int i = comps.Count - 1; i >= 0; i--) {
                        if (!ComponentListContainsType(otherComps, comps[i].GetType())) {
                            comps.RemoveAt(i);
                        }
                    }
                }
                
                return comps;
            }
        }
        
        private bool ComponentListContainsType(List<Component> list, Type componentType) {
            foreach (Component component in list) {
                if (component.GetType() == componentType) {
                    return true;
                }
            }
            return false;
        }

        private List<Component> GetAllVisibleComponents(GameObject gameObject) {
            Component[] comps = gameObject.GetComponents<Component>();
            List<Component> res = new List<Component>(comps.Length);
            foreach (Component comp in comps) {
                if (ComponentIsVisible(comp)) {
                    res.Add(comp);
                }
            }
            return res;
        }

        private bool ComponentIsVisible(Component comp) {
            // Comp can be null if the associated script cannot be loaded
            return comp && !comp.hideFlags.HasFlag(HideFlags.HideInInspector) && !ComponentIsOnBanList(comp);
        }

        private bool ComponentIsOnBanList(Component comp) {
            return comp is ParticleSystemRenderer;
        }

        private WingmanId ComponentIdFromIndex(int index) {
            return GetWingmanId(compFromIndex[index]);
        }

        private Component ComponentFromId(WingmanId compId) {
            int index = 0;
            for (int i = 0; i < validCompIds.Count; i++) {
                if (validCompIds[i] == compId) {
                    index = i;
                }
            }
            return compFromIndex[index];
        }

        private bool AllIsSelected() {
            return selectedCompIds.Count == 0;
        }

        public bool InspectorIsLocked() {
            return (bool)lockedPropertyInfo.GetValue(inspectorWindow);
        }

        private void CheckForLockStatusChange() {
            bool currentlyLocked = InspectorIsLocked();

            bool wasJustLocked = currentlyLocked && !inspectorWasLocked;
            if (wasJustLocked) {
                persistentData.SetDataForLockedInspector(inspectorWindow, inspectingObject);
            }
            
            bool wasJustUnlocked = !currentlyLocked && inspectorWasLocked;
            if (wasJustUnlocked && Selection.activeObject != inspectingObject) {
                SetContainerSelectionToObject(Selection.activeObject); 
            }
            
            inspectorWasLocked = currentlyLocked;
        }

        private int MiniMapIndex() {
            return inspectingAssetType is AssetType.ProjectPrefab ? 2 : 1;
        }

        private int SearchResultsIndex() {
            return inspectingAssetType is AssetType.ProjectPrefab ? 3 : 2;
        }

        private int ComponentStartIndex() {
            return inspectingAssetType == AssetType.ProjectPrefab ? 3 : 2;
        }

        private void RemoveSearchGui() {
            if (ShowingSearchResults()) {
                editorListVisual.RemoveAt(SearchResultsIndex());
                searchResultsGuiContainer = null;
            }
        }

        private bool HasTextInSearchField() {
            return !string.IsNullOrWhiteSpace(persistentData.SearchString(inspectingObject));
        }

        private float CalculateMiniMapHeight() {
            float searchBarAndPadding = searchBarHeight + SearchCompListSpace;
            
            if (Settings.TransOnlyKeepCopyPaste && OnlyHasTransform()) {
                return searchBarHeight;
            }
            
            float[] buttonWidths = GetButtonWidths(GetAllVisibleComponents());
            
            // Important! Use editor list width as container width as MiniMap.layout
            // is not always as up to date as it should be (if it were just created).
            // This prevents the container from flickering when changing objects.
            float guiContainerWidth = editorListVisual.layout.width - MiniMapMargin * 2f;
            float rowCount = Mathf.Clamp(GetRowCount(guiContainerWidth, buttonWidths), 1, Settings.MaxNumberOfRows);
            return (rowCount * RowHeight) + (Settings.HideToolbar ? 0f : searchBarAndPadding);
        }
        
        private StyleLength FullLength() {
            return new StyleLength(StyleKeyword.Auto);
        }
        
        private bool InspectingObjectIsValid() {
            return inspectingObject && inspectingObject is GameObject && inspectingAssetType is not AssetType.NotImportant;
        }
        
        // Use this instead of property.isArray
        private bool PropertyIsPureArray(SerializedProperty property) {
            // Strings have the isArray field set to true but we don't want to treat them as arrays
            return property.isArray && property.propertyType != SerializedPropertyType.String;    
        }
        
        // Add all visual elements to the noMultiEditVisualElements set so we know which components are not
        // being displayed in the inspector when multi-inspecting is occurring.
        // During multi-inspecting the editor list may have non-shared (hidden) components inserted as children 
        // that we need to skip over when updating component visibility to not throw off component indexing.
        // Any visual element after no-multi-edit warning tells us what is being hidden in the inspector.
        private void RefreshNoMultiInspectVisualsSet() {
            noMultiEditVisualElements.Clear();

            if (Selection.gameObjects.Length <= 1 || editorListVisual == null) return;
            
            int noMultiEditIndex = editorListVisual.childCount;

            for (int i = 0; i < editorListVisual.childCount; i++) {
                if (editorListVisual[i].ClassListContains(InspectorNoMultiEditClassName)) {
                    noMultiEditIndex = i;
                    break;
                }
            }
                
            for (int i = noMultiEditIndex + 1; i < editorListVisual.childCount; i++) {
                noMultiEditVisualElements.Add(editorListVisual[i].name);
            }
        }
        
        private void CheckToShowContextMenu(List<Component> comps, List<Rect> buttonRects) {
            bool mouseDown = Event.current.type is EventType.MouseDown;
            bool rightClicking = Event.current.button == 1;
            if (!mouseDown || !rightClicking) return;
            
            Event.current.Use(); // Eat event so right clicking doesn't toggle component
            
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Selection"), false, CopySelectedToClipboard);
            menu.AddItem(new GUIContent("Paste Clipboard"), false, PasteFromClipboard);
            
            Component compUnderCursor = GetComponentUnderCursor(comps, buttonRects);

            if (compUnderCursor) {
                menu.AddSeparator("");
                string compName = GetComponentName(compUnderCursor);
                
                // Copy component
                menu.AddItem(new GUIContent($"Copy { compName }"), false, () => {
                    persistentData.clipboard.CopyComponents(new() { compUnderCursor });
                });
                
                // Open component as script
                if (compUnderCursor is MonoBehaviour) {
                    menu.AddItem(new GUIContent($"Edit { compName } Script"), false, () => {
                        MonoScript script = MonoScript.FromMonoBehaviour(compUnderCursor as MonoBehaviour);
                        if (script) AssetDatabase.OpenAsset(script);
                    });
                }

                // Remove component
                if (compUnderCursor is not Transform) {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent($"Remove { compName }"), false, () => {
                        RemoveComponentTypeFromSelection(compUnderCursor.GetType());
                    });
                }
            }
            
            menu.ShowAsContext();
        }

        private Component GetComponentUnderCursor(List<Component> comps, List<Rect> buttonRects) {
            for (int i = 1; i < buttonRects.Count; i++) {
                if (buttonRects[i].Contains(Event.current.mousePosition + miniMapScrollPos)) {
                    return comps[i - 1];
                }
            }
            return null;
        }

        private void RemoveComponentTypeFromSelection(Type compType) {
            GroupUndoAction("Remove Component", () => {
                foreach (GameObject gameObject in Selection.gameObjects) {
                    if (gameObject.TryGetComponent(compType, out Component component)) {
                        Undo.DestroyObjectImmediate(component);
                    }
                }
            });
        }

        private void CopySelectedToClipboard() {
            persistentData.clipboard.CopyComponents(GetComponentsFromSelection());
        }

        private void PasteFromClipboard() {
            if (InspectorIsLocked()) {
                (inspectingObject as GameObject).PasteComponents(persistentData.clipboard.Copies);
                return;
            }
            
            foreach (GameObject gameObject in Selection.gameObjects) {
                gameObject.PasteComponents(persistentData.clipboard.Copies);
            }
        }
        
        private void CheckForShortcutOperations(List<Component> comps, List<Rect> buttonRects) {
            if (activeShortcutToPerform == ShortcutOperation.ToggleComponent) {
                Component compUnderCursor = GetComponentUnderCursor(comps, buttonRects);
                if (compUnderCursor && ComponentIsTogglable(compUnderCursor)) {
                    ToggleComponent(compUnderCursor);
                }
            }
            activeShortcutToPerform = ShortcutOperation.Nothing;
        }
        
        private bool ComponentIsTogglable(Component comp) {
            return comp is Behaviour or Renderer or Collider;
        }
        
        private bool GetComponentEnabledState(Component comp) {
            return comp switch {
                Behaviour b => b.enabled,
                Renderer r  => r.enabled,
                Collider c  => c.enabled,
                _           => true,
            };
        }
        
        private void ToggleComponent(Component comp) {
            _ = comp switch {
                Behaviour b => b.enabled = !b.enabled,
                Renderer r  => r.enabled = !r.enabled,
                Collider c  => c.enabled = !c.enabled,
                _           => false,
            };
        }

        private Rect ShiftRectStartVertically(Rect rect, float length) { 
            rect.position += new Vector2(0f, length);
            rect.height -= length;
            return rect;
        }
        
        private void Fix2021EditorMargins() {
            bool ShowingTransform() {
                if (!InspectingObjectIsValid()) {
                    return false;
                }

                int compStartIndex = ComponentStartIndex();
                if (editorListVisual.childCount <= compStartIndex) {
                    return false;
                }
                
                return editorListVisual[compStartIndex].style.display !=  DisplayStyle.None;
            }

            if (miniMapGuiContainer == null) return;
            
            if (ShowingTransform()) {
                const float transformHeaderMissingHeight = 7f;
                miniMapGuiContainer.style.marginTop = 0f;
                miniMapGuiContainer.style.marginBottom = transformHeaderMissingHeight + MiniMapMargin;
            }
            else {
                Margin(miniMapGuiContainer.style, MiniMapMargin);
                miniMapGuiContainer.style.marginTop = 0f;
            }
        }
        
        private string GetComponentName(Component comp) {
            #if COMPONENT_NAMES
                return Sisus.ComponentNames.ComponentExtensions.GetName(comp);
            #else
                return comp.GetType().Name;
            #endif
        }
        
    }
}
#endif