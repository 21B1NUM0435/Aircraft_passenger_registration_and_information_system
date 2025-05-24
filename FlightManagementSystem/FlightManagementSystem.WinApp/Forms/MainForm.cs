using FlightManagementSystem.WinApp.Models;
using FlightManagementSystem.WinApp.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlightManagementSystem.WinApp.Forms
{
    public partial class MainForm : Form
    {
        private readonly ApiService _apiService;
        private readonly string _staffId;
        private readonly string _staffName;
        private readonly string _counterId;

        // Fix nullable warnings by proper initialization
        private List<FlightDto> _flights = new();
        private List<SeatDto> _availableSeats = new();
        private BookingDto? _currentBooking;
        private HubConnection? _hubConnection;
        private WebSocketClient? _webSocketClient;

        // Seat visualization - Fix nullable warnings
        private Panel _seatMapPanel = new();
        private Dictionary<string, Button> _seatButtons = new();
        private string _selectedSeatId = "";

        // Connection status label - Fix nullable warning
        private Label lblConnectionStatus = new();

        private async void MainForm_Load(object sender, EventArgs e)
        {
            Console.WriteLine("🚀 MainForm loading with WebSocket...");

            await LoadFlightsAsync();

            // Initialize WebSocket client (instead of raw TCP socket)
            try
            {
                Console.WriteLine("🌐 Initializing WebSocket client...");
                _webSocketClient = new WebSocketClient("localhost", 8080); // Changed port to 8080

                // Register ALL event handlers before connecting
                _webSocketClient.OnSeatReserved += OnSeatReservedFromSocket;
                _webSocketClient.OnFlightStatusChanged += OnFlightStatusChangedFromSocket;
                _webSocketClient.OnSeatLocked += OnSeatLockedFromSocket;
                _webSocketClient.OnCheckInComplete += OnCheckInCompleteFromSocket;
                _webSocketClient.OnConnectionStatusChanged += OnConnectionStatusChanged;

                var connected = await _webSocketClient.ConnectAsync();
                if (connected)
                {
                    Console.WriteLine("✅ WebSocket client connected successfully");
                    UpdateConnectionStatus("WebSocket Connected", Color.Green);

                    // Subscribe to flight updates for current flight
                    if (cboFlights.SelectedItem is FlightDto selectedFlight)
                    {
                        await _webSocketClient.SubscribeToFlightAsync(selectedFlight.FlightNumber);
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to connect to WebSocket server");
                    MessageBox.Show("Could not connect to WebSocket server. Real-time updates may not work.",
                        "Connection Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    UpdateConnectionStatus("WebSocket Disconnected", Color.Red);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket client initialization error: {ex.Message}");
                MessageBox.Show($"Could not connect to WebSocket server: {ex.Message}", "Connection Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                UpdateConnectionStatus("WebSocket Error", Color.Red);
            }
        }

        private void CreateConnectionStatusLabel()
        {
            lblConnectionStatus = new Label
            {
                Text = "Disconnected",
                Location = new Point(12, 70),
                Size = new Size(200, 25),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblConnectionStatus);
        }

        private void CreateSeatMapPanel()
        {
            _seatMapPanel = new Panel
            {
                Size = new Size(700, 500),
                Location = new Point(12, 420),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
                BackColor = Color.White
            };

            this.Controls.Add(_seatMapPanel);

            // Add title label for seat map
            var titleLabel = new Label
            {
                Text = "Aircraft Seat Map - Select Available Seat",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(12, 395),
                Size = new Size(400, 25)
            };
            this.Controls.Add(titleLabel);
        }

        // CRITICAL: Add the missing seat visualization method
        private void CreateSeatVisualization()
        {
            _seatMapPanel.Controls.Clear();
            _seatButtons.Clear();

            if (_availableSeats.Count == 0)
            {
                var noSeatsLabel = new Label
                {
                    Text = "No available seats for this flight",
                    Location = new Point(20, 20),
                    Size = new Size(300, 30),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.Red
                };
                _seatMapPanel.Controls.Add(noSeatsLabel);
                return;
            }

            // Group seats by class and row
            var businessSeats = _availableSeats.Where(s => s.SeatClass == "Business").ToList();
            var economySeats = _availableSeats.Where(s => s.SeatClass == "Economy").ToList();

            int yOffset = 20;

            // Business Class Header
            if (businessSeats.Any())
            {
                var businessHeader = new Label
                {
                    Text = "Business Class",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Location = new Point(20, yOffset),
                    Size = new Size(150, 20),
                    ForeColor = Color.DarkBlue
                };
                _seatMapPanel.Controls.Add(businessHeader);
                yOffset += 30;

                // Create business class seats (2-2 configuration)
                yOffset = CreateSeatRows(businessSeats, yOffset, new[] { "A", "B", "C", "D" }, 2, Color.Gold);
                yOffset += 20; // Space between classes
            }

            // Economy Class Header
            if (economySeats.Any())
            {
                var economyHeader = new Label
                {
                    Text = "Economy Class",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Location = new Point(20, yOffset),
                    Size = new Size(150, 20),
                    ForeColor = Color.DarkGreen
                };
                _seatMapPanel.Controls.Add(economyHeader);
                yOffset += 30;

                // Create economy class seats (3-3 configuration)
                CreateSeatRows(economySeats, yOffset, new[] { "A", "B", "C", "D", "E", "F" }, 3, Color.LightBlue);
            }

            // Add legend
            CreateSeatLegend();

            Console.WriteLine($"✅ Created seat visualization with {_availableSeats.Count} available seats");
        }

        private int CreateSeatRows(List<SeatDto> seats, int startY, string[] columns, int seatsPerSide, Color seatColor)
        {
            // Group seats by row number
            var seatsByRow = seats.GroupBy(s => ExtractRowNumber(s.SeatNumber))
                                  .OrderBy(g => g.Key)
                                  .ToList();

            int yOffset = startY;

            foreach (var rowGroup in seatsByRow)
            {
                int rowNumber = rowGroup.Key;
                var rowSeats = rowGroup.ToList();

                // Row number label
                var rowLabel = new Label
                {
                    Text = rowNumber.ToString(),
                    Location = new Point(20, yOffset + 5),
                    Size = new Size(30, 25),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold)
                };
                _seatMapPanel.Controls.Add(rowLabel);

                int xOffset = 60;
                int seatIndex = 0;

                foreach (var column in columns)
                {
                    var seat = rowSeats.FirstOrDefault(s => s.SeatNumber.EndsWith(column));

                    var seatButton = new Button
                    {
                        Size = new Size(35, 35),
                        Location = new Point(xOffset, yOffset),
                        Text = seat?.SeatNumber ?? $"{rowNumber}{column}",
                        Font = new Font("Segoe UI", 8, FontStyle.Bold),
                        FlatStyle = FlatStyle.Flat,
                        Tag = seat
                    };

                    if (seat != null)
                    {
                        // Available seat
                        seatButton.BackColor = seatColor;
                        seatButton.ForeColor = Color.Black;
                        seatButton.Enabled = true;
                        seatButton.Click += SeatButton_Click;
                        _seatButtons[seat.SeatId] = seatButton;
                    }
                    else
                    {
                        // Occupied or unavailable seat
                        seatButton.BackColor = Color.Gray;
                        seatButton.ForeColor = Color.White;
                        seatButton.Enabled = false;
                    }

                    _seatMapPanel.Controls.Add(seatButton);

                    xOffset += 40;
                    seatIndex++;

                    // Add aisle space
                    if (seatIndex == seatsPerSide)
                    {
                        xOffset += 20; // Aisle space
                    }
                }

                yOffset += 45; // Move to next row
            }

            return yOffset;
        }

        private void CreateSeatLegend()
        {
            int legendY = Math.Max(_seatMapPanel.Height - 80, 400);

            // Available seat legend
            var availableBox = new Panel
            {
                Size = new Size(20, 20),
                Location = new Point(20, legendY),
                BackColor = Color.LightBlue,
                BorderStyle = BorderStyle.FixedSingle
            };
            _seatMapPanel.Controls.Add(availableBox);

            var availableLabel = new Label
            {
                Text = "Available",
                Location = new Point(50, legendY),
                Size = new Size(70, 20)
            };
            _seatMapPanel.Controls.Add(availableLabel);

            // Selected seat legend
            var selectedBox = new Panel
            {
                Size = new Size(20, 20),
                Location = new Point(130, legendY),
                BackColor = Color.Red,
                BorderStyle = BorderStyle.FixedSingle
            };
            _seatMapPanel.Controls.Add(selectedBox);

            var selectedLabel = new Label
            {
                Text = "Selected",
                Location = new Point(160, legendY),
                Size = new Size(70, 20)
            };
            _seatMapPanel.Controls.Add(selectedLabel);

            // Occupied seat legend
            var occupiedBox = new Panel
            {
                Size = new Size(20, 20),
                Location = new Point(240, legendY),
                BackColor = Color.Gray,
                BorderStyle = BorderStyle.FixedSingle
            };
            _seatMapPanel.Controls.Add(occupiedBox);

            var occupiedLabel = new Label
            {
                Text = "Occupied",
                Location = new Point(270, legendY),
                Size = new Size(70, 20)
            };
            _seatMapPanel.Controls.Add(occupiedLabel);

            // Business class legend
            var businessBox = new Panel
            {
                Size = new Size(20, 20),
                Location = new Point(350, legendY),
                BackColor = Color.Gold,
                BorderStyle = BorderStyle.FixedSingle
            };
            _seatMapPanel.Controls.Add(businessBox);

            var businessLabel = new Label
            {
                Text = "Business",
                Location = new Point(380, legendY),
                Size = new Size(70, 20)
            };
            _seatMapPanel.Controls.Add(businessLabel);
        }

        private int ExtractRowNumber(string seatNumber)
        {
            // Extract row number from seat number (e.g., "12A" -> 12)
            var rowPart = new string(seatNumber.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(rowPart, out int row) ? row : 0;
        }

        private void SeatButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button button && button.Tag is SeatDto seat)
            {
                // Clear previous selection
                if (!string.IsNullOrEmpty(_selectedSeatId) && _seatButtons.ContainsKey(_selectedSeatId))
                {
                    var prevButton = _seatButtons[_selectedSeatId];
                    var prevSeat = prevButton.Tag as SeatDto;
                    prevButton.BackColor = prevSeat?.SeatClass == "Business" ? Color.Gold : Color.LightBlue;
                }

                // Set new selection
                _selectedSeatId = seat.SeatId;
                button.BackColor = Color.Red;

                // Update selected seat info
                lblSelectedSeat.Text = $"Selected: {seat.SeatNumber} ({seat.SeatClass}) - ${seat.Price}";
                lblSelectedSeat.ForeColor = Color.Black;

                // Enable check-in button
                btnCheckIn.Enabled = _currentBooking != null && !_currentBooking.CheckedIn;

                Console.WriteLine($"✅ Seat selected: {seat.SeatNumber} ({seat.SeatId})");
            }
        }

        private void UpdateConnectionStatus(string status, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateConnectionStatus(status, color)));
                return;
            }

            lblConnectionStatus.Text = status;
            lblConnectionStatus.ForeColor = color;
        }

        private async Task LoadFlightsAsync()
        {
            try
            {
                cboFlights.Items.Clear();
                UseWaitCursor = true;

                _flights = await _apiService.GetFlightsAsync();

                foreach (var flight in _flights)
                {
                    cboFlights.Items.Add(flight);
                }

                if (cboFlights.Items.Count > 0)
                {
                    cboFlights.SelectedIndex = 0;
                }

                Console.WriteLine($"✅ Loaded {_flights.Count} flights");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading flights: {ex.Message}");
                MessageBox.Show($"Error loading flights: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async Task LoadAvailableSeatsAsync()
        {
            if (cboFlights.SelectedItem is not FlightDto selectedFlight)
                return;

            try
            {
                UseWaitCursor = true;
                Console.WriteLine($"🪑 Loading seats for flight {selectedFlight.FlightNumber}");

                _availableSeats = await _apiService.GetAvailableSeatsAsync(selectedFlight.FlightNumber);

                Console.WriteLine($"✅ Loaded {_availableSeats.Count} available seats");

                // CRITICAL: Create seat visualization
                CreateSeatVisualization();

                UpdateFlightStatusUI(selectedFlight.Status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading seats: {ex.Message}");
                MessageBox.Show($"Error loading available seats: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void UpdateFlightStatusUI(string status)
        {
            if (cboFlightStatus != null)
            {
                cboFlightStatus.Text = status;
            }

            bool isCheckInOpen = status == "CheckingIn";

            if (txtPassportNumber != null) txtPassportNumber.Enabled = isCheckInOpen;
            if (btnSearch != null) btnSearch.Enabled = isCheckInOpen;
            if (btnCheckIn != null) btnCheckIn.Enabled = isCheckInOpen && _currentBooking != null && !string.IsNullOrEmpty(_selectedSeatId);

            // Enable/disable seat selection
            foreach (var button in _seatButtons.Values)
            {
                button.Enabled = isCheckInOpen;
            }

            if (lblFlightStatus != null)
            {
                lblFlightStatus.Text = $"Status: {status}";
                lblFlightStatus.ForeColor = GetStatusColor(status);
            }
        }

        private Color GetStatusColor(string status)
        {
            return status.ToLower() switch
            {
                "checkingin" => Color.Green,
                "boarding" => Color.Blue,
                "departed" => Color.DarkBlue,
                "delayed" => Color.Orange,
                "cancelled" => Color.Red,
                _ => Color.Black
            };
        }

        private async void cboFlights_SelectedIndexChanged(object? sender, EventArgs e)
        {
            await LoadAvailableSeatsAsync();
            ClearPassengerInfo();

            // Subscribe to WebSocket updates for the selected flight
            if (cboFlights.SelectedItem is FlightDto selectedFlight && _webSocketClient != null)
            {
                try
                {
                    await _webSocketClient.SubscribeToFlightAsync(selectedFlight.FlightNumber);
                    Console.WriteLine($"✅ Subscribed to WebSocket updates for flight {selectedFlight.FlightNumber}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to subscribe to flight updates: {ex.Message}");
                }
            }
        }

        private async void btnSearch_Click(object? sender, EventArgs e)
        {
            if (cboFlights.SelectedItem is not FlightDto selectedFlight)
                return;

            var passportNumber = txtPassportNumber?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(passportNumber))
            {
                MessageBox.Show("Please enter a passport number.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                UseWaitCursor = true;

                _currentBooking = await _apiService.SearchBookingAsync(passportNumber, selectedFlight.FlightNumber);

                if (_currentBooking != null)
                {
                    if (lblPassengerName != null) lblPassengerName.Text = _currentBooking.PassengerName;
                    if (lblBookingReference != null) lblBookingReference.Text = _currentBooking.BookingReference;

                    if (_currentBooking.CheckedIn)
                    {
                        MessageBox.Show("This passenger is already checked in.", "Already Checked In",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (btnCheckIn != null) btnCheckIn.Enabled = false;
                    }
                    else
                    {
                        if (btnCheckIn != null) btnCheckIn.Enabled = !string.IsNullOrEmpty(_selectedSeatId);
                    }

                    if (grpPassengerInfo != null) grpPassengerInfo.Visible = true;
                    Console.WriteLine($"✅ Found booking for {_currentBooking.PassengerName}");
                }
                else
                {
                    MessageBox.Show("No booking found for the provided passport number on this flight.",
                        "Booking Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ClearPassengerInfo();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Search error: {ex.Message}");
                MessageBox.Show($"Error searching for booking: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        [Obsolete]
        private async void btnCheckIn_Click(object? sender, EventArgs e)
        {
            if (_currentBooking == null || string.IsNullOrEmpty(_selectedSeatId))
            {
                MessageBox.Show("Please search for a passenger and select a seat first.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Double-check that seat is still available
            var selectedSeat = _availableSeats.FirstOrDefault(s => s.SeatId == _selectedSeatId);
            if (selectedSeat == null)
            {
                MessageBox.Show("The selected seat is no longer available. Please select a different seat.",
                    "Seat Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ClearPassengerInfo();
                return;
            }

            try
            {
                UseWaitCursor = true;
                if (btnCheckIn != null) btnCheckIn.Enabled = false;

                Console.WriteLine($"🎫 Starting check-in process for {_currentBooking.PassengerName}");

                var checkInRequest = new CheckInRequestDto
                {
                    BookingReference = _currentBooking.BookingReference,
                    FlightNumber = _currentBooking.FlightNumber,
                    SeatId = _selectedSeatId,
                    StaffId = _staffId,
                    CounterId = _counterId,
                    PassengerName = _currentBooking.PassengerName
                };

                var (success, message, boardingPassPdf) = await _apiService.ProcessCheckInAsync(checkInRequest);

                if (success)
                {
                    Console.WriteLine($"✅ Check-in successful for {_currentBooking.PassengerName}");

                    MessageBox.Show("Check-in completed successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Print boarding pass
                    if (cboFlights.SelectedItem is FlightDto selectedFlight)
                    {
                        PrintService.PrintBoardingPass(
                            _currentBooking.PassengerName,
                            selectedFlight.FlightNumber,
                            selectedFlight.Origin,
                            selectedFlight.Destination,
                            "A12",
                            selectedFlight.DepartureTime,
                            selectedSeat.SeatNumber,
                            _currentBooking.BookingReference);
                    }

                    // CRITICAL: Notify other terminals via WebSocket
                    if (_webSocketClient != null) // Updated reference
                    {
                        try
                        {
                            await _webSocketClient.NotifySeatReservedAsync(_selectedSeatId, _currentBooking.FlightNumber);
                            Console.WriteLine($"📡 Sent seat reservation notification via WebSocket");
                        }
                        catch (Exception socketEx)
                        {
                            Console.WriteLine($"⚠️ Failed to send WebSocket notification: {socketEx.Message}");
                        }
                    }

                    // Clear the form for the next passenger
                    ClearPassengerInfo();
                    if (txtPassportNumber != null) txtPassportNumber.Clear();

                    // Refresh available seats
                    await LoadAvailableSeatsAsync();
                }
                else
                {
                    Console.WriteLine($"❌ Check-in failed: {message}");
                    MessageBox.Show($"Check-in failed: {message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Check-in exception: {ex.Message}");
                MessageBox.Show($"Error during check-in: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                if (btnCheckIn != null) btnCheckIn.Enabled = _currentBooking != null && !string.IsNullOrEmpty(_selectedSeatId);
            }
        }

        private async void btnUpdateStatus_Click(object? sender, EventArgs e)
        {
            if (cboFlights.SelectedItem is not FlightDto selectedFlight)
                return;

            var newStatus = cboFlightStatus?.Text ?? "";

            if (newStatus == selectedFlight.Status)
            {
                MessageBox.Show("Flight status is already set to this value.", "No Change",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                UseWaitCursor = true;
                if (btnUpdateStatus != null) btnUpdateStatus.Enabled = false;

                Console.WriteLine($"🔄 Updating flight status: {selectedFlight.FlightNumber} -> {newStatus}");

                var success = await _apiService.UpdateFlightStatusAsync(selectedFlight.FlightNumber, newStatus);

                if (success)
                {
                    Console.WriteLine($"✅ Flight status updated successfully via API");

                    MessageBox.Show("Flight status updated successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    selectedFlight.Status = newStatus;
                    UpdateFlightStatusUI(newStatus);
                }
                else
                {
                    Console.WriteLine($"❌ Failed to update flight status via API");
                    MessageBox.Show("Failed to update flight status.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Flight status update exception: {ex.Message}");
                MessageBox.Show($"Error updating flight status: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                if (btnUpdateStatus != null) btnUpdateStatus.Enabled = true;
            }
        }

        private void ClearPassengerInfo()
        {
            if (lblPassengerName != null) lblPassengerName.Text = "";
            if (lblBookingReference != null) lblBookingReference.Text = "";
            if (lblSelectedSeat != null)
            {
                lblSelectedSeat.Text = "Selected: None";
                lblSelectedSeat.ForeColor = Color.Black;
            }
            if (grpPassengerInfo != null) grpPassengerInfo.Visible = false;
            _currentBooking = null;

            // Clear seat selection
            if (!string.IsNullOrEmpty(_selectedSeatId) && _seatButtons.ContainsKey(_selectedSeatId))
            {
                var button = _seatButtons[_selectedSeatId];
                var seat = button.Tag as SeatDto;
                button.BackColor = seat?.SeatClass == "Business" ? Color.Gold : Color.LightBlue;
            }
            _selectedSeatId = "";
        }

        // Socket event handlers
        private async void OnSeatReservedFromSocket(string seatId, string flightNumber)
        {
            Console.WriteLine($"📡 Socket: Received seat reserved - {seatId} for flight {flightNumber}");

            if (cboFlights?.SelectedItem is FlightDto selectedFlight &&
                selectedFlight.FlightNumber == flightNumber)
            {
                await InvokeAsync(() =>
                {
                    var seatToRemove = _availableSeats.FirstOrDefault(s => s.SeatId == seatId);
                    if (seatToRemove != null)
                    {
                        Console.WriteLine($"🔄 Removing seat {seatId} from available seats");
                        _availableSeats.Remove(seatToRemove);

                        // Update seat button to show as occupied
                        if (_seatButtons.ContainsKey(seatId))
                        {
                            var button = _seatButtons[seatId];
                            button.BackColor = Color.Gray;
                            button.ForeColor = Color.White;
                            button.Enabled = false;
                            button.Text = button.Text + " (TAKEN)";
                            _seatButtons.Remove(seatId);
                            Console.WriteLine($"✅ Updated seat button for {seatId}");
                        }

                        // Clear selection if this seat was selected
                        if (_selectedSeatId == seatId)
                        {
                            _selectedSeatId = "";
                            if (lblSelectedSeat != null)
                            {
                                lblSelectedSeat.Text = "Selected: None - Seat was taken by another passenger";
                                lblSelectedSeat.ForeColor = Color.Red;
                            }
                            if (btnCheckIn != null) btnCheckIn.Enabled = false;
                            Console.WriteLine($"⚠️ Cleared selection - seat {seatId} was taken");
                        }
                    }
                });
            }
        }

        private async void OnFlightStatusChangedFromSocket(string flightNumber, string newStatus)
        {
            Console.WriteLine($"📡 Socket: Received flight status change - {flightNumber} -> {newStatus}");

            await InvokeAsync(() =>
            {
                var flight = _flights.FirstOrDefault(f => f.FlightNumber == flightNumber);
                if (flight != null)
                {
                    Console.WriteLine($"🔄 Updating flight status: {flight.Status} -> {newStatus}");
                    flight.Status = newStatus;

                    if (cboFlights?.SelectedItem is FlightDto selectedFlight &&
                        selectedFlight.FlightNumber == flightNumber)
                    {
                        UpdateFlightStatusUI(newStatus);
                        Console.WriteLine($"✅ Updated UI for flight {flightNumber}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Flight {flightNumber} not found in local list");
                }
            });
        }

        private async void OnSeatLockedFromSocket(string seatId, bool isLocked)
        {
            Console.WriteLine($"📡 Socket: Received seat lock - {seatId} = {isLocked}");

            await InvokeAsync(() =>
            {
                if (_seatButtons.ContainsKey(seatId))
                {
                    var button = _seatButtons[seatId];
                    if (isLocked)
                    {
                        button.BackColor = Color.Orange;
                        button.Text = button.Text.Replace(" (PROCESSING)", "") + " (PROCESSING)";
                        button.Enabled = false;
                    }
                    else if (!button.Text.Contains("(TAKEN)"))
                    {
                        // Only re-enable if not permanently taken
                        var seat = button.Tag as SeatDto;
                        button.BackColor = seat?.SeatClass == "Business" ? Color.Gold : Color.LightBlue;
                        button.Text = button.Text.Replace(" (PROCESSING)", "");
                        button.Enabled = true;
                    }
                    Console.WriteLine($"✅ Updated seat lock status for {seatId}");
                }
            });
        }

        private async void OnCheckInCompleteFromSocket(string flightNumber, string passengerName, string seatId)
        {
            Console.WriteLine($"📡 Socket: Received check-in complete - {passengerName} for flight {flightNumber}");

            await InvokeAsync(() =>
            {
                // Show notification
                MessageBox.Show($"Passenger {passengerName} has checked in for flight {flightNumber}",
                    "Check-in Notification", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private void OnConnectionStatusChanged(string status)
        {
            Console.WriteLine($"📡 WebSocket: Connection status changed - {status}");

            this.Invoke(new Action(() =>
            {
                if (status.Contains("Connected"))
                {
                    UpdateConnectionStatus(status, Color.Green);
                }
                else if (status.Contains("Disconnected") || status.Contains("Failed"))
                {
                    UpdateConnectionStatus(status, Color.Red);
                }
                else
                {
                    UpdateConnectionStatus(status, Color.Orange);
                }
            }));
        }

        private async Task InvokeAsync(Action action)
        {
            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        private async Task InitializeSignalRConnectionAsync()
        {
            // Placeholder for SignalR initialization if needed
            // This was referenced in the constructor but not implemented
            await Task.CompletedTask;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Console.WriteLine("🧹 MainForm closing - cleaning up resources...");

            base.OnFormClosing(e);

            try
            {
                if (_webSocketClient != null) // Updated reference
                {
                    _webSocketClient.Disconnect();
                    _webSocketClient.Dispose();
                    Console.WriteLine("✅ WebSocket client disposed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error disposing WebSocket client: {ex.Message}");
            }
        }

        private void InitializeComponent()
        {
            lblStaffName = new Label();
            lblCounterId = new Label();
            groupBox1 = new GroupBox();
            cboFlightStatus = new ComboBox();
            btnUpdateStatus = new Button();
            lblFlightStatus = new Label();
            btnRefreshFlights = new Button();
            label2 = new Label();
            cboFlights = new ComboBox();
            groupBox2 = new GroupBox();
            btnSearch = new Button();
            txtPassportNumber = new TextBox();
            label1 = new Label();
            lblSelectedSeat = new Label();
            grpPassengerInfo = new GroupBox();
            btnCheckIn = new Button();
            lblBookingReference = new Label();
            lblPassengerName = new Label();
            label5 = new Label();
            label4 = new Label();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            grpPassengerInfo.SuspendLayout();
            SuspendLayout();

            // 
            // lblStaffName
            // 
            lblStaffName.AutoSize = true;
            lblStaffName.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblStaffName.Location = new Point(12, 9);
            lblStaffName.Name = "lblStaffName";
            lblStaffName.Size = new Size(104, 20);
            lblStaffName.TabIndex = 0;
            lblStaffName.Text = "Staff Name: -";

            // 
            // lblCounterId
            // 
            lblCounterId.AutoSize = true;
            lblCounterId.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblCounterId.Location = new Point(12, 39);
            lblCounterId.Name = "lblCounterId";
            lblCounterId.Size = new Size(85, 20);
            lblCounterId.TabIndex = 1;
            lblCounterId.Text = "Counter: -";

            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBox1.Controls.Add(cboFlightStatus);
            groupBox1.Controls.Add(btnUpdateStatus);
            groupBox1.Controls.Add(lblFlightStatus);
            groupBox1.Controls.Add(btnRefreshFlights);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(cboFlights);
            groupBox1.Location = new Point(12, 75);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(776, 143);
            groupBox1.TabIndex = 2;
            groupBox1.TabStop = false;
            groupBox1.Text = "Flight Selection";

            // 
            // cboFlightStatus
            // 
            cboFlightStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFlightStatus.FormattingEnabled = true;
            cboFlightStatus.Items.AddRange(new object[] { "CheckingIn", "Boarding", "Departed", "Delayed", "Cancelled" });
            cboFlightStatus.Location = new Point(459, 89);
            cboFlightStatus.Name = "cboFlightStatus";
            cboFlightStatus.Size = new Size(151, 28);
            cboFlightStatus.TabIndex = 5;

            // 
            // btnUpdateStatus
            // 
            btnUpdateStatus.BackColor = Color.FromArgb(52, 152, 219);
            btnUpdateStatus.FlatStyle = FlatStyle.Flat;
            btnUpdateStatus.ForeColor = Color.White;
            btnUpdateStatus.Location = new Point(616, 89);
            btnUpdateStatus.Name = "btnUpdateStatus";
            btnUpdateStatus.Size = new Size(131, 29);
            btnUpdateStatus.TabIndex = 4;
            btnUpdateStatus.Text = "Update Status";
            btnUpdateStatus.UseVisualStyleBackColor = false;
            btnUpdateStatus.Click += btnUpdateStatus_Click;

            // 
            // lblFlightStatus
            // 
            lblFlightStatus.AutoSize = true;
            lblFlightStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblFlightStatus.Location = new Point(104, 93);
            lblFlightStatus.Name = "lblFlightStatus";
            lblFlightStatus.Size = new Size(78, 20);
            lblFlightStatus.TabIndex = 3;
            lblFlightStatus.Text = "Status: —";

            // 
            // btnRefreshFlights
            // 
            btnRefreshFlights.BackColor = Color.FromArgb(46, 204, 113);
            btnRefreshFlights.FlatStyle = FlatStyle.Flat;
            btnRefreshFlights.ForeColor = Color.White;
            btnRefreshFlights.Location = new Point(616, 41);
            btnRefreshFlights.Name = "btnRefreshFlights";
            btnRefreshFlights.Size = new Size(131, 29);
            btnRefreshFlights.TabIndex = 2;
            btnRefreshFlights.Text = "Refresh";
            btnRefreshFlights.UseVisualStyleBackColor = false;
            btnRefreshFlights.Click += async (s, e) => await LoadFlightsAsync();

            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(25, 45);
            label2.Name = "label2";
            label2.Size = new Size(73, 20);
            label2.TabIndex = 1;
            label2.Text = "Flight No:";

            // 
            // cboFlights
            // 
            cboFlights.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFlights.FormattingEnabled = true;
            cboFlights.Location = new Point(104, 42);
            cboFlights.Name = "cboFlights";
            cboFlights.Size = new Size(506, 28);
            cboFlights.TabIndex = 0;
            cboFlights.SelectedIndexChanged += cboFlights_SelectedIndexChanged;

            // 
            // groupBox2
            // 
            groupBox2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBox2.Controls.Add(btnSearch);
            groupBox2.Controls.Add(txtPassportNumber);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(lblSelectedSeat);
            groupBox2.Location = new Point(12, 224);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(776, 125);
            groupBox2.TabIndex = 3;
            groupBox2.TabStop = false;
            groupBox2.Text = "Passenger Check-In";

            // 
            // btnSearch
            // 
            btnSearch.BackColor = Color.FromArgb(52, 152, 219);
            btnSearch.FlatStyle = FlatStyle.Flat;
            btnSearch.ForeColor = Color.White;
            btnSearch.Location = new Point(616, 36);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new Size(131, 29);
            btnSearch.TabIndex = 4;
            btnSearch.Text = "Search";
            btnSearch.UseVisualStyleBackColor = false;
            btnSearch.Click += btnSearch_Click;

            // 
            // txtPassportNumber
            // 
            txtPassportNumber.Location = new Point(157, 36);
            txtPassportNumber.Name = "txtPassportNumber";
            txtPassportNumber.Size = new Size(453, 27);
            txtPassportNumber.TabIndex = 3;

            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(25, 39);
            label1.Name = "label1";
            label1.Size = new Size(126, 20);
            label1.TabIndex = 2;
            label1.Text = "Passport Number:";

            // 
            // lblSelectedSeat
            // 
            lblSelectedSeat.AutoSize = true;
            lblSelectedSeat.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblSelectedSeat.Location = new Point(25, 81);
            lblSelectedSeat.Name = "lblSelectedSeat";
            lblSelectedSeat.Size = new Size(104, 20);
            lblSelectedSeat.TabIndex = 0;
            lblSelectedSeat.Text = "Selected: None";

            // 
            // grpPassengerInfo
            // 
            grpPassengerInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpPassengerInfo.Controls.Add(btnCheckIn);
            grpPassengerInfo.Controls.Add(lblBookingReference);
            grpPassengerInfo.Controls.Add(lblPassengerName);
            grpPassengerInfo.Controls.Add(label5);
            grpPassengerInfo.Controls.Add(label4);
            grpPassengerInfo.Location = new Point(730, 400);
            grpPassengerInfo.Name = "grpPassengerInfo";
            grpPassengerInfo.Size = new Size(300, 125);
            grpPassengerInfo.TabIndex = 4;
            grpPassengerInfo.TabStop = false;
            grpPassengerInfo.Text = "Passenger Information";
            grpPassengerInfo.Visible = false;

            // 
            // btnCheckIn
            // 
            btnCheckIn.BackColor = Color.FromArgb(46, 204, 113);
            btnCheckIn.FlatStyle = FlatStyle.Flat;
            btnCheckIn.ForeColor = Color.White;
            btnCheckIn.Location = new Point(150, 48);
            btnCheckIn.Name = "btnCheckIn";
            btnCheckIn.Size = new Size(131, 41);
            btnCheckIn.TabIndex = 4;
            btnCheckIn.Text = "Check In";
            btnCheckIn.UseVisualStyleBackColor = false;
            //btnCheckIn.Click += BtnCheckIn_Click;

            // 
            // lblBookingReference
            // 
            lblBookingReference.AutoSize = true;
            lblBookingReference.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblBookingReference.Location = new Point(15, 76);
            lblBookingReference.Name = "lblBookingReference";
            lblBookingReference.Size = new Size(14, 20);
            lblBookingReference.TabIndex = 3;
            lblBookingReference.Text = "-";

            // 
            // lblPassengerName
            // 
            lblPassengerName.AutoSize = true;
            lblPassengerName.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblPassengerName.Location = new Point(15, 38);
            lblPassengerName.Name = "lblPassengerName";
            lblPassengerName.Size = new Size(14, 20);
            lblPassengerName.TabIndex = 2;
            lblPassengerName.Text = "-";

            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(15, 56);
            label5.Name = "label5";
            label5.Size = new Size(76, 20);
            label5.TabIndex = 1;
            label5.Text = "Booking#:";

            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(15, 18);
            label4.Name = "label4";
            label4.Size = new Size(52, 20);
            label4.TabIndex = 0;
            label4.Text = "Name:";

            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1050, 950);
            Controls.Add(grpPassengerInfo);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(lblCounterId);
            Controls.Add(lblStaffName);
            MinimumSize = new Size(1068, 997);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Flight Check-In System";
            Load += MainForm_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            grpPassengerInfo.ResumeLayout(false);
            grpPassengerInfo.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private Label lblStaffName;
        private Label lblCounterId;
        private GroupBox groupBox1;
        private Button btnRefreshFlights;
        private Label label2;
        private ComboBox cboFlights;
        private GroupBox groupBox2;
        private Button btnSearch;
        private TextBox txtPassportNumber;
        private Label label1;
        private Label lblSelectedSeat;
        private GroupBox grpPassengerInfo;
        private Button btnCheckIn;
        private Label lblBookingReference;
        private Label lblPassengerName;
        private Label label5;
        private Label label4;
        private Label lblFlightStatus;
        private Button btnUpdateStatus;
        private ComboBox cboFlightStatus;
    }
}

