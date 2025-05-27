using FlightSystem.Desktop.Services;
using System.Text.Json;

namespace FlightSystem.Desktop;

public partial class MainForm : Form, IAsyncDisposable
{
    private readonly SignalRService _signalRService;
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private readonly System.Threading.Timer _connectionCheckTimer;

    // Form controls - declared as fields for access across methods
    private Label lblConnectionStatus = null!;
    private Button btnConnect = null!;
    private ComboBox cmbFlights = null!;
    private ComboBox cmbFlightStatus = null!;
    private Button btnUpdateStatus = null!;
    private TextBox txtPassportNumber = null!;
    private Button btnSearch = null!;
    private Panel pnlPassengerInfo = null!;
    private Label lblPassengerName = null!;
    private Label lblBookingRef = null!;
    private FlowLayoutPanel pnlSeats = null!;
    private Button btnCheckIn = null!;
    private RichTextBox txtLog = null!;

    // Current data
    private List<Flight> _flights = new();
    private List<Seat> _availableSeats = new();
    private Booking? _currentBooking;
    private string? _selectedSeatId;
    private string? _currentFlightNumber;

    // Connection state
    private bool _isConnected = false;
    private DateTime _lastSuccessfulConnection = DateTime.MinValue;

    public MainForm()
    {
        _serverUrl = Environment.GetEnvironmentVariable("FLIGHT_SERVER_URL") ?? "http://localhost:5000";

        InitializeComponent();

        // Configure HTTP client with proper error handling
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FlightSystem-Desktop/1.0");

        // Create SignalR service without logger for desktop
        _signalRService = new SignalRService(_serverUrl);

        SetupSignalREvents();
        SetupValidation();

        // Connection check timer
        _connectionCheckTimer = new System.Threading.Timer(
            async _ => await CheckConnectionHealthAsync(),
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30));

        _ = LoadAsync(); // Fire and forget async call
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form properties
        this.Size = new Size(1200, 800);
        this.Text = "Flight Check-In System v1.0";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(1000, 600);
        this.FormBorderStyle = FormBorderStyle.Sizable;

        CreateControls();
        LayoutControls();

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void CreateControls()
    {
        // Status Panel
        var statusPanel = new Panel
        {
            Name = "statusPanel",
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(45, 45, 48),
            Padding = new Padding(10)
        };

        var lblConnectionTitle = new Label
        {
            Text = "Connection Status:",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(10, 10),
            AutoSize = true
        };

        lblConnectionStatus = new Label
        {
            Name = "lblConnectionStatus",
            Text = "Disconnected",
            ForeColor = Color.Red,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(150, 10),
            AutoSize = true
        };

        btnConnect = new Button
        {
            Name = "btnConnect",
            Text = "Connect",
            Size = new Size(100, 30),
            Location = new Point(300, 8),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
        btnConnect.Click += BtnConnect_Click;

        var lblServerUrl = new Label
        {
            Text = $"Server: {_serverUrl}",
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 8),
            Location = new Point(10, 35),
            AutoSize = true
        };

        statusPanel.Controls.AddRange(new Control[] { lblConnectionTitle, lblConnectionStatus, btnConnect, lblServerUrl });

        // Main Panel using simple Panel instead of TableLayoutPanel for compatibility
        var mainPanel = new Panel
        {
            Name = "mainPanel",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        // Flight Selection Group
        var flightGroup = new GroupBox
        {
            Name = "flightGroup",
            Text = "Flight Selection",
            Location = new Point(10, 20),
            Size = new Size(1160, 80),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblFlight = new Label { Text = "Flight:", Location = new Point(10, 30), AutoSize = true };
        cmbFlights = new ComboBox
        {
            Name = "cmbFlights",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(70, 27),
            Size = new Size(300, 25),
            Font = new Font("Segoe UI", 9)
        };
        cmbFlights.SelectedIndexChanged += CmbFlights_SelectedIndexChanged;

        var lblStatus = new Label { Text = "Status:", Location = new Point(380, 30), AutoSize = true };
        cmbFlightStatus = new ComboBox
        {
            Name = "cmbFlightStatus",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(430, 27),
            Size = new Size(150, 25),
            Font = new Font("Segoe UI", 9)
        };
        cmbFlightStatus.Items.AddRange(new[] { "CheckingIn", "Boarding", "Departed", "Delayed", "Cancelled" });

        btnUpdateStatus = new Button
        {
            Name = "btnUpdateStatus",
            Text = "Update Status",
            Location = new Point(590, 25),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnUpdateStatus.Click += BtnUpdateStatus_Click;

        flightGroup.Controls.AddRange(new Control[] { lblFlight, cmbFlights, lblStatus, cmbFlightStatus, btnUpdateStatus });

        // Passenger Search Group
        var passengerGroup = new GroupBox
        {
            Name = "passengerGroup",
            Text = "Passenger Search & Information",
            Location = new Point(10, 100),
            Size = new Size(1160, 120),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblPassport = new Label { Text = "Passport Number:", Location = new Point(10, 30), AutoSize = true };
        txtPassportNumber = new TextBox
        {
            Name = "txtPassportNumber",
            Location = new Point(130, 27),
            Size = new Size(200, 25),
            Font = new Font("Segoe UI", 9),
            MaxLength = 20,
            CharacterCasing = CharacterCasing.Upper
        };
        txtPassportNumber.KeyPress += TxtPassportNumber_KeyPress;

        btnSearch = new Button
        {
            Name = "btnSearch",
            Text = "Search Passenger",
            Location = new Point(340, 25),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(255, 140, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSearch.Click += BtnSearch_Click;

        pnlPassengerInfo = new Panel
        {
            Name = "pnlPassengerInfo",
            Location = new Point(10, 60),
            Size = new Size(1140, 50),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false,
            BackColor = Color.FromArgb(245, 245, 245),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        lblPassengerName = new Label
        {
            Name = "lblPassengerName",
            Location = new Point(10, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        lblBookingRef = new Label
        {
            Name = "lblBookingRef",
            Location = new Point(10, 30),
            AutoSize = true,
            Font = new Font("Segoe UI", 9)
        };

        pnlPassengerInfo.Controls.AddRange(new Control[] { lblPassengerName, lblBookingRef });
        passengerGroup.Controls.AddRange(new Control[] { lblPassport, txtPassportNumber, btnSearch, pnlPassengerInfo });

        // Seat Selection Group
        var seatGroup = new GroupBox
        {
            Name = "seatGroup",
            Text = "Available Seats",
            Location = new Point(10, 230),
            Size = new Size(780, 350),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
        };

        pnlSeats = new FlowLayoutPanel
        {
            Name = "pnlSeats",
            Location = new Point(10, 25),
            Size = new Size(760, 280),
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
        };

        btnCheckIn = new Button
        {
            Name = "btnCheckIn",
            Text = "Check In Passenger",
            Location = new Point(610, 310),
            Size = new Size(150, 35),
            BackColor = Color.FromArgb(76, 175, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnCheckIn.Click += BtnCheckIn_Click;

        seatGroup.Controls.AddRange(new Control[] { pnlSeats, btnCheckIn });

        // Activity Log Group
        var logGroup = new GroupBox
        {
            Name = "logGroup",
            Text = "Activity Log",
            Location = new Point(800, 230),
            Size = new Size(370, 350),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
        };

        txtLog = new RichTextBox
        {
            Name = "txtLog",
            Location = new Point(10, 25),
            Size = new Size(350, 280),
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGray,
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
        };

        var btnClearLog = new Button
        {
            Text = "Clear Log",
            Location = new Point(280, 310),
            Size = new Size(80, 25),
            BackColor = Color.FromArgb(220, 53, 69),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnClearLog.Click += (s, e) => txtLog.Clear();

        logGroup.Controls.AddRange(new Control[] { txtLog, btnClearLog });

        mainPanel.Controls.AddRange(new Control[] { flightGroup, passengerGroup, seatGroup, logGroup });
        this.Controls.AddRange(new Control[] { statusPanel, mainPanel });
    }

    private void LayoutControls()
    {
        // Simple layout - positions are set in CreateControls
    }

    private void SetupValidation()
    {
        txtPassportNumber.Leave += ValidatePassportNumber;
    }

    private void ValidatePassportNumber(object? sender, EventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var passport = textBox.Text.Trim();
            if (!string.IsNullOrEmpty(passport))
            {
                if (passport.Length < 6 || passport.Length > 20)
                {
                    ShowValidationError("Passport number must be between 6 and 20 characters");
                    textBox.Focus();
                    textBox.BackColor = Color.LightPink;
                    return;
                }

                textBox.BackColor = SystemColors.Window;
            }
        }
    }

    private void ShowValidationError(string message)
    {
        LogMessage($"❌ Validation Error: {message}");
        MessageBox.Show(message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void TxtPassportNumber_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsLetterOrDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
        {
            e.Handled = true;
        }

        if (e.KeyChar == (char)Keys.Enter)
        {
            e.Handled = true;
            btnSearch?.PerformClick();
        }
    }

    private void SetupSignalREvents()
    {
        _signalRService.ConnectionStatusChanged += (status) =>
        {
            this.BeginInvoke(() =>
            {
                lblConnectionStatus.Text = status;
                lblConnectionStatus.ForeColor = status == "Connected" ? Color.Green : Color.Red;
                _isConnected = status == "Connected";

                if (_isConnected)
                {
                    _lastSuccessfulConnection = DateTime.Now;
                    btnConnect.Text = "Connected";
                    btnConnect.BackColor = Color.FromArgb(76, 175, 80);
                }
                else
                {
                    btnConnect.Text = "Connect";
                    btnConnect.BackColor = Color.FromArgb(0, 122, 204);
                }

                LogMessage($"🔌 Connection: {status}");
            });
        };

        _signalRService.FlightStatusChanged += (update) =>
        {
            this.BeginInvoke(() =>
            {
                LogMessage($"✈️ Flight {update.FlightNumber} status changed: {update.OldStatus} → {update.NewStatus}");
                _ = RefreshFlightsAsync();
            });
        };

        _signalRService.SeatAssigned += (update) =>
        {
            this.BeginInvoke(() =>
            {
                LogMessage($"🪑 Seat {update.SeatNumber} assigned to {update.PassengerName}");
                _ = RefreshAvailableSeatsAsync();
            });
        };
    }

    private async Task LoadAsync()
    {
        try
        {
            LogMessage("🚀 Application starting...");
            await RefreshFlightsAsync();
            LogMessage("✅ Application initialized successfully");
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Error during application startup: {ex.Message}");
            Console.WriteLine($"Error during application startup: {ex}");
        }
    }

    private async Task CheckConnectionHealthAsync()
    {
        if (!_isConnected && (DateTime.Now - _lastSuccessfulConnection).TotalMinutes > 2)
        {
            try
            {
                await _signalRService.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-reconnection attempt failed: {ex.Message}");
            }
        }
    }

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        btnConnect.Enabled = false;
        btnConnect.Text = "Connecting...";

        try
        {
            var success = await _signalRService.ConnectAsync();
            if (success)
            {
                LogMessage("✅ Successfully connected to server");
                await RefreshFlightsAsync();
            }
            else
            {
                LogMessage("❌ Failed to connect to server");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Connection error: {ex.Message}");
            Console.WriteLine($"Connection error: {ex}");
        }
        finally
        {
            btnConnect.Enabled = true;
        }
    }

    private async void CmbFlights_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbFlights.SelectedItem is Flight flight)
        {
            _currentFlightNumber = flight.FlightNumber;
            cmbFlightStatus.Text = flight.Status;

            await RefreshAvailableSeatsAsync();
            await _signalRService.JoinFlightGroupAsync(flight.FlightNumber);

            LogMessage($"📋 Selected flight: {flight.FlightNumber} ({flight.Origin} → {flight.Destination})");
        }
    }

    private async void BtnUpdateStatus_Click(object? sender, EventArgs e)
    {
        if (cmbFlights.SelectedItem is Flight flight && !string.IsNullOrEmpty(cmbFlightStatus.Text))
        {
            btnUpdateStatus.Enabled = false;
            btnUpdateStatus.Text = "Updating...";

            try
            {
                var json = JsonSerializer.Serialize(cmbFlightStatus.Text);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{_serverUrl}/api/flights/{flight.FlightNumber}/status", content);

                if (response.IsSuccessStatusCode)
                {
                    LogMessage($"✅ Flight status updated successfully: {flight.FlightNumber} → {cmbFlightStatus.Text}");
                    await RefreshFlightsAsync();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    LogMessage($"❌ Failed to update flight status: {response.StatusCode} - {errorContent}");
                    MessageBox.Show($"Failed to update flight status: {response.StatusCode}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error updating flight status: {ex.Message}");
                MessageBox.Show($"Error updating flight status: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Error updating flight status: {ex}");
            }
            finally
            {
                btnUpdateStatus.Enabled = true;
                btnUpdateStatus.Text = "Update Status";
            }
        }
    }

    private async void BtnSearch_Click(object? sender, EventArgs e)
    {
        if (cmbFlights.SelectedItem is Flight flight && !string.IsNullOrEmpty(txtPassportNumber.Text))
        {
            btnSearch.Enabled = false;
            btnSearch.Text = "Searching...";

            try
            {
                var searchRequest = new
                {
                    PassportNumber = txtPassportNumber.Text.Trim().ToUpper(),
                    FlightNumber = flight.FlightNumber
                };

                var json = JsonSerializer.Serialize(searchRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_serverUrl}/api/checkin/search", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var booking = JsonSerializer.Deserialize<Booking>(result, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (booking != null)
                    {
                        _currentBooking = booking;
                        ShowPassengerInfo(booking);
                        LogMessage($"✅ Found booking for {booking.PassengerName} (Ref: {booking.BookingReference})");
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ShowValidationError("Passenger not found for this flight");
                    LogMessage($"🔍 Passenger not found: {txtPassportNumber.Text} on flight {flight.FlightNumber}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    LogMessage($"❌ Search error: {response.StatusCode} - {errorContent}");
                    MessageBox.Show($"Search failed: {response.StatusCode}", "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error searching passenger: {ex.Message}");
                MessageBox.Show($"Error searching passenger: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Error searching passenger: {ex}");
            }
            finally
            {
                btnSearch.Enabled = true;
                btnSearch.Text = "Search Passenger";
            }
        }
        else
        {
            ShowValidationError("Please select a flight and enter a passport number");
        }
    }

    private async void BtnCheckIn_Click(object? sender, EventArgs e)
    {
        if (_currentBooking != null && !string.IsNullOrEmpty(_selectedSeatId))
        {
            btnCheckIn.Enabled = false;
            btnCheckIn.Text = "Processing...";

            try
            {
                var request = new
                {
                    BookingReference = _currentBooking.BookingReference,
                    SeatId = _selectedSeatId,
                    StaffName = Environment.UserName
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_serverUrl}/api/checkin/assign-seat", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("✅ Check-in successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LogMessage($"✅ Check-in successful for {_currentBooking.PassengerName}");

                    _currentBooking = null;
                    _selectedSeatId = null;
                    ClearPassengerInfo();
                    txtPassportNumber.Clear();

                    await RefreshAvailableSeatsAsync();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    string userFriendlyMessage = GetUserFriendlyErrorMessage(response.StatusCode, errorContent);

                    MessageBox.Show(userFriendlyMessage, "Check-in Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    LogMessage($"❌ Check-in failed: {errorContent}");

                    await RefreshAvailableSeatsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error during check-in: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"💥 Error during check-in: {ex.Message}");
                Console.WriteLine($"Error during check-in: {ex}");
            }
            finally
            {
                btnCheckIn.Enabled = _currentBooking != null && !string.IsNullOrEmpty(_selectedSeatId);
                btnCheckIn.Text = "Check In Passenger";
            }
        }
    }

    private string GetUserFriendlyErrorMessage(System.Net.HttpStatusCode statusCode, string errorContent)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize<JsonElement>(errorContent);
            var message = errorResponse.TryGetProperty("message", out var msgElement)
                ? msgElement.GetString() ?? "Unknown error"
                : "Unknown error";

            return statusCode switch
            {
                System.Net.HttpStatusCode.Conflict =>
                    $"🚫 Seat Assignment Conflict\n\n{message}\n\nPlease select a different seat.",
                System.Net.HttpStatusCode.NotFound =>
                    $"❓ Not Found\n\n{message}\n\nPlease refresh and try again.",
                System.Net.HttpStatusCode.BadRequest =>
                    $"⚠️ Invalid Request\n\n{message}",
                _ =>
                    $"❌ Check-in Failed\n\nStatus: {statusCode}\nMessage: {message}"
            };
        }
        catch
        {
            return $"❌ Check-in failed with status: {statusCode}";
        }
    }

    private async Task RefreshFlightsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_serverUrl}/api/flights");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _flights = JsonSerializer.Deserialize<List<Flight>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Flight>();

                cmbFlights.Items.Clear();
                foreach (var flight in _flights.OrderBy(f => f.DepartureTime))
                {
                    cmbFlights.Items.Add(flight);
                }

                if (cmbFlights.Items.Count > 0)
                {
                    cmbFlights.SelectedIndex = 0;
                }

                LogMessage($"📋 Loaded {_flights.Count} flights");
            }
            else
            {
                LogMessage($"❌ Failed to load flights: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            LogMessage($"🌐 Network error loading flights: {ex.Message}");
            if (!_isConnected)
            {
                MessageBox.Show("Unable to connect to server. Please check your connection and try again.",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Error loading flights: {ex.Message}");
            Console.WriteLine($"Error loading flights: {ex}");
        }
    }

    private async Task RefreshAvailableSeatsAsync()
    {
        if (!string.IsNullOrEmpty(_currentFlightNumber))
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/api/flights/{_currentFlightNumber}/available-seats");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _availableSeats = JsonSerializer.Deserialize<List<Seat>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Seat>();

                    DisplayAvailableSeats();
                    LogMessage($"🪑 Loaded {_availableSeats.Count} available seats for flight {_currentFlightNumber}");
                }
                else
                {
                    LogMessage($"❌ Failed to load seats: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error loading seats: {ex.Message}");
                Console.WriteLine($"Error loading seats: {ex}");
            }
        }
    }

    private void DisplayAvailableSeats()
    {
        pnlSeats.Controls.Clear();
        _selectedSeatId = null;
        btnCheckIn.Enabled = false;

        if (!_availableSeats.Any())
        {
            var noSeatsLabel = new Label
            {
                Text = "No available seats for this flight",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 12, FontStyle.Italic),
                AutoSize = true,
                Margin = new Padding(10, 20, 10, 10)
            };
            pnlSeats.Controls.Add(noSeatsLabel);
            return;
        }

        var seatsByClass = _availableSeats
            .OrderBy(s => s.SeatNumber)
            .GroupBy(s => s.Class)
            .OrderBy(g => GetClassPriority(g.Key));

        foreach (var classGroup in seatsByClass)
        {
            var classLabel = new Label
            {
                Text = $"{GetClassDisplayName(classGroup.Key)} ({classGroup.Count()} available)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = GetClassColor(classGroup.Key),
                AutoSize = true,
                Margin = new Padding(5, 10, 5, 5)
            };
            pnlSeats.Controls.Add(classLabel);
            pnlSeats.SetFlowBreak(classLabel, true);

            foreach (var seat in classGroup)
            {
                var btnSeat = new Button
                {
                    Text = seat.SeatNumber,
                    Size = new Size(60, 45),
                    BackColor = GetSeatColor(seat.Class),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Tag = seat,
                    Margin = new Padding(2),
                    UseVisualStyleBackColor = false,
                    Cursor = Cursors.Hand
                };

                btnSeat.FlatAppearance.BorderSize = 1;
                btnSeat.FlatAppearance.BorderColor = Color.DarkGray;

                var tooltip = new ToolTip();
                tooltip.SetToolTip(btnSeat, $"Seat: {seat.SeatNumber}\nClass: {GetClassDisplayName(seat.Class)}\nPrice: ${seat.Price}");

                btnSeat.Click += SeatButton_Click;
                btnSeat.MouseEnter += (s, e) => {
                    if (btnSeat.BackColor != Color.Red)
                        btnSeat.BackColor = Color.FromArgb(70, 130, 180);
                };
                btnSeat.MouseLeave += (s, e) => {
                    if (btnSeat.BackColor != Color.Red)
                        btnSeat.BackColor = GetSeatColor(seat.Class);
                };

                pnlSeats.Controls.Add(btnSeat);
            }

            if (classGroup != seatsByClass.Last())
            {
                pnlSeats.SetFlowBreak(pnlSeats.Controls[pnlSeats.Controls.Count - 1], true);
            }
        }
    }

    private void SeatButton_Click(object? sender, EventArgs e)
    {
        if (sender is Button btnSeat && btnSeat.Tag is Seat seat)
        {
            foreach (Control control in pnlSeats.Controls)
            {
                if (control is Button btn && btn != btnSeat && btn.Tag is Seat)
                {
                    var s = btn.Tag as Seat;
                    btn.BackColor = GetSeatColor(s!.Class);
                }
            }

            btnSeat.BackColor = Color.Red;
            _selectedSeatId = seat.SeatId;

            btnCheckIn.Enabled = _currentBooking != null;

            LogMessage($"🎯 Selected seat: {seat.SeatNumber} ({GetClassDisplayName(seat.Class)} class - ${seat.Price})");
        }
    }

    private int GetClassPriority(string seatClass)
    {
        return seatClass.ToLower() switch
        {
            "firstclass" => 0,
            "business" => 1,
            _ => 2
        };
    }

    private string GetClassDisplayName(string seatClass)
    {
        return seatClass.ToLower() switch
        {
            "firstclass" => "First Class",
            "business" => "Business Class",
            _ => "Economy Class"
        };
    }

    private Color GetSeatColor(string seatClass)
    {
        return seatClass.ToLower() switch
        {
            "business" => Color.FromArgb(255, 193, 7),
            "firstclass" => Color.FromArgb(138, 43, 226),
            _ => Color.FromArgb(52, 152, 219)
        };
    }

    private Color GetClassColor(string seatClass)
    {
        return seatClass.ToLower() switch
        {
            "business" => Color.FromArgb(255, 140, 0),
            "firstclass" => Color.FromArgb(128, 0, 128),
            _ => Color.FromArgb(70, 130, 180)
        };
    }

    private void ShowPassengerInfo(Booking booking)
    {
        lblPassengerName.Text = $"Passenger: {booking.PassengerName}";
        lblBookingRef.Text = $"Booking Reference: {booking.BookingReference}";

        if (booking.Status == "CheckedIn")
        {
            lblBookingRef.Text += $" (Already checked in: {booking.AssignedSeat})";
            lblBookingRef.ForeColor = Color.Green;
        }
        else
        {
            lblBookingRef.ForeColor = Color.Black;
        }

        pnlPassengerInfo.Visible = true;
        btnCheckIn.Enabled = booking.Status != "CheckedIn" && !string.IsNullOrEmpty(_selectedSeatId);
    }

    private void ClearPassengerInfo()
    {
        pnlPassengerInfo.Visible = false;
        btnCheckIn.Enabled = false;

        foreach (Control control in pnlSeats.Controls)
        {
            if (control is Button btn && btn.Tag is Seat seat)
            {
                btn.BackColor = GetSeatColor(seat.Class);
            }
        }
        _selectedSeatId = null;
    }

    private void LogMessage(string message)
    {
        if (txtLog.IsDisposed) return;

        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\n";

            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke(() => AppendLogMessage(logEntry));
            }
            else
            {
                AppendLogMessage(logEntry);
            }

            Console.WriteLine($"[Desktop] {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error logging message: {ex.Message}");
        }
    }

    private void AppendLogMessage(string logEntry)
    {
        try
        {
            txtLog.AppendText(logEntry);
            txtLog.ScrollToCaret();

            if (txtLog.Lines.Length > 1000)
            {
                var lines = txtLog.Lines.Skip(100).ToArray();
                txtLog.Lines = lines;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error appending log: {ex.Message}");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            LogMessage("👋 Application shutting down...");

            if (e.CloseReason == CloseReason.UserClosing)
            {
                var result = MessageBox.Show("Are you sure you want to exit the Flight Check-In System?",
                    "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            _connectionCheckTimer?.Dispose();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _signalRService.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing SignalR service: {ex.Message}");
                }
            });

            _httpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during form closing: {ex.Message}");
        }

        base.OnFormClosing(e);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _connectionCheckTimer?.Dispose();

            if (_signalRService != null)
            {
                await _signalRService.DisposeAsync();
            }

            _httpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during disposal: {ex.Message}");
        }
    }
}

// Data models for API responses
public class Flight
{
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AircraftModel { get; set; } = string.Empty;

    public override string ToString()
    {
        var departureTime = DepartureTime.ToString("HH:mm");
        var departureDate = DepartureTime.ToString("MMM dd");
        var status = Status switch
        {
            "CheckingIn" => "✅ Check-in Open",
            "Boarding" => "🚪 Boarding",
            "Departed" => "✈️ Departed",
            "Delayed" => "⏰ Delayed",
            "Cancelled" => "❌ Cancelled",
            _ => Status
        };

        return $"{FlightNumber} - {Origin} → {Destination} ({departureTime}, {departureDate}) [{status}]";
    }
}

public class Seat
{
    public string SeatId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class Booking
{
    public string BookingReference { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssignedSeat { get; set; }
    public DateTime? CheckInTime { get; set; }
}

public enum FlightStatus
{
    CheckingIn,
    Boarding,
    Departed,
    Delayed,
    Cancelled
}