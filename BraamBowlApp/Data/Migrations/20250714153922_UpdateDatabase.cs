using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BraamBowlApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompanyCredit",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "Last_Deposit_Month",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Monthly_Deposit_Total",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyCredit",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Last_Deposit_Month",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Monthly_Deposit_Total",
                table: "AspNetUsers");
        }
    }
}
