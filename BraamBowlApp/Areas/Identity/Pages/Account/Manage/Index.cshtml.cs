// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using BraamBowlApp.Data;
using BraamBowlApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace BraamBowlApp.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _Db;
        private readonly IConfiguration _config;

        public IndexModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager, ApplicationDbContext DB,IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _Db= DB;
            _config= config;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

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
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
            [Required]
            [Display(Name = "SAP Number")]
            public string Employee_ID { get; set; }

            [Required]
            [Display(Name = "Street Address")]
            public string Street_Address { get; set; }

            [Display(Name = "City")]
            public string City { get; set; }

            [Display(Name = "Postal Code")]
            public string Postal_Code { get; set; }

            [Display(Name = "Region")]
            public string Region { get; set; }

        

        }

        private async Task LoadAsync(IdentityUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            //var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            var userId = await _userManager.GetUserIdAsync(user);

            Username = userName;
            Input = new InputModel();

            var connectionString = _config.GetConnectionString("DefaultConnection");
            using (var connection = new SqlConnection(connectionString))
            {

                await connection.OpenAsync();
                using (var command = new SqlCommand(
                    "SELECT PhoneNumber, Employee_ID, Street_Address, City, Postal_Code, Region FROM AspNetUsers WHERE Id = @UserId",
                    connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            Input.PhoneNumber = reader["PhoneNumber"] as string;
                            Input.Employee_ID = reader["Employee_ID"] as string;
                            Input.Street_Address = reader["Street_Address"] as string;
                            Input.City = reader["City"] as string;
                            Input.Postal_Code = reader["Postal_Code"] as string;
                            Input.Region = reader["Region"] as string;
                        }
                    }
                }
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var connectionString = _config.GetConnectionString("DefaultConnection");
            using (var connection = new SqlConnection(connectionString)) { 
            await connection.OpenAsync();
                using (var command = new SqlCommand("UPDATE AspNetUsers SET PhoneNumber = @PhoneNumber, Employee_ID = @Employee_ID, Street_Address = @Street_Address, City = @City, Postal_Code = @Postal_Code, Region = @Region WHERE Id = @UserId",
                    connection)) {
                    command.Parameters.AddWithValue("@UserId", user.Id);
                    command.Parameters.AddWithValue("@PhoneNumber", Input.PhoneNumber ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Employee_ID", Input.Employee_ID ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Street_Address", Input.Street_Address ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@City", Input.City ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Postal_Code", Input.Postal_Code ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Region", Input.Region ?? (object)DBNull.Value);
                    await command.ExecuteNonQueryAsync();
                }
            
            }

                var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }
    }
}
