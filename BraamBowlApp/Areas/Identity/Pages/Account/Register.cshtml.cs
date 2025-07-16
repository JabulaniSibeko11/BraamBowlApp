// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using BraamBowlApp.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace BraamBowlApp.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        private readonly ApplicationDbContext _DB;
        private readonly IConfiguration _config;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender, ApplicationDbContext DB, IConfiguration config)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _DB = DB;
            _config = config;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            public string Surname { get; set; }
            public string First_Name { get; set; }
            public string? Mobile_Number { get; set; }
            public string Employee_ID { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }


        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync1(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                    var connectionString = _config.GetConnectionString("DefaultConnection");
                    using (var connection = new SqlConnection(connectionString)) {

                
                        await connection.OpenAsync();
                        using (var command = new SqlCommand(
                            "UPDATE AspNetUsers SET EmailConfirmed = 1, First_Name = @FirstName, Surname = @Surname, Mobile_Number = @PhoneNumber WHERE Id = @UserId",
                            connection))
                        {
                            command.Parameters.AddWithValue("@UserId", userId);
                            command.Parameters.AddWithValue("@FirstName", Input.First_Name ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@Surname", Input.Surname ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@PhoneNumber", Input.Mobile_Number ?? (object)DBNull.Value);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    // Send welcome email
                    var callbackUrl = Url.Page(
                        "/Account/Login",
                        pageHandler: null,
                        values: new { area = "Identity", returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(
                  Input.Email,
                  "Welcome to BraamBowl!",
                  $"Thank you for registering with BraamBowl, {Input.First_Name},{Input.Surname}! Your account is now active. " +
                  $"You can access the website by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    // Sign in the user immediately
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);


                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);

                    var connectionString = _config.GetConnectionString("DefaultConnection");
                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (var command = new SqlCommand(
                            "UPDATE AspNetUsers SET EmailConfirmed = 1,Employee_ID=@EmpID, First_Name = @FirstName, Surname = @Surname, Mobile_Number = @PhoneNumber WHERE Id = @UserId",
                            connection))
                        {
                            command.Parameters.AddWithValue("@UserId", userId);
                            command.Parameters.AddWithValue("@EmpID",Input.Employee_ID );
                            command.Parameters.AddWithValue("@FirstName", Input.First_Name ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@Surname", Input.Surname ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@PhoneNumber", Input.Mobile_Number ?? (object)DBNull.Value);
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    // Send welcome email
                    var callbackUrl = Url.Page(
                        "/Account/Login",
                        pageHandler: null,
                        values: new { area = "Identity", returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await SendWelcomeEmail(Input.Email, Input.First_Name, Input.Surname, callbackUrl);

                    // Sign in the user immediately
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private async Task SendWelcomeEmail(string email, string firstName, string surname, string callbackUrl)
        {
            var fromAddress = new MailAddress("noreply@braambowl.com", "BraamBowl Team");
            var toAddress = new MailAddress(email, $"{firstName} {surname}");
            const string subject = "Welcome to BraamBowl! 🎉";

            string body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Welcome to BraamBowl</title>
    <style>
        * {{
            font-family: 'Poppins', sans-serif;
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            background: #f1f5f9;
            padding: 20px;
        }}

        .glass-card {{
            background: rgba(255, 255, 255, 0.9);
            backdrop-filter: blur(12px);
            border: 1px solid rgba(255, 255, 255, 0.3);
            box-shadow: 0 10px 20px rgba(0, 0, 0, 0.1);
            border-radius: 16px;
            max-width: 600px;
            margin: 0 auto;
            padding: 2rem;
        }}

        .section-title {{
            color: #2C3E50; /* --dark-gray */
            font-weight: 700;
            margin-bottom: 1.5rem;
            position: relative;
            padding-left: 1rem;
            font-size: 1.5rem;
        }}

        .section-title::before {{
            content: '';
            position: absolute;
            left: 0;
            top: 50%;
            transform: translateY(-50%);
            width: 4px;
            height: 24px;
            background: linear-gradient(135deg, #2ECC71 0%, #27AE60 100%); /* --primary-green gradient */
            border-radius: 2px;
        }}

        .action-btn {{
            display: inline-block;
            background: #2ECC71; /* --primary-green */
            color: #FFFFFF; /* --neutral-white */
            padding: 12px 24px;
            border-radius: 8px;
            text-decoration: none;
            font-weight: 500;
            transition: background-color 0.3s ease;
        }}

        .action-btn:hover {{
            background: #27AE60; /* Darker green on hover */
        }}

        p {{
            color: #374151;
            line-height: 1.6;
            margin-bottom: 1rem;
            font-size: 16px;
        }}

        .footer {{
            text-align: center;
            color: #6b7280;
            font-size: 0.9rem;
            margin-top: 2rem;
            padding-top: 1rem;
            border-top: 1px solid #e2e8f0;
        }}

        .emoji {{
            font-size: 1.2rem;
            margin-right: 0.5rem;
        }}
    </style>
</head>
<body>
    <div class=""glass-card"">
        <div style=""width: 48px; height: 48px; display: inline-flex; align-items: center; justify-content: center; background: linear-gradient(135deg, #f3f4f6 0%, #e5e7eb 100%); border-radius: 12px; margin-bottom: 12px; font-size: 24px;"">🥗</div>
        <h1 class=""section-title"">Welcome to BraamBowl <span class=""emoji"">🎉</span></h1>
        <p>Dear {firstName} {surname} <span class=""emoji"">👋</span>,</p>
        <p>Thank you for joining <strong>BraamBowl</strong>! We're thrilled to have you on board to explore our healthy and delicious meals <span class=""emoji"">🌱</span>.</p>
        <p>Your account is now active. Start ordering your favorite bowls now!</p>
        <div style=""text-align: center; margin: 1.5rem 0;"">
            <a href=""{HtmlEncoder.Default.Encode(callbackUrl)}"" class=""action-btn""><span class=""emoji"">✅</span> Log In to Your Account</a>
        </div>
        <p>If you have any questions, feel free to reach out to our support team at <a href=""mailto:support@braambowl.com"" style=""color: #F39C12;"">support@braambowl.com</a> <span class=""emoji"">📧</span>.</p>
        <div class=""footer"">
            <p>Best regards,<br>BraamBowl Team <span class=""emoji"">🌟</span></p>
            <p>© 2025 BraamBowl. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

            using (var smtp = new SmtpClient
            {
                Host = _config.GetValue<string>("Smtp:Host"),
                Port = _config.GetValue<int>("Smtp:Port"),
                EnableSsl = _config.GetValue<bool>("Smtp:EnableSsl"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _config.GetValue<string>("Smtp:Username"),
                    _config.GetValue<string>("Smtp:Password")
                )
            })
            {
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    await smtp.SendMailAsync(message);
                }
            }
        }
        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}
