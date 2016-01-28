namespace TestApiServer
{
    using System.Web.Http;

    /// <summary>
    /// Provides methods to start the api server used in testing.
    /// </summary>
    public class WebApiApplication : System.Web.HttpApplication
    {
        /// <summary>
        /// Starts the server.
        /// </summary>
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
