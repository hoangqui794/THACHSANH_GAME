using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    // TODO: Move this to some shared assembly?
    abstract class BaseInteraction<TOutput> : VisualElement, IUserInteraction<TOutput>
    {
        public event Action<TOutput> OnCompleted;

        public TaskCompletionSource<TOutput> TaskCompletionSource { get; } = new();

        protected void CompleteInteraction(TOutput output)
        {
            TaskCompletionSource.SetResult(output);
            OnCompleted?.Invoke(output);
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
            OnCanceled();
        }

        protected virtual void OnCanceled() { }
    }
}
