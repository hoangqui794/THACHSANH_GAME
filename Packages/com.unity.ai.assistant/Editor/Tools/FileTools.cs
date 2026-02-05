using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Tools.Editor
{
    class FileTools
    {
        const string k_FindFilesFunctionId = "Unity.FindFiles";
        const string k_GetFileContentFunctionId = "Unity.GetFileContent";

        internal const int k_FindFilesMaxResults = 100;

        static readonly string[] k_SearchFolders = { "Assets" };

        [Serializable]
        public struct FileMatch
        {
            [Description("The relative path to the file containing the match")]
            public string FilePath;

            [Description("The line number where the match was found")]
            public int LineNumber;

            [Description("The found content, including context lines before and after.")]
            public string MatchingContent;
        }

        [Serializable]
        public struct SearchFileContentOutput
        {
            [Description("The matching results")]
            public List<FileMatch> Matches;

            public string Info;
        }

        [AgentTool(
            "Search for content within files and return a list of matching file with the found content, including some context.",
            k_FindFilesFunctionId,
            assistantMode: AssistantMode.Agent | AssistantMode.Ask,
            tags: FunctionCallingUtilities.k_SmartContextTag)]
        public static async Task<SearchFileContentOutput> FindFiles(
            ToolExecutionContext context,
            [Parameter(
                "Regex pattern to search for in file contents.\n" +
                "Examples:\n" +
                "  \"TODO\": Match any line containing 'TODO'\n" +
                "  \"public\\s+class\\s+\\w+\": Match class declarations in C#\n" +
                "Leave empty to not filter by content (files will still be filtered by 'nameRegex').\n" +
                "In that case, will return a preview of the file content."
            )]
            string searchPattern,
	        [Parameter(
		        "Regex pattern applied to the relative file path (including filename + extension).\n" +
		        "Examples:\n" +
		        "  \".*Program\\.cs$\": Match a specific filename 'Program.cs'\n" +
		        "  \".*Controllers/.*\": Match all files under a 'Controllers' folder\n" +
		        "  \".*\\.txt$\": Match all files with the .txt extension\n" +
		        "  \".*Test.*\": Match any file path containing 'Test'\n" +
                "Leave empty to include all files BUT try to use this field as much as possible to limit the number of results."
	        )]
            string nameRegex = "",
            [Parameter("If true, will also search into Packages, otherwise only in Assets. True by default.")]
            bool includePackages = true,
	        [Parameter("Number of context lines to show around matches (Defaults to 2)")]
            int contextLines = 2,
	        [Parameter("Index of the first match to return (for pagination, defaults to 0 to get the first page)")]
            int startIndex = 0
        )
        {
	        const int maxResults = k_FindFilesMaxResults;
            const int maxReadLines = 5000;

            var results = new SearchFileContentOutput { Matches = new List<FileMatch>(), Info = "" };
	        var projectPath = Directory.GetCurrentDirectory();

            var searchPaths = k_SearchFolders.Select(folder => Path.Combine(projectPath, folder)).ToList();
            foreach (var folder in searchPaths)
            {
                await context.Permissions.CheckFileSystemAccess(IToolPermissions.ItemOperation.Read, Path.Combine(projectPath, folder));
            }

            // Add resolved package paths
            if (includePackages)
            {
                await context.Permissions.CheckFileSystemAccess(IToolPermissions.ItemOperation.Read, Path.Combine(projectPath, "Packages"));
                var packagePaths = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(p => p.resolvedPath);
                searchPaths.AddRange(packagePaths);
            }

	        Regex searchRegex = null;
	        Regex fileRegex = null;
	        try
	        {
		        if (!string.IsNullOrEmpty(searchPattern))
			        searchRegex = new Regex(searchPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		        if (!string.IsNullOrWhiteSpace(nameRegex))
			        fileRegex = new Regex(nameRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
	        }
	        catch (ArgumentException ex)
	        {
		        throw new ArgumentException($"Invalid regex: {ex.Message}", ex);
	        }

	        var skipped = 0;
            var linesCache = new Queue<string>(); // for backward context
            var forwardBuffer = new Queue<string>(); // to store lines read ahead for context
	        foreach (var searchPath in searchPaths)
	        {
		        if (!Directory.Exists(searchPath))
			        continue;

		        IEnumerable<string> files;
		        try
		        {
			        files = Directory.EnumerateFiles(searchPath, "*.*", SearchOption.AllDirectories);
		        }
		        catch (Exception)
		        {
			        continue;
		        }

		        foreach (var file in files)
		        {
			        var relativePath = Path.GetRelativePath(projectPath, file);

                    // File path matching
			        if (fileRegex != null && !fileRegex.IsMatch(relativePath))
				        continue;

			        try
			        {
				        using var reader = new StreamReader(file);
				        var lineNumber = 0;

                        // No filtering by content
				        if (searchRegex == null)
				        {
                            // pagination logic
                            if (skipped < startIndex)
                            {
                                skipped++;
                                continue;
                            }

					        // No content filter: return file preview
					        var previewLines = new List<string>();
					        for (var i = 0; i < contextLines * 2 + 1; i++)
					        {
						        if (reader.Peek() < 0)
                                    break;

						        var line = reader.ReadLine();
						        if (line == null)
                                    break;

						        previewLines.Add(line);
					        }

					        results.Matches.Add(new FileMatch
					        {
						        FilePath = relativePath,
						        LineNumber = -1,
						        MatchingContent = string.Join("\n", previewLines)
					        });

					        if (results.Matches.Count >= maxResults)
                            {
                                results.Info = $"Result set truncated. Use startIndex={startIndex + results.Matches.Count} to fetch next page.";
						        return results;
					        }

					        continue;
				        }

				        // Normal search mode
                        linesCache.Clear();
                        forwardBuffer.Clear();

                        while (true)
                        {
                            var line = forwardBuffer.Count > 0 ?
                                forwardBuffer.Dequeue() :
                                reader.ReadLine();

                            if (line == null || lineNumber >= maxReadLines)
                                break;

                            lineNumber++;
                            linesCache.Enqueue(line);
                            if (linesCache.Count > contextLines * 2 + 1)
                                linesCache.Dequeue();

                            if (searchRegex.IsMatch(line))
                            {
                                // Pagination logic
                                if (skipped < startIndex)
                                {
                                    skipped++;
                                    continue;
                                }

                                // Store the line number at the match
                                var matchLineNumber = lineNumber;

                                // Collect backward context
                                var contextLinesList = new List<string>(linesCache);

                                // Collect forward context without skipping matches
                                var tempForward = new List<string>();
                                for (var j = 0; j < contextLines; j++)
                                {
                                    var nextLine = reader.ReadLine();
                                    if (nextLine == null)
                                        break;
                                    tempForward.Add(nextLine);
                                    linesCache.Enqueue(nextLine);
                                    if (linesCache.Count > contextLines * 2 + 1)
                                        linesCache.Dequeue();
                                }

                                contextLinesList.AddRange(tempForward);

                                results.Matches.Add(new FileMatch
                                {
                                    FilePath = relativePath,
                                    LineNumber = matchLineNumber,
                                    MatchingContent = string.Join("\n", contextLinesList)
                                });

                                if (results.Matches.Count >= maxResults)
                                {
                                    results.Info = $"Result set truncated. Use startIndex={startIndex + results.Matches.Count} to fetch next page.";
                                    return results;
                                }

                                // Add forward lines to buffer so they get checked in the next iterations
                                foreach (var bufferedLine in tempForward)
                                    forwardBuffer.Enqueue(bufferedLine);
                            }
                        }

			        }
			        catch (Exception)
			        {
				        continue;
			        }
		        }
	        }

	        return results;
        }

        [AgentTool(
            "Returns the text content of a file.",
            k_GetFileContentFunctionId,
            assistantMode: AssistantMode.Agent | AssistantMode.Ask,
            tags: FunctionCallingUtilities.k_SmartContextTag)]
        public static async Task<string> GetFileContent(
            ToolExecutionContext context,
            [Parameter("The path to the file, as returned by FindFile(...).")]
            string filePath
            )
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return "File path cannot be null or empty.";

                var projectPath = Directory.GetCurrentDirectory();
                var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(projectPath, filePath);

                if (!File.Exists(fullPath))
                    return $"File at path '{filePath}' not found";


                await context.Permissions.CheckFileSystemAccess(IToolPermissions.ItemOperation.Read, fullPath);
                var content = File.ReadAllText(fullPath);
                return content;
            }
            catch (UnauthorizedAccessException)
            {
                return $"Access denied to file '{filePath}'.";
            }
            catch (IOException ex)
            {
                return $"IO error reading file '{filePath}': {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error reading file '{filePath}': {ex.Message}";
            }
        }
    }
}
