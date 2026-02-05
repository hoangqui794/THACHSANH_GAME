using System;
using System.Diagnostics;
using System.Threading;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.SessionBanner;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.History;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils.Event;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit.Accounts.Components;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantView : ManagedTemplate
    {
        static readonly char[] k_MessageTrimChars = { ' ', '\n', '\r', '\t' };

        const string k_HistoryOpenClass = "mui-chat-history-open";

        readonly IAssistantHostWindow k_HostWindow;

        static CancellationTokenSource s_NewChatActiveTokenSource;

        VisualElement m_RootMain;
        VisualElement m_RootPanel;

        Button m_NewChatButton;
        Button m_HistoryButton;

        Label m_ConversationName;

        AssistantConversationPanel m_ConversationPanel;

        VisualElement m_HistoryPanelRoot;
        VisualElement m_ProgressElementRoot;
        ProgressElement m_ProgressElement;

        HistoryPanel m_HistoryPanel;
        Rect m_HistoryPanelWorldBounds;

        VisualElement m_HeaderRow;
        VisualElement m_FooterRoot;
        VisualElement m_PopupAnchor;

        VisualElement m_ChatInputRoot;
        AssistantTextField m_ChatInput;

        VisualElement m_PopupRoot;
        SelectionPopup m_SelectionPopup;
        PopupTracker m_SelectionPopupTracker;
        PopupTracker m_ContextDropdownTracker;
        VisualElement m_ContextDropdownButton;
        Label m_DropdownToggleLabel;
        readonly string k_ContextDropdownToggleFocused = "mui-context-dropdown-toggle-focused";

        Button m_ClearContextButton;

        VisualElement m_SelectedContextRoot;
        ContextDropdown m_SelectedContextDropdown;

        Button m_WhatsNewButton;
        VisualElement m_EmptyStateRoot;

        int m_SelectedConsoleMessageNum;
        string m_SelectedConsoleMessageContent;
        string m_SelectedGameObjectName;

        bool m_WaitingForConversationChange;

        BaseEventSubscriptionTicket m_ConversationSelectedEventTicket;

        /// <summary>
        /// Constructor for the MuseChatView.
        /// </summary>
        public AssistantView()
            : this(null)
        {
        }

        public AssistantView(IAssistantHostWindow hostWindow)
            : base(AssistantUIConstants.UIModulePath)
        {
            k_HostWindow = hostWindow;

            AssistantUIAPISettings.Initialize();

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);
        }

        public void InitializeThemeAndStyle()
        {
            LoadStyle(m_RootPanel, EditorGUIUtility.isProSkin ? AssistantUIConstants.AssistantSharedStyleDark : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(m_RootPanel, AssistantUIConstants.AssistantBaseStyle, true);
        }

        public bool TryPushInteraction(ToolExecutionContext.CallInfo callInfo, VisualElement userInteraction)
        {
            return m_ConversationPanel.TryPushInteraction(callInfo, userInteraction);
        }

        public bool TryPopInteraction(ToolExecutionContext.CallInfo callInfo, VisualElement userInteraction)
        {
            return m_ConversationPanel.TryPopInteraction(callInfo, userInteraction);
        }

        /// <summary>
        /// Provide access to the currently active context to Debug and Internal tools
        /// Note: Do not use this for public facing operations
        /// </summary>
        internal AssistantUIContext ActiveUIContext => Context;

        /// <summary>
        /// Initialize the view and its component, called by the managed template
        /// </summary>
        /// <param name="view">the template container of the current element</param>
        protected override void InitializeView(TemplateContainer view)
        {
            // Suspend any saving of state during initialization until the state was restored (RestoreState)
            Context.Blackboard.SuspendStateSave();

            style.flexGrow = 1;
            view.style.flexGrow = 1;

            m_HeaderRow = view.Q<VisualElement>("headerRow");
            m_HeaderRow.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);

            m_RootMain = view.Q<VisualElement>("root-main");
            m_RootMain.RegisterCallback<DragEnterEvent>(OnMainDragEnter);
            m_RootMain.RegisterCallback<DragLeaveEvent>(OnMainDragLeave);
            m_RootMain.RegisterCallback<DragExitedEvent>(OnMainDragExit);

            m_RootPanel = view.Q<VisualElement>("root-panel");

            m_NewChatButton = view.SetupButton("newChatButton", OnNewChatClicked);
            m_NewChatButton.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);
            m_HistoryButton = view.SetupButton("historyButton", OnHistoryClicked);
            m_HistoryButton.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);

            m_ConversationName = view.Q<Label>("conversationNameLabel");
            m_ConversationName.enableRichText = false;

            var panelRoot = view.Q<VisualElement>("chatPanelRoot");
            m_ConversationPanel = new AssistantConversationPanel();
            m_ConversationPanel.Initialize(Context);
            m_ConversationPanel.RegisterCallback<MouseUpEvent>(OnConversationPanelClicked);
            panelRoot.Add(m_ConversationPanel);

            m_HistoryPanelRoot = view.Q<VisualElement>("historyPanelRoot");
            m_HistoryPanel = new HistoryPanel();
            m_HistoryPanel.Initialize(Context);
            m_HistoryPanelRoot.Add(m_HistoryPanel);
            RegisterCallback<ClickEvent>(CheckHistoryPanelClick);
            m_HistoryPanelRoot.style.display = AssistantUISessionState.instance.IsHistoryOpen ? DisplayStyle.Flex : DisplayStyle.None;

            m_ProgressElementRoot = view.Q<VisualElement>("progressElementContainer");
            m_ProgressElement = new ProgressElement();
            m_ProgressElement.Initialize(Context);
            m_ProgressElement.Hide();
            m_ProgressElementRoot.Add(m_ProgressElement);

            var contentRoot = view.Q<VisualElement>("chatContentRoot");
            contentRoot.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);

            view.AddSessionRefreshManipulators(Context.API.Provider);

            m_FooterRoot = view.Q<VisualElement>("footerRoot");
            m_FooterRoot.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);

            m_PopupAnchor = view.Q<VisualElement>("popupAnchor");

            m_SelectedContextRoot = view.Q<VisualElement>("userSelectedContextRoot");
            m_ClearContextButton = view.Q<Button>("clearContextButton");

            m_ChatInputRoot = view.Q<VisualElement>("chatTextFieldRoot");

            m_PopupRoot = view.Q<VisualElement>("chatModalPopupRoot");
            InitializeSelectionPopup();

            m_ContextDropdownButton = view.Q<VisualElement>("dropdownToggle");
            InitializeContextDropdown();
            m_DropdownToggleLabel = view.Q<Label>("dropdownToggleLabel");

            m_ChatInput = new AssistantTextField();
            m_ChatInput.Initialize(Context);
            m_ChatInput.SetHost(m_PopupRoot);
            m_ChatInput.SubmitRequest += OnRequestSubmit;
            m_ChatInput.CancelRequest += OnActiveProgressCancelRequested;
            m_ChatInput.ContextButton.RegisterCallback<PointerUpEvent>(_ => ToggleSelectionPopup());
            m_ContextDropdownButton.RegisterCallback<PointerUpEvent>(_ => ToggleContextDropdown());
            m_ChatInputRoot.Add(m_ChatInput);

            m_EmptyStateRoot = view.Q<VisualElement>("emptyStateRoot");
            m_WhatsNewButton = view.Q<Button>("museChatWhatsNewButton");
            m_WhatsNewButton.clicked += WhatsNewWindow.ShowWindow;

            UpdateAssistantEditorDriverContext();
            UpdateWarnings();

            EditorApplication.hierarchyChanged += OnHierarchChanged;

            ClearChat();

            m_DropZoneRoot = view.Q<VisualElement>("dropZoneRoot");
            m_DropZone = new ChatDropZone();
            m_DropZone.Initialize(Context);
            m_DropZoneRoot.Add(m_DropZone);
            m_DropZoneOverlay = view.Q<VisualElement>("dropZoneOverlay");
            m_DropZone.SetupDragDrop(m_DropZoneOverlay, OnDropped);

            m_DropZone.SetDropZoneActive(false);

            view.AddManipulator(Context.SearchHelper);
            view.AddManipulator(new PointsBalanceChanges(OnPointsBalanceChanged));

            view.RegisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);

            Context.Initialize();

            UpdateContextSelectionElements();

            Context.API.ConversationReload += OnConversationReload;
            Context.API.ConversationChanged += OnConversationChanged;
            Context.API.ConversationDeleted += OnConversationDeleted;

            ScheduleConversationRefresh();

            Context.ConversationRenamed += OnConversationRenamed;

            RegisterContextCallbacks();
        }

        private void OnPointsBalanceChanged()
        {
            if (!Account.pointsBalance.CanAfford(AssistantConstants.ChatPreAuthorizePoints))
                m_ChatInput.Disable();    
            else
                m_ChatInput.Enable();
        }

        public void InitializeState()
        {
            RestoreConversationState();
        }

        void ScheduleConversationRefresh()
        {
            Context.API.RefreshConversations();

            // Schedule another history update in 5 minutes.
            schedule.Execute(ScheduleConversationRefresh).StartingIn(1000 * 60 * 5);
        }

        void CheckHistoryPanelClick(ClickEvent e)
        {
            var clickOfHistoryButton = m_HistoryButton.worldBound.Contains(e.position);
            var clickWithinHistoryPanel = m_HistoryPanel.worldBound.Contains(e.position);

            if (!clickWithinHistoryPanel && AssistantUISessionState.instance.IsHistoryOpen && !clickOfHistoryButton)
            {
                SetHistoryDisplay(false);
            }
        }

        void OnConversationPanelClicked(MouseUpEvent evt)
        {
            SetHistoryDisplay(false);
        }

        public void Deinit()
        {
            Context.Deinitialize();

            UnregisterContextCallbacks();
        }

        void RestoreConversationState()
        {
            var lastMode = AssistantUISessionState.instance.LastActiveMode;
            Context.Blackboard.ActiveMode = lastMode;

            var lastConvId = AssistantUISessionState.instance.LastActiveConversationId;
            if (string.IsNullOrEmpty(lastConvId))
            {
                RestoreUIState(default);
                return;
            }

            m_WaitingForConversationChange = true;

            var id = new AssistantConversationId(lastConvId);
            Context.Blackboard.SetActiveConversation(id);

            Context.API.ConversationLoad(id);

            EditorApplication.delayCall += () => RestoreUIState(id);
        }

        void RestoreUIState(AssistantConversationId conversationId)
        {
            if (m_WaitingForConversationChange)
            {
                EditorApplication.delayCall += () => RestoreUIState(conversationId);
                return;
            }

            // Check for incomplete message to recover
            string incompleteId = AssistantUISessionState.instance.IncompleteMessageId;
            if (conversationId != default && !string.IsNullOrEmpty(incompleteId))
            {
                // Recover incomplete message now that conversation is loaded
                Context.API.RecoverIncompleteMessage(conversationId);
            }

            m_ChatInput.SetText(AssistantUISessionState.instance.Prompt);
            var serializableContextList = JsonUtility.FromJson<AssistantContextList>(AssistantUISessionState.instance.Context);
            k_SelectedContext.Clear();
            if (serializableContextList?.m_ContextList.Count > 0)
            {
                RestoreContextSelection(serializableContextList.m_ContextList);
                UpdateContextSelectionElements();
            }

            Context.Blackboard.ResumeStateSave();
        }

        void OnConversationDeleted(AssistantConversationId conversationId)
        {
            if (!Context.Blackboard.ActiveConversationId.IsValid)
            {
                // Clear the chat, in case we deleted our active conversation
                ClearChat();
            }
        }

        void OnConversationRenamed(AssistantConversationId id)
        {
            if (Context.Blackboard.ActiveConversationId == id)
            {
                UpdateConversationTitle(id);
            }
        }

        void OnConversationChanged(AssistantConversationId conversationId)
        {
            UpdateConversationTitle(conversationId);
        }

        void UpdateConversationTitle(AssistantConversationId conversationId)
        {
            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation == null)
            {
                // We have not received this conversation data yet
                return;
            }

            m_ConversationName.text = conversation.Title;
        }

        void OnConversationReload(AssistantConversationId conversationId)
        {
            InternalLogUtils.PerformAndSetupDomainReloadLog(conversationId, Context);

            // If this conversation is not active, we don't display it
            if (Context.Blackboard.ActiveConversationId != conversationId)
                return;

            ClearChat(false);

            m_WaitingForConversationChange = false;
            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation == null)
            {
                // We have not received this conversation data yet
                return;
            }

            var sw = new Stopwatch();
            sw.Start();
            try
            {
                m_ConversationName.text = conversation.Title;
                m_ConversationPanel.Populate(conversation);
                m_EmptyStateRoot.SetDisplay(false);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to populate conversation panel: " + e.Message);
            }
            finally
            {
                sw.Stop();

                InternalLog.Log($"PopulateConversation took {sw.ElapsedMilliseconds}ms ({conversation.Messages.Count} Messages)");
            }
        }

        void ClearChat(bool clearInput = true)
        {
            m_ConversationName.text = "New conversation";

            if (clearInput)
            {
                m_ChatInput.ClearText();
            }

            m_ConversationPanel.ClearConversation();
            m_EmptyStateRoot.SetDisplay(true);
        }

        void OnHistoryClicked(PointerUpEvent evt)
        {
            Context.API.RefreshConversations();

            bool status = !(m_HistoryPanelRoot.style.display == DisplayStyle.Flex);
            SetHistoryDisplay(status);
        }

        void SetHistoryDisplay(bool isVisible)
        {
            m_HistoryPanelRoot.style.display = isVisible
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            m_HistoryButton.EnableInClassList(k_HistoryOpenClass, isVisible);

            AssistantUISessionState.instance.IsHistoryOpen = isVisible;
        }

        void OnHistoryEntrySelected(EventHistoryConversationSelected eventData)
        {
            SetHistoryDisplay(false);
        }

        internal void OnNewChatClicked(PointerUpEvent evt)
        {
            Context.API.CancelPrompt();
            Context.Blackboard.ClearActiveConversation(true);
            ClearChat();
            ClearContext(evt);
            m_ProgressElement.Stop();
            Context.API.Reset();

            m_NewChatButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            TimerUtils.DelayedAction(ref s_NewChatActiveTokenSource, () =>
            {
                m_NewChatButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
            });


            AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.CreateNewConversation);
        }

        void OnHierarchChanged()
        {
            UpdateContextSelectionElements();
        }

        void OnAssetDeletes(string[] paths)
        {
            CheckContextForDeletedAssets(paths);
        }

        void OnActiveProgressCancelRequested()
        {
            if (!Context.Blackboard.IsAPIWorking)
            {
                return;
            }

            AIAssistantAnalytics.ReportUITriggerBackendEvent(UITriggerBackendEventSubType.CancelRequest, d => d.ConversationId = Context.Blackboard.ActiveConversationId.Value);
            Context.API.CancelAssistant(Context.Blackboard.ActiveConversationId);
        }

        void OnRequestSubmit(string message)
        {
            message = message.Trim(k_MessageTrimChars);
            // If musing is in progress and the submit button is pressed, stop the current request:
            if (Context.Blackboard.IsAPIWorking || string.IsNullOrEmpty(message))
            {
                if (Context.Blackboard.IsAPIWorking)
                {
                    Context.API.CancelAssistant(Context.Blackboard.ActiveConversationId);
                }

                m_ChatInput.ClearText();
                return;
            }

            Context.Blackboard.UnlockConversationChange();

            m_ChatInput.ClearText();
            m_EmptyStateRoot.SetDisplay(false);
            Context.API.SendPrompt(message, Context.Blackboard.ActiveMode);

            // Clear screenshot attachments after sending the prompt
            ClearScreenshotContextEntries();
            UpdateContextSelectionElements();
        }

        void OnViewGeometryChanged(GeometryChangedEvent evt)
        {
            bool isCompactView = evt.newRect.width < AssistantUIConstants.CompactWindowThreshold;

            m_HistoryButton.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);
            m_NewChatButton.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);

            m_ConversationName.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);

            m_FooterRoot.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);
        }

        void AddItemsNumberToLabel(int numItems)
        {
            m_DropdownToggleLabel.text = $"Attached items ({numItems})";
        }

        void ToggleContextDropdown()
        {
            if (m_SelectedContextDropdown.IsShown)
            {
                m_ContextDropdownButton.RemoveFromClassList(k_ContextDropdownToggleFocused);
                HideContextPopup();
            }
            else
            {
                m_ContextDropdownButton.AddToClassList(k_ContextDropdownToggleFocused);
                ShowContextPopup();
            }
        }

        void ToggleSelectionPopup()
        {
            if (m_SelectionPopup.IsShown)
            {
                HideSelectionPopup();
            }
            else
            {
                ShowSelectionPopup();
            }
        }

        void ShowContextPopup()
        {
            m_SelectedContextDropdown.Show();

            m_ContextDropdownTracker = new PopupTracker(
                m_SelectedContextDropdown,
                m_ContextDropdownButton,
                new Vector2Int(-1, 47),
                m_ContextDropdownButton
            );
            m_ContextDropdownTracker.Dismiss += HideContextPopup;
        }

        void HideContextPopup()
        {
            if (m_ContextDropdownTracker == null)
            {
                // Popup is not active
                return;
            }

            m_ContextDropdownTracker.Dismiss -= HideContextPopup;
            m_ContextDropdownTracker.Dispose();
            m_ContextDropdownTracker = null;

            m_SelectedContextDropdown.Hide();
        }

        void ShowSelectionPopup()
        {
            // Restore previous context selection
            m_SelectionPopup.SetSelectionFromContext(k_SelectedContext);

            m_ChatInput.ContextButton.EnableInClassList("mui-selected-context-button-open", true);

            m_SelectionPopup.ShowPopup();

            m_SelectionPopupTracker = new PopupTracker(m_SelectionPopup, m_ChatInput.ContextButton, m_PopupAnchor);
            m_SelectionPopupTracker.Dismiss += HideSelectionPopup;
        }

        void HideSelectionPopup()
        {
            if (m_SelectionPopupTracker == null)
            {
                // Popup is not active
                return;
            }

            m_SelectionPopupTracker.Dismiss -= HideSelectionPopup;
            m_SelectionPopupTracker.Dispose();
            m_SelectionPopupTracker = null;

            m_SelectionPopup.Hide();

            m_ChatInput.ContextButton.EnableInClassList("mui-selected-context-button-open", false);
            m_ChatInput.ContextButton.EnableInClassList("mui-selected-context-button-default-behavior", true);
        }

        void InitializeContextDropdown()
        {
            m_SelectedContextDropdown = new ContextDropdown();
            m_SelectedContextDropdown.Initialize(Context);
            m_SelectedContextDropdown.Hide();

            m_PopupRoot.Add(m_SelectedContextDropdown);

            if (k_HostWindow != null)
            {
                k_HostWindow.FocusLost += HideContextPopup;
            }
        }

        void InitializeSelectionPopup()
        {
            m_SelectionPopup = new SelectionPopup();
            m_SelectionPopup.Initialize(Context);
            m_SelectionPopup.Hide();
            m_SelectionPopup.OnSelectionChanged += () =>
            {
                // Memorize current context selection
                SyncContextSelection(m_SelectionPopup.ObjectSelection, m_SelectionPopup.ConsoleSelection);

                UpdateContextSelectionElements();
            };

            m_PopupRoot.Add(m_SelectionPopup);

            if (k_HostWindow != null)
            {
                k_HostWindow.FocusLost += HideSelectionPopup;
            }
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssistantAssetModificationDelegates.AssetDeletes -= OnAssetDeletes;
            
            AssistantEvents.Unsubscribe(ref m_ConversationSelectedEventTicket);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssistantAssetModificationDelegates.AssetDeletes += OnAssetDeletes;
            
            m_ConversationSelectedEventTicket = AssistantEvents.Subscribe<EventHistoryConversationSelected>(OnHistoryEntrySelected);
        }
    }
}
