using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlightSystem.Server.Migrations
{
    /// <inheritdoc />
    public partial class CreateFlightSystemTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aircraft",
                columns: table => new
                {
                    AircraftId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    ManufacturedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.AircraftId);
                });

            migrationBuilder.CreateTable(
                name: "AirlineStaff",
                columns: table => new
                {
                    StaffId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Department = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    HiredDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirlineStaff", x => x.StaffId);
                });

            migrationBuilder.CreateTable(
                name: "Passengers",
                columns: table => new
                {
                    PassportNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PassportExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Nationality = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passengers", x => x.PassportNumber);
                });

            migrationBuilder.CreateTable(
                name: "Flights",
                columns: table => new
                {
                    FlightNumber = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Destination = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    DepartureTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArrivalTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AircraftId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.FlightNumber);
                    table.ForeignKey(
                        name: "FK_Flights_Aircraft_AircraftId",
                        column: x => x.AircraftId,
                        principalTable: "Aircraft",
                        principalColumn: "AircraftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckInCounters",
                columns: table => new
                {
                    CounterId = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    StaffId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Terminal = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInCounters", x => x.CounterId);
                    table.ForeignKey(
                        name: "FK_CheckInCounters_AirlineStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AirlineStaff",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    SeatId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AircraftId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FlightNumber = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SeatNumber = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Class = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsWindow = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAisle = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEmergencyExit = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
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
                    table.ForeignKey(
                        name: "FK_Seats_Flights_FlightNumber",
                        column: x => x.FlightNumber,
                        principalTable: "Flights",
                        principalColumn: "FlightNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingReference = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PassportNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FlightNumber = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SeatId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BookingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CheckInStaff = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CheckInCounter = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    PaymentStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
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
                        name: "FK_Bookings_Passengers_PassportNumber",
                        column: x => x.PassportNumber,
                        principalTable: "Passengers",
                        principalColumn: "PassportNumber",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "SeatId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BoardingPasses",
                columns: table => new
                {
                    BoardingPassId = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    BookingReference = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Gate = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    BoardingTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    IssuedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsPrinted = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PrintedBy = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
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
                    CheckInId = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    BookingReference = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CounterId = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    StaffId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    CheckInTime = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    CheckInMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInRecords", x => x.CheckInId);
                    table.ForeignKey(
                        name: "FK_CheckInRecords_AirlineStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AirlineStaff",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.SetNull);
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
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Aircraft",
                columns: new[] { "AircraftId", "Capacity", "CreatedAt", "IsActive", "ManufacturedDate", "Manufacturer", "Model" },
                values: new object[,]
                {
                    { "AC001", 189, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1124), true, new DateTime(2020, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Boeing", "Boeing 737-800" },
                    { "AC002", 180, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1129), true, new DateTime(2019, 6, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Airbus", "Airbus A320" },
                    { "AC003", 350, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1132), true, new DateTime(2018, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "Boeing", "Boeing 777-300ER" }
                });

            migrationBuilder.InsertData(
                table: "AirlineStaff",
                columns: new[] { "StaffId", "CreatedAt", "Department", "Email", "HiredDate", "IsActive", "LastLoginAt", "Name", "PasswordHash", "Phone", "Position", "UpdatedAt", "Username" },
                values: new object[,]
                {
                    { "ST001", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1408), "IT", "admin@flightsystem.com", new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Админ Систем", "$2a$11$dummy.hash.for.development.only", "+976-99000001", "System Administrator", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1409), "admin" },
                    { "ST002", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1413), "Ground Services", "checkin1@flightsystem.com", new DateTime(2023, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Бүртгэлийн Ажилтан 1", "$2a$11$dummy.hash.for.development.only", "+976-99000002", "Check-in Agent", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1414), "checkin1" }
                });

            migrationBuilder.InsertData(
                table: "CheckInCounters",
                columns: new[] { "CounterId", "ClosedAt", "CreatedAt", "Location", "OpenedAt", "StaffId", "Status", "Terminal", "UpdatedAt" },
                values: new object[] { "C002", null, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1444), "Terminal 1, Gate A6-A10", null, null, 0, "T1", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1444) });

            migrationBuilder.InsertData(
                table: "Passengers",
                columns: new[] { "PassportNumber", "Address", "CreatedAt", "DateOfBirth", "Email", "FirstName", "Gender", "LastName", "Nationality", "PassportExpiryDate", "Phone", "UpdatedAt" },
                values: new object[,]
                {
                    { "MN11223344", "Darkhan, Mongolia", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1305), new DateTime(1988, 12, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "tomor@email.com", "Төмөр", "Male", "Бат", "Mongolian", new DateTime(2031, 12, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "+976-99778899", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1306) },
                    { "MN12345678", "Ulaanbaatar, Mongolia", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1296), new DateTime(1985, 5, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "batbold@email.com", "Батболд", "Male", "Мөнх", "Mongolian", new DateTime(2030, 5, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "+976-99112233", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1297) },
                    { "MN87654321", "Ulaanbaatar, Mongolia", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1301), new DateTime(1990, 8, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "sarantuya@email.com", "Сарантуяа", "Female", "Болд", "Mongolian", new DateTime(2029, 8, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "+976-99445566", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1302) }
                });

            migrationBuilder.InsertData(
                table: "CheckInCounters",
                columns: new[] { "CounterId", "ClosedAt", "CreatedAt", "Location", "OpenedAt", "StaffId", "Status", "Terminal", "UpdatedAt" },
                values: new object[] { "C001", null, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1441), "Terminal 1, Gate A1-A5", new DateTime(2025, 5, 27, 16, 33, 15, 801, DateTimeKind.Local).AddTicks(1437), "ST002", 1, "T1", new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1442) });

            migrationBuilder.InsertData(
                table: "Flights",
                columns: new[] { "FlightNumber", "AircraftId", "ArrivalTime", "CreatedAt", "CreatedBy", "DepartureTime", "Destination", "Origin", "Status", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { "MR101", "AC001", new DateTime(2025, 5, 27, 13, 0, 0, 0, DateTimeKind.Local), new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1266), "System", new DateTime(2025, 5, 27, 11, 0, 0, 0, DateTimeKind.Local), "PEK", "ULN", 0, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1267), "System" },
                    { "MR102", "AC002", new DateTime(2025, 5, 27, 16, 0, 0, 0, DateTimeKind.Local), new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1270), "System", new DateTime(2025, 5, 27, 13, 0, 0, 0, DateTimeKind.Local), "ICN", "ULN", 0, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1271), "System" },
                    { "MR103", "AC001", new DateTime(2025, 5, 27, 17, 0, 0, 0, DateTimeKind.Local), new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1274), "System", new DateTime(2025, 5, 27, 15, 0, 0, 0, DateTimeKind.Local), "ULN", "PEK", 3, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1274), "System" }
                });

            migrationBuilder.InsertData(
                table: "Bookings",
                columns: new[] { "BookingReference", "BookingDate", "CheckInCounter", "CheckInStaff", "CheckInTime", "CreatedAt", "FlightNumber", "PassportNumber", "PaymentStatus", "SeatId", "Status", "TotalPrice", "UpdatedAt" },
                values: new object[,]
                {
                    { "BK001", new DateTime(2025, 5, 20, 18, 33, 15, 801, DateTimeKind.Local).AddTicks(1324), null, null, null, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1332), "MR101", "MN12345678", 1, null, 0, 200m, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1332) },
                    { "BK002", new DateTime(2025, 5, 22, 18, 33, 15, 801, DateTimeKind.Local).AddTicks(1334), null, null, null, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1335), "MR101", "MN87654321", 1, null, 0, 200m, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1336) },
                    { "BK003", new DateTime(2025, 5, 24, 18, 33, 15, 801, DateTimeKind.Local).AddTicks(1338), null, null, null, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1339), "MR102", "MN11223344", 1, null, 0, 200m, new DateTime(2025, 5, 27, 10, 33, 15, 801, DateTimeKind.Utc).AddTicks(1339) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirlineStaff_Email",
                table: "AirlineStaff",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AirlineStaff_Username",
                table: "AirlineStaff",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardingPasses_BookingReference",
                table: "BoardingPasses",
                column: "BookingReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Passenger_Flight",
                table: "Bookings",
                columns: new[] { "PassportNumber", "FlightNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Status",
                table: "Bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_FlightNumber",
                table: "Bookings",
                column: "FlightNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SeatId",
                table: "Bookings",
                column: "SeatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckInCounters_StaffId",
                table: "CheckInCounters",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInRecord_Time",
                table: "CheckInRecords",
                column: "CheckInTime");

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
                name: "IX_Flight_DepartureTime",
                table: "Flights",
                column: "DepartureTime");

            migrationBuilder.CreateIndex(
                name: "IX_Flight_Status",
                table: "Flights",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights",
                column: "AircraftId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_Email",
                table: "Passengers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Seat_Flight_Available",
                table: "Seats",
                columns: new[] { "FlightNumber", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_Seat_Number",
                table: "Seats",
                column: "SeatNumber");

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
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "CheckInCounters");

            migrationBuilder.DropTable(
                name: "Passengers");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "AirlineStaff");

            migrationBuilder.DropTable(
                name: "Flights");

            migrationBuilder.DropTable(
                name: "Aircraft");
        }
    }
}
