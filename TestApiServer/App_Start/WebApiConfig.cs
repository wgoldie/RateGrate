namespace TestApiServer
{
    using System.Web.Http;

    /// <summary>
    /// Configures the api server used in testing.
    /// </summary>
    public static class WebApiConfig
    {
        /// <summary>
        /// Registers http action routes.
        /// </summary>
        /// <param name="config">The current http configuration for the server.</param>
        public static void Register(HttpConfiguration config)
        {
            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional });
        }
    }
}
