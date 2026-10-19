using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal sealed class StudioDialoguePasteWindow : EditorWindow
    {
        [SerializeField]
        private CutsceneStudioWindow owner;
        [SerializeField]
        private StudioSequence sequence;
        internal static void Open(CutsceneStudioWindow owner, StudioSequence sequence)
        {
            var window = CreateInstance<StudioDialoguePasteWindow>();
            window.owner = owner;
            window.sequence = sequence;
            window.titleContent = new GUIContent("대사 붙여넣기");
            window.minSize = new Vector2(500, 460);
            window.position = new Rect(owner.position.center - new Vector2(290, 250), new Vector2(580, 500));
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            if (owner == null || sequence == null)
            {
                root.schedule.Execute(Close);
                return;
            }

            root.style.paddingTop = root.style.paddingBottom = root.style.paddingLeft = root.style.paddingRight = 16;
            root.Add(new Label("대사 붙여넣기") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 12 } });
            var duration = new DoubleField("대사별 길이 (초)")
            {
                value = 3
            };
            root.Add(duration);
            var input = new TextField
            {
                name = "dialogue-paste-input",
                multiline = true
            };
            input.textEdition.placeholder = "{화자} : {대사}";
            input.style.flexGrow = 1;
            input.style.minHeight = 120;
            input.style.marginTop = 10;
            input.style.marginBottom = 10;
            input.style.whiteSpace = WhiteSpace.Normal;
            input.verticalScrollerVisibility = ScrollerVisibility.Auto;
            root.Add(input);
            input.tooltip = "등록된 화자 이름 또는 ID : 대사\n예: 루나 : 안녕하세요.\n화자가 없는 대사: 내레이션 : 잠시 후…";
            var errorLabel = new Label();
            errorLabel.style.whiteSpace = WhiteSpace.Normal;
            errorLabel.style.marginBottom = 6;
            root.Add(errorLabel);
            root.Add(new HelpBox("한 줄을 대사 클립 하나로 추가합니다. 빈 줄은 제외됩니다.\n입력 시 다음과 같이 형식을 지켜주세요. {화자} : {대상}", HelpBoxMessageType.Info) { style = { marginBottom = 8, flexShrink = 0 } });
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.Add(new Button(Close) { text = "취소", style = { width = 80, height = 26 } });
            var apply = new Button(ApplyDialoguePaste)
            {
                text = "타임라인에 추가",
                style =
                {
                    width = 130,
                    height = 26
                }
            };
            buttons.Add(apply);
            root.Add(buttons);
            void Refresh()
            {
                bool valid = StudioDialogueImport.TryParse(sequence, input.value, out _, out _, out var error);
                errorLabel.text = error;
                errorLabel.style.display = string.IsNullOrEmpty(error) ? DisplayStyle.None : DisplayStyle.Flex;
                apply.SetEnabled(valid && double.IsFinite(duration.value) && duration.value > 0);
            }

            input.RegisterValueChangedCallback(ValidateInput);
            duration.RegisterValueChangedCallback(ValidateDuration);
            Refresh();
            root.RegisterCallback<KeyDownEvent>(CloseOnEscape);
            void ApplyDialoguePaste()
            {
                if (owner == null || sequence == null)
                {
                    Close();
                    return;
                }

                if (!StudioDialogueImport.TryParse(sequence, input.value, out var texts, out var ids, out var error))
                {
                    errorLabel.text = error;
                    errorLabel.style.display = DisplayStyle.Flex;
                    return;
                }

                owner.AppendDialogueEntries(sequence, texts, ids, duration.value);
                Close();
            }

            void ValidateInput(ChangeEvent<string> _)
            {
                Refresh();
            }

            void ValidateDuration(ChangeEvent<double> _)
            {
                Refresh();
            }

            void CloseOnEscape(KeyDownEvent e)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    Close();
                    e.StopPropagation();
                }
            }
        }
    }
}
