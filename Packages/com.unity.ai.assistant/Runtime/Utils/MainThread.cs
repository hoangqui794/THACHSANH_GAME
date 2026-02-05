using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AI.Assistant.Utils
{
    sealed class MainThread
    {
        // Note: this must NOT be statically instantiated as we need this to happen on main thread
        static MainThread s_Instance = null;

        SynchronizationContext Context { get; }

        MainThread()
        {
            Context = SynchronizationContext.Current;

            if (Context == null)
                throw new Exception(
                    "SynchronizationContext is null. MainThreadContext must be initialized on the main thread.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeOnRuntime() => InitializeOnMainThread();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitializeOnEditor() => InitializeOnMainThread();
#endif

        static void InitializeOnMainThread() => s_Instance = new MainThread();

        public static void DispatchAndForget(Action action)
        {
#if UNITY_EDITOR
            UnityEditor.Search.Dispatcher.Enqueue(() =>
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    InternalLog.LogException(e);
                }
            });
#else
            s_Instance.Context?.Post(_ => action(), null);
#endif
        }

        public static void DispatchAndForgetAsync(Func<Task> func)
        {
#if UNITY_EDITOR
            UnityEditor.Search.Dispatcher.Enqueue(async () =>
            {
                try
                {
                    await func();
                }
                catch (Exception e)
                {
                    InternalLog.LogException(e);
                }
            });
#else
            s_Instance.Context?.Post(async _ => await func(), null);
#endif
        }

    }
}
