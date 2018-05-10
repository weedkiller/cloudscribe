﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    public static class RoutingAndMvc
    {
        public static IRouteBuilder UseCustomRoutes(this IRouteBuilder routes, bool useFolders)
        {
            routes.AddCloudscribeFileManagerRoutes();

            if (useFolders)
            {
                routes.MapRoute(
                   name: "foldererrorhandler",
                   template: "{sitefolder}/oops/error/{statusCode?}",
                   defaults: new { controller = "Oops", action = "Error" },
                   constraints: new { name = new cloudscribe.Core.Web.Components.SiteFolderRouteConstraint() }
                );


                routes.MapRoute(
                    name: "folderdefault",
                    template: "{sitefolder}/{controller}/{action}/{id?}",
                    defaults: new { controller = "Home", action = "Index" },
                    constraints: new { name = new cloudscribe.Core.Web.Components.SiteFolderRouteConstraint() });


            }

            routes.MapRoute(
               name: "stateautosuggest",
               template: "coredata/stateautosuggestjson",
               defaults: new { controller = "CoreDaata", action = "StateAutoSuggestJson" }
               );

            routes.MapRoute(
               name: "errorhandler",
               template: "oops/error/{statusCode?}",
               defaults: new { controller = "Oops", action = "Error" }
               );

            routes.MapRoute(
                name: "default",
                template: "{controller=Home}/{action=Index}/{id?}"
                //,defaults: new { controller = "Home", action = "Index" }
                );


            return routes;
        }

        public static IServiceCollection SetupMvc(
            this IServiceCollection services,
            IConfiguration config,
            bool sslIsAvailable
            )
        {

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
            });

            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
            });
            
            if (sslIsAvailable)
            {
                services.Configure<MvcOptions>(options =>
                {
                    options.Filters.Add(new RequireHttpsAttribute());
                });
            }

            var boostrapVersion = config.GetValue<int>("DevOptions:BootstrapVersion");

            services.AddMvc()
                    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                    .AddDataAnnotationsLocalization()
                    .AddRazorOptions(options =>
                    {
                        options.AddCloudscribeViewLocationFormats();
                        
                        switch (boostrapVersion)
                        {
                            case 4:

                                options.AddCloudscribeCommonEmbeddedViews();
                                options.AddCloudscribeNavigationBootstrap4Views();
                                options.AddCloudscribeFileManagerBootstrap4Views();

                                options.AddCloudscribeCoreBootstrap4Views();
                                options.AddCloudscribeLoggingBootstrap4Views();
                                options.AddCloudscribeCoreIdentityServerIntegrationBootstrap4Views();

                                break;

                            case 3:
                            default:

                                options.AddCloudscribeCommonEmbeddedViews();
                                options.AddCloudscribeNavigationBootstrap3Views();
                                options.AddCloudscribeFileManagerBootstrap3Views();

                                options.AddCloudscribeCoreBootstrap3Views();
                                options.AddCloudscribeLoggingBootstrap3Views();
                                options.AddCloudscribeCoreIdentityServerIntegrationBootstrap3Views();

                                break;
                        }

                        

                        options.ViewLocationExpanders.Add(new cloudscribe.Core.Web.Components.SiteViewLocationExpander());

                    })
                    ;

            return services;
        }

    }
}
