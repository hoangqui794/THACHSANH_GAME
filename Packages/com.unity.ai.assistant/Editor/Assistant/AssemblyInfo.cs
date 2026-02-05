using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.AI.Assistant.UI.Editor")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.DeveloperTools")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.Tests")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.Tests.E2E")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.Benchmark.Tests")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.CodeLibrary.Editor")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.API.Editor")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.Tools.Editor")]

[assembly: InternalsVisibleTo("Unity.AI.Agents.Shared.Tests")]
[assembly: InternalsVisibleTo("Unity.AI.Agents.Profiler.Editor")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.Integrations.Profiler.Editor")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.Integrations.Sample.Editor")]

[assembly: InternalsVisibleTo("Unity.AI.Assistant.AssetGenerators.Editor")]
[assembly: InternalsVisibleTo("Unity.AI.Search.Editor")]
[assembly: InternalsVisibleTo("Unity.AI.Assistant.GameDataCollection.Editor")]

// Required for advanced mocking with Moq
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
