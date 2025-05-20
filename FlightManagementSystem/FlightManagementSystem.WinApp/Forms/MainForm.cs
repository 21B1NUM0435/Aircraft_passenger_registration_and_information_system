using FlightManagementSystem.WinApp.Models;
using FlightManagementSystem.WinApp.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace FlightManagementSystem.WinApp.Forms
{
    public partial class MainForm : Form
    {
        private readonly ApiService _apiService;
        private readonly string _staffId;
        private readonly string _staffName;
        private readonly string _counterId;
        private List<FlightDto> _flights = new();
        private List<SeatDto> _availableSeats = new();
        private BookingDto? _currentBooking;
        private HubConnection? _hubConnection;

        public MainForm(ApiService apiService, string staffId, string staffName, string counterId)
        {
            _apiService = apiService;
            _staffId = staffId;
            _staffName = staffName;
            _counterId = counterId;

            InitializeComponent();

            // Set staff info
            lblStaffName.Text = $"Staff: {_staffName}";
            lblCounterId.Text = $"Counter: {_counterId}";

            // Initialize SignalR connection
            InitializeSignalRConnection();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await LoadFlightsAsync();
        }

        private async Task LoadFlightsAsync()
        {
            try
            {
                cboFlights.Items.Clear();

                // Show loading indicator
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading flights: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async void cboFlights_SelectedIndexChanged(object sender, EventArgs e)
        {
            await LoadAvailableSeatsAsync();
            ClearPassengerInfo();
        }

        private async Task LoadAvailableSeatsAsync()
        {
            if (cboFlights.SelectedItem is not FlightDto selectedFlight)
                return;

            try
            {
                cboAvailableSeats.Items.Clear();

                // Show loading indicator
                UseWaitCursor = true;

                _availableSeats = await _apiService.GetAvailableSeatsAsync(selectedFlight.FlightNumber);

                foreach (var seat in _availableSeats)
                {
                    cboAvailableSeats.Items.Add(seat);
                }

                if (cboAvailableSeats.Items.Count > 0)
                {
                    cboAvailableSeats.SelectedIndex = 0;
                }

                // Update UI with flight status
                UpdateFlightStatusUI(selectedFlight.Status);
            }
            catch (Exception ex)
            {
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
            cboFlightStatus.Text = status;

            // Enable/disable controls based on status
            bool isCheckInOpen = status == "CheckingIn";

            txtPassportNumber.Enabled = isCheckInOpen;
            btnSearch.Enabled = isCheckInOpen;
            cboAvailableSeats.Enabled = isCheckInOpen;
            btnCheckIn.Enabled = isCheckInOpen;

            // Update status label color
            lblFlightStatus.Text = $"Status: {status}";
            lblFlightStatus.ForeColor = GetStatusColor(status);
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

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            if (cboFlights.SelectedItem is not FlightDto selectedFlight)
                return;

            var passportNumber = txtPassportNumber.Text.Trim();

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
                    // Display passenger info
                    lblPassengerName.Text = _currentBooking.PassengerName;
                    lblBookingReference.Text = _currentBooking.BookingReference;

                    if (_currentBooking.CheckedIn)
                    {
                        MessageBox.Show("This passenger is already checked in.", "Already Checked In",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);

                        btnCheckIn.Enabled = false;
                    }
                    else
                    {
                        btnCheckIn.Enabled = true;
                    }

                    grpPassengerInfo.Visible = true;
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
                MessageBox.Show($"Error searching for booking: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void ClearPassengerInfo()
        {
            lblPassengerName.Text = "";
            lblBookingReference.Text = "";
            grpPassengerInfo.Visible = false;
            _currentBooking = null;
        }

        private async void btnCheckIn_Click(object sender, EventArgs e)
        {
            if (_currentBooking == null)
            {
                MessageBox.Show("Please search for a passenger first.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboAvailableSeats.SelectedItem is not SeatDto selectedSeat)
            {
                MessageBox.Show("Please select a seat.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                UseWaitCursor = true;

                var checkInRequest = new CheckInRequestDto
                {
                    BookingReference = _currentBooking.BookingReference,
                    FlightNumber = _currentBooking.FlightNumber,
                    SeatId = selectedSeat.SeatId,
                    StaffId = _staffId,
                    CounterId = _counterId,
                    PassengerName = _currentBooking.PassengerName
                };

                var (success, message, boardingPassPdf) = await _apiService.ProcessCheckInAsync(checkInRequest);

                if (success)
                {
                    MessageBox.Show("Check-in completed successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Print boarding pass
                    if (boardingPassPdf != null && boardingPassPdf.Length > 0)
                    {
                        var tempFile = Path.GetTempFileName() + ".pdf";
                        File.WriteAllBytes(tempFile, boardingPassPdf);

                        // Open the PDF file
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = tempFile,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        // If we didn't get a PDF from the API, generate one locally
                        // (normally we'd just use the one from the API)
                        if (cboFlights.SelectedItem is FlightDto selectedFlight)
                        {
                            PrintService.PrintBoardingPass(
                                _currentBooking.PassengerName,
                                selectedFlight.FlightNumber,
                                selectedFlight.Origin,
                                selectedFlight.Destination,
                                "TBD", // Gate would come from the API in a real system
                                selectedFlight.DepartureTime,
                                selectedSeat.SeatNumber,
                                _currentBooking.BookingReference);
                        }
                    }

                    // Clear the form for the next passenger
                    ClearPassengerInfo();
                    txtPassportNumber.Clear();

                    // Refresh available seats
                    await LoadAvailableSeatsAsync();
                }
                else
                {
                    MessageBox.Show($"Check-in failed: {message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during check-in: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async void btnUpdateStatus_Click(object sender, EventArgs e)
        {
            if (cboFlights.SelectedItem is not FlightDto selectedFlight)
                return;

            var newStatus = cboFlightStatus.Text;

            // Check if status has changed
            if (newStatus == selectedFlight.Status)
            {
                MessageBox.Show("Flight status is already set to this value.", "No Change",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                UseWaitCursor = true;

                var success = await _apiService.UpdateFlightStatusAsync(selectedFlight.FlightNumber, newStatus);

                if (success)
                {
                    MessageBox.Show("Flight status updated successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Update the selected flight's status in our local list
                    selectedFlight.Status = newStatus;

                    // Update UI with new status
                    UpdateFlightStatusUI(newStatus);
                }
                else
                {
                    MessageBox.Show("Failed to update flight status.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating flight status: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async void InitializeSignalRConnection()
        {
            try
            {
                // Get base URL from API service
                string baseUrl = _apiService.HttpClient.BaseAddress?.ToString() ?? "https://localhost:7215";

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{baseUrl}/flighthub")
                    .WithAutomaticReconnect()
                    .Build();

                // Handle flight status updates
                _hubConnection.On<dynamic>("FlightStatusChanged", async (message) =>
                {
                    string flightNumber = message.FlightNumber.ToString();
                    string newStatus = message.NewStatus.ToString();

                    // Update the matching flight in our flights list
                    var flight = _flights.FirstOrDefault(f => f.FlightNumber == flightNumber);
                    if (flight != null)
                    {
                        flight.Status = newStatus;

                        // If this is the currently selected flight, update the UI
                        if (cboFlights.SelectedItem is FlightDto selectedFlight &&
                            selectedFlight.FlightNumber == flightNumber)
                        {
                            // We need to use Invoke because we're on a different thread
                            this.Invoke((MethodInvoker)delegate
                            {
                                UpdateFlightStatusUI(newStatus);
                            });
                        }
                    }
                });

                // Handle seat assignments
                _hubConnection.On<dynamic>("SeatAssigned", async (message) =>
                {
                    string flightNumber = message.FlightNumber.ToString();
                    string seatId = message.SeatId.ToString();
                    bool isAssigned = (bool)message.IsAssigned;

                    // If this is for the currently selected flight, refresh the available seats
                    if (cboFlights.SelectedItem is FlightDto selectedFlight &&
                        selectedFlight.FlightNumber == flightNumber)
                    {
                        this.Invoke((MethodInvoker)async delegate
                        {
                            await LoadAvailableSeatsAsync();
                        });
                    }
                });

                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                // Just log the error but don't crash the application
                Console.WriteLine($"Error connecting to SignalR hub: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // Dispose of the SignalR connection
            if (_hubConnection != null)
            {
                _ = _hubConnection.DisposeAsync();
            }
        }

        // Designer-generated code for the form
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
            cboAvailableSeats = new ComboBox();
            label3 = new Label();
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
            groupBox2.Controls.Add(cboAvailableSeats);
            groupBox2.Controls.Add(label3);
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
            // cboAvailableSeats
            // 
            cboAvailableSeats.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAvailableSeats.FormattingEnabled = true;
            cboAvailableSeats.Location = new Point(157, 78);
            cboAvailableSeats.Name = "cboAvailableSeats";
            cboAvailableSeats.Size = new Size(453, 28);
            cboAvailableSeats.TabIndex = 1;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(25, 81);
            label3.Name = "label3";
            label3.Size = new Size(98, 20);
            label3.TabIndex = 0;
            label3.Text = "Assign Seat:";
            // 
            // grpPassengerInfo
            // 
            grpPassengerInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpPassengerInfo.Controls.Add(btnCheckIn);
            grpPassengerInfo.Controls.Add(lblBookingReference);
            grpPassengerInfo.Controls.Add(lblPassengerName);
            grpPassengerInfo.Controls.Add(label5);
            grpPassengerInfo.Controls.Add(label4);
            grpPassengerInfo.Location = new Point(12, 355);
            grpPassengerInfo.Name = "grpPassengerInfo";
            grpPassengerInfo.Size = new Size(776, 125);
            grpPassengerInfo.TabIndex = 4;
            grpPassengerInfo.TabStop = false;
            grpPassengerInfo.Text = "Passenger Information";
            grpPassengerInfo.Visible = false;
            // 
            // btnCheckIn
            // 
            btnCheckIn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCheckIn.BackColor = Color.FromArgb(46, 204, 113);
            btnCheckIn.FlatStyle = FlatStyle.Flat;
            btnCheckIn.ForeColor = Color.White;
            btnCheckIn.Location = new Point(616, 48);
            btnCheckIn.Name = "btnCheckIn";
            btnCheckIn.Size = new Size(131, 41);
            btnCheckIn.TabIndex = 4;
            btnCheckIn.Text = "Check In";
            btnCheckIn.UseVisualStyleBackColor = false;
            btnCheckIn.Click += btnCheckIn_Click;
            // 
            // lblBookingReference
            // 
            lblBookingReference.AutoSize = true;
            lblBookingReference.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblBookingReference.Location = new Point(170, 76);
            lblBookingReference.Name = "lblBookingReference";
            lblBookingReference.Size = new Size(14, 20);
            lblBookingReference.TabIndex = 3;
            lblBookingReference.Text = "-";
            // 
            // lblPassengerName
            // 
            lblPassengerName.AutoSize = true;
            lblPassengerName.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblPassengerName.Location = new Point(170, 38);
            lblPassengerName.Name = "lblPassengerName";
            lblPassengerName.Size = new Size(14, 20);
            lblPassengerName.TabIndex = 2;
            lblPassengerName.Text = "-";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(25, 76);
            label5.Name = "label5";
            label5.Size = new Size(139, 20);
            label5.TabIndex = 1;
            label5.Text = "Booking Reference:";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(25, 38);
            label4.Name = "label4";
            label4.Size = new Size(52, 20);
            label4.TabIndex = 0;
            label4.Text = "Name:";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 500);
            Controls.Add(grpPassengerInfo);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(lblCounterId);
            Controls.Add(lblStaffName);
            MinimumSize = new Size(818, 547);
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
        private ComboBox cboAvailableSeats;
        private Label label3;
        private GroupBox grpPassengerInfo;
        private Button btnCheckIn;
        private Label lblBookingReference;
        private Label lblPassengerName;
        private Label label5;
        private Label label4;
        private Label lblFlightStatus;
        private Button btnUpdateStatus;
        private ComboBox cboFlightStatus;
    };
}