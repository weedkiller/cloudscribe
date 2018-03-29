﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
//	Author:                 Joe Audette
//  Created:			    2016-06-06
//	Last Modified:		    2017-05-26
// 

using cloudscribe.Core.Identity;
using cloudscribe.Core.Models;
using cloudscribe.Web.Common;
using Microsoft.AspNetCore.Http;
using SaasKit.Multitenancy;
using System.Threading;
using System.Threading.Tasks;

namespace cloudscribe.Core.Web.Components
{
    public class RequestTimeZoneIdResolver : ITimeZoneIdResolver
    {
        public RequestTimeZoneIdResolver(
            IHttpContextAccessor contextAccessor,
            ITenantResolver<SiteContext> siteResolver,
            SiteUserManager<SiteUser> userManager
            )
        {
            _contextAccessor = contextAccessor;
            _siteResolver = siteResolver;
            _userManager = userManager;
        }

        private IHttpContextAccessor _contextAccessor;
        private ITenantResolver<SiteContext> _siteResolver;
        private SiteUserManager<SiteUser> _userManager;

        public async Task<string> GetUserTimeZoneId(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = _contextAccessor.HttpContext;
            if (context.User.Identity.IsAuthenticated)
            {
                var user = await _userManager.FindByIdAsync(context.User.GetUserId());
                if ((user != null) && (!string.IsNullOrEmpty(user.TimeZoneId)))
                {
                    return user.TimeZoneId;
                }
            }

            return await GetSiteTimeZoneId(cancellationToken);
        }

        public Task<string> GetSiteTimeZoneId(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tenant = _contextAccessor.HttpContext.GetTenant<SiteContext>();
            if (tenant != null)
            {
                if (!string.IsNullOrEmpty(tenant.TimeZoneId))
                    return Task.FromResult(tenant.TimeZoneId);
            }

            return Task.FromResult("America/New_York"); //default
        }

    }
}
