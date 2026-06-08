namespace ArticulateDockerSite.Options
{
    /// <summary>
    /// Configuration for bootstrapping the Articulate dev automation API user and client credentials.
    /// </summary>
    public sealed class ArticulateDevAutomationOptions
    {
        /// <summary>
        /// The configuration section that maps to <see cref="ArticulateDevAutomationOptions"/>.
        /// </summary>
        public const string SectionName = "Articulate:DevAutomation";

        /// <summary>
        /// Gets or sets whether the bootstrap is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the OAuth client id used by dev automation.
        /// </summary>
        public string ClientId { get; set; } = "articulate-dev-automation";

        /// <summary>
        /// Gets or sets the client secret used by dev automation.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the API user's username.
        /// </summary>
        public string UserName { get; set; } = "articulate-dev-automation";

        /// <summary>
        /// Gets or sets the API user's email address.
        /// </summary>
        public string Email { get; set; } = "articulate-dev-automation@localhost";

        /// <summary>
        /// Gets or sets the API user's display name.
        /// </summary>
        public string Name { get; set; } = "Articulate Dev Automation";

        /// <summary>
        /// Gets or sets the user group aliases the API user should be assigned to.
        /// Defaults to the built-in administrator group so dev automation can cover content and schema tasks in dev.
        /// </summary>
        public List<string> UserGroupAliases { get; set; } = [];
    }
}
