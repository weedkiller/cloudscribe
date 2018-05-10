﻿using cloudscribe.Core.Models;
using System.Threading.Tasks;

namespace cloudscribe.Core.Web.Components.Messaging
{
    /// <summary>
    /// this is just temporary while we wait for MailKit/MimeKit to be released for rc2
    /// </summary>
    public class FakeSiteEmailSender : ISiteMessageEmailSender
    {

        public Task SendAccountConfirmationEmailAsync(
            ISiteContext siteSettings,
            string toAddress,
            string subject,
            string confirmationUrl,
            string confirmCode)
        {
            return Task.CompletedTask;
        }

        public Task SendSecurityCodeEmailAsync(
            ISiteContext siteSettings,
            string toAddress,
            string subject,
            string securityCode)
        {
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(
            ISiteContext siteSettings,
            string toAddress,
            string subject,
            string resetUrl)
        {
            return Task.CompletedTask;
        }

        public Task AccountPendingApprovalAdminNotification(
            ISiteContext siteSettings,
            IUserContext user)
        {
            return Task.CompletedTask;
        }

        public Task SendAccountApprovalNotificationAsync(
            ISiteContext siteSettings,
            string toAddress,
            string subject,
            string loginUrl)
        {
            return Task.CompletedTask;
        }

        public Task SendSiteMessage(
            ISiteContext siteSettings,
            SiteMessageModel model,
            string baseUrl)
        {
            return Task.CompletedTask;
        }

    }
}
