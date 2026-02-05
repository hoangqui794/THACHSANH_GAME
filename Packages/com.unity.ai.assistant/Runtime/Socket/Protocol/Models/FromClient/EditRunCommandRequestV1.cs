using Newtonsoft.Json;

namespace Unity.AI.Assistant.Socket.Protocol.Models.FromClient
{
    /// <summary>
    /// A user request to edit the content of a specific message
    /// </summary>
    class EditRunCommandRequestV1 : IModel
    {
        [JsonProperty("$type", Required = Required.Always)]
        public const string Type = "EDIT_RUN_COMMAND_REQUEST_V1";
        public string GetModelType() => Type;

        [JsonProperty("message_id", Required = Required.Always)]
        public string MessageId { get; set; }

        [JsonProperty("updated_run_command", Required = Required.Always)]
        public string UpdatedRunCommand { get; set; }
    }
}
