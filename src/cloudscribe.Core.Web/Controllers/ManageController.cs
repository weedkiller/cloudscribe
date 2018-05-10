﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2014-10-26
// Last Modified:			2018-04-05
// 

using cloudscribe.Core.Identity;
using cloudscribe.Core.Models;
using cloudscribe.Core.Web.ExtensionPoints;
using cloudscribe.Core.Web.ViewModels.SiteUser;
using cloudscribe.Web.Common;
using cloudscribe.Web.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace cloudscribe.Core.Web.Controllers.Mvc
{
    [Authorize]
    public class ManageController : Controller
    {
        public ManageController(
            SiteContext currentSite,
            SiteUserManager<SiteUser> userManager,
            SignInManager<SiteUser> signInManager,
            //ISmsSender smsSender,
            IStringLocalizer<CloudscribeCore> localizer,
            ITimeZoneIdResolver timeZoneIdResolver,
            ITimeZoneHelper timeZoneHelper,
            IHandleCustomUserInfo customUserInfo,
            ILogger<ManageController> logger,
            UrlEncoder urlEncoder
            )
        {
            CurrentSite = currentSite; 
            UserManager = userManager;
            SignInManager = signInManager;
            StringLocalizer = localizer;
            TimeZoneIdResolver = timeZoneIdResolver;
            TimeZoneHelper = timeZoneHelper;
            CustomUserInfo = customUserInfo;
            Log = logger;
            UrlEncoder = urlEncoder;
        }

        protected ILogger Log { get; private set; }
        protected ISiteContext CurrentSite { get; private set; }
        protected SiteUserManager<SiteUser> UserManager { get; private set; }
        protected SignInManager<SiteUser> SignInManager { get; private set; }
        protected IStringLocalizer StringLocalizer { get; private set; }
        protected ITimeZoneIdResolver TimeZoneIdResolver { get; private set; }
        protected ITimeZoneHelper TimeZoneHelper { get; private set; }
        protected IHandleCustomUserInfo CustomUserInfo { get; private set; }
        protected const string AuthenicatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
        protected UrlEncoder UrlEncoder { get; private set; }

        [TempData]
        public string StatusMessage { get; set; }


        // GET: /Manage/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            var model = new AccountIndexViewModel
            {
                HasPassword = (!string.IsNullOrWhiteSpace(user.PasswordHash)),
                PhoneNumber = !string.IsNullOrWhiteSpace(user.PhoneNumber) ? user.PhoneNumber : null,
                TwoFactor = user.TwoFactorEnabled,
                Logins = await UserManager.GetLoginsAsync(user),
                BrowserRemembered = await SignInManager.IsTwoFactorClientRememberedAsync(user),
                TimeZone = user.TimeZoneId
                
            };

            if(string.IsNullOrEmpty(model.TimeZone))
            {
                model.TimeZone = await TimeZoneIdResolver.GetSiteTimeZoneId();
            }
            
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TimeZone()
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());

            var model = new TimeZoneViewModel
            {
                TimeZoneId = user.TimeZoneId
            };
            if (string.IsNullOrEmpty(model.TimeZoneId))
            {
                model.TimeZoneId = await TimeZoneIdResolver.GetSiteTimeZoneId();
            }
            model.AllTimeZones = TimeZoneHelper.GetTimeZoneList().Select(x =>
                           new SelectListItem
                           {
                               Text = x,
                               Value = x,
                               Selected = model.TimeZoneId == x
                           });

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TimeZone(string timeZoneId)
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());

            if (user != null)
            {
                user.TimeZoneId = timeZoneId;
                await UserManager.UpdateAsync(user);
                this.AlertSuccess(StringLocalizer["Your time zone has been updated."]);
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> UserInfo()
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            var model = new UserInfoViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                DateOfBirth = user.DateOfBirth,
                WebSiteUrl = user.WebSiteUrl,
                PhoneNumber = user.PhoneNumber
            };

            var viewName = await CustomUserInfo.GetUserInfoViewName(CurrentSite, user, HttpContext);
            await CustomUserInfo.HandleUserInfoGet(
                CurrentSite,
                user,
                model,
                HttpContext,
                ViewData);

            return View(viewName, model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserInfo(UserInfoViewModel model)
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            var viewName = await CustomUserInfo.GetUserInfoViewName(CurrentSite, user, HttpContext);

            bool isValid = ModelState.IsValid;
            bool customDataIsValid = await CustomUserInfo.HandleUserInfoValidation(
                CurrentSite,
                user,
                model,
                HttpContext,
                ViewData,
                ModelState);

            if (!isValid || !customDataIsValid)
            {
                return View(viewName, model);
            }

            if (user != null)
            {
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.PhoneNumber = model.PhoneNumber;
                if(model.DateOfBirth.HasValue)
                {
                    user.DateOfBirth = model.DateOfBirth;
                }
                
                
                user.WebSiteUrl = model.WebSiteUrl;

                await CustomUserInfo.HandleUserInfoPostSuccess(
                        CurrentSite,
                        user,
                        model,
                        HttpContext
                        );


                await UserManager.UpdateAsync(user);

                

                this.AlertSuccess(StringLocalizer["Your information has been updated."]);
            }
            return RedirectToAction("Index");
        }

        //
        // GET: /Manage/RemoveLogin
        [HttpGet]
        public async Task<IActionResult> RemoveLogin()
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            var linkedAccounts = await UserManager.GetLoginsAsync(user);
            ViewData["ShowRemoveButton"] = await UserManager.HasPasswordAsync(user) || linkedAccounts.Count > 1;
            return View(linkedAccounts);
        }


        // POST: /Manage/RemoveLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());

            if (user != null)
            {
                var result = await UserManager.RemoveLoginAsync(user, loginProvider, providerKey);
                if (result.Succeeded)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false);
                    this.AlertSuccess(StringLocalizer["The external login was removed."]);
                }
                else
                {
                    this.AlertDanger(StringLocalizer["oops something went wrong, the external login was not removed, please try again."]);

                }
            }
            return RedirectToAction("ManageLogins");

        }


        // GET: /Manage/AddPhoneNumber
        //[HttpGet]
        //public IActionResult AddPhoneNumber()
        //{
        //    return View();
        //}


        //// POST: /Manage/AddPhoneNumber
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> AddPhoneNumber(AddPhoneNumberViewModel model)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return View(model);
        //    }
        //    // Generate the token and send it
        //    var user = await userManager.FindByIdAsync(HttpContext.User.GetUserId());
        //    var code = await userManager.GenerateChangePhoneNumberTokenAsync(user, model.Number);
        //    await smsSender.SendSmsAsync(
        //        Site, 
        //        model.Number,
        //        string.Format(sr["Your security code is: {0}"], code)
        //        );
        //    return RedirectToAction("VerifyPhoneNumber", new { PhoneNumber = model.Number });

        //}

        [HttpGet]
        public async Task<IActionResult> TwoFactorAuthentication()
        {
            ViewData["Title"] = StringLocalizer["Two-factor authentication"];

            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{UserManager.GetUserId(User)}'.");
            }

            var model = new TwoFactorAuthenticationViewModel
            {
                HasAuthenticator = await UserManager.GetAuthenticatorKeyAsync(user) != null,
                Is2faEnabled = user.TwoFactorEnabled,
                RecoveryCodesLeft = await UserManager.CountRecoveryCodesAsync(user),
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Disable2faWarning()
        {
            ViewData["Title"] = StringLocalizer["Disable two-factor authentication (2FA)"];

            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{User.GetUserId()}'.");
            }

            if (!user.TwoFactorEnabled)
            {
                throw new ApplicationException($"Unexpected error occured disabling 2FA for user with ID '{user.Id}'.");
            }

            return View(nameof(Disable2fa));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable2fa()
        {
            ViewData["Title"] = StringLocalizer["Disable two-factor authentication (2FA)"];

            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{User.GetUserId()}'.");
            }

            var disable2faResult = await UserManager.SetTwoFactorEnabledAsync(user, false);
            if (!disable2faResult.Succeeded)
            {
                throw new ApplicationException($"Unexpected error occured disabling 2FA for user with ID '{user.Id}'.");
            }
            
            Log.LogInformation("User with ID {UserId} has disabled 2fa.", user.Id);
            return RedirectToAction(nameof(TwoFactorAuthentication));
        }

        [HttpGet]
        public async Task<IActionResult> EnableAuthenticator()
        {
            ViewData["Title"] = StringLocalizer["Enable authenticator"];

            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{User.GetUserId()}'.");
            }

            var unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await UserManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
            }

            var model = new EnableAuthenticatorViewModel
            {
                SharedKey = FormatKey(unformattedKey),
                AuthenticatorUri = GenerateQrCodeUri(user.Email, unformattedKey)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
        {
            ViewData["Title"] = StringLocalizer["Enable authenticator"];

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{User.GetUserId()}'.");
            }

            // Strip spaces and hypens
            var verificationCode = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

            var is2faTokenValid = await UserManager.VerifyTwoFactorTokenAsync(
                user, UserManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

            if (!is2faTokenValid)
            {
                ModelState.AddModelError("model.Code", "Verification code is invalid.");
                return View(model);
            }

            await UserManager.SetTwoFactorEnabledAsync(user, true);
            Log.LogInformation("User with ID {UserId} has enabled 2FA with an authenticator app.", user.Id);
            return RedirectToAction(nameof(GenerateRecoveryCodes));
        }

        [HttpGet]
        public IActionResult ResetAuthenticatorWarning()
        {
            ViewData["Title"] = StringLocalizer["Reset authenticator key"];

            return View(nameof(ResetAuthenticator));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAuthenticator()
        {
            ViewData["Title"] = StringLocalizer["Reset authenticator key"];

            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{User.GetUserId()}'.");
            }

            await UserManager.SetTwoFactorEnabledAsync(user, false);
            await UserManager.ResetAuthenticatorKeyAsync(user);
            Log.LogInformation("User with id '{UserId}' has reset their authentication app key.", user.Id);

            return RedirectToAction(nameof(EnableAuthenticator));
        }

        [HttpGet]
        public async Task<IActionResult> GenerateRecoveryCodes()
        {
            ViewData["Title"] = StringLocalizer["Recovery codes"];

            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{User.GetUserId()}'.");
            }

            if (!user.TwoFactorEnabled)
            {
                throw new ApplicationException($"Cannot generate recovery codes for user with ID '{user.Id}' as they do not have 2FA enabled.");
            }

            var recoveryCodes = await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            var model = new GenerateRecoveryCodesViewModel { RecoveryCodes = recoveryCodes.ToArray() };

            Log.LogInformation("User with ID {UserId} has generated new 2FA recovery codes.", user.Id);

            return View(model);
        }


        // POST: /Manage/EnableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTwoFactorAuthentication()
        {
            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user != null)
            {
                await UserManager.SetTwoFactorEnabledAsync(user, true);
                await SignInManager.SignInAsync(user, isPersistent: false);
            }
            return RedirectToAction("Index", "Manage");
        }


        // POST: /Manage/DisableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactorAuthentication()
        {
            var user = await UserManager.FindByIdAsync(User.GetUserId());
            if (user != null)
            {
                await UserManager.SetTwoFactorEnabledAsync(user, false);
                await SignInManager.SignInAsync(user, isPersistent: false);
            }
            return RedirectToAction("Index", "Manage");
        }


        // GET: /Manage/VerifyPhoneNumber
        //[HttpGet]
        //public async Task<IActionResult> VerifyPhoneNumber(string phoneNumber)
        //{
        //    var user = await userManager.FindByIdAsync(HttpContext.User.GetUserId());
        //    var code = await userManager.GenerateChangePhoneNumberTokenAsync(user, phoneNumber);
        //    // Send an SMS to verify the phone number
        //    return phoneNumber == null ? View("Error") : View(new VerifyPhoneNumberViewModel { PhoneNumber = phoneNumber });
        //}


        //// POST: /Manage/VerifyPhoneNumber
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> VerifyPhoneNumber(VerifyPhoneNumberViewModel model)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return View(model);
        //    }
        //    var user = await userManager.FindByIdAsync(HttpContext.User.GetUserId());
        //    if (user != null)
        //    {
        //        var result = await userManager.ChangePhoneNumberAsync(user, model.PhoneNumber, model.Code);
        //        if (result.Succeeded)
        //        {
        //            await signInManager.SignInAsync(user, isPersistent: false);

        //            this.AlertSuccess(sr["Your phone number was added."]);

        //            return RedirectToAction("Index");
        //        }
        //    }
        //    // If we got this far, something failed, redisplay the form
        //    ModelState.AddModelError(string.Empty, sr["Failed to verify phone number"]);
        //    return View(model);

        //}

        //
        // GET: /Manage/RemovePhoneNumber
        [HttpGet]
        public async Task<IActionResult> RemovePhoneNumber()
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            if (user != null)
            {
                var result = await UserManager.SetPhoneNumberAsync(user, null);
                if (result.Succeeded)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false);
                    this.AlertSuccess(StringLocalizer["Your phone number was removed."]);
                }
                else
                {
                    this.AlertDanger(StringLocalizer["oops something went wrong please try again"]);
                }
            }
            return RedirectToAction("Index");
        }


        // GET: /Manage/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }


        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            if (user != null)
            {
                var result = await UserManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
                if (result.Succeeded)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false);

                    this.AlertSuccess(StringLocalizer["Your password has been changed."]);
                    return RedirectToAction("Index");
                }
                else
                {
                    this.AlertDanger(StringLocalizer["oops something went wrong please try again"]);
                }
                AddErrors(result);
            }
           
            return View(model);

        }


        // GET: /Manage/SetPassword
        [HttpGet]
        public IActionResult SetPassword()
        {
            return View();
        }


        // POST: /Manage/SetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            if (user != null)
            {
                var result = await UserManager.AddPasswordAsync(user, model.NewPassword);
                if (result.Succeeded)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false);
                    this.AlertSuccess(StringLocalizer["Your password has been set."]);

                    return RedirectToAction("Index");
                }
                else
                {
                    this.AlertDanger(StringLocalizer["oops something went wrong please try again"]);
                }

                AddErrors(result);
                
            }

            return View(model);     
        }


        // GET: /Manage/ManageLogins
        [HttpGet]
        public async Task<IActionResult> ManageLogins()
        {
            if(!CurrentSite.HasAnySocialAuthEnabled())
            {
                return RedirectToAction("Index");
            }

            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            if (user == null)
            {
                return View("Error");
            }
            var userLogins = await UserManager.GetLoginsAsync(user);
            var externalSchemes = await SignInManager.GetExternalAuthenticationSchemesAsync();
            var otherLogins = externalSchemes.Where(auth => userLogins.All(ul => auth.Name != ul.LoginProvider)).ToList();
            
            var model = new ManageLoginsViewModel
            {
                CurrentLogins = userLogins,
                OtherLogins = otherLogins

            };
            model.ShowRemoveButton = await UserManager.HasPasswordAsync(user) || model.CurrentLogins.Count > 1;

            return View(model);  
        }


        // POST: /Manage/LinkLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LinkLogin(string provider)
        {
            // Request a redirect to the external login provider to link a login for the current user
            var redirectUrl = Url.Action("LinkLoginCallback", "Manage");
            var properties = SignInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, User.GetUserId());
            return new ChallengeResult(provider, properties);
        }


        // GET: /Manage/LinkLoginCallback
        [HttpGet]
        public async Task<IActionResult> LinkLoginCallback()
        {
            var user = await UserManager.FindByIdAsync(HttpContext.User.GetUserId());
            if (user == null)
            {
                return View("Error");
            }
            var info = await SignInManager.GetExternalLoginInfoAsync(User.GetUserId());
            if (info == null)
            {
                this.AlertDanger(StringLocalizer["oops something went wrong please try again"]);
                return RedirectToAction("ManageLogins");
            }
            var result = await UserManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                this.AlertDanger(StringLocalizer["oops something went wrong, please try again"]);
            }

            return RedirectToAction("ManageLogins");

        }

        #region Helpers

        private string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition, 4)).Append(" ");
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            return string.Format(
                AuthenicatorUriFormat,
                UrlEncoder.Encode(UserManager.Site.SiteName),
                UrlEncoder.Encode(email),
                unformattedKey);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        

        #endregion
    }
}