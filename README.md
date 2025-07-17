# BraamBowlApp# Employee Cafeteria Credit & Ordering System

## Overview
This project is a web application developed using ASP.NET Core to manage an employee cafeteria credit and ordering system. It allows employees to deposit funds, receive company bonuses, browse restaurant menus, place orders, and track delivery statuses. The application uses Entity Framework Core with SQL Server LocalDB for data persistence and implements basic CRUD operations for employee accounts, restaurants, and menu items.

## Prerequisites
- .NET 8.0 SDK or later
- SQL Server LocalDB
- Visual Studio 2022 or another compatible IDE
- Git for version control

## Setup Instructions
1. **Clone the Repository**:
   ```bash
   git clone <repository-url>
   cd EmployeeCafeteriaSystem
   ```

2. **Restore Dependencies**:
   Run the following command to restore NuGet packages:
   ```bash
   dotnet restore
   ```

3. **Configure Database**:
   - Ensure SQL Server LocalDB is installed.
   - Update the connection string in `appsettings.json` if necessary:
     ```json
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CafeteriaDb;Trusted_Connection=True;MultipleActiveResultSets=true"
     }
     ```
   - Apply Entity Framework Core migrations to create the database:
     ```bash
     dotnet ef database update
     ```

4. **Run the Application**:
   Start the application using:
   ```bash
   dotnet run
   ```
   Alternatively, open the solution in Visual Studio and press `F5` to build and run.

5. **Access the Application**:
   Open a web browser and navigate to `https://localhost:5001` (or the port specified in the console output).

## Registration Of Employee
- **Employee Account Management**:
- I used Sap Number and Work Email to register- When you test the App please register with the Work Email(e.g JabulaniSib@joburg.org.za)
- So that you can be able to get the emails of Welcome Email that you can use to login. but the app automatically send you to landing page 

## Features
- **Employee Account Management**:
  - Create, view, and manage employee accounts with details like name, employee number, and balance.
- **Deposits and Bonuses**:
  - Employees can deposit funds, with a R500 bonus applied for every R250 deposited in a given month.
  - Tracks last deposit month to reset bonus calculations.
  - To complete the Yoco Payment the card number is **4242 4242 4242 4242**and you can use any **month/yyyy** and **123** security number
    
- **Restaurant and Menu Management**:
  - Admin CRUD operations for restaurants and their menu items.
  - Displays restaurant details and associated menus.
- **Ordering System**:
  - Employees can browse restaurants, view menus, select items, and place orders.
  - Validates sufficient balance before order placement and deducts the total amount.
- **Order Tracking**:
  - Employees can view their order history and current order status.
  - Admins can update order statuses (e.g., Pending, Preparing, Delivering, Delivered).

## Usage
1. **Employee Actions**:
   - Navigate to the "Employees" section to view or create accounts.
   - Use the "Deposit" page to add funds and receive bonuses.
   - Browse restaurants, select menu items, and place orders from the restaurant menu page.
   - Check order status under the "Orders" section.
2. **Admin Actions**:
   - Access the "Restaurants" section to manage restaurant details and menus.
   - Update order statuses in the admin order management view.

## Notes
- The application uses MVC architecture for clear separation of concerns.
- Entity Framework Core handles database operations with proper migrations.
- Basic error handling and input validation are implemented (e.g., positive deposit amounts, sufficient balance checks).
- The UI is minimal but functional, with potential for enhancement via CSS frameworks or JavaScript libraries.

## Troubleshooting
- **Database Issues**: Ensure LocalDB is running and the connection string is correct. Re-run migrations if needed.
- **Port
-  "Smtp": {
   "Host": "168.89.180.38",
   "Port": 25,
   "EnableSsl": false,
   "Username": "braamBowl@joburg.org.za",
   "Password": "BraamBowl"
 }, Are hosted local which is internal server
