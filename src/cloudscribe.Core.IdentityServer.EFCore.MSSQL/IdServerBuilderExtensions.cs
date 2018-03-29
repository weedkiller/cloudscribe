﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using cloudscribe.Core.IdentityServer.EFCore.Extensions;
using cloudscribe.Core.IdentityServer.EFCore.Interfaces;
using cloudscribe.Core.IdentityServer.EFCore.MSSQL;
using cloudscribe.Core.IdentityServer.EFCore.Stores;
using cloudscribe.Core.IdentityServerIntegration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IdServerBuilderExtensions
    {
        public static IIdentityServerBuilder AddCloudscribeCoreEFIdentityServerStorageMSSQL(
            this IIdentityServerBuilder builder,
            string connectionString,
            int maxConnectionRetryCount = 0,
            int maxConnectionRetryDelaySeconds = 30,
            ICollection<int> transientSqlErrorNumbersToAdd = null
            )
        {
            //builder.AddConfigurationStoreMSSQL(connectionString);    
            //builder.AddOperationalStoreMSSQL(connectionString);
            builder.Services.AddCloudscribeCoreIdentityServerEFStorageMSSQL(connectionString, maxConnectionRetryCount, maxConnectionRetryDelaySeconds, transientSqlErrorNumbersToAdd);
            builder.Services.AddScoped<IStorageInfo, StorageInfo>();

            return builder;
        }

        //public static IIdentityServerBuilder AddConfigurationStoreMSSQL(
        //    this IIdentityServerBuilder builder, 
        //    string connectionString,
        //    Action<DbContextOptionsBuilder> optionsAction = null)
        //{
            
        //    builder.Services.AddEntityFrameworkSqlServer()
        //        .AddDbContext<ConfigurationDbContext>((serviceProvider, options) =>
        //        options.UseSqlServer(connectionString)
        //               .UseInternalServiceProvider(serviceProvider)
        //               );

        //    builder.Services.AddCloudscribeCoreIdentityServerStores();

        //    builder.Services.AddScoped<IConfigurationDbContext, ConfigurationDbContext>();
            
        //    return builder;
        //}

        public static IIdentityServerBuilder AddConfigurationStoreCache(
            this IIdentityServerBuilder builder)
        {
            builder.Services.AddMemoryCache(); // TODO: remove once update idsvr since it does this
            builder.AddInMemoryCaching();

            // these need to be registered as concrete classes in DI for
            // the caching decorators to work
            builder.Services.AddTransient<ClientStore>();
            builder.Services.AddTransient<ResourceStore>();

            // add the caching decorators
            builder.AddClientStoreCache<ClientStore>();
            builder.AddResourceStoreCache<ResourceStore>();

            return builder;
        }

        //public static IIdentityServerBuilder AddOperationalStoreMSSQL(
        //    this IIdentityServerBuilder builder,
        //    string connectionString,
        //    Action<DbContextOptionsBuilder> optionsAction = null)
        //{
            
        //    builder.Services.AddEntityFrameworkSqlServer()
        //        .AddDbContext<PersistedGrantDbContext>((serviceProvider, options) =>
        //        options.UseSqlServer(connectionString)
        //               .UseInternalServiceProvider(serviceProvider)
        //               );

        //    builder.Services.AddScoped<IPersistedGrantDbContext, PersistedGrantDbContext>();
            
        //    return builder;
        //}

        public static IServiceCollection AddCloudscribeCoreIdentityServerEFStorageMSSQL(
            this IServiceCollection services,
            string connectionString,
            int maxConnectionRetryCount = 0,
            int maxConnectionRetryDelaySeconds = 30,
            ICollection<int> transientSqlErrorNumbersToAdd = null,
            bool useSql2008Compatibility = false
            )
        {
            //services.AddEntityFrameworkSqlServer()
            //    .AddDbContext<ConfigurationDbContext>(options =>
            //        options.UseSqlServer(connectionString));

            services.AddEntityFrameworkSqlServer()
                .AddDbContext<ConfigurationDbContext>(options =>
                    options.UseSqlServer(connectionString,
                        sqlServerOptionsAction: sqlOptions =>
                        {
                            if (maxConnectionRetryCount > 0)
                            {
                                //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency 
                                sqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: maxConnectionRetryCount,
                                    maxRetryDelay: TimeSpan.FromSeconds(maxConnectionRetryDelaySeconds),
                                    errorNumbersToAdd: transientSqlErrorNumbersToAdd);
                            }
                            if(useSql2008Compatibility)
                            {
                                sqlOptions.UseRowNumberForPaging();
                            }


                        }));

            services.AddCloudscribeCoreIdentityServerStores();

            services.AddScoped<IConfigurationDbContext, ConfigurationDbContext>();

            //services.AddEntityFrameworkSqlServer()
            //    .AddDbContext<PersistedGrantDbContext>(options =>
            //        options.UseSqlServer(connectionString));

            services.AddEntityFrameworkSqlServer()
                .AddDbContext<PersistedGrantDbContext>(options =>
                    options.UseSqlServer(connectionString,
                        sqlServerOptionsAction: sqlOptions =>
                        {
                            if (maxConnectionRetryCount > 0)
                            {
                                //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency 
                                sqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: maxConnectionRetryCount,
                                    maxRetryDelay: TimeSpan.FromSeconds(maxConnectionRetryDelaySeconds),
                                    errorNumbersToAdd: transientSqlErrorNumbersToAdd);
                            }

                            if (useSql2008Compatibility)
                            {
                                sqlOptions.UseRowNumberForPaging();
                            }
                            

                        }));

            services.AddScoped<IPersistedGrantDbContext, PersistedGrantDbContext>();

            return services;
        }




    }
}