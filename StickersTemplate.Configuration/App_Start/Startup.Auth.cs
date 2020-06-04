﻿//----------------------------------------------------------------------------------------------
// <copyright file="Startup.Auth.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace StickersTemplate.Configuration
{
    using System;
    using System.Configuration;
    using System.IdentityModel.Claims;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web.Helpers;
    using Microsoft.IdentityModel.Protocols.OpenIdConnect;
    using Microsoft.Owin.Security;
    using Microsoft.Owin.Security.Cookies;
    using Microsoft.Owin.Security.OpenIdConnect;
    using Owin;

    /// <summary>
    /// Startup
    /// </summary>
    public partial class Startup
    {
        private static readonly string ClientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static readonly string AadInstance = EnsureTrailingSlash(ConfigurationManager.AppSettings["ida:AADInstance"]);
        private static readonly string TenantId = ConfigurationManager.AppSettings["ida:TenantId"];
        private static readonly string PostLogoutRedirectUri = ConfigurationManager.AppSettings["ida:PostLogoutRedirectUri"];
        private static readonly string Authority = AadInstance + TenantId;

        /// <summary>
        /// Configure Auth
        /// </summary>
        /// <param name="app">App</param>
        public void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            var validUpns = ConfigurationManager.AppSettings["ValidUpns"]
                ?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                ?.Select(s => s.Trim())
                ?? new string[0];
            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    ClientId = ClientId,
                    Authority = Authority,
                    PostLogoutRedirectUri = PostLogoutRedirectUri,
                    Notifications = new OpenIdConnectAuthenticationNotifications()
                    {
                        SecurityTokenValidated = (context) =>
                        {
                            var upnClaim = context?.AuthenticationTicket?.Identity?.Claims?
                                .FirstOrDefault(c => c.Type == ClaimTypes.Upn);
                            var upnOrEmail = upnClaim?.Value;

                            // Externally-authenticated users don't have the UPN claim by default. Instead they have email.
                            if (upnOrEmail == null)
                            {
                                var emailClaim = context?.AuthenticationTicket?.Identity?.Claims?
                                    .FirstOrDefault(c => c.Type == ClaimTypes.Email);
                                upnOrEmail = emailClaim?.Value;
                            }

                            if (string.IsNullOrWhiteSpace(upnOrEmail)
                                || !validUpns.Contains(upnOrEmail, StringComparer.OrdinalIgnoreCase))
                            {
                                context.OwinContext.Response.Redirect("/Account/InvalidUser?upn=" + Uri.EscapeDataString(upnOrEmail));
                                context.HandleResponse(); // Suppress further processing
                            }

                            return Task.CompletedTask;
                        },
                        RedirectToIdentityProvider = (context) =>
                        {
                            if (context.ProtocolMessage.RequestType == OpenIdConnectRequestType.Authentication)
                            {
                                context.ProtocolMessage.Prompt = OpenIdConnectPrompt.Login;
                            }
                            return Task.CompletedTask;
                        }
                    }
                });

            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;
        }

        private static string EnsureTrailingSlash(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            if (!value.EndsWith("/", StringComparison.Ordinal))
            {
                return value + "/";
            }

            return value;
        }
    }
}