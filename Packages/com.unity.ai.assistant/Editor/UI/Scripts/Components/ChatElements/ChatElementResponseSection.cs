using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementResponseSection : ManagedTemplate
    {
        const string k_ReasoningSeparatorClass = "mui-reasoning-separator";

        readonly List<ChatElementBlock> k_Blocks = new();

        VisualElement m_ReasoningTitle;
        Foldout m_ReasoningFoldout;
        VisualElement m_ReasoningContainer;
        VisualElement m_AnswerContainer;
        VisualElement m_ReasoningLoadingSpinnerContainer;
        LoadingSpinner m_ReasoningLoadingSpinner;
        bool m_IsToggleEnabled;
        bool m_IsWorking;

        public ChatElementResponseSection() : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_ReasoningTitle = view.Q("reasoningTitle");
            m_ReasoningTitle.RegisterCallback<ClickEvent>(OnTitleClicked);
            m_ReasoningFoldout = view.Q<Foldout>("reasoningFoldout");
            m_ReasoningFoldout.value = true;

            // Listen to value changes when the toggle is clicked directly
            m_ReasoningFoldout.RegisterValueChangedCallback(OnFoldoutValueChanged);

            // Prevent the foldout toggle click from bubbling to the title
            var toggle = m_ReasoningFoldout.Q<Toggle>();
            toggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);

            // Initially hide the foldout until response block is created
            m_ReasoningFoldout.SetDisplay(false);
            m_IsToggleEnabled = false;

            m_ReasoningContainer = view.Q("reasoningContent");
            m_AnswerContainer = view.Q("answerContent");

            // Setup loading spinner for reasoning section
            m_ReasoningLoadingSpinnerContainer = view.Q("reasoningLoadingSpinnerContainer");
            m_ReasoningLoadingSpinner = new LoadingSpinner();
            m_ReasoningLoadingSpinner.style.marginRight = 4;
            m_ReasoningLoadingSpinner.Show();
            m_ReasoningLoadingSpinnerContainer.Add(m_ReasoningLoadingSpinner);
        }

        public void UpdateData(List<IMessageBlockModel> blocks)
        {
            // Create new blocks based on the message model
            for (var i = 0; i < blocks.Count; i++)
            {
                var blockModel = blocks[i];

                ChatElementBlock blockElement;
                if (i >= k_Blocks.Count)
                {
                    blockElement = blockModel switch
                    {
                        ThoughtBlockModel => new ChatElementBlockThought(),
                        ResponseBlockModel => new ChatElementBlockResponse(),
                        FunctionCallBlockModel => new ChatElementBlockFunctionCall(),
                        ErrorBlockModel => new ChatElementBlockError(),
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    blockElement.Initialize(Context);
                    k_Blocks.Add(blockElement);

                    if (blockModel is ResponseBlockModel or ErrorBlockModel)
                    {
                        m_AnswerContainer.Add(blockElement);
                        UpdateLoadingSpinner();

                        if (blockModel is ResponseBlockModel)
                            OnResponseBlockCreated();
                    }
                    else
                    {
                        InternalLog.Log($"[Reasoning] Adding block: {blockElement.GetType()}.");
                        m_ReasoningContainer.Add(blockElement);
                    }
                }
                else
                    blockElement = k_Blocks[i];

                blockElement.SetBlockModel(blockModel);
            }
        }

        public void SetIsWorkingState(bool isWorking)
        {
            if (m_IsWorking == isWorking)
                return;

            m_IsWorking = isWorking;
            UpdateLoadingSpinner();
        }

        public void OnConversationCancelled()
        {
            foreach (var block in k_Blocks)
            {
                block.OnConversationCancelled();
            }
        }

        void UpdateLoadingSpinner()
        {
            // To be visible, this section must be working and no response container
            bool show = m_IsWorking && m_AnswerContainer.childCount == 0;
            if (show)
                m_ReasoningLoadingSpinner.Show();
            else
                m_ReasoningLoadingSpinner.Hide();
        }

        void OnTitleClicked(ClickEvent evt)
        {
            // Only allow toggling if response block has been created
            if (!m_IsToggleEnabled)
                return;

            DisplayReasoning(!m_ReasoningFoldout.value);
        }

        void DisplayReasoning(bool isVisible)
        {
            m_ReasoningFoldout.value = isVisible;
            m_ReasoningContainer.SetDisplay(isVisible);
        }

        void OnFoldoutValueChanged(ChangeEvent<bool> evt)
        {
            if (m_IsToggleEnabled)
                DisplayReasoning(evt.newValue);
        }

        void OnResponseBlockCreated()
        {
            // Show a separator when responses comes in
            m_ReasoningContainer.AddToClassList(k_ReasoningSeparatorClass);

            // Activate the ability to toggle reasoning
            m_ReasoningFoldout.SetDisplay(true);
            m_IsToggleEnabled = true;

            if (AssistantEditorPreferences.CollapseReasoningWhenComplete)
                DisplayReasoning(false);
        }

        public bool TryPushInteraction(ToolExecutionContext.CallInfo callInfo, VisualElement userInteraction)
        {
            if (k_Blocks.Count == 0)
                return false;

            for (var i = k_Blocks.Count - 1; i >= 0; i--)
            {
                if (k_Blocks[i] is not ChatElementBlockFunctionCall elementBlock)
                    continue;

                // Interaction should only be for pending tool calls
                if (elementBlock.IsDone)
                    continue;

                if (callInfo.CallId != elementBlock.CallId)
                    continue;

                elementBlock.PushInteraction(userInteraction);
                return true;
            }

            return false;
        }

        public bool TryPopInteraction(ToolExecutionContext.CallInfo callInfo, VisualElement userInteraction)
        {
            if (k_Blocks.Count == 0)
                return false;

            for (var i = k_Blocks.Count - 1; i >= 0; i--)
            {
                if (k_Blocks[i] is not ChatElementBlockFunctionCall elementBlock)
                    continue;

                if (callInfo.CallId != elementBlock.CallId)
                    continue;

                elementBlock.PopInteraction(userInteraction);
                return true;
            }

            return false;
        }
    }
}
