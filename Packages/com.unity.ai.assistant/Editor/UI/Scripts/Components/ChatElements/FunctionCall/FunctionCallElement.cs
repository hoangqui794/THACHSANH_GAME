using System;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Base class for function call chat elements.
    /// It provides a title and a foldout to show the content of the function call result if any (or error if any).
    ///
    /// Specific function call chat elements can derive from this class and implement the CreateContent method to
    /// render the content of the function call result.
    /// </summary>
    class FunctionCallElement : ManagedTemplate
    {
        const string k_TextFieldClassName = "mui-function-call-text-field";
        const string k_ExpandedContentClassName = "mui-function-call-expanded";
        const string k_StatusSuccessClassName = "mui-function-call-status-success";
        const string k_StatusInProgressClassName = "mui-function-call-status-in-progress";
        const string k_StatusFailedClassName = "mui-function-call-status-failed";
        const string k_IconCheckmarkClassName = "mui-icon-checkmark";
        const string k_IconCloseClassName = "mui-icon-close";

        VisualElement m_Root;
        VisualElement m_Header;
        Label m_Title;
        Label m_Parameters;
        VisualElement m_StatusElement;
        LoadingSpinner m_LoadingSpinner;
        ScrollView m_ScrollView;

        enum State
        {
            InProgress,
            Success,
            Failed
        }

        VisualElement ContentRoot { get; set; }
        State CurrentState { get; set; }
        IFunctionCallRenderer Renderer { get; }
        bool GotResult { get; set; }
        Guid CallId { get; set; }
        string FunctionId { get; set; }

        public FunctionCallElement() : this(null) { }
        public FunctionCallElement(IFunctionCallRenderer renderer) : base(AssistantUIConstants.UIModulePath)
        {
            Renderer = renderer;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q("functionCallEntryRoot");
            m_Header = view.Q("functionCallHeader");

            m_Title = view.Q<Label>("functionCallTitle");
            m_Parameters = view.Q<Label>("functionCallParameters");
            ContentRoot = view.Q<VisualElement>("functionCallContent");
            m_StatusElement = view.Q("functionCallStatus");
            m_ScrollView = view.Q<ScrollView>("functionCallScrollView");

            m_Title.AddToClassList(k_TextFieldClassName);

            // Create and initialize the loading spinner
            m_LoadingSpinner = new LoadingSpinner();
            m_StatusElement.Add(m_LoadingSpinner);
            m_LoadingSpinner.AddToClassList(k_StatusInProgressClassName);
            m_LoadingSpinner.Show();

            ContentRoot.SetDisplay(false);
            ContentRoot.Add(Renderer as VisualElement);
            
            if (Renderer is ManagedTemplate managedTemplate)
                managedTemplate.Initialize(Context);
            if (Renderer is IAssistantUIContextAware contextAware)
                contextAware.Context = Context;
        }

        public void OnConversationCancelled()
        {
            if (CurrentState == State.InProgress)
                OnCallError(FunctionId, CallId, "Conversation cancelled.");
        }

        public void UpdateData(AssistantFunctionCall functionCall)
        {
            if (CallId != functionCall.CallId)
            {
                // Store the call id and function id to track the state of the function call
                CallId = functionCall.CallId;
                FunctionId = functionCall.FunctionId;
                GotResult = false;

                OnCallRequest(functionCall);
            }

            if (!GotResult && functionCall.Result.IsDone)
            {
                if (functionCall.Result.HasFunctionCallSucceeded)
                    OnCallSuccess(functionCall.FunctionId, functionCall.CallId, functionCall.Result);
                else
                    OnCallError(functionCall.FunctionId, functionCall.CallId, GetErrorMessage(functionCall.Result.Result));

                GotResult = true;
            }
        }

        // Success means the call was performed without throwing any exception.
        // Internal logic to display a failed state even if the call succeeded (ex: didCompile = false) should be handled here
        void OnCallRequest(AssistantFunctionCall functionCall)
        {
            SetState(State.InProgress);
            
            Renderer.OnCallRequest(functionCall);

            SetTitle(Renderer.Title);
            SetParameters(Renderer.TitleDetails);

            if (Renderer.Expanded)
            {
                EnableFoldout();
                SetFoldoutExpanded(true);
            }
        }

        void OnCallSuccess(string functionId, Guid callId, IFunctionCaller.CallResult result)
        {
            SetState(State.Success);
            Renderer.OnCallSuccess(functionId, callId, result);
            if (Renderer.Expanded)
                SetFoldoutExpanded(true);
            EnableFoldout();
        }

        void OnCallError(string functionId, Guid callId, string error)
        {
            SetState(State.Failed);

            Renderer.OnCallError(functionId, callId, error);
            
            if (error != null)
                EnableFoldout();
        }

        void SetState(State state)
        {
            if (state == CurrentState)
                return;

            switch (state)
            {
                case State.InProgress:
                    m_StatusElement.RemoveFromClassList(k_StatusSuccessClassName);
                    m_StatusElement.RemoveFromClassList(k_StatusFailedClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCheckmarkClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCloseClassName);
                    m_LoadingSpinner?.Show();
                    break;
                case State.Success:
                    m_LoadingSpinner?.Hide();
                    m_StatusElement.RemoveFromClassList(k_StatusFailedClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCloseClassName);
                    m_StatusElement.AddToClassList(k_StatusSuccessClassName);
                    m_StatusElement.AddToClassList(k_IconCheckmarkClassName);

                    // This is temporary until this is fixed in UITK https://jira.unity3d.com/browse/UUM-108227
                    m_StatusElement.AddToClassList("mui-icon-tint-success");
                    break;
                case State.Failed:
                    m_LoadingSpinner?.Hide();
                    m_StatusElement.RemoveFromClassList(k_StatusSuccessClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCheckmarkClassName);
                    m_StatusElement.AddToClassList(k_StatusFailedClassName);
                    m_StatusElement.AddToClassList(k_IconCloseClassName);

                    // This is temporary until this is fixed in UITK https://jira.unity3d.com/browse/UUM-108227
                    m_StatusElement.AddToClassList("mui-icon-tint-error");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            CurrentState = state;
        }

        void SetTitle(string title) => m_Title.text = title;

        void SetContentMaxHeight(float height) => m_ScrollView.style.maxHeight = height;

        void SetParameters(string param)
        {
            m_Parameters.text = param;
            m_Header.tooltip = param;
        }

        void EnableFoldout()
        {
            // Clicking on the view toggle the foldout
            m_Header.RegisterCallback<MouseDownEvent>(ToggleFoldout);
        }
        
        void ToggleFoldout(MouseDownEvent evt)
        {
            var newValue = !IsContentVisible();
            SetFoldoutExpanded(newValue);
        }

        void SetFoldoutExpanded(bool expanded)
        {
            ContentRoot.SetDisplay(expanded);

            if (expanded)
                m_Root.AddToClassList(k_ExpandedContentClassName);
            else
                m_Root.RemoveFromClassList(k_ExpandedContentClassName);
        }

        bool IsContentVisible() => ContentRoot.style.display != DisplayStyle.None;

        static string GetErrorMessage(JToken result) => result?.Type == JTokenType.String ? result.Value<string>() : null;
    }
}
