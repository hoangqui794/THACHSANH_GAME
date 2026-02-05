using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;

namespace Unity.AI.Assistant.Integrations.Sample.Editor
{
    class SampleTools
    {
        [Serializable]
        public struct WeatherOutput
        {
            public enum WeatherType
            {
                Sun,
                Cloud,
                Rain,
                Snow
            }
            
            [Description("The weather type")]
            public WeatherType Type;

            [Description("The temperature, in Celsius")]
            public int Temperature;
        }

        [AgentTool(
            "Get the current location of the user.",
            "Unity.ApiSample.GetLocation",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]  // Available in both modes as it is a read-only tool
        public static async Task<string> GetLocation(ToolExecutionContext context)
        {
            // Tools should avoid user interactions in auto-run mode
            if (AssistantEditorPreferences.AutoRun)
                return "Paris";
            
            // Create an interaction UX to pick a city
            var locationInteraction = new SampleInteraction(new List<string> { "Paris", "London", "Montreal", "Berlin" });

            // Wait for user interaction result
            var location = await context.Interactions.WaitForUser(locationInteraction);
            return location;
        }

        [AgentTool(
            "Return the weather at a given location.",
            "Unity.ApiSample.GetWeather",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]  // Available in both modes as it is a read-only tool
        public static WeatherOutput GetWeather(
            [Parameter("The location from which to get the weather")]
            string location
        )
        {
            if (location == "Paris")
                return new WeatherOutput
                {
                    Type = WeatherOutput.WeatherType.Sun,
                    Temperature = 27
                };
            
            if (location == "London")
                return new WeatherOutput
                {
                    Type = WeatherOutput.WeatherType.Rain,
                    Temperature = 17
                };
            
            if (location == "Montreal")
                return new WeatherOutput
                {
                    Type = WeatherOutput.WeatherType.Snow,
                    Temperature = -15
                };
            
            if (location == "Berlin")
                return new WeatherOutput
                {
                    Type = WeatherOutput.WeatherType.Cloud,
                    Temperature = 19
                };
            
            throw new Exception("Unknown location. Location must be in: 'Paris', 'London', 'Montreal', 'Berlin'");
        }
        
        
        [AgentTool(
            "Saves a note to a file.",
            "Unity.ApiSample.SaveNote",
            assistantMode: AssistantMode.Agent)] // Only available in Agent mode as it is not a read-only tool
        public static async Task SaveNote(
            ToolExecutionContext context,
            [Parameter("The text of the note to save")]
            string text
        )
        {
            const string folderPath = "Assets/TempNotes";
            
            var fileName = $"Note_{DateTime.Now:yyyyMMdd_HHmmssfff}.txt";
            var filePath = Path.Combine(folderPath, fileName);

            // Any operation that create / delete / modify data, objects or files should first check for permissions
            await context.Permissions.CheckFileSystemAccess(IToolPermissions.ItemOperation.Create, filePath);
            
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            await File.WriteAllTextAsync(filePath, text);
            
            AssetDatabase.ImportAsset(filePath);
            AssetDatabase.Refresh();
            
            // TODO: Any operation that create / delete / modify files should record the operation through the Undo system
            // context.Undo.RecordFile(Create, filePath);
        }
    }
}
