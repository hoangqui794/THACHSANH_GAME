using Unity.AI.Assistant.Editor.ServerCompatibility;
using Unity.AI.Toolkit.Accounts.Components;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.SessionBanner
{
    /// <summary>
    /// Session top banners.
    ///
    /// Acts as a state machine where `CurrentView` return the view that should currently be showed.
    /// </summary>
    [UxmlElement]
    partial class SessionBanner : AssistantSessionStatusBanner
    {
        AssistantInsufficientPointsBanner m_InsufficientPointsBanner;

        public SessionBanner()
        {
            NotificationsState.instance.hideCompatibility = false;
            this.AddManipulator(new ServerCompatibilityChanges(Refresh));
            this.AddManipulator(new PointsBalanceChanges(Refresh));
        }

        protected override VisualElement CurrentView()
        {
            var view = base.CurrentView();  // Prioritize base session status views.
            if (view == null)
            {
                if(ServerCompatibility.ServerCompatibility.Status == ServerCompatibility.ServerCompatibility.CompatibilityStatus.Unsupported)
                    view = new ServerCompatibilityNotSupportedBanner();
                else if(ServerCompatibility.ServerCompatibility.Status == ServerCompatibility.ServerCompatibility.CompatibilityStatus.Deprecated &&
                        !NotificationsState.instance.hideCompatibility)
                    view = new ServerCompatibilityDeprecatedNotificationView(Dismiss);
                else if (!Account.pointsBalance.CanAfford(AssistantConstants.ChatPreAuthorizePoints))
                    view = m_InsufficientPointsBanner ??= new AssistantInsufficientPointsBanner();
            }
            else if (view is LowPointsBanner && !Account.pointsBalance.CanAfford(AssistantConstants.ChatPreAuthorizePoints)) // Replace low points banner by insufficient points
                view = m_InsufficientPointsBanner ??= new AssistantInsufficientPointsBanner();
            
            EnableInClassList("empty", view == null);
            return view;
        }

        void Dismiss()
        {
            Clear();
            Refresh();
        }
    }
}
