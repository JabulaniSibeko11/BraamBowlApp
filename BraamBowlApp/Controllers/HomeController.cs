using BraamBowlApp.Data;
using BraamBowlApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BraamBowlApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _DB;
        private readonly UserManager<User> _userManager;

        public HomeController(ILogger<HomeController> logger, UserManager<User> userManager, IConfiguration config,ApplicationDbContext DB)
        {
            _logger = logger;
            _config = config;
            _DB = DB;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
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
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                   return Unauthorized();
                }

                //Validate deposit amount 
                if (model.DepositAmount <= 0) {

                    ModelState.AddModelError("DepositAmount", "The Deposit Amount must be positive");
                    return View();
                }

                //Determine the month and check if its a new month 
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (user.Last_Deposit_Month == null || user.Last_Deposit_Month < currentMonth) {
                    user.Monthly_Deposit_Total = 0;
                    user.Last_Deposit_Month = currentMonth;
                }

                var previous_Total= user.Monthly_Deposit_Total;

                var new_Total = user.Monthly_Deposit_Total+ model.DepositAmount;

                var previous_Bonus_Amount = (int)Math.Floor(previous_Total / 250);

                var new_Bonus_Amount = (int)Math.Floor(new_Total / 250);

                var company_Credit = (new_Bonus_Amount - previous_Bonus_Amount) * 500m;

                //update user data
                user.Monthly_Deposit_Total = new_Total;
                user.Balance += model.DepositAmount + company_Credit;

                var payment = new Payment
                {
                    Employee_ID = user.Employee_ID,
                    DepositAmount = model.DepositAmount,
                    CompanyCredit = company_Credit,
                    Payment_Method=model.PaymentMethod,
                    Payment_Date=DateTime.Now,
                    IsSuccessful=true,
                };
                _DB.Payments.Add(payment);
                await _DB.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Successfully deposited R{model.DepositAmount}. Company added R{company_Credit}. New balance: R{user.Balance}.";
                return RedirectToAction("Balance");
            }

            return View(model); 
        }

        [HttpGet]
        public async Task<IActionResult> GetBalance() {
        
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            { 
            return Unauthorized();

            }

            ViewBag.Balance = user.Balance;
            ViewBag.Employee_Id=user.Employee_ID;
        return View();
        }
        //---------------------------------------------------------------------------------



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
