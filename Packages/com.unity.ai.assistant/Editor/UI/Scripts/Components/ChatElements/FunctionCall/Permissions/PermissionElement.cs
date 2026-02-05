using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class PermissionElement : UserInteractionElement<ToolPermissions.UserAnswer>
    {
        readonly string k_Action;
        readonly string k_Question;
        readonly string k_Code;
        readonly int k_PointCount;

        const string k_PermissionGrantedText = "Permission Granted";
        const string k_PermissionDeniedText = "Permission Denied";
        const string k_PermissionCanceledText = "Canceled";

        VisualElement m_PermissionInteraction;
        VisualElement m_PermissionAnswer;
        Label m_AnswerLabel;
        Label m_AnswerActionLabel;
        Label m_SessionMessageLabel;

        public PermissionElement() : this("") { }

        public PermissionElement(string action, string question = null, string code = null, int pointCount = 0)
        {
            k_Action = action;
            k_Question = question;
            k_Code = code;
            k_PointCount = pointCount;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            style.flexShrink = 0;

            m_PermissionInteraction = view.Q<VisualElement>("permissionInteraction");

            var actionLabel = view.Q<Label>("actionLabel");
            actionLabel.text = k_Action;

            var questionLabel = view.Q<Label>("questionLabel");
            if (!string.IsNullOrEmpty(k_Question))
                questionLabel.text = k_Question;
            else
                questionLabel.SetDisplay(false);

            var codeContainer = view.Q("codeContainer");
            if (!string.IsNullOrEmpty(k_Code))
            {
                var codePreview = new CodeBlockElement();
                codePreview.Initialize(Context);

                codePreview.SetCode(k_Code);
                codePreview.ShowSaveButton(false);
                codePreview.ShowEditButton(false);

                codeContainer.Add(codePreview);
            }
            else
            {
                codeContainer.SetDisplay(false);
            }

            var yesButton = view.Q<Button>("yesButton");
            yesButton.RegisterCallback<ClickEvent>(_ => OnAnswerSelected(ToolPermissions.UserAnswer.AllowOnce));

            var yesAlwaysButton = view.Q<Button>("yesAlwaysButton");
            yesAlwaysButton.RegisterCallback<ClickEvent>(_ => OnAnswerSelected(ToolPermissions.UserAnswer.AllowAlways));

            var noButton = view.Q<Button>("noButton");
            noButton.RegisterCallback<ClickEvent>(_ => OnAnswerSelected(ToolPermissions.UserAnswer.DenyOnce));

            // Set up point count badge
            var pointCountLabel = view.Q<Label>("pointCountLabel");
            var sparkleBadge = view.Q<VisualElement>("sparkleBadge");

            if (k_PointCount > 0)
            {
                pointCountLabel.text = k_PointCount.ToString();
                yesButton.AddToClassList("has-badge");
                yesAlwaysButton.AddToClassList("has-badge");
            }
            else
            {
                pointCountLabel.SetDisplay(false);
                sparkleBadge.SetDisplay(false);
            }

            // Initially hide the permission answer section
            m_PermissionAnswer = view.Q<VisualElement>("permissionAnswer");
            m_PermissionAnswer.SetDisplay(false);

            // Cache answer display elements
            m_AnswerLabel = m_PermissionAnswer.Q<Label>("answerLabel");
            m_AnswerActionLabel = m_PermissionAnswer.Q<Label>("answerActionLabel");
            m_SessionMessageLabel = m_PermissionAnswer.Q<Label>("sessionMessageLabel");
        }

        void OnAnswerSelected(ToolPermissions.UserAnswer answer)
        {
            // Hide the interaction container, show the answer
            m_PermissionInteraction.SetDisplay(false);
            m_PermissionAnswer.SetDisplay(true);

            bool granted = answer is ToolPermissions.UserAnswer.AllowOnce or ToolPermissions.UserAnswer.AllowAlways;

            m_AnswerLabel.text = granted ? k_PermissionGrantedText : k_PermissionDeniedText;

            m_AnswerActionLabel.text = k_Action;

            // Show session message if "Don't ask again for this session" was selected
            if (answer == ToolPermissions.UserAnswer.AllowAlways)
            {
                m_SessionMessageLabel.enableRichText = true;
                m_SessionMessageLabel.text = $"Assistant won't ask permission to <b>{k_Action}</b> this conversation.";
                m_SessionMessageLabel.SetDisplay(true);
            }
            else
            {
                m_SessionMessageLabel.SetDisplay(false);
            }

            // Complete the interaction
            CompleteInteraction(answer);
        }

        protected override void OnCanceled()
        {
            // Hide the interaction container, show the answer
            m_PermissionInteraction.SetDisplay(false);
            m_PermissionAnswer.SetDisplay(true);

            m_AnswerLabel.text = k_PermissionCanceledText;
            m_SessionMessageLabel.SetDisplay(false);
        }
    }
}
