namespace Unity.Relay
{
    /// <summary>
    /// Constants for relay WebSocket protocol message types
    /// </summary>
    static class RelayConstants
    {
        // Client → Server messages
        public const string RELAY_PING = "RELAY_PING";
        public const string RELAY_SHUTDOWN = "RELAY_SHUTDOWN";
        public const string RELAY_BLOCK_INCOMING_CLOUD_MESSAGES = "RELAY_BLOCK_INCOMING_CLOUD_MESSAGES";
        public const string RELAY_RECOVER_MESSAGES = "RELAY_RECOVER_MESSAGES";
        public const string RELAY_SESSION_END = "RELAY_SESSION_END";
        public const string RELAY_SESSION_START = "RELAY_SESSION_START";

        // Server → Client messages
        public const string RELAY_PONG = "RELAY_PONG";
        public const string RELAY_RECOVER_MESSAGES_COMPLETED = "RELAY_RECOVER_MESSAGES_COMPLETED";
        public const string RELAY_MESSAGE_PARSE_ERROR = "RELAY_MESSAGE_PARSE_ERROR";
        public const string RELAY_UNKNOWN_MESSAGE_TYPE = "RELAY_UNKNOWN_MESSAGE_TYPE";
    }
}
