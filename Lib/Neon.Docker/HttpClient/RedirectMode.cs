namespace Microsoft.Net.Http.Client
{
    /// <summary>
    /// Enumerates HTTP request redirect modes.
    /// </summary>
    public enum RedirectMode
    {
        /// <summary>
        /// Do not follow redirects.
        /// </summary>
        None,

        /// <summary>
        /// Disallows redirecting from HTTPS to HTTP
        /// </summary>
        NoDowngrade,

        /// <summary>
        /// Follow all redirects
        /// </summary>
        All,
    }
}