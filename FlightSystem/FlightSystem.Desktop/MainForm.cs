using FlightSystem.Desktop.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FlightSystem.Desktop;

public partial class MainForm : Form
{
    private readonly SignalRService _signalRService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MainForm>? _logger;
    private readonly string _serverUrl = "http://localhost:5000"; // Use standard HTTP port

    // Form controls
    private ComboBox cmbFlights = new();
    private TextBox txtPassportNumber = new();
    private Label lblConnectionStatus = new();
    private Button btnSearch = new();
    private Button btnConnect = new();
    private Button btnUpdateStatus = new();
    private ComboBox cmbFlightStatus = new();
    private Panel pnlPassengerInfo = new();
    private Label lblPassengerName = new();
    private Label lblBookingRef = new();
    private FlowLayoutPanel pnlSeats = new();
    private Button btnCheckIn = new();
    private RichTextBox txtLog = new();

    // Current data
    private List<Flight> _flights = new();
    private List<Seat> _availableSeats = new();
    private Booking? _currentBooking;
    private string? _selectedSeatId;

    public MainForm()
    {
        InitializeComponent();

        // Configure HTTP client for development (ignore SSL errors)
        _httpClient = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        });

        _signalRService = new SignalRService(_serverUrl);

        SetupSignalREvents();
        LoadAsync();
    }

    private void InitializeComponent()
    {
        this.Size = new Size(1000, 700);
        this.Text = "Flight Check-In System";
        this.StartPosition = FormStartPosition.CenterScreen;

        CreateControls();
        LayoutControls();
    }

    private void CreateControls()
    {
        // Connection status
        lblConnectionStatus = new Label
        {
            Text = "Disconnected",
            ForeColor = Color.Red,
            Font = new Font("Arial", 10, FontStyle.Bold),
            AutoSize = true
        };

        btnConnect = new Button
        {
            Text = "Connect",
            Size = new Size(100, 30),
            BackColor = Color.Green,
            ForeColor = Color.White
        };
        btnConnect.Click += BtnConnect_Click;

        // Flight selection
        cmbFlights = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(300, 25)
        };
        cmbFlights.SelectedIndexChanged += CmbFlights_SelectedIndexChanged;

        cmbFlightStatus = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(150, 25)
        };
        cmbFlightStatus.Items.AddRange(new[] { "CheckingIn", "Boarding", "Departed", "Delayed", "Cancelled" });

        btnUpdateStatus = new Button
        {
            Text = "Update Status",
            Size = new Size(120, 30),
            BackColor = Color.Blue,
            ForeColor = Color.White
        };
        btnUpdateStatus.Click += BtnUpdateStatus_Click;

        // Passenger search
        txtPassportNumber = new TextBox
        {
            Size = new Size(200, 25),
            PlaceholderText = "Enter passport number"
        };

        btnSearch = new Button
        {
            Text = "Search",
            Size = new Size(80, 30),
            BackColor = Color.Orange,
            ForeColor = Color.White
        };
        btnSearch.Click += BtnSearch_Click;

        // Passenger info panel
        pnlPassengerInfo = new Panel
        {
            BorderStyle = BorderStyle.FixedSingle,
            Size = new Size(400, 100),
            Visible = false
        };

        lblPassengerName = new Label { Location = new Point(10, 10), AutoSize = true };
        lblBookingRef = new Label { Location = new Point(10, 35), AutoSize = true };

        pnlPassengerInfo.Controls.AddRange(new Control[] { lblPassengerName, lblBookingRef });

        // Seat selection panel
        pnlSeats = new FlowLayoutPanel
        {
            Size = new Size(600, 200),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };

        // Check-in button
        btnCheckIn = new Button
        {
            Text = "Check In",
            Size = new Size(100, 40),
            BackColor = Color.Green,
            ForeColor = Color.White,
            Enabled = false
        };
        btnCheckIn.Click += BtnCheckIn_Click;

        // Log display
        txtLog = new RichTextBox
        {
            Size = new Size(400, 200),
            ReadOnly = true,
            Font = new Font("Consolas", 9)
        };
    }

    private void LayoutControls()
    {
        var y = 20;

        // Connection status row
        var lblConn = new Label { Text = "Connection:", Location = new Point(20, y), AutoSize = true };
        lblConnectionStatus.Location = new Point(100, y);
        btnConnect.Location = new Point(250, y - 3);
        this.Controls.AddRange(new Control[] { lblConn, lblConnectionStatus, btnConnect });

        y += 40;

        // Flight selection row
        var lblFlight = new Label { Text = "Flight:", Location = new Point(20, y), AutoSize = true };
        cmbFlights.Location = new Point(70, y - 3);
        var lblStatus = new Label { Text = "Status:", Location = new Point(380, y), AutoSize = true };
        cmbFlightStatus.Location = new Point(430, y - 3);
        btnUpdateStatus.Location = new Point(590, y - 3);
        this.Controls.AddRange(new Control[] { lblFlight, cmbFlights, lblStatus, cmbFlightStatus, btnUpdateStatus });

        y += 50;

        // Passenger search row
        var lblPassport = new Label { Text = "Passport:", Location = new Point(20, y), AutoSize = true };
        txtPassportNumber.Location = new Point(80, y - 3);
        btnSearch.Location = new Point(290, y - 3);
        this.Controls.AddRange(new Control[] { lblPassport, txtPassportNumber, btnSearch });

        y += 40;

        // Passenger info
        var lblPassengerTitle = new Label { Text = "Passenger Info:", Location = new Point(20, y), AutoSize = true };
        pnlPassengerInfo.Location = new Point(20, y + 20);
        this.Controls.AddRange(new Control[] { lblPassengerTitle, pnlPassengerInfo });

        y += 130;

        // Seat selection
        var lblSeats = new Label { Text = "Available Seats:", Location = new Point(20, y), AutoSize = true };
        pnlSeats.Location = new Point(20, y + 20);
        btnCheckIn.Location = new Point(630, y + 80);
        this.Controls.AddRange(new Control[] { lblSeats, pnlSeats, btnCheckIn });

        y += 230;

        // Log
        var lblLog = new Label { Text = "Activity Log:", Location = new Point(20, y), AutoSize = true };
        txtLog.Location = new Point(20, y + 20);
        this.Controls.AddRange(new Control[] { lblLog, txtLog });
    }

    private void SetupSignalREvents()
    {
        _signalRService.ConnectionStatusChanged += (status) =>
        {
            this.Invoke(() =>
            {
                lblConnectionStatus.Text = status;
                lblConnectionStatus.ForeColor = status == "Connected" ? Color.Green : Color.Red;
                LogMessage($"Connection: {status}");
            });
        };

        _signalRService.FlightStatusChanged += (update) =>
        {
            this.Invoke(() =>
            {
                LogMessage($"Flight {update.FlightNumber} status changed: {update.OldStatus} → {update.NewStatus}");
                RefreshFlights();
            });
        };

        _signalRService.SeatAssigned += (update) =>
        {
            this.Invoke(() =>
            {
                LogMessage($"Seat {update.SeatNumber} assigned to {update.PassengerName}");
                RefreshAvailableSeats();
            });
        };
    }

    private async void LoadAsync()
    {
        await RefreshFlights();
    }

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        await _signalRService.ConnectAsync();
    }

    private async void CmbFlights_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbFlights.SelectedItem is Flight flight)
        {
            cmbFlightStatus.Text = flight.Status;
            await RefreshAvailableSeats();
            await _signalRService.JoinFlightGroupAsync(flight.FlightNumber);
        }
    }

    private async void BtnUpdateStatus_Click(object? sender, EventArgs e)
    {
        if (cmbFlights.SelectedItem is Flight flight && !string.IsNullOrEmpty(cmbFlightStatus.Text))
        {
            try
            {
                var json = JsonSerializer.Serialize(cmbFlightStatus.Text);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{_serverUrl}/api/flights/{flight.FlightNumber}/status", content);

                if (response.IsSuccessStatusCode)
                {
                    LogMessage($"Flight status updated successfully");
                }
                else
                {
                    LogMessage($"Failed to update flight status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating flight status: {ex.Message}");
            }
        }
    }

    private async void BtnSearch_Click(object? sender, EventArgs e)
    {
        if (cmbFlights.SelectedItem is Flight flight && !string.IsNullOrEmpty(txtPassportNumber.Text))
        {
            try
            {
                var searchRequest = new
                {
                    PassportNumber = txtPassportNumber.Text,
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
                        LogMessage($"Found booking for {booking.PassengerName}");
                    }
                }
                else
                {
                    MessageBox.Show("Passenger not found", "Search Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LogMessage("Passenger not found");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error searching passenger: {ex.Message}");
            }
        }
    }

    private async void BtnCheckIn_Click(object? sender, EventArgs e)
    {
        if (_currentBooking != null && !string.IsNullOrEmpty(_selectedSeatId))
        {
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
                    var result = await response.Content.ReadAsStringAsync();
                    MessageBox.Show("Check-in successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LogMessage($"Check-in successful for {_currentBooking.PassengerName}");

                    // Clear current booking and refresh
                    _currentBooking = null;
                    _selectedSeatId = null;
                    pnlPassengerInfo.Visible = false;
                    btnCheckIn.Enabled = false;
                    txtPassportNumber.Clear();
                    await RefreshAvailableSeats();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Check-in failed: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogMessage($"Check-in failed: {error}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during check-in: {ex.Message}");
            }
        }
    }

    private async Task RefreshFlights()
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
                foreach (var flight in _flights)
                {
                    cmbFlights.Items.Add(flight);
                }

                if (cmbFlights.Items.Count > 0)
                {
                    cmbFlights.SelectedIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error loading flights: {ex.Message}");
        }
    }

    private async Task RefreshAvailableSeats()
    {
        if (cmbFlights.SelectedItem is Flight flight)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/api/flights/{flight.FlightNumber}/available-seats");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _availableSeats = JsonSerializer.Deserialize<List<Seat>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Seat>();

                    DisplayAvailableSeats();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading seats: {ex.Message}");
            }
        }
    }

    private void DisplayAvailableSeats()
    {
        pnlSeats.Controls.Clear();
        _selectedSeatId = null;
        btnCheckIn.Enabled = false;

        foreach (var seat in _availableSeats.OrderBy(s => s.SeatNumber))
        {
            var btnSeat = new Button
            {
                Text = seat.SeatNumber,
                Size = new Size(50, 40),
                BackColor = seat.Class == "Business" ? Color.Gold : Color.LightBlue,
                ForeColor = Color.Black,
                Tag = seat
            };

            btnSeat.Click += (sender, e) =>
            {
                // Clear previous selection
                foreach (Control control in pnlSeats.Controls)
                {
                    if (control is Button btn && btn != sender)
                    {
                        var s = btn.Tag as Seat;
                        btn.BackColor = s?.Class == "Business" ? Color.Gold : Color.LightBlue;
                    }
                }

                // Set new selection
                btnSeat.BackColor = Color.Red;
                _selectedSeatId = seat.SeatId;
                btnCheckIn.Enabled = _currentBooking != null;
                LogMessage($"Selected seat: {seat.SeatNumber}");
            };

            pnlSeats.Controls.Add(btnSeat);
        }
    }

    private void ShowPassengerInfo(Booking booking)
    {
        lblPassengerName.Text = $"Name: {booking.PassengerName}";
        lblBookingRef.Text = $"Booking: {booking.BookingReference}";
        pnlPassengerInfo.Visible = true;
    }

    private void LogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        txtLog.AppendText($"[{timestamp}] {message}\n");
        txtLog.ScrollToCaret();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _signalRService?.DisconnectAsync();
        _httpClient?.Dispose();
        base.OnFormClosing(e);
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
        return $"{FlightNumber} - {Origin} to {Destination} ({Status})";
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