using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aircraft",
                columns: table => new
                {
                    AircraftId = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    ManufacturedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.AircraftId);
                });

            migrationBuilder.CreateTable(
                name: "AirlineStaff",
                columns: table => new
                {
                    StaffId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirlineStaff", x => x.StaffId);
                });

            migrationBuilder.CreateTable(
                name: "Passengers",
                columns: table => new
                {
                    PassengerId = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    PassportNumber = table.Column<string>(type: "TEXT", nullable: false),
                    PassportExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passengers", x => x.PassengerId);
                });

            migrationBuilder.CreateTable(
                name: "Flights",
                columns: table => new
                {
                    FlightNumber = table.Column<string>(type: "TEXT", nullable: false),
                    AircraftId = table.Column<string>(type: "TEXT", nullable: false),
                    DepartureTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArrivalTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.FlightNumber);
                    table.ForeignKey(
                        name: "FK_Flights_Aircraft_AircraftId",
                        column: x => x.AircraftId,
                        principalTable: "Aircraft",
                        principalColumn: "AircraftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    SeatId = table.Column<string>(type: "TEXT", nullable: false),
                    AircraftId = table.Column<string>(type: "TEXT", nullable: false),
                    SeatNumber = table.Column<string>(type: "TEXT", nullable: false),
                    SeatClass = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.SeatId);
                    table.ForeignKey(
                        name: "FK_Seats_Aircraft_AircraftId",
                        column: x => x.AircraftId,
                        principalTable: "Aircraft",
                        principalColumn: "AircraftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CheckInCounters",
                columns: table => new
                {
                    CounterId = table.Column<string>(type: "TEXT", nullable: false),
                    StaffId = table.Column<string>(type: "TEXT", nullable: false),
                    Terminal = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInCounters", x => x.CounterId);
                    table.ForeignKey(
                        name: "FK_CheckInCounters_AirlineStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AirlineStaff",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingReference = table.Column<string>(type: "TEXT", nullable: false),
                    PassengerId = table.Column<string>(type: "TEXT", nullable: false),
                    FlightNumber = table.Column<string>(type: "TEXT", nullable: false),
                    BookingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckInStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingReference);
                    table.ForeignKey(
                        name: "FK_Bookings_Flights_FlightNumber",
                        column: x => x.FlightNumber,
                        principalTable: "Flights",
                        principalColumn: "FlightNumber",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Passengers_PassengerId",
                        column: x => x.PassengerId,
                        principalTable: "Passengers",
                        principalColumn: "PassengerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardingPasses",
                columns: table => new
                {
                    BoardingPassId = table.Column<string>(type: "TEXT", nullable: false),
                    BookingReference = table.Column<string>(type: "TEXT", nullable: false),
                    Gate = table.Column<string>(type: "TEXT", nullable: false),
                    BoardingTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardingPasses", x => x.BoardingPassId);
                    table.ForeignKey(
                        name: "FK_BoardingPasses_Bookings_BookingReference",
                        column: x => x.BookingReference,
                        principalTable: "Bookings",
                        principalColumn: "BookingReference",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CheckInRecords",
                columns: table => new
                {
                    CheckInId = table.Column<string>(type: "TEXT", nullable: false),
                    BookingReference = table.Column<string>(type: "TEXT", nullable: false),
                    CounterId = table.Column<string>(type: "TEXT", nullable: false),
                    StaffId = table.Column<string>(type: "TEXT", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckInMethod = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInRecords", x => x.CheckInId);
                    table.ForeignKey(
                        name: "FK_CheckInRecords_AirlineStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AirlineStaff",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInRecords_Bookings_BookingReference",
                        column: x => x.BookingReference,
                        principalTable: "Bookings",
                        principalColumn: "BookingReference",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInRecords_CheckInCounters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "CheckInCounters",
                        principalColumn: "CounterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeatAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<string>(type: "TEXT", nullable: false),
                    BookingReference = table.Column<string>(type: "TEXT", nullable: false),
                    SeatId = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatAssignments", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_SeatAssignments_Bookings_BookingReference",
                        column: x => x.BookingReference,
                        principalTable: "Bookings",
                        principalColumn: "BookingReference",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeatAssignments_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "SeatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardingPasses_BookingReference",
                table: "BoardingPasses",
                column: "BookingReference");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_FlightNumber",
                table: "Bookings",
                column: "FlightNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PassengerId",
                table: "Bookings",
                column: "PassengerId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInCounters_StaffId",
                table: "CheckInCounters",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInRecords_BookingReference",
                table: "CheckInRecords",
                column: "BookingReference");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInRecords_CounterId",
                table: "CheckInRecords",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInRecords_StaffId",
                table: "CheckInRecords",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights",
                column: "AircraftId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatAssignments_BookingReference",
                table: "SeatAssignments",
                column: "BookingReference");

            migrationBuilder.CreateIndex(
                name: "IX_SeatAssignments_SeatId",
                table: "SeatAssignments",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_AircraftId",
                table: "Seats",
                column: "AircraftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardingPasses");

            migrationBuilder.DropTable(
                name: "CheckInRecords");

            migrationBuilder.DropTable(
                name: "SeatAssignments");

            migrationBuilder.DropTable(
                name: "CheckInCounters");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "AirlineStaff");

            migrationBuilder.DropTable(
                name: "Flights");

            migrationBuilder.DropTable(
                name: "Passengers");

            migrationBuilder.DropTable(
                name: "Aircraft");
        }
    }
}
