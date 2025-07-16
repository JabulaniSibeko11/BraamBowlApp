using BraamBowlApp.Data;
using BraamBowlApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using System.Data;
using System.Diagnostics;
using System.Net.Mail;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Hangfire;

namespace BraamBowlApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _DB;
        private readonly IBackgroundJobClient _bck;


        public HomeController(ILogger<HomeController> logger, IConfiguration config, ApplicationDbContext DB, IBackgroundJobClient backgroundJobClient)
        {
            _logger = logger;
            _config = config;
            _DB = DB;
            _bck = backgroundJobClient;
           
        }


        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var model = new IndexViewModel();

            // Check if user is authenticated (handles logout scenario)
            if (string.IsNullOrEmpty(userId) || !User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "User not authenticated. Please log in.";
                return Redirect("~/Identity/Account/Login");
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Call stored procedure to fetch user balance
                    using (var command = new SqlCommand("sp_GetUserBalance", connection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;

                        // Input parameter
                        command.Parameters.AddWithValue("@UserId", userId);

                        // Output parameter
                        var balanceParam = new SqlParameter("@Balance", System.Data.SqlDbType.Decimal)
                        {
                            Direction = System.Data.ParameterDirection.Output,
                            Precision = 18,
                            Scale = 2
                        };
                        command.Parameters.Add(balanceParam);

                        // Execute and get return value
                        await command.ExecuteNonQueryAsync();

                        // Check return value
                        
                        // Get balance from output parameter
                        model.Balance = balanceParam.Value == DBNull.Value ? 0 : (decimal)balanceParam.Value;
                    }
                }

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return View(model);
            }
        }
        public IActionResult Index1()
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "User not authenticated.";
                return Redirect("~/Identity/Account/Login");
            }

            var userName = User.Identity.Name;
            if (string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "User name not found.";
                return Redirect("~/Identity/Account/Login");
            }

            List<User> users = new();

            try
            {
                var connectionString = _config.GetConnectionString("DefaultConnection");

                using (var con = new SqlConnection(connectionString))
                using (var com = new SqlCommand())
                {
                    com.Connection = con;
                    com.CommandText = @"
                SELECT TOP 1 [Id], [UserName], [Email], [PhoneNumber], [City], [Country],
                       [Delivery_Address], [Employee_ID], [First_Name], [Mobile_Number],
                       [Postal_Code], [Region], [Street_address], [Surname], [Balance],
                       [Last_Deposit_Month], [Monthly_Deposit_Total], [HasSeenWelcomeModal]
                FROM [BraamBowlOrderDatabase].[dbo].[AspNetUsers]
                WHERE [UserName] = @UserName";

                    com.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = userName;

                    con.Open();
                    using (var dr = com.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            var user = new User
                            {
                                Id = dr["Id"].ToString(),
                                UserName = dr["UserName"].ToString(),
                                Email = dr["Email"].ToString(),
                                PhoneNumber = dr["PhoneNumber"]?.ToString(),
                                City = dr["City"]?.ToString(),
                                Country = dr["Country"]?.ToString(),
                                Delivery_Address = dr["Delivery_Address"]?.ToString(),
                              
                                First_Name = dr["First_Name"]?.ToString(),
                                Mobile_Number = dr["Mobile_Number"]?.ToString(),
                                Postal_Code = dr["Postal_Code"]?.ToString(),
                                Region = dr["Region"]?.ToString(),
                                Street_address = dr["Street_address"]?.ToString(),
                                Surname = dr["Surname"]?.ToString(),
                                Balance = dr["Balance"] != DBNull.Value ? Convert.ToDecimal(dr["Balance"]) : 0m,
                                Last_Deposit_Month = dr["Last_Deposit_Month"] != DBNull.Value ? Convert.ToDateTime(dr["Last_Deposit_Month"]) : null,
                                Monthly_Deposit_Total = dr["Monthly_Deposit_Total"] != DBNull.Value ? Convert.ToDecimal(dr["Monthly_Deposit_Total"]) : 0m,
                                HasSeenWelcomeModal = dr["HasSeenWelcomeModal"] != DBNull.Value && Convert.ToBoolean(dr["HasSeenWelcomeModal"])
                            };

                            users.Add(user);
                        }
                    }
                }

                if (users.Count == 0)
                {
                    TempData["ErrorMessage"] = "User not found in database.";
                    return PartialView("_Error403");
                }

                var currentUser = users.First();
                TempData["currentUserEmail"] = currentUser.Email;
                TempData["currentUserSurname"] = currentUser.Surname;
                TempData["currentUserFirstname"] = currentUser.First_Name;
                TempData["currentUserPhoneNo"] = currentUser.Mobile_Number ?? currentUser.PhoneNumber;
                TempData["currentUserEmployeeID"] = currentUser.Employee_ID;
                TempData["currentUserBalance"] = currentUser.Balance.ToString("C");
                TempData["currentUserCity"] = currentUser.City;
                TempData["currentUserCountry"] = currentUser.Country;
                TempData["currentUserDeliveryAddress"] = currentUser.Delivery_Address;
                TempData["currentUserPostalCode"] = currentUser.Postal_Code;
                TempData["currentUserRegion"] = currentUser.Region;
                TempData["currentUserStreetAddress"] = currentUser.Street_address;
                TempData["hasSeenWelcomeModal"] = currentUser.HasSeenWelcomeModal.ToString();

                return View(currentUser); // Pass the user object to the view
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Internal server error: " + ex.Message;
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }




        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Check if user is authenticated
            if (string.IsNullOrEmpty(userId) || !User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "User not authenticated. Please log in.";
                return RedirectToAction("Login", "Account");
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            var model = new DashboardViewModel
            {
                Restuarant = new List<Restuarants>(),
                OrderHistory = new List<OrderView>(),
                CurrentOrders = new List<CurrentOrder>(),
                depositModels = new List<Deposit> { new Deposit() }
            };

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Fetch user data
                    using (var userCommand = new SqlCommand("sp_GetUserDashboardData", connection))
                    {
                        userCommand.CommandType = System.Data.CommandType.StoredProcedure;
                        userCommand.Parameters.AddWithValue("@UserId", userId);

                        // Output parameters
                        var employeeIdParam = new SqlParameter("@EmployeeId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                        var balanceParam = new SqlParameter("@Balance", System.Data.SqlDbType.Decimal) { Direction = System.Data.ParameterDirection.Output, Precision = 18, Scale = 2 };
                        var monthlyDepositParam = new SqlParameter("@MonthlyDepositTotal", System.Data.SqlDbType.Decimal) { Direction = System.Data.ParameterDirection.Output, Precision = 18, Scale = 2 };
                        var lastDepositMonthParam = new SqlParameter("@LastDepositMonth", System.Data.SqlDbType.DateTime) { Direction = System.Data.ParameterDirection.Output };
                        var hasSeenModalParam = new SqlParameter("@HasSeenWelcomeModal", System.Data.SqlDbType.Bit) { Direction = System.Data.ParameterDirection.Output };

                        userCommand.Parameters.AddRange(new[] { employeeIdParam, balanceParam, monthlyDepositParam, lastDepositMonthParam, hasSeenModalParam });

                        await userCommand.ExecuteNonQueryAsync();

                        if (employeeIdParam.Value == DBNull.Value)
                        {
                            TempData["ErrorMessage"] = "Employee ID not found for user.";
                            return RedirectToAction("Login", "Account");
                        }

                        model.EmployeeId = (int)employeeIdParam.Value;
                        model.Balance = balanceParam.Value == DBNull.Value ? 0 : (decimal)balanceParam.Value;
                        model.MonthlyDeposits = monthlyDepositParam.Value == DBNull.Value ? 0 : (decimal)monthlyDepositParam.Value;
                        model.LastDepositDate = lastDepositMonthParam.Value == DBNull.Value ? null : (DateTime?)lastDepositMonthParam.Value;
                        model.ShowWelcomeModal = hasSeenModalParam.Value == DBNull.Value ? true : !(bool)hasSeenModalParam.Value;
                        model.LastDepositAmount = model.MonthlyDeposits;
                    }

                    // Fetch shops and menu items
                    using (var shopCommand = new SqlCommand("sp_GetShopsAndMenuItems", connection))
                    {
                        shopCommand.CommandType = System.Data.CommandType.StoredProcedure;
                        using (var reader = await shopCommand.ExecuteReaderAsync())
                        {
                            var restaurantDict = new Dictionary<int, Restuarants>();
                            while (await reader.ReadAsync())
                            {
                                int shopId = reader.GetInt32(reader.GetOrdinal("Shop_ID"));
                                if (!restaurantDict.ContainsKey(shopId))
                                {
                                    restaurantDict[shopId] = new Restuarants
                                    {
                                        ShopId = shopId,
                                        ShopName = reader.GetString(reader.GetOrdinal("Shop_Name")),
                                        MenuItems = new List<MenuItem>()
                                    };
                                }

                                if (!reader.IsDBNull(reader.GetOrdinal("MenuItem_ID")))
                                {
                                    restaurantDict[shopId].MenuItems.Add(new MenuItem
                                    {
                                        MenuItem_ID = reader.GetInt32(reader.GetOrdinal("MenuItem_ID")),
                                        Item_Name = reader.GetString(reader.GetOrdinal("Item_Name")),
                                        Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                                        Price = reader.GetDecimal(reader.GetOrdinal("Price"))
                                    });
                                }
                            }
                            model.Restuarant = restaurantDict.Values.ToList();
                        }
                    }

                    // Fetch order history
                    using (var orderCommand = new SqlCommand("sp_GetOrderHistory", connection))
                    {
                        orderCommand.CommandType = System.Data.CommandType.StoredProcedure;
                        orderCommand.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
                        using (var reader = await orderCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.OrderHistory.Add(new OrderView
                                {
                                    OrderDate = reader.GetDateTime(reader.GetOrdinal("Order_Date")),
                                    ItemName = reader.GetString(reader.GetOrdinal("Item_Name")),
                                    ShopName = reader.GetString(reader.GetOrdinal("Shop_Name")),
                                    Amount = reader.GetDecimal(reader.GetOrdinal("Amount"))
                                });
                            }
                        }
                    }

                    // Fetch current orders
                    using (var currentOrderCommand = new SqlCommand("sp_GetCurrentOrders", connection))
                    {
                        currentOrderCommand.CommandType = System.Data.CommandType.StoredProcedure;
                        currentOrderCommand.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
                        using (var reader = await currentOrderCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.CurrentOrders.Add(new CurrentOrder
                                {
                                    OrderId = reader.GetInt32(reader.GetOrdinal("Order_ID")).ToString(),
                                    ItemName = reader.GetString(reader.GetOrdinal("Item_Name")),
                                    ShopName = reader.GetString(reader.GetOrdinal("Shop_Name")),
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    EstimatedDeliveryMinutes = 15 // Static for demo; calculate dynamically if needed
                                });
                            }
                        }
                    }

                    // Fetch deposits
                    using (var depositCommand = new SqlCommand("sp_GetDeposits", connection))
                    {
                        depositCommand.CommandType = System.Data.CommandType.StoredProcedure;
                        depositCommand.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
                        using (var reader = await depositCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.depositModels.Add(new Deposit
                                {
                                    DepositAmount = reader.GetDecimal(reader.GetOrdinal("DepositAmount")),
                                    PaymentMethod = reader.GetString(reader.GetOrdinal("Payment_Method")),
                                });

                                if (!reader.IsDBNull(reader.GetOrdinal("CompanyCredit")))
                                {
                                    model.CompanyMatch += reader.GetDecimal(reader.GetOrdinal("CompanyCredit"));
                                }
                            }
                        }
                    }

                    // Calculate stats
                    model.TotalDeposited = model.depositModels.Sum(d => d.DepositAmount);
                    model.MonthlyCompanyMatch = model.MonthlyDeposits * 2;
                    model.TotalSpent = model.OrderHistory.Sum(o => o.Amount);
                    model.OrdersPlaced = model.CurrentOrders.Count; // Today's orders
                    model.AverageOrderAmount = model.CurrentOrders.Any() ?
                        model.CurrentOrders.Average(o => model.OrderHistory.FirstOrDefault(h => h.OrderId == o.OrderId)?.Amount ?? 0) : 0;
                    model.DepositAmount = model.depositModels.Any() ? model.depositModels.First().DepositAmount : 0;
                    model.PaymentMethod = model.depositModels.Any() ? model.depositModels.First().PaymentMethod : null;

                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while loading the dashboard: {ex.Message}";
                return View(model);
            }
        }
    
    [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeWelcome()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    string updateQuery = "UPDATE AspNetUsers SET HasSeenWelcomeModal = 1 WHERE Id = @UserId";
                    using (var command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                // Log the exception (implement logging as needed)
                return StatusCode(500, "An error occurred while acknowledging the welcome modal.");
            }
        }
        public IActionResult Privacy()
        {
            return View();
        }

        //---------------------------ENSURING EMPLOYEE CAN MANAGE DEPOSIT-------------------------------------------

        [HttpGet]
        public IActionResult GetDeposit()
        {
            var model = new DepositModel();
            return View();
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deposit([FromBody] DepositModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated." });
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                try
                {
                    // Retrieve user data
                    using (var selectCommand = new SqlCommand("sp_GetUserDepositData", connection))
                    {
                        selectCommand.CommandType = System.Data.CommandType.StoredProcedure;
                        selectCommand.Parameters.AddWithValue("@UserId", userId);

                        // Output parameters
                        var employeeIdParam = new SqlParameter("@EmployeeId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                        var monthlyDepositParam = new SqlParameter("@MonthlyDepositTotal", System.Data.SqlDbType.Decimal) { Direction = System.Data.ParameterDirection.Output, Precision = 18, Scale = 2 };
                        var lastDepositMonthParam = new SqlParameter("@LastDepositMonth", System.Data.SqlDbType.DateTime) { Direction = System.Data.ParameterDirection.Output };
                        var balanceParam = new SqlParameter("@Balance", System.Data.SqlDbType.Decimal) { Direction = System.Data.ParameterDirection.Output, Precision = 18, Scale = 2 };

                        selectCommand.Parameters.AddRange(new[] { employeeIdParam, monthlyDepositParam, lastDepositMonthParam, balanceParam });

                        await selectCommand.ExecuteNonQueryAsync();

                        

                        if (employeeIdParam.Value == DBNull.Value)
                        {
                            return Json(new { success = false, message = "Employee ID not found." });
                        }

                        int employeeId = (int)employeeIdParam.Value;
                        decimal? monthlyDepositTotal = monthlyDepositParam.Value == DBNull.Value ? (decimal?)null : (decimal)monthlyDepositParam.Value;
                        DateTime? lastDepositMonth = lastDepositMonthParam.Value == DBNull.Value ? (DateTime?)null : (DateTime)lastDepositMonthParam.Value;
                        decimal? balance = balanceParam.Value == DBNull.Value ? (decimal?)null : (decimal)balanceParam.Value;

                        // Validate deposit amount
                        if (model.DepositAmount <= 0)
                        {
                            return Json(new { success = false, message = "The Deposit Amount must be positive." });
                        }

                        // Check payment method
                        if (string.IsNullOrEmpty(model.PaymentMethod))
                        {
                            return Json(new { success = false, message = "Please select a payment method." });
                        }

                        // Handle current month logic
                        var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                        if (lastDepositMonth == null || lastDepositMonth < currentMonth)
                        {
                            monthlyDepositTotal = 0;
                            lastDepositMonth = currentMonth;
                        }

                        var previousTotal = monthlyDepositTotal ?? 0;
                        var newTotal = previousTotal + model.DepositAmount;

                        var previousBonusAmount = (int)Math.Floor(previousTotal / 250);
                        var newBonusAmount = (int)Math.Floor(newTotal / 250);
                        var companyCredit = (newBonusAmount - previousBonusAmount) * 500m;

                        // Update balance
                        balance = (balance ?? 0) + model.DepositAmount + companyCredit;

                        // Process deposit and log payment
                        using (var processCommand = new SqlCommand("sp_ProcessDeposit", connection))
                        {
                            processCommand.CommandType = System.Data.CommandType.StoredProcedure;
                            processCommand.Parameters.AddWithValue("@UserId", userId);
                            processCommand.Parameters.AddWithValue("@EmployeeId", employeeId);
                            processCommand.Parameters.AddWithValue("@NewTotal", newTotal);
                            processCommand.Parameters.AddWithValue("@LastDepositMonth", lastDepositMonth);
                            processCommand.Parameters.AddWithValue("@Balance", balance);
                            processCommand.Parameters.AddWithValue("@DepositAmount", model.DepositAmount);
                            processCommand.Parameters.AddWithValue("@CompanyCredit", companyCredit);
                            processCommand.Parameters.AddWithValue("@PaymentMethod", model.PaymentMethod);
                            processCommand.Parameters.AddWithValue("@PaymentDate", DateTime.Now);

                            await processCommand.ExecuteNonQueryAsync();

                           
                        }

                        var successUrl = Url.Action("DepositSuccess", "Home", new
                        {
                            DepositAmount = model.DepositAmount,
                            CompanyCredit = companyCredit,
                            NewBalance = balance
                        });
                        return Json(new
                        {
                            success = true,
                            redirectUrl = successUrl,
                            message = $"Successfully deposited R{model.DepositAmount:F2}. Company added R{companyCredit:F2}. New balance: R{balance:F2}."
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error processing deposit: {ex.Message}" });
                }
            }
        }

        [HttpGet]
        public IActionResult DepositSuccess(decimal depositAmount, decimal companyCredit, decimal newBalance)
        {
            return View(new
            {
                DepositAmount = depositAmount,
                CompanyCredit = companyCredit,
                NewBalance = newBalance
            });
        }
        private static async Task<bool> VerifyYocoPayment(string token, decimal amount)
        {
            // Placeholder for Yoco API call to verify/capture payment
            // Replace with actual Yoco API integration using your secret key
            // Example: Use HttpClient to call Yoco's capture endpoint
            // Return true if payment is successful, false otherwise
            if (string.IsNullOrEmpty(token) || amount <= 0)
            {
                return false;
            }

            // Mock implementation for testing (remove in production)
            if (token.StartsWith("mock_token_"))
            {
                return true; // Accept mock tokens for testing
            }

            // Real Yoco API call (implement this)
            // var client = new HttpClient();
            // var response = await client.PostAsync($"https://api.yoco.com/v1/payments/capture", new { token, amountInCents = (int)(amount * 100) });
            // return response.IsSuccessStatusCode;

            return false; // Default to false until real API is implemented
        }

        [HttpGet]
        public async Task<IActionResult> Balance()
        {
          
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            
            string connectionString = _config.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Fetch user data
                string selectQuery = "SELECT Employee_ID, Balance FROM AspNetUsers WHERE Id = @UserId";
                using (var selectCommand = new SqlCommand(selectQuery, connection))
                {
                    selectCommand.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return Unauthorized(); 
                        }


                        int? employeeId = reader.IsDBNull(reader.GetOrdinal("Employee_ID")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("Employee_ID"));
                        decimal balance = reader.GetDecimal(reader.GetOrdinal("Balance"));

                        
                        ViewBag.Balance = balance;
                        ViewBag.Employee_Id = employeeId;

                        return View();
                    }
                }
            }
        }
        //---------------------------------------------------------------------------------


        //---------------------------BROWSING  MENU FROM DIFFERENT SHOP -------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Restuarants()
        {
            var restaurants = await GetRestaurantsFromDatabase();
            return View(restaurants);
        }

        private async Task<List<Shop>> GetRestaurantsFromDatabase()
        {
            var restaurants = new List<Shop>();
            const string query = @"
        SELECT [Shop_ID], [Shop_Name], [Address], [Contact_Number], [Email], [IsActive] 
        FROM [BraamBowlOrderDatabase].[dbo].[Shops]
        WHERE [IsActive] = 1
        ORDER BY [Shop_Name]";

            try
            {
                var connectionString = _config.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var restaurant = new Shop
                    {
                        Shop_ID = reader.GetInt32("Shop_ID"),
                        Shop_Name = reader.GetString("Shop_Name"),
                        Address = reader.GetString("Address"),
                        Contact_Number = reader.GetString("Contact_Number"),
                        Email = reader.IsDBNull("Email") ? null : reader.GetString("Email"),
                        IsActive = reader.GetBoolean("IsActive")
                    };
                    restaurants.Add(restaurant);
                }
            }
            catch (SqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
                // You might want to show an error view or redirect
                throw new Exception("Database error occurred while retrieving restaurants", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General Error: {ex.Message}");
                throw new Exception("An unexpected error occurred while retrieving restaurants", ex);
            }

            return restaurants;
        }




        //---------------------------------------------------------------------------------


        //---------------------------PLACE ORDER  -------------------------------------------
        [HttpGet]
        [Route("Menu/{id}")]
        public async Task<IActionResult> Menu(int id)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");

            Shop shop = null;
            List<MenuItem> menuItems = new List<MenuItem>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Get the shop
                using (SqlCommand cmd = new SqlCommand("SELECT Shop_ID, Shop_Name, IsActive FROM Shops WHERE Shop_ID = @ShopId AND IsActive = 1", connection))
                {
                    cmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = id;
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            shop = new Shop
                            {
                                Shop_ID = reader.GetInt32(0),
                                Shop_Name = reader.GetString(1),
                                IsActive = reader.GetBoolean(2)
                            };
                        }
                        else
                        {
                            return NotFound("Shop not found or inactive.");
                        }
                    }
                }

                // Get the menu items
                using (SqlCommand cmd = new SqlCommand("SELECT MenuItem_ID, Item_Name, Price, Category, Description, Tags FROM MenuItems WHERE Shop_ID = @ShopId", connection))
                {
                    cmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = id;
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            menuItems.Add(new MenuItem
                            {
                                MenuItem_ID = reader.GetInt32(0),
                                Item_Name = reader.GetString(1),
                                Price = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                                Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Tags = reader.IsDBNull(5) ? null : reader.GetString(5)
                            });
                        }
                    }
                }
            }

            // If no menu items, still return the view but with an empty model
            var orderModel = new OrderModel
            {
                Items = menuItems.Select(m => new OrderItemModel
                {
                    MenuItemId = m.MenuItem_ID,
                    Name = m.Item_Name,
                    Price = m.Price,
                    Category = m.Category,
                    Description = m.Description,
                    Tags = m.Tags
                }).ToList()
            };

            ViewBag.ShopId = id;
            ViewBag.ShopName = shop.Shop_Name;
            // Use distinct categories from menu items, handling null/empty cases
            ViewBag.Categories = menuItems
                .Where(m => !string.IsNullOrWhiteSpace(m.Category))
                .Select(m => m.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return View(orderModel);
        }

        [HttpPost]
        [Route("Checkout/{shopId}")]
        public async Task<IActionResult> Checkout(int shopId, string[] cartItems)
        {
            if (cartItems == null || !cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction("Menu", "Restaurants", new { id = shopId });
            }

            // Parse cart items from JSON
            var orderItems = new List<OrderItemModel>();
            decimal totalAmount = 0;
            foreach (var item in cartItems)
            {
                var cartItem = JsonConvert.DeserializeObject<CartItem>(item);
                orderItems.Add(new OrderItemModel
                {
                    MenuItemId = cartItem.MenuItemId,
                    Name = cartItem.Item_Name,
                    Price = cartItem.Price,
                    Quantity = 1 // Default quantity, will be adjustable in view
                });
                totalAmount += cartItem.Price;
            }

            // Group items by MenuItemId to handle quantities
            var groupedItems = orderItems
                .GroupBy(i => i.MenuItemId)
                .Select(g => new OrderItemModel
                {
                    MenuItemId = g.Key,
                    Name = g.First().Name,
                    Price = g.First().Price,
                    Quantity = g.Count(),
                    Category = g.First().Category,
                    Description = g.First().Description,
                    Tags = g.First().Tags
                }).ToList();

            var model = new CheckoutViewModel
            {
                ShopId = shopId,
                Items = groupedItems,
                TotalAmount = groupedItems.Sum(i => i.Price * i.Quantity)
            };

            // Get shop name and employee information
            string connectionString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Get shop name
                using (SqlCommand cmd = new SqlCommand("sp_GetShopName", connection))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ShopId", shopId);
                    var shopNameParam = new SqlParameter("@ShopName", System.Data.SqlDbType.NVarChar, 100) { Direction = System.Data.ParameterDirection.Output };
                    cmd.Parameters.Add(shopNameParam);

                    await cmd.ExecuteNonQueryAsync();

                  
                    model.Shop_Name = shopNameParam.Value?.ToString();
                }

                // Get employee information
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                using (SqlCommand cmd = new SqlCommand("sp_GetEmployeeCheckoutInfo", connection))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    // Output parameters
                    var firstNameParam = new SqlParameter("@FirstName", System.Data.SqlDbType.NVarChar, 50) { Direction = System.Data.ParameterDirection.Output };
                    var surnameParam = new SqlParameter("@Surname", System.Data.SqlDbType.NVarChar, 50) { Direction = System.Data.ParameterDirection.Output };
                    var balanceParam = new SqlParameter("@Balance", System.Data.SqlDbType.Decimal) { Direction = System.Data.ParameterDirection.Output, Precision = 18, Scale = 2 };
                    var deliveryAddressParam = new SqlParameter("@DeliveryAddress", System.Data.SqlDbType.NVarChar, -1) { Direction = System.Data.ParameterDirection.Output };

                    cmd.Parameters.AddRange(new[] { firstNameParam, surnameParam, balanceParam, deliveryAddressParam });

                    await cmd.ExecuteNonQueryAsync();

                   

                    model.EmployeeName = firstNameParam.Value?.ToString();
                    model.EmployeeSurname = surnameParam.Value?.ToString();
                    model.EmployeeBalance = balanceParam.Value == DBNull.Value ? 0 : (decimal)balanceParam.Value;
                    model.DeliveryAddress = deliveryAddressParam.Value == DBNull.Value ? "No address provided" : deliveryAddressParam.Value.ToString();

                    // Check if balance is sufficient
                    if (model.EmployeeBalance < model.TotalAmount)
                    {
                        TempData["ErrorMessage"] = "Insufficient balance. Please add funds to your account.";
                        return RedirectToAction("Menu", "Restaurants", new { id = shopId });
                    }
                }
            }

            return View(model);
        }


     
        public async Task<IActionResult> PlaceOrder1(int shopId, CheckoutViewModel model)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(identityUserId))
            {
                TempData["ErrorMessage"] = "User not authenticated.";
                return RedirectToAction("Login", "Account");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Get Employee ID
                        int employeeId;
                        using (SqlCommand getEmpCmd = new SqlCommand("SELECT Employee_ID FROM AspNetUsers WHERE Id = @UserId", connection, transaction))
                        {
                            getEmpCmd.Parameters.AddWithValue("@UserId", identityUserId);
                            var result = await getEmpCmd.ExecuteScalarAsync();
                            if (result == null || !int.TryParse(result.ToString(), out employeeId))
                            {
                                TempData["ErrorMessage"] = "Employee ID not found.";
                                return RedirectToAction("Login", "Account");
                            }
                        }

                        // Check employee balance
                        decimal balance;
                        using (SqlCommand cmd = new SqlCommand("SELECT Balance FROM AspNetUsers WHERE Employee_ID = @EmployeeId", connection, transaction))
                        {
                            cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                            balance = (decimal)await cmd.ExecuteScalarAsync();
                        }

                        decimal totalAmount = model.Items.Sum(i => i.Price * i.Quantity);
                        if (balance < totalAmount)
                        {
                            TempData["ErrorMessage"] = "Insufficient balance to place this order.";
                            return RedirectToAction("Checkout", new { shopId = shopId });
                        }

                        // Get shop name for Order_Number prefix
                        string shopName;
                        using (SqlCommand cmd = new SqlCommand("SELECT Shop_Name FROM Shops WHERE Shop_ID = @ShopId AND IsActive = 1", connection, transaction))
                        {
                            cmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = shopId;
                            shopName = (string)await cmd.ExecuteScalarAsync();
                            if (string.IsNullOrEmpty(shopName))
                            {
                                TempData["ErrorMessage"] = "Shop not found or inactive.";
                                return RedirectToAction("Checkout", new { shopId = shopId });
                            }
                        }

                        // Generate Order_Number
                        string orderNumber;
                        using (SqlCommand cmd = new SqlCommand(
                            @"SELECT COUNT(*) FROM Orders WHERE Shop_ID = @ShopId AND CAST(Order_Date AS DATE) = CAST(GETDATE() AS DATE)",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = shopId;
                            int orderCount = (int)await cmd.ExecuteScalarAsync();
                            string shopPrefix = shopName.ToUpper().Replace(" ", "");
                            string datePart = DateTime.Now.ToString("yyyyMMdd");
                            orderNumber = $"{shopPrefix}-{datePart}-{orderCount + 1:D3}";
                        }

                        // Auto-assign driver
                        int driverId;
                        string driverName;
                        using (SqlCommand cmd = new SqlCommand(
                            "SELECT TOP 1 Driver_ID, Full_Name FROM Drivers WHERE Status = 'Active'", connection, transaction))
                        {
                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    driverId = reader.GetInt32(0);
                                    driverName = reader.GetString(1);
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "No available drivers found.";
                                    return RedirectToAction("Checkout", new { shopId = shopId });
                                }
                            }
                        }

                        // Update driver status to Busy
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Drivers SET Status = 'Busy' WHERE Driver_ID = @DriverId", connection, transaction))
                        {
                            cmd.Parameters.Add("@DriverId", SqlDbType.Int).Value = driverId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Generate OTP
                        string otp = new Random().Next(100000, 999999).ToString();

                        // Create Order
                        int orderId;
                        using (SqlCommand cmd = new SqlCommand(
                            @"INSERT INTO Orders (Employee_ID, Shop_ID, Order_Date, Amount, Status, Order_Number, Delivery_Address)
                        OUTPUT INSERTED.Order_ID
                        VALUES (@EmployeeId, @ShopId, @OrderDate, @TotalAmount, @Status, @OrderNumber, @DeliveryAddress)",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                            cmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = shopId;
                            cmd.Parameters.Add("@OrderDate", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@TotalAmount", SqlDbType.Decimal).Value = totalAmount;
                            cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = "Preparing";
                            cmd.Parameters.Add("@OrderNumber", SqlDbType.NVarChar).Value = orderNumber;
                            cmd.Parameters.Add("@DeliveryAddress", SqlDbType.NVarChar).Value = model.DeliveryAddress ?? "No address provided";
                            orderId = (int)await cmd.ExecuteScalarAsync();
                        }

                        // Create OrderItems
                        foreach (var item in model.Items)
                        {
                            using (SqlCommand cmd = new SqlCommand(
                                @"INSERT INTO OrderItems ([Order_Id], [Menu_Item_Id], Quantity, Unit_Price_At_Time_Of_Order)
                            VALUES (@OrderId, @MenuItemId, @Quantity, @UnitPrice)",
                                connection, transaction))
                            {
                                cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                                cmd.Parameters.Add("@MenuItemId", SqlDbType.Int).Value = item.MenuItemId;
                                cmd.Parameters.Add("@Quantity", SqlDbType.Int).Value = item.Quantity;
                                cmd.Parameters.Add("@UnitPrice", SqlDbType.Decimal).Value = item.Price;
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // Create Delivery record with OTP
                        using (SqlCommand cmd = new SqlCommand(
                            @"INSERT INTO Deliveries (Order_ID, OrderId, Driver_Name, DriverId, Delivery_Address, Status, Dispatched_Time, OTP, IsOTPVerified)
                        VALUES (@OrderId, @OrderId2, @DriverName, @DriverId, @DeliveryAddress, @Status, @DispatchedTime, @OTP, @IsOTPVerified)",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            cmd.Parameters.Add("@OrderId2", SqlDbType.Int).Value = orderId;
                            cmd.Parameters.Add("@DriverName", SqlDbType.NVarChar).Value = driverName;
                            cmd.Parameters.Add("@DriverId", SqlDbType.Int).Value = driverId;
                            cmd.Parameters.Add("@DeliveryAddress", SqlDbType.NVarChar).Value = model.DeliveryAddress ?? "No address provided";
                            cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = "Preparing";
                            cmd.Parameters.Add("@DispatchedTime", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@OTP", SqlDbType.NVarChar).Value = otp;
                            cmd.Parameters.Add("@IsOTPVerified", SqlDbType.Bit).Value = false;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update employee balance
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE AspNetUsers SET Balance = Balance - @TotalAmount WHERE Employee_ID = @EmployeeId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@TotalAmount", SqlDbType.Decimal).Value = totalAmount;
                            cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Get employee email and name
                        string employeeEmail, employeeName, employeeSurname;
                        using (SqlCommand cmd = new SqlCommand(
                            "SELECT Email, First_Name, Surname FROM AspNetUsers WHERE Employee_ID = @EmployeeId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    employeeEmail = reader.GetString(0);
                                    employeeName = reader.GetString(1);
                                    employeeSurname = reader.GetString(2);
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "Employee details not found.";
                                    return RedirectToAction("Checkout", new { shopId = shopId });
                                }
                            }
                        }

                        transaction.Commit();

                        // Schedule background job for status update and email
                        var estimatedDelivery = DateTime.Now.AddMinutes(5);
                        var onTheWayTime = estimatedDelivery.AddMinutes(-1);
                        _bck.Schedule(
                            () => UpdateOrderStatusAndNotify(orderId, employeeEmail, employeeName, employeeSurname, orderNumber, shopName, driverName, model.DeliveryAddress, otp),
                            onTheWayTime);

                        // Send initial order confirmation email with OTP
                        await SendOrderConfirmationEmail(employeeEmail, employeeName, employeeSurname, orderNumber, shopName, model.Items, totalAmount, estimatedDelivery, model.DeliveryAddress, driverName, otp);

                        // Clear cart
                        TempData["ClearCart"] = true;
                        TempData["SuccessMessage"] = "Order placed successfully!";

                        // Redirect to Receipt action
                        return RedirectToAction("Receipt", new
                        {
                            orderId,
                            orderNumber,
                            shopName,
                            employeeName,
                            employeeSurname,
                            totalAmount,
                            estimatedDelivery = estimatedDelivery.ToString("yyyy-MM-dd HH:mm:ss"),
                            deliveryAddress = model.DeliveryAddress,
                            driverName,
                            otp
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["ErrorMessage"] = $"Error placing order: {ex.Message}";
                        return RedirectToAction("Checkout", new { shopId = shopId });
                    }
                }
            }
        }

        [HttpPost]
        [Route("PlaceOrder/{shopId}")]
        public async Task<IActionResult> PlaceOrder(int shopId, CheckoutViewModel model)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(identityUserId))
            {
                TempData["ErrorMessage"] = "User not authenticated.";
                return RedirectToAction("Login", "Account");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Get Employee ID
                        int employeeId;
                        using (SqlCommand cmd = new SqlCommand("sp_GetEmployeeId", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@UserId", identityUserId);
                            var employeeIdParam = new SqlParameter("@EmployeeId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                            cmd.Parameters.Add(employeeIdParam);

                            await cmd.ExecuteNonQueryAsync();
                           
                            employeeId = (int)employeeIdParam.Value;
                        }

                        // Check employee balance
                        decimal balance;
                        using (SqlCommand cmd = new SqlCommand("sp_GetEmployeeBalance", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                            var balanceParam = new SqlParameter("@Balance", System.Data.SqlDbType.Decimal) { Direction = System.Data.ParameterDirection.Output, Precision = 18, Scale = 2 };
                            cmd.Parameters.Add(balanceParam);

                            await cmd.ExecuteNonQueryAsync();
                            balance = balanceParam.Value == DBNull.Value ? 0 : (decimal)balanceParam.Value;
                        }

                        decimal totalAmount = model.Items.Sum(i => i.Price * i.Quantity);
                        if (balance < totalAmount)
                        {
                            transaction.Rollback();
                            TempData["ErrorMessage"] = "Insufficient balance to place this order.";
                            return RedirectToAction("Checkout", new { shopId = shopId });
                        }

                        // Get shop name
                        string shopName;
                        using (SqlCommand cmd = new SqlCommand("sp_GetShopNameForOrder", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@ShopId", shopId);
                            var shopNameParam = new SqlParameter("@ShopName", System.Data.SqlDbType.NVarChar, 100) { Direction = System.Data.ParameterDirection.Output };
                            cmd.Parameters.Add(shopNameParam);

                            await cmd.ExecuteNonQueryAsync();
                           
                            shopName = shopNameParam.Value.ToString();
                        }

                        // Generate Order_Number
                        string orderNumber;
                        using (SqlCommand cmd = new SqlCommand("sp_GetDailyOrderCount", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@ShopId", shopId);
                            var orderCountParam = new SqlParameter("@OrderCount", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                            cmd.Parameters.Add(orderCountParam);

                            await cmd.ExecuteNonQueryAsync();
                            int orderCount = (int)orderCountParam.Value;
                            string shopPrefix = shopName.ToUpper().Replace(" ", "");
                            string datePart = DateTime.Now.ToString("yyyyMMdd");
                            orderNumber = $"{shopPrefix}-{datePart}-{orderCount + 1:D3}";
                        }

                        // Auto-assign driver
                        int driverId;
                        string driverName;
                        using (SqlCommand cmd = new SqlCommand("sp_GetAvailableDriver", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            var driverIdParam = new SqlParameter("@DriverId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                            var driverNameParam = new SqlParameter("@DriverName", System.Data.SqlDbType.NVarChar, 100) { Direction = System.Data.ParameterDirection.Output };
                            cmd.Parameters.AddRange(new[] { driverIdParam, driverNameParam });

                            await cmd.ExecuteNonQueryAsync();
                            
                            driverId = (int)driverIdParam.Value;
                            driverName = driverNameParam.Value.ToString();
                        }

                        // Update driver status
                        using (SqlCommand cmd = new SqlCommand("sp_UpdateDriverStatus", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@DriverId", driverId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Generate OTP
                        string otp = new Random().Next(100000, 999999).ToString();

                        // Create Order
                        int orderId;
                        using (SqlCommand cmd = new SqlCommand("sp_CreateOrder", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                            cmd.Parameters.AddWithValue("@ShopId", shopId);
                            cmd.Parameters.AddWithValue("@OrderDate", DateTime.Now);
                            cmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                            cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                            cmd.Parameters.AddWithValue("@DeliveryAddress", model.DeliveryAddress ?? "No address provided");
                            cmd.Parameters.AddWithValue("@DriverId", driverId);
                            cmd.Parameters.AddWithValue("@DriverName", driverName);
                            cmd.Parameters.AddWithValue("@OTP", otp);
                            var orderIdParam = new SqlParameter("@OrderId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                            cmd.Parameters.Add(orderIdParam);

                            await cmd.ExecuteNonQueryAsync();
                           
                            orderId = (int)orderIdParam.Value;
                        }

                        // Create OrderItems
                        foreach (var item in model.Items)
                        {
                            using (SqlCommand cmd = new SqlCommand("sp_CreateOrderItem", connection, transaction))
                            {
                                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@OrderId", orderId);
                                cmd.Parameters.AddWithValue("@MenuItemId", item.MenuItemId);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.Parameters.AddWithValue("@UnitPrice", item.Price);

                                await cmd.ExecuteNonQueryAsync();
                               
                            }
                        }

                        // Update employee balance
                        using (SqlCommand cmd = new SqlCommand("sp_UpdateEmployeeBalance", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                            cmd.Parameters.AddWithValue("@TotalAmount", totalAmount);

                            await cmd.ExecuteNonQueryAsync();
                           
                        }

                        // Get employee contact info
                        string employeeEmail, employeeName, employeeSurname;
                        using (SqlCommand cmd = new SqlCommand("sp_GetEmployeeContactInfo", connection, transaction))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                            var emailParam = new SqlParameter("@Email", System.Data.SqlDbType.NVarChar, 256) { Direction = System.Data.ParameterDirection.Output };
                            var firstNameParam = new SqlParameter("@FirstName", System.Data.SqlDbType.NVarChar, 50) { Direction = System.Data.ParameterDirection.Output };
                            var surnameParam = new SqlParameter("@Surname", System.Data.SqlDbType.NVarChar, 50) { Direction = System.Data.ParameterDirection.Output };
                            cmd.Parameters.AddRange(new[] { emailParam, firstNameParam, surnameParam });

                            await cmd.ExecuteNonQueryAsync();
                           
                            employeeEmail = emailParam.Value.ToString();
                            employeeName = firstNameParam.Value.ToString();
                            employeeSurname = surnameParam.Value.ToString();
                        }

                        transaction.Commit();

                        // Schedule background job for status update and email
                        var estimatedDelivery = DateTime.Now.AddMinutes(5);
                        var onTheWayTime = estimatedDelivery.AddMinutes(-1);
                        _bck.Schedule(
                            () => UpdateOrderStatusAndNotify(orderId, employeeEmail, employeeName, employeeSurname, orderNumber, shopName, driverName, model.DeliveryAddress, otp),
                            onTheWayTime);

                        // Send initial order confirmation email with OTP
                        await SendOrderConfirmationEmail(employeeEmail, employeeName, employeeSurname, orderNumber, shopName, model.Items, totalAmount, estimatedDelivery, model.DeliveryAddress, driverName, otp);

                        // Clear cart
                        TempData["ClearCart"] = true;
                        TempData["SuccessMessage"] = "Order placed successfully!";

                        // Redirect to Receipt action
                        return RedirectToAction("Receipt", new
                        {
                            orderId,
                            orderNumber,
                            shopName,
                            employeeName,
                            employeeSurname,
                            totalAmount,
                            estimatedDelivery = estimatedDelivery.ToString("yyyy-MM-dd HH:mm:ss"),
                            deliveryAddress = model.DeliveryAddress,
                            driverName,
                            otp
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["ErrorMessage"] = $"Error placing order: {ex.Message}";
                        return RedirectToAction("Checkout", new { shopId = shopId });
                    }
                }
            }
        }
        public async Task UpdateOrderStatusAndNotify(int orderId, string employeeEmail, string employeeName, string employeeSurname, string orderNumber, string shopName, string driverName, string deliveryAddress, string otp)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Update Orders status
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Orders SET Status = 'On the way' WHERE Order_ID = @OrderId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update Deliveries status
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Deliveries SET Status = 'On the way' WHERE Order_ID = @OrderId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        // Send "On the way" email
                        await SendOnTheWayEmail(employeeEmail, employeeName, employeeSurname, orderNumber, shopName, driverName, deliveryAddress, otp);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Log error (implement logging as needed)
                        Console.WriteLine($"Error updating status: {ex.Message}");
                    }
                }
            }
        }
        [HttpGet]
        [Route("Receipt")]
        public IActionResult Receipt(int orderId, string orderNumber, string shopName, string employeeName, string employeeSurname, decimal totalAmount, string estimatedDelivery, string deliveryAddress, string driverName, string otp)
        {
            var model = new ReceiptViewModel
            {
                OrderId = orderId,
                OrderNumber = orderNumber,
                ShopName = shopName,
                EmployeeName = employeeName,
                EmployeeSurname = employeeSurname,
                TotalAmount = totalAmount,
                OrderDate = DateTime.Now,
                EstimatedDelivery = DateTime.Parse(estimatedDelivery),
                DeliveryAddress = deliveryAddress,
                DriverName = driverName,
                OTP = otp,
                Items = new List<OrderItemModel>()
            };

            string connectionString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT oi.Menu_Item_Id, oi.Quantity, oi.Unit_Price_At_Time_Of_Order, m.Item_Name
                FROM OrderItems oi
                JOIN MenuItems m ON oi.Menu_Item_Id = m.MenuItem_ID
                WHERE oi.Order_Id = @OrderId",
                    connection))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.Items.Add(new OrderItemModel
                            {
                                MenuItemId = reader.GetInt32(0),
                                Quantity = reader.GetInt32(1),
                                Price = reader.GetDecimal(2),
                                Name = reader.GetString(3)
                            });
                        }
                    }
                }
            }

            return View(model);
        }


        [HttpGet]
        [Route("VerifyOTP/{orderId}")]
        public IActionResult VerifyOTP(int orderId)
        {
            var model = new VerifyOTPViewModel
            {
                OrderId = orderId
            };
            return View(model);
        }

        [HttpPost]
        [Route("VerifyOTP/{orderId}")]
        public async Task<IActionResult> VerifyOTP(int orderId, VerifyOTPViewModel model)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Verify OTP
                        string storedOTP;
                        int driverId;
                        using (SqlCommand cmd = new SqlCommand(
                            "SELECT OTP, DriverId FROM Deliveries WHERE Order_ID = @OrderId AND IsOTPVerified = 0",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    storedOTP = reader.GetString(0);
                                    driverId = reader.GetInt32(1);
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "Invalid or already verified order.";
                                    return RedirectToAction("Home", "Restaurants");
                                }
                            }
                        }

                        if (storedOTP != model.OTP)
                        {
                            TempData["ErrorMessage"] = "Invalid OTP.";
                            return View(model);
                        }

                        // Update Deliveries table
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Deliveries SET IsOTPVerified = 1, Delivered_Time = @DeliveredTime, Status = 'Delivered' WHERE Order_ID = @OrderId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            cmd.Parameters.Add("@DeliveredTime", SqlDbType.DateTime).Value = DateTime.Now;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update Orders table
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Orders SET Status = 'Delivered' WHERE Order_ID = @OrderId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Reset driver status to Active
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Drivers SET Status = 'Active' WHERE Driver_ID = @DriverId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@DriverId", SqlDbType.Int).Value = driverId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        TempData["SuccessMessage"] = "Delivery verified successfully!";
                        return RedirectToAction("Home", "Restaurants");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["ErrorMessage"] = $"Error verifying OTP: {ex.Message}";
                        return View(model);
                    }
                }
            }
        }

        private async Task SendOrderConfirmationEmail1(string email, string firstName, string surname, string orderNumber, string shopName, List<OrderItemModel> items, decimal totalAmount, DateTime estimatedDelivery, string deliveryAddress, string driverName, string otp)
        {
            var fromAddress = new MailAddress("noreply@braambowl.com", "BraamBowl Orders");
            var toAddress = new MailAddress(email, $"{firstName} {surname}");
            const string subject = "Your Order Confirmation";

            string body = $@"<!DOCTYPE html>
                <html lang=""en"">
                <head>
                    <meta charset=""UTF-8"">
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                    <title>Order Confirmation</title>
                </head>
                <body style=""font-family: 'Poppins', sans-serif; background: #f1f5f9; margin: 0; padding: 20px;"">
                    <div style=""max-width: 600px; margin: 0 auto; background: rgba(255, 255, 255, 0.9); backdrop-filter: blur(12px); border: 1px solid rgba(255, 255, 255, 0.3); box-shadow: 0 10px 20px rgba(0, 0, 0, 0.1); border-radius: 16px; padding: 24px;"">
                        <h1 style=""color: #1f2937; font-weight: 700; margin-bottom: 1.5rem; position: relative; padding-left: 1rem;"">
                            Order Confirmation 🎉
                            <span style=""content: ''; position: absolute; left: 0; top: 50%; transform: translateY(-50%); width: 4px; height: 24px; background: linear-gradient(135deg, #2563eb 0%, #3b82f6 100%); border-radius: 2px;""></span>
                        </h1>
                        <p style=""color: #374151; font-size: 16px; margin-bottom: 16px;"">
                            Dear {firstName} {surname} 👋,
                        </p>
                        <p style=""color: #374151; font-size: 16px; margin-bottom: 24px;"">
                            Thank you for your order from {shopName}! We're excited to get your meal to you 🍽️.
                        </p>
                        <div style=""background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 16px; margin-bottom: 24px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.05);"">
                            <h2 style=""color: #1f2937; font-size: 18px; font-weight: 600; margin-bottom: 12px;"">Order Details 📋</h2>
                            <p style=""color: #374151; margin: 8px 0;""><strong>Order Number:</strong> {orderNumber}</p>
                            <p style=""color: #374151; margin: 8px 0;""><strong>Order Date:</strong> {DateTime.Now:yyyy-MM-dd HH:mm} 🕒</p>
                            <p style=""color: #374151; margin: 8px 0;""><strong>Estimated Delivery:</strong> {estimatedDelivery:yyyy-MM-dd HH:mm} 🚚</p>
                            <p style=""color: #374151; margin: 8px 0;""><strong>Delivery Address:</strong> {deliveryAddress} 📍</p>
                            <p style=""color: #374151; margin: 8px 0;""><strong>Driver:</strong> {driverName} 👨‍✈️</p>
                            <p style=""color: #374151; margin: 8px 0;""><strong>OTP:</strong> {otp} 🔒 <span style=""color: #6b7280; font-size: 14px;"">(Please keep this OTP to verify your delivery upon arrival)</span></p>
                        </div>
                        <h2 style=""color: #1f2937; font-size: 18px; font-weight: 600; margin-bottom: 12px;"">Items Ordered 🛒</h2>
                        {string.Join("", items.Select(item => $"<div style=\"background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 16px; margin-bottom: 12px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.05);\">{item.Name} x{item.Quantity}: R{(item.Price * item.Quantity):F2} 🍴</div>"))}
                        <div style=""text-align: right; font-size: 18px; font-weight: 600; color: #1f2937; margin-top: 16px;"">
                            Total: R{totalAmount:F2} 💸
                        </div>
                        <p style=""color: #374151; font-size: 16px; margin-top: 24px;"">
                            We hope you enjoy your meal! 😋
                        </p>
                        <p style=""color: #6b7280; font-size: 14px; margin-top: 16px;"">
                            Best regards,<br>BraamBowl Orders 🌟
                        </p>
                    </div>
                </body>
                </html>";
            foreach (var item in items)
            {
                body += $"- {item.Name} x{item.Quantity}: R{(item.Price * item.Quantity):F2}\n";
            }
            body += $"\nTotal: R{totalAmount:F2}\n\nWe hope you enjoy your meal!";

            using (var smtp = new SmtpClient
            {
                Host = "168.89.180.38",
                Port = 25,
                EnableSsl = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("your-email@yourprovider.com", "your-password")
            })
            {
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    await smtp.SendMailAsync(message);
                }
            }
        }

        private async Task SendOrderConfirmationEmail(string email, string firstName, string surname, string orderNumber, string shopName, List<OrderItemModel> items, decimal totalAmount, DateTime estimatedDelivery, string deliveryAddress, string driverName, string otp)
        {
            var fromAddress = new MailAddress("noreply@braambowl.com", "BraamBowl Orders");
            var toAddress = new MailAddress(email, $"{firstName} {surname}");
            const string subject = "Your Order Confirmation 🎉";

            string body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Order Confirmation</title>
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
            color: #1f2937;
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
            background: linear-gradient(135deg, #2563eb 0%, #3b82f6 100%);
            border-radius: 2px;
        }}

        .order-item {{
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 12px;
            padding: 16px;
            margin-bottom: 12px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.05);
        }}

        .status-badge {{
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            background: #ccfbf1;
            color: #0d9488;
            display: inline-block;
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
    </style>
</head>
<body>
    <div class=""glass-card"">
        <div style=""width: 48px; height: 48px; display: inline-flex; align-items: center; justify-content: center; background: linear-gradient(135deg, #f3f4f6 0%, #e5e7eb 100%); border-radius: 12px; margin-bottom: 12px; font-size: 24px;"">🛒</div>
        <h1 class=""section-title"">Order Confirmation 🎉</h1>
        <p>Dear {firstName} {surname} 👋,</p>
        <p>Thank you for your order from <strong>{shopName}</strong>! We're excited to get your meal to you 🍽️.</p>
        <div class=""order-item"">
            <h2 style=""color: #1f2937; font-size: 18px; font-weight: 600; margin-bottom: 12px;"">Order Details 📋</h2>
            <p><strong>Order Number:</strong> {orderNumber}</p>
            <p><strong>Order Date:</strong> {DateTime.Now:yyyy-MM-dd HH:mm} 🕒</p>
            <p><strong>Estimated Delivery:</strong> {estimatedDelivery:yyyy-MM-dd HH:mm} 🚚</p>
            <p><strong>Delivery Address:</strong> {deliveryAddress} 📍</p>
            <p><strong>Driver:</strong> {driverName} 👨‍✈️</p>
            <p><strong>OTP:</strong> <span class=""status-badge"">{otp}</span> 🔒 <span style=""color: #6b7280; font-size: 14px;"">(Please provide this OTP to verify delivery)</span></p>
        </div>
        <h2 style=""color: #1f2937; font-size: 18px; font-weight: 600; margin-bottom: 12px;"">Items Ordered 🛒</h2>
        {string.Join("", items.Select(item => $"<div class=\"order-item\"><p>{item.Name} x{item.Quantity}: R{(item.Price * item.Quantity):F2} 🍴</p></div>"))}
        <div style=""text-align: right; font-size: 18px; font-weight: 600; color: #1f2937; margin-top: 16px;"">
            Total: R{totalAmount:F2} 💸
        </div>
        <p>We hope you enjoy your meal! 😋</p>
        <div class=""footer"">
            <p>Best regards,<br>BraamBowl Orders 🌟</p>
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

        private async Task SendOnTheWayEmail1(string email, string firstName, string surname, string orderNumber, string shopName, string driverName, string deliveryAddress, string otp)
        {
            var fromAddress = new MailAddress("noreply@braambowl.com", "BraamBowl Orders");
            var toAddress = new MailAddress(email, $"{firstName} {surname}");
            const string subject = "Your Order is On the Way!";

            string body = $@"Dear {firstName} {surname},

Your order from {shopName} is on the way!

Order Number: {orderNumber}
Delivery Address: {deliveryAddress}
Driver: {driverName}
OTP: {otp} (Please provide this OTP to the driver to verify delivery)

Your order should arrive soon. Thank you for choosing us!";

            using (var smtp = new SmtpClient
            {
                Host = "168.89.180.38",
                Port = 25,
                EnableSsl = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("your-email@yourprovider.com", "your-password")
            })
            {
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    await smtp.SendMailAsync(message);
                }
            }
        }



        private async Task SendOnTheWayEmail(string email, string firstName, string surname, string orderNumber, string shopName, string driverName, string deliveryAddress, string otp)
        {
            var fromAddress = new MailAddress("noreply@braambowl.com", "BraamBowl Orders");
            var toAddress = new MailAddress(email, $"{firstName} {surname}");
            const string subject = "Your Order is On the Way! 🚚";

            string body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Your Order is On the Way!</title>
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
            color: #1f2937;
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
            background: linear-gradient(135deg, #2563eb 0%, #3b82f6 100%);
            border-radius: 2px;
        }}

        .order-item {{
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 12px;
            padding: 16px;
            margin-bottom: 12px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.05);
        }}

        .status-badge {{
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            background: #ccfbf1;
            color: #0d9488;
            display: inline-block;
        }}

        .icon-wrapper {{
            width: 48px;
            height: 48px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            background: linear-gradient(135deg, #f3f4f6 0%, #e5e7eb 100%);
            border-radius: 12px;
            margin-bottom: 12px;
            font-size: 24px;
        }}

        p {{
            color: #374151;
            line-height: 1.6;
            margin-bottom: 1rem;
        }}

        .footer {{
            text-align: center;
            color: #6b7280;
            font-size: 0.9rem;
            margin-top: 2rem;
            padding-top: 1rem;
            border-top: 1px solid #e2e8f0;
        }}
    </style>
</head>
<body>
    <div class=""glass-card"">
        <div class=""icon-wrapper"">🚚</div>
        <h1 class=""section-title"">Your Order is On the Way!</h1>
        <p>Dear {firstName} {surname},</p>
        <p>Your order from <strong>{shopName}</strong> is zooming to you! 🎉</p>
        <div class=""order-item"">
            <p><strong>Order Number:</strong> {orderNumber}</p>
            <p><strong>Delivery Address:</strong> {deliveryAddress} 📍</p>
            <p><strong>Driver:</strong> {driverName} 👨‍✈️</p>
            <p><strong>OTP:</strong> <span class=""status-badge"">{otp}</span> 🔐</p>
            <p>Please provide this OTP to the driver to verify delivery.</p>
        </div>
        <p>Your order should arrive soon. Thank you for choosing BraamBowl! 😊</p>
        <div class=""footer"">
            <p>Happy Eating! 🍽️</p>
            <p>&copy; 2025 BraamBowl. All rights reserved.</p>
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










        [HttpGet]
        [Route("OrderConfirmation/{orderId}")]
        public async Task<IActionResult> OrderConfirmation(int orderId)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");

            // Get current employee ID from session/authentication
            int employeeId = GetCurrentEmployeeId(); // You'll need to implement this

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Get order details
                Order order = null;
                using (SqlCommand cmd = new SqlCommand(@"
            SELECT o.Order_ID, o.Employee_ID, o.Shop_ID, o.OrderDate, o.TotalAmount, o.Status, s.Shop_Name
            FROM Orders o
            INNER JOIN Shops s ON o.Shop_ID = s.Shop_ID
            WHERE o.Order_ID = @OrderId AND o.Employee_ID = @EmployeeId", connection))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                    cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            order = new Order
                            {
                                Order_ID = reader.GetInt32(0),
                                Employee_ID = reader.GetInt32(1),
                                Shop_ID = reader.GetInt32(2),
                                Order_Date = reader.GetDateTime(3),
                                Amount = reader.GetDecimal(4),
                                Status = reader.GetString(5)
                            };
                            ViewBag.ShopName = reader.GetString(6);
                        }
                    }
                }

                if (order == null)
                {
                    return NotFound("Order not found.");
                }

                // Get order items
                List<OrderItem> orderItems = new List<OrderItem>();
                using (SqlCommand cmd = new SqlCommand(@"
            SELECT oi.OrderItem_ID, oi.Order_ID, oi.MenuItem_ID, oi.Quantity, oi.UnitPriceAtTimeOfOrder, mi.Item_Name
            FROM OrderItems oi
            INNER JOIN MenuItems mi ON oi.MenuItem_ID = mi.MenuItem_ID
            WHERE oi.Order_ID = @OrderId", connection))
                {
                    cmd.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orderItems.Add(new OrderItem
                            {
                                Order_Item_ID = reader.GetInt32(0),
                                Order_Id = reader.GetInt32(1),
                                Menu_Item_Id = reader.GetInt32(2),
                                Quantity = reader.GetInt32(3),
                                Unit_Price_At_Time_Of_Order = reader.GetDecimal(4)
                            });
                        }
                    }
                }

                ViewBag.OrderItems = orderItems;
                return View(order);
            }
        }

        private int GetCurrentEmployeeId()
        {
            if (User.Identity.IsAuthenticated)
            {
                string employeeIdString = User.Identity.Name; // Assumes EmployeeId is stored in Name claim
                if (int.TryParse(employeeIdString, out int employeeId))
                {
                    return employeeId;
                }
                throw new InvalidOperationException("Employee ID is not a valid integer.");
            }
            throw new InvalidOperationException("User is not authenticated.");
        }
        private async Task<Shop> GetShopById(int shopId)
        {
            const string query = @"
            SELECT [Shop_ID], [Shop_Name], [Address], [Contact_Number], [Email], [IsActive]
            FROM [BraamBowlOrderDatabase].[dbo].[Shops]
            WHERE [Shop_ID] = @ShopId AND [IsActive] = 1";

            try
            {
                var connectionString = _config.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Shop
                    {
                        Shop_ID = reader.GetInt32("Shop_ID"),
                        Shop_Name = reader.GetString("Shop_Name"),
                        Address = reader.GetString("Address"),
                        Contact_Number = reader.GetString("Contact_Number"),
                        Email = reader.IsDBNull("Email") ? null : reader.GetString("Email"),
                        IsActive = reader.GetBoolean("IsActive")
                    };
                }
                return null;
            }
            catch (SqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
                throw new Exception("Database error occurred while retrieving shop", ex);
            }
        }

        private async Task<List<MenuItem>> GetMenuItemsByShopId(int shopId)
        {
            var menuItems = new List<MenuItem>();
            const string query = @"
            SELECT [MenuItem_ID]
      ,[Item_Name]
      ,[Quantity]
      ,[Price]
      ,[Shop_ID]
      ,[Category]
      ,[Description]
      ,[Tags]
            FROM [BraamBowlOrderDatabase].[dbo].[MenuItems]
            WHERE [Shop_ID] = @ShopId";

            try
            {
                var connectionString = _config.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ShopId", shopId);

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    menuItems.Add(new MenuItem
                    {
                        MenuItem_ID = reader.GetInt32("MenuItem_ID"),
                        Item_Name = reader.GetString("Item_Name"),
                        Price = reader.GetDecimal("Price")
                    });
                }
            }
            catch (SqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"SQL Error: {ex.Message}");
                throw new Exception("Database error occurred while retrieving menu items", ex);
            }

            return menuItems;
        }

      
        private async Task<IActionResult> ReloadMenuView(int shopId, OrderModel model)
        {
            var shop = await GetShopById(shopId);
            if (shop == null || !shop.IsActive)
            {
                return NotFound();
            }
            var menuItems = await GetMenuItemsByShopId(shopId);
            model.Items = menuItems.Select(m => new OrderItemModel
            {
                MenuItemId = m.MenuItem_ID,
                Name = m.Item_Name,
                Price = m.Price
            }).ToList();
            ViewBag.ShopId = shopId;
            ViewBag.ShopName = shop.Shop_Name;
            ViewBag.Categories = menuItems.Select(m => m.Category).Distinct().ToList();
            return View("Menu", model);
        }

        //---------------------------------------------------------------------------------

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
