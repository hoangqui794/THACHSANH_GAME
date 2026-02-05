using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Unity.AI.Assistant.Utils
{
    /// TODO - https://jira.unity3d.com/browse/ASST-1993:
    /// This is a relatively fast way to patch an issue where other threads are requesting conversation update more
    /// frequently than the main thread is processing them. Ideally, we need to take a look at this more closely. It
    /// might be necessary to create a more robust scheduling system for our purposes. This should be revisited and a
    /// better architectural decision should be made.
    static class DispatchUtility
    {
        static readonly object k_RegisteredActionLock = new();
        static readonly Dictionary<string, Action> k_RegisteredActions = new();

        /// <summary>
        /// Dispatches an action that can be overriden any number of times until it is called. This means that if
        /// <see cref="DispatchWithOverride"/> is called n times before the main thread attempts to call the dispatched
        /// function, only the nth <param name="action"></param> will actually be called.
        /// </summary>
        /// <param name="id">An id to identify functions that should override previous dispatches</param>
        /// <param name="action">The newest version of the function to dispatch</param>
        public static void DispatchWithOverride(string id, Action action)
        {
            bool isFirstUpdate;

            lock (k_RegisteredActionLock)
            {
                isFirstUpdate = !k_RegisteredActions.ContainsKey(id);
                k_RegisteredActions[id] = action;
            }

            if (isFirstUpdate)
            {
                MainThread.DispatchAndForget(() =>
                {
                    Action actionToRun = null;
                    lock (k_RegisteredActionLock)
                    {
                        k_RegisteredActions.Remove(id, out actionToRun);
                    }

                    actionToRun?.Invoke();
                });
            }
        }
    }
}
