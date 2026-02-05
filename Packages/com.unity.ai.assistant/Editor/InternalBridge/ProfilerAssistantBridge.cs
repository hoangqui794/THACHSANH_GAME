using System;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEngine;

namespace Unity.AI.Assistant.Bridge.Editor
{
    interface IProxyAskAssistantService : IDisposable
    {
        bool Initialize();

        public struct Context
        {
            public string Payload;

            public string Type;

            public string DisplayName;

            public object Metadata;
        }
        void ShowAskAssistantPopup(Rect parentRect, Context context, string prompt);
    }

#if PROFILER_ASSISTANT_INTEGRATION_ENABLED
    [AskAssistantServiceRole("CPU Profiler Assistant")]
    class ProfilerAssistantService : IAskAssistantService
    {
        IProxyAskAssistantService m_ProfilerAssistant;

        public ProfilerAssistantService()
        {
            var proxyProfilerAssistants = TypeCache.GetTypesDerivedFrom<IProxyAskAssistantService>();
            if (proxyProfilerAssistants.Count == 0)
            {
                throw new InvalidOperationException("No implementation of IProxyAskAssistantService found.");
            }

            m_ProfilerAssistant = Activator.CreateInstance(proxyProfilerAssistants[0]) as IProxyAskAssistantService;
        }

        public bool Initialize()
        {
            return m_ProfilerAssistant.Initialize();
        }

        public void Dispose()
        {
            m_ProfilerAssistant.Dispose();
            m_ProfilerAssistant = null;
        }

        public void ShowAskAssistantPopup(Rect parentRect, IAskAssistantService.Context context, string prompt)
        {
            var proxyContext = new IProxyAskAssistantService.Context()
            {
                Payload = context.Payload,
                Type = context.Type,
                DisplayName = context.DisplayName,
                Metadata = context.Metadata
            };
            m_ProfilerAssistant.ShowAskAssistantPopup(parentRect, proxyContext, prompt);
        }
    }

    static class ProfilerMarkerInformationProvider
    {
        public static string GetMarkerInformation(string markerName)
        {
            return MarkersInformationProvider.GetMarkerInfo(markerName);
        }
    }
#endif
}
