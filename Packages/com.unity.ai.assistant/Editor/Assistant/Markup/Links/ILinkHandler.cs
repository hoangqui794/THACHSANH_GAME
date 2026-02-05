namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// An interface for handling custom links in a markdown block
    /// </summary>
    interface ILinkHandler
    {
        /// <summary>
        /// Handles the given link
        /// </summary>
        /// <param name="prefix">
        /// The prefix that was used for this url.
        /// For instance 'custom://my_link' would result in the prefix 'custom'
        /// </param>
        /// <param name="url">
        /// The link to handle, excluding any prefix.
        /// For instance 'custom://my_link' would result in the link 'my_link'
        /// </param>
        public void Handle(string prefix, string url);
    }
}
