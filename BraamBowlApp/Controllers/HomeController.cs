using BraamBowlApp.Data;
using BraamBowlApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace BraamBowlApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _DB;
        

        public HomeController(ILogger<HomeController> logger, IConfiguration config, ApplicationDbContext DB)
        {
            _logger = logger;
            _config = config;
            _DB = DB;
           
        }


        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "User not authenticated.";
                return Redirect("~/Identity/Account/Login");
            }

            return RedirectToAction("Dashboard");
        }


        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User not authenticated.";
                return RedirectToAction("Login", "Account");
            }

            string connectionString = _config.GetConnectionString("DefaultConnection");
            var model = new DashboardViewModel
            {
                Restuarant = new List<Restuarants>(),
                OrderHistory = new List<OrderView>(),
                CurrentOrders = new List<CurrentOrder>()
            };

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Fetch user data
                    string userQuery = @"
                    SELECT Employee_ID, Balance, Monthly_Deposit_Total, Last_Deposit_Month, HasSeenWelcomeModal 
                    FROM AspNetUsers 
                    WHERE Id = @UserId";
                    using (var userCommand = new SqlCommand(userQuery, connection))
                    {
                        userCommand.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await userCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int employeeIdIndex = reader.GetOrdinal("Employee_ID");
                                if (!reader.IsDBNull(employeeIdIndex))
                                {
                                    model.EmployeeId = reader.GetInt32(employeeIdIndex);
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "Employee ID not found for user.";
                                    return RedirectToAction("Login", "Account");
                                }

                                int balanceIndex = reader.GetOrdinal("Balance");
                                model.Balance = reader.IsDBNull(balanceIndex) ? 0 : reader.GetDecimal(balanceIndex);

                                int monthlyDepositIndex = reader.GetOrdinal("Monthly_Deposit_Total");
                                model.MonthlyDeposits = reader.IsDBNull(monthlyDepositIndex) ? 0 : reader.GetDecimal(monthlyDepositIndex);

                                int lastDepositDateIndex = reader.GetOrdinal("Last_Deposit_Month");
                                model.LastDepositDate = reader.IsDBNull(lastDepositDateIndex) ? null : reader.GetDateTime(lastDepositDateIndex);

                                int hasSeenModalIndex = reader.GetOrdinal("HasSeenWelcomeModal");
                                model.ShowWelcomeModal = reader.IsDBNull(hasSeenModalIndex) ? true : !reader.GetBoolean(hasSeenModalIndex);

                                model.LastDepositAmount = model.MonthlyDeposits;
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "User not found.";
                                return Unauthorized();
                            }
                        }
                    }

                    // Fetch shops and menu items in a single query
                    string shopMenuQuery = @"
                    SELECT s.Shop_ID, s.Shop_Name, m.MenuItem_ID, m.Item_Name, m.Category, m.Price
_DEPS_                    FROM Shops s
                    LEFT JOIN MenuItems m ON s.Shop_ID = m.Shop_ID
                    WHERE s.IsActive = 1
                    ORDER BY s.Shop_ID";
                    using (var command = new SqlCommand(shopMenuQuery, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
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
                    string orderHistoryQuery = @"
                    SELECT o.Order_ID, o.Order_Date, s.Shop_Name, oi.Quantity, m.Item_Name, oi.UnitPriceAtTimeOfOrder
                    FROM Orders o
                    JOIN OrderItems oi ON o.Order_ID = oi.Order_ID
                    JOIN MenuItems m ON oi.MenuItem_ID = m.MenuItem_ID
                    JOIN Shops s ON o.Shop_ID = s.Shop_ID
                    WHERE o.Employee_ID = @EmployeeId";
                    using (var orderCommand = new SqlCommand(orderHistoryQuery, connection))
                    {
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
                                    Amount = reader.GetInt32(reader.GetOrdinal("Quantity")) * reader.GetDecimal(reader.GetOrdinal("UnitPriceAtTimeOfOrder"))
                                });
                            }
                        }
                    }

                    // Fetch current orders
                    string currentOrderQuery = @"
                    SELECT o.Order_ID, o.Order_Date, s.Shop_Name, o.Status, m.Item_Name
                    FROM Orders o
                    JOIN OrderItems oi ON o.Order_ID = oi.Order_ID
                    JOIN MenuItems m ON oi.MenuItem_ID = m.MenuItem_ID
                    JOIN Shops s ON o.Shop_ID = s.Shop_ID
                    WHERE o.Employee_ID = @EmployeeId
                    AND o.Status IN ('Pending', 'Preparing')";
                    using (var currentOrderCommand = new SqlCommand(currentOrderQuery, connection))
                    {
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
                }

                // Calculate stats
                model.TotalDeposited = model.MonthlyDeposits;
                model.CompanyMatch = model.MonthlyDeposits * 2;
                model.TotalSpent = model.OrderHistory.Sum(o => o.Amount);
                model.OrdersPlaced = model.OrderHistory.Count;
                model.MonthlyCompanyMatch = model.MonthlyDeposits * 2;
                model.AverageOrderAmount = model.OrderHistory.Any() ? model.OrderHistory.Average(o => o.Amount) : 0;

                return View(model);
            }
            catch (Exception ex)
            {
                // Log the exception (implement logging as needed, e.g., using ILogger)
                TempData["ErrorMessage"] = $"An error occurred while loading the dashboard: {ex.Message}";
                return View(model); // Return partial model to avoid complete failure
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
        public async Task<IActionResult> Deposit(DepositModel model)
        {
            if (ModelState.IsValid)
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

                   
                    string selectQuery = "SELECT Employee_ID, Monthly_Deposit_Total, Last_Deposit_Month, Balance FROM AspNetUsers WHERE Id = @UserId";
                    using (var selectCommand = new SqlCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await selectCommand.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                return Unauthorized(); 
                            }


                            int? employeeId = reader.IsDBNull(reader.GetOrdinal("Employee_ID"))? (int?)null: reader.GetInt32(reader.GetOrdinal("Employee_ID"));

                            decimal? monthlyDepositTotal = reader.IsDBNull(reader.GetOrdinal("Monthly_Deposit_Total"))? null: reader.GetDecimal(reader.GetOrdinal("Monthly_Deposit_Total"));

                            DateTime? lastDepositMonth = reader.IsDBNull(reader.GetOrdinal("Last_Deposit_Month"))? (DateTime?)null: reader.GetDateTime(reader.GetOrdinal("Last_Deposit_Month"));

                            decimal? balance = reader.IsDBNull(reader.GetOrdinal("Balance"))? (decimal?)null: reader.GetDecimal(reader.GetOrdinal("Balance"));



                            if (model.DepositAmount <= 0)
                            {
                                ModelState.AddModelError("DepositAmount", "The Deposit Amount must be positive");
                                return View();
                            }

                            
                            var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                            if (lastDepositMonth == null || lastDepositMonth < currentMonth)
                            {
                                monthlyDepositTotal = 0;
                                lastDepositMonth = currentMonth;
                            }

                            var previousTotal = monthlyDepositTotal;
                            var newTotal = monthlyDepositTotal + model.DepositAmount;

                            var previousBonusAmount = (int)Math.Floor((previousTotal ?? 0) / 250);
                           
                            var newBonusAmount = (int)Math.Floor((newTotal ?? 0) / 250);

                            var companyCredit = (newBonusAmount - previousBonusAmount) * 500m;

                         
                            monthlyDepositTotal = newTotal;
                            balance = model.DepositAmount + companyCredit;

                            
                            reader.Close();

                            
                            string updateQuery = @"
                        UPDATE AspNetUsers 
                        SET Monthly_Deposit_Total = @NewTotal, 
                            Last_Deposit_Month = @LastDepositMonth, 
                            Balance = @Balance 
                        WHERE Id = @UserId";
                            using (var updateCommand = new SqlCommand(updateQuery, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@NewTotal", (object?)monthlyDepositTotal ?? DBNull.Value);
                                updateCommand.Parameters.AddWithValue("@LastDepositMonth", (object?)lastDepositMonth ?? DBNull.Value);
                                updateCommand.Parameters.AddWithValue("@Balance", (object?)balance ?? DBNull.Value);

                                updateCommand.Parameters.AddWithValue("@UserId", userId);
                                await updateCommand.ExecuteNonQueryAsync();
                            }

                            
                            string insertQuery = @"
                        INSERT INTO Payments (Employee_ID, DepositAmount, CompanyCredit, Payment_Method, Payment_Date, IsSuccessful)
                        VALUES (@EmployeeId, @DepositAmount, @CompanyCredit, @PaymentMethod, @PaymentDate, @IsSuccessful)";
                            using (var insertCommand = new SqlCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@EmployeeId", employeeId);
                                insertCommand.Parameters.AddWithValue("@DepositAmount", model.DepositAmount);
                                insertCommand.Parameters.AddWithValue("@CompanyCredit", companyCredit);
                                insertCommand.Parameters.AddWithValue("@PaymentMethod", model.PaymentMethod);
                                insertCommand.Parameters.AddWithValue("@PaymentDate", DateTime.Now);
                                insertCommand.Parameters.AddWithValue("@IsSuccessful", true);
                                await insertCommand.ExecuteNonQueryAsync();
                            }

                            TempData["SuccessMessage"] = $"Successfully deposited R{model.DepositAmount}. Company added R{companyCredit}. New balance: R{balance}.";
                            return RedirectToAction("Balance");
                        }
                    }
                }
            }

            return View(model);
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

     

        
        public async Task<IActionResult> Checkout1(int shopId, List<string> cartItems)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");

            var identityUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(identityUserId))
            {
                TempData["ErrorMessage"] = "User not authenticated.";
                return RedirectToAction("Login", "Account");
            }

            int employeeId;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Get Employee_ID using Identity User ID
                    using (SqlCommand getEmpCmd = new SqlCommand("SELECT Employee_ID FROM AspNetUsers WHERE Id = @UserId", connection))
                    {
                        getEmpCmd.Parameters.Add("@UserId", SqlDbType.NVarChar).Value = identityUserId;
                        var result = await getEmpCmd.ExecuteScalarAsync();

                        if (result == null || !int.TryParse(result.ToString(), out employeeId))
                        {
                            TempData["ErrorMessage"] = "Employee ID not found.";
                            return RedirectToAction("Login", "Account");
                        }
                    }

                    // Parse and process cart items
                    var parsedCartItems = new List<CartItem>();
                    var cartItemGroups = new Dictionary<int, CartItem>();

                    foreach (var cartItemJson in cartItems)
                    {
                        var cartItem = System.Text.Json.JsonSerializer.Deserialize<CartItem>(cartItemJson);
                        if (cartItem == null)
                        {
                            TempData["ErrorMessage"] = "Invalid cart item data.";
                            return RedirectToAction("Menu", new { id = shopId });
                        }

                        if (cartItemGroups.ContainsKey(cartItem.MenuItemId))
                        {
                            cartItemGroups[cartItem.MenuItemId].Quantity++;
                        }
                        else
                        {
                            cartItem.Quantity = 1;
                            cartItemGroups[cartItem.MenuItemId] = cartItem;
                        }
                    }

                    parsedCartItems = cartItemGroups.Values.ToList();

                    // Calculate TotalPrice for each cart item
                    foreach (var item in parsedCartItems)
                    {
                        item.TotalPrice = item.Quantity * item.Price; // Ensure TotalPrice is calculated
                    }

                    // Get shop details
                    Shop shop = null;
                    using (SqlCommand shopCmd = new SqlCommand("SELECT Shop_ID, Shop_Name FROM Shops WHERE Shop_ID = @ShopId AND IsActive = 1", connection))
                    {
                        shopCmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = shopId;
                        using (SqlDataReader reader = await shopCmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                shop = new Shop
                                {
                                    Shop_ID = reader.GetInt32(0),
                                    Shop_Name = reader.GetString(1)
                                };
                            }
                        }
                    }

                    if (shop == null)
                    {
                        TempData["ErrorMessage"] = "Shop not found or inactive.";
                        return NotFound("Shop not found or inactive.");
                    }

                    // Get employee details
                    User employee = null;
                    using (SqlCommand empCmd = new SqlCommand("SELECT Employee_ID, Surname, First_Name, Balance FROM AspNetUsers WHERE Employee_ID = @EmployeeId", connection))
                    {
                        empCmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                        using (SqlDataReader reader = await empCmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                employee = new User
                                {
                                    Employee_ID = reader.GetInt32(0),
                                    Surname = reader.GetString(1),
                                    First_Name = reader.GetString(2),
                                    Balance = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3)
                                };
                            }
                        }
                    }

                    if (employee == null)
                    {
                        TempData["ErrorMessage"] = "Employee not found.";
                        return NotFound("Employee not found.");
                    }

                    // Calculate total amount
                    decimal totalAmount = parsedCartItems.Sum(item => item.TotalPrice);

                    // Check if balance is sufficient
                    if (employee.Balance < totalAmount)
                    {
                        TempData["ErrorMessage"] = $"Insufficient balance. Your current balance is R{employee.Balance:F2}, but the order total is R{totalAmount:F2}.";
                        return RedirectToAction("Menu", new { id = shopId });
                    }

                    var checkoutViewModel = new CheckoutViewModel
                    {
                        ShopId = shopId,
                        Shop_Name = shop.Shop_Name,
                        CartItems = parsedCartItems,
                        TotalAmount = totalAmount,
                        EmployeeBalance = employee.Balance,
                        EmployeeName = employee.First_Name
                    };

                    return View(checkoutViewModel);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while processing your request: {ex.Message}";
                return RedirectToAction("Menu", new { id = shopId });
            }
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

            // Get shop name for display
            string connectionString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("SELECT Shop_Name FROM Shops WHERE Shop_ID = @ShopId AND IsActive = 1", connection))
                {
                    cmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = shopId;
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            model.Shop_Name = reader.GetString(0);
                        }
                        else
                        {
                            return NotFound("Shop not found or inactive.");
                        }
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        [Route("Checkout/PlaceOrder/{shopId}")]
        public async Task<IActionResult> PlaceOrder(int shopId, CheckoutViewModel model)
        {
            //if (!ModelState.IsValid || model.Items == null || !model.Items.Any())
            //{
            //    TempData["ErrorMessage"] = "Invalid order data.";
            //    return RedirectToAction("Home", "Restaurants", new { id = shopId });
            //}

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

                        // Create Order
                        int orderId;
                        using (SqlCommand cmd = new SqlCommand(
                            @"INSERT INTO Orders (Employee_ID, Shop_ID, Order_Date, Amount, Status)
                          OUTPUT INSERTED.Order_ID
                          VALUES (@EmployeeId, @ShopId, @OrderDate, @TotalAmount, @Status)",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                            cmd.Parameters.Add("@ShopId", SqlDbType.Int).Value = shopId;
                            cmd.Parameters.Add("@OrderDate", SqlDbType.DateTime).Value = DateTime.Now;
                            cmd.Parameters.Add("@TotalAmount", SqlDbType.Decimal).Value = totalAmount;
                            cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = "Pending";
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

                        // Update employee balance
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE AspNetUsers SET Balance = Balance - @TotalAmount WHERE Employee_ID = @EmployeeId",
                            connection, transaction))
                        {
                            cmd.Parameters.Add("@TotalAmount", SqlDbType.Decimal).Value = totalAmount;
                            cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        // Clear cart
                        TempData["ClearCart"] = true;
                        TempData["SuccessMessage"] = "Order placed successfully!";
                        return RedirectToAction("Home", "Restaurants", new { id = shopId });
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
