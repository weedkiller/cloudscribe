﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2017-05-22
// Last Modified:			2018-03-07
// 

using cloudscribe.Core.Identity;
using cloudscribe.Core.Models;
using cloudscribe.Core.Web.ExtensionPoints;
using cloudscribe.Core.Web.ViewModels.Account;
using cloudscribe.Core.Web.ViewModels.SiteUser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using cloudscribe.Core.Models.Identity;

namespace cloudscribe.Core.Web.Components
{
    public class AccountService : IAccountService
    {
        public AccountService(
            SiteUserManager<SiteUser> userManager,
            SignInManager<SiteUser> signInManager,
            IIdentityServerIntegration identityServerIntegration,
            ISocialAuthEmailVerfificationPolicy socialAuthEmailVerificationPolicy,
            IProcessAccountLoginRules loginRulesProcessor,
            INewUserDisplayNameResolver displayNameResolver
            //,ILogger<AccountService> logger
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _identityServerIntegration = identityServerIntegration;
            _socialAuthEmailVerificationPolicy = socialAuthEmailVerificationPolicy;
            _loginRulesProcessor = loginRulesProcessor;
            _displayNameResolver = displayNameResolver;
            
        }

        protected readonly SiteUserManager<SiteUser> _userManager;
        protected readonly SignInManager<SiteUser> _signInManager;
        protected readonly IIdentityServerIntegration _identityServerIntegration;
        protected readonly ISocialAuthEmailVerfificationPolicy _socialAuthEmailVerificationPolicy;
        protected readonly IProcessAccountLoginRules _loginRulesProcessor;
        protected readonly INewUserDisplayNameResolver _displayNameResolver;
        
        private async Task<SiteUser> CreateUserFromExternalLogin(
            ExternalLoginInfo externalLoginInfo, 
            string providedEmail = null,
            bool? didAcceptTerms = null
            )
        {
            var email = providedEmail;
            if (string.IsNullOrWhiteSpace(email))
            {
                email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email);
            }

            DateTime? termsAcceptedDate = null;
            if (didAcceptTerms == true && !string.IsNullOrWhiteSpace(_userManager.Site.RegistrationAgreement)) { termsAcceptedDate = DateTime.UtcNow; }

            if (!string.IsNullOrWhiteSpace(email) && email.Contains("@"))
            {
                var userName = await _userManager.SuggestLoginNameFromEmail(_userManager.Site.Id, email);
                var newUser = new SiteUser
                {
                    SiteId = _userManager.Site.Id,
                    UserName = userName,
                    Email = email,
                    FirstName = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.GivenName),
                    LastName = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Surname),
                    AccountApproved = _userManager.Site.RequireApprovalBeforeLogin ? false : true,
                    EmailConfirmed = _socialAuthEmailVerificationPolicy.HasVerifiedEmail(externalLoginInfo),
                    AgreementAcceptedUtc = termsAcceptedDate,
                    LastLoginUtc = DateTime.UtcNow
                };
                //https://github.com/joeaudette/cloudscribe/issues/346
                newUser.DisplayName = _displayNameResolver.ResolveDisplayName(newUser);

                var identityResult = await _userManager.CreateAsync(newUser);
                if (identityResult.Succeeded)
                {
                    identityResult = await _userManager.AddLoginAsync(newUser, externalLoginInfo);
                    return newUser;
                }
            }
            return null;
        }

        
        public virtual async Task<UserLoginResult> TryExternalLogin(string providedEmail = "", bool? didAcceptTerms = null)
        {
            var template = new LoginResultTemplate();
            IUserContext userContext = null;
            var email = providedEmail;

            template.ExternalLoginInfo = await _signInManager.GetExternalLoginInfoAsync();
            if (template.ExternalLoginInfo == null)
            {
                template.RejectReasons.Add("signInManager.GetExternalLoginInfoAsync returned null");
            }
            else
            {
                template.User = await _userManager.FindByLoginAsync(template.ExternalLoginInfo.LoginProvider, template.ExternalLoginInfo.ProviderKey);
                
                if(template.User == null)
                {
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        email = template.ExternalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email);
                    }

                    if (!string.IsNullOrWhiteSpace(email) && email.Contains("@"))
                    {
                        template.User = await _userManager.FindByNameAsync(email);
                    }
                }
                
                if (template.User == null)
                {
                    template.User = await CreateUserFromExternalLogin(template.ExternalLoginInfo, email, didAcceptTerms);
                    if(template.User != null) { template.IsNewUserRegistration = true; }
                }
            }
 
            if (template.User != null)
            {
                await _loginRulesProcessor.ProcessAccountLoginRules(template);
            }
            
            if (template.SignInResult == SignInResult.Failed && template.User != null && template.RejectReasons.Count == 0)
            {
                template.SignInResult = await _signInManager.ExternalLoginSignInAsync(template.ExternalLoginInfo.LoginProvider, template.ExternalLoginInfo.ProviderKey, isPersistent: false);
                if(template.SignInResult.Succeeded)
                {
                  
                    //update last login time
                    if(!template.IsNewUserRegistration)
                    {
                        //already tracked if user was just created
                        template.User.LastLoginUtc = DateTime.UtcNow;
                        await _userManager.UpdateAsync(template.User);
                    }
                    
                }      
            }

            if(template.User != null
                && template.SignInResult != SignInResult.Success 
                && template.SignInResult != SignInResult.TwoFactorRequired)
            {
                //clear the external login 
                await _signInManager.SignOutAsync();
            }

            if(template.User != null) { userContext = new UserContext(template.User); }
            
            return new UserLoginResult(
                template.SignInResult,
                template.RejectReasons,
                userContext,
                template.IsNewUserRegistration,
                template.MustAcceptTerms,
                template.NeedsAccountApproval,
                template.NeedsEmailConfirmation,
                template.EmailConfirmationToken,
                template.NeedsPhoneConfirmation,
                template.ExternalLoginInfo
                );

        }

        //public bool IsValidPassowrd(string password)
        //{
        //    return passwordValidator.
        //}
        
        public virtual async Task<UserLoginResult> TryLogin(LoginViewModel model)
        {
            var template = new LoginResultTemplate();
            IUserContext userContext = null;
           
            if(_userManager.Site.UseEmailForLogin)
            {
                template.User = await _userManager.FindByNameAsync(model.Email);
            }
            else
            {
                template.User = await _userManager.FindByNameAsync(model.UserName);
            }
            
            if (template.User != null)
            {
                await _loginRulesProcessor.ProcessAccountLoginRules(template);
            }

            if(template.User != null)
            {
                userContext = new UserContext(template.User);
            }
           
            if(userContext != null 
                && template.SignInResult == SignInResult.Failed 
                &&  template.RejectReasons.Count == 0)
            {
                var persistent = false;
                if (_userManager.Site.AllowPersistentLogin)
                {
                    persistent = model.RememberMe;
                }

                if (_userManager.Site.UseEmailForLogin)
                {
                    template.SignInResult = await _signInManager.PasswordSignInAsync(
                        model.Email,
                        model.Password,
                        persistent,
                        lockoutOnFailure: false);
                }
                else
                {
                    template.SignInResult = await _signInManager.PasswordSignInAsync(
                        model.UserName,
                        model.Password,
                        persistent,
                        lockoutOnFailure: false);
                }

                if(template.SignInResult.Succeeded)
                {
                    //update last login time
                    template.User.LastLoginUtc = DateTime.UtcNow;
                    await _userManager.UpdateAsync(template.User);
                }
            }
            
            return new UserLoginResult(
                template.SignInResult, 
                template.RejectReasons, 
                userContext,
                template.IsNewUserRegistration,
                template.MustAcceptTerms,
                template.NeedsAccountApproval,
                template.NeedsEmailConfirmation,
                template.EmailConfirmationToken,
                template.NeedsPhoneConfirmation
                );
        }

        public virtual async Task<UserLoginResult> Try2FaLogin(LoginWith2faViewModel model, bool rememberMe)
        {
            var template = new LoginResultTemplate();
            IUserContext userContext = null;
            template.User = await _signInManager.GetTwoFactorAuthenticationUserAsync();

            if (template.User != null)
            {
                await _loginRulesProcessor.ProcessAccountLoginRules(template);
            }

            if (template.User != null)
            {
                userContext = new UserContext(template.User);
            }
            
            if(userContext != null
                && template.SignInResult == SignInResult.Failed //initial state
                && template.RejectReasons.Count == 0
                )
            {
                var authenticatorCode = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
                template.SignInResult = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, model.RememberMachine);
            }
            
            if (template.SignInResult.Succeeded)
            {
                //update last login time
                template.User.LastLoginUtc = DateTime.UtcNow;
                await _userManager.UpdateAsync(template.User);
            }

            return new UserLoginResult(
                template.SignInResult,
                template.RejectReasons,
                userContext,
                template.IsNewUserRegistration,
                template.MustAcceptTerms,
                template.NeedsAccountApproval,
                template.NeedsEmailConfirmation,
                template.EmailConfirmationToken,
                template.NeedsPhoneConfirmation
                );

        }

        public virtual async Task<UserLoginResult> TryLoginWithRecoveryCode(LoginWithRecoveryCodeViewModel model)
        {
            var template = new LoginResultTemplate();
            IUserContext userContext = null;
            template.User = await _signInManager.GetTwoFactorAuthenticationUserAsync();

            if (template.User != null)
            {
                await _loginRulesProcessor.ProcessAccountLoginRules(template);
            }

            if (template.User != null)
            {
                userContext = new UserContext(template.User);
            }

            if (userContext != null
                && template.SignInResult == SignInResult.Failed //initial state
                && template.RejectReasons.Count == 0
                )
            {
                var recoveryCode = model.RecoveryCode.Replace(" ", string.Empty);
                template.SignInResult = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
            }

            if(template.SignInResult.Succeeded)
            {
                //update last login time
                template.User.LastLoginUtc = DateTime.UtcNow;
                await _userManager.UpdateAsync(template.User);
            }

            return new UserLoginResult(
                template.SignInResult,
                template.RejectReasons,
                userContext,
                template.IsNewUserRegistration,
                template.MustAcceptTerms,
                template.NeedsAccountApproval,
                template.NeedsEmailConfirmation,
                template.EmailConfirmationToken,
                template.NeedsPhoneConfirmation
                );

        }



        public virtual async Task<UserLoginResult> TryRegister(
            RegisterViewModel model, 
            ModelStateDictionary modelState,
            HttpContext httpContext,
            IHandleCustomRegistration customRegistration
            )
        {
            var template = new LoginResultTemplate();
            IUserContext userContext = null;

            var userName = !string.IsNullOrWhiteSpace(model.Username) ? model.Username : await _userManager.SuggestLoginNameFromEmail(_userManager.Site.Id, model.Email);
            var userNameAvailable = await _userManager.LoginIsAvailable(Guid.Empty, userName);
            if (!userNameAvailable)
            {
                userName = await _userManager.SuggestLoginNameFromEmail(_userManager.Site.Id, model.Email);
            }
            
            var user = new SiteUser
            {
                SiteId = _userManager.Site.Id,
                UserName = userName,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                DisplayName = model.DisplayName,
                LastLoginUtc = DateTime.UtcNow,
                AccountApproved = _userManager.Site.RequireApprovalBeforeLogin ? false : true
            };

            await customRegistration.ProcessUserBeforeCreate(user, httpContext);
            

            if (model.DateOfBirth.HasValue)
            {
                user.DateOfBirth = model.DateOfBirth.Value;
            }

            if (!string.IsNullOrWhiteSpace(_userManager.Site.RegistrationAgreement))
            {
                if (model.AgreeToTerms)
                {
                    user.AgreementAcceptedUtc = DateTime.UtcNow;
                }
            }

            var result = await _userManager.CreateAsync(user, model.Password);
            
            if (result.Succeeded)
            {
                template.User = user;
                template.IsNewUserRegistration = true;
                await _loginRulesProcessor.ProcessAccountLoginRules(template);
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    modelState.AddModelError(string.Empty, error.Description);
                }
            }
           

            if(template.RejectReasons.Count == 0 
                && user != null 
                && template.SignInResult == SignInResult.Failed // failed is initial state, could have been changed to lockedout
                && result.Errors.Count<IdentityError>() == 0
                ) 
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                template.SignInResult = SignInResult.Success;
            }

            if(template.User != null)
            {
                userContext = new UserContext(template.User);
            }

            return new UserLoginResult(
                template.SignInResult,
                template.RejectReasons,
                userContext,
                template.IsNewUserRegistration,
                template.MustAcceptTerms,
                template.NeedsAccountApproval,
                template.NeedsEmailConfirmation,
                template.EmailConfirmationToken,
                template.NeedsPhoneConfirmation
                );
        }

        public async Task<ResetPasswordInfo> GetPasswordResetInfo(string email)
        {
            IUserContext userContext = null;
            string token = null;

            var user = await _userManager.FindByNameAsync(email);
            if(user != null)
            {
                token = await _userManager.GeneratePasswordResetTokenAsync(user);
                userContext = new UserContext(user);
            }

            return new ResetPasswordInfo(userContext, token);
        }

        public async Task<ResetPasswordResult> ResetPassword(string email, string password, string resetCode)
        {
            IUserContext userContext = null;
            IdentityResult result = IdentityResult.Failed(null);

            var user = await _userManager.FindByNameAsync(email);
            if (user != null)
            {
                userContext = new UserContext(user);
                result = await _userManager.ResetPasswordAsync(user, resetCode, password);
            }

            return new ResetPasswordResult(userContext, result);
        }

        public async Task<VerifyEmailInfo> GetEmailVerificationInfo(Guid userId)
        {
            IUserContext userContext = null;
            string token = null;
            var user = await _userManager.Fetch(_userManager.Site.Id, userId);
            if(user != null)
            {
                token = await _userManager.GenerateEmailConfirmationTokenAsync((SiteUser)user);
                userContext = new UserContext(user);
            }

            return new VerifyEmailInfo(userContext, token);
        }

        public virtual async Task<VerifyEmailResult> ConfirmEmailAsync(string userId, string code)
        {
            IUserContext userContext = null;
            IdentityResult result = IdentityResult.Failed(null);

            var user = await _userManager.FindByIdAsync(userId);
            if(user != null)
            {
                userContext = new UserContext(user);
                result = await _userManager.ConfirmEmailAsync(user, code);
            }

            return new VerifyEmailResult(userContext, result);
        }

        public async Task<IUserContext> GetTwoFactorAuthenticationUserAsync()
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if(user != null)
            {
                return new UserContext(user);
            }

            return null;
        }

        public async Task<TwoFactorInfo> GetTwoFactorInfo(string provider = null)
        {
            IUserContext userContext = null;
            IList<string> userFactors = new List<string>();
            string token = null;
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if(user != null)
            {
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    token = await _userManager.GenerateTwoFactorTokenAsync(user, provider);
                }
                userContext = new UserContext(user);
                userFactors = await _userManager.GetValidTwoFactorProvidersAsync(user);
            }

            return new TwoFactorInfo(userContext, userFactors, token);
        }

        public async Task HandleUserRolesChanged(ClaimsPrincipal principal)
        {
            if (principal == null) return;
            var userId = principal.GetUserId();
            if (string.IsNullOrEmpty(userId)) return;
            var user = await _userManager.FindByIdAsync(userId);
            await _signInManager.SignOutAsync();
            if (user != null)
            {
                user.RolesChanged = false;
                var result = await _userManager.UpdateAsync(user);
                if(result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                }
            }
            
        }

        public async Task<bool> AcceptRegistrationAgreement(ClaimsPrincipal principal)
        {
            if (principal == null) return false;
            var userId = principal.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            var user = await _userManager.FindByIdAsync(userId);
            user.AgreementAcceptedUtc = DateTime.UtcNow;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded) return true;

            return false;
        }

        //public async Task<string> GenerateTwoFactorTokenAsync(string provider)
        //{
        //    var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        //    if(user != null)
        //    {
        //        return await userManager.GenerateTwoFactorTokenAsync(user, provider);
        //    }

        //    return null;
        //}

        public async Task<SignInResult> TwoFactorSignInAsync(string provider, string code, bool rememberMe, bool rememberBrowser)
        {
            return await _signInManager.TwoFactorSignInAsync(provider, code, rememberMe, rememberBrowser);
        }

        public async Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string code, bool rememberMe, bool rememberBrowser)
        {
            return await _signInManager.TwoFactorAuthenticatorSignInAsync(code, rememberMe, rememberBrowser);
        }

        public async Task<SignInResult> TwoFactorRecoveryCodeSignInAsync(string code)
        {
            return await _signInManager.TwoFactorRecoveryCodeSignInAsync(code);
        }

        public AuthenticationProperties ConfigureExternalAuthenticationProperties(string provider, string returnUrl = null)
        {
            return _signInManager.ConfigureExternalAuthenticationProperties(provider, returnUrl);
        }

        public async Task<List<Microsoft.AspNetCore.Authentication.AuthenticationScheme>> GetExternalAuthenticationSchemes()
        {
            var result = await _signInManager.GetExternalAuthenticationSchemesAsync();
            var allProviders = result.OrderBy(x => x.DisplayName).ToList();
            var filteredProviders = new List<Microsoft.AspNetCore.Authentication.AuthenticationScheme>();
            foreach(var provider in allProviders)
            {
                if(IsSocialAuthConfigured(provider))
                {
                    filteredProviders.Add(provider);
                }
            }

            return filteredProviders;
        }

        private bool IsSocialAuthConfigured(Microsoft.AspNetCore.Authentication.AuthenticationScheme scheme)
        {
            switch(scheme.Name)
            {
                case "Microsoft":
                    if(!string.IsNullOrWhiteSpace(_userManager.Site.MicrosoftClientId)) { return true; }
                    break;

                case "Google":
                    if (!string.IsNullOrWhiteSpace(_userManager.Site.GoogleClientId)) { return true; }
                    break;
                case "Facebook":
                    if (!string.IsNullOrWhiteSpace(_userManager.Site.FacebookAppId)) { return true; }
                    break;
                case "Twitter":
                    if (!string.IsNullOrWhiteSpace(_userManager.Site.TwitterConsumerKey)) { return true; }
                    break;
                case "OpenIdConnect":
                    if (!string.IsNullOrWhiteSpace(_userManager.Site.OidConnectAppId)) { return true; }
                    break;
            }

            return false;
        }

        public bool IsSignedIn(ClaimsPrincipal user)
        {
            return _signInManager.IsSignedIn(user);
        }

        public async Task SignOutAsync()
        {
            await _signInManager.SignOutAsync();
        }

        public async Task<bool> LoginNameIsAvailable(Guid userId, string loginName)
        {
            return await _userManager.LoginIsAvailable(userId, loginName);
        }

    }
}
