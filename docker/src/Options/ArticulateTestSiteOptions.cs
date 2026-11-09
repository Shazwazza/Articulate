namespace ArticulateDockerSite.Options
{
    /// <summary>
    /// Configuration for bootstrapping the Articulate test-site API user and client credentials.
    /// </summary>
    public sealed class ArticulateTestSiteOptions
    {
        /// <summary>
        /// The configuration section that maps to <see cref="ArticulateTestSiteOptions"/>.
        /// </summary>
        internal const string SectionName = "Articulate:TestSite";
        internal const string ClientId = "articulate-test-site";
        internal const string UserName = "articulate-test-site";
        internal const string Email = "articulate-test-site@localhost";
        internal const string DisplayName = "Articulate Test Site";
        internal const string UserGroupAlias = "admin";

        /// <summary>
        /// Gets or sets whether the bootstrap is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the client secret used by test-site bootstrap.
        /// </summary>
        public string? ClientSecret { get; set; }

    }
}
