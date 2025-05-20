using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlightManagementSystem.WinApp.Models;

namespace FlightManagementSystem.WinApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public HttpClient HttpClient => _httpClient;

        public ApiService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        }

        public async Task<List<FlightDto>> GetFlightsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/flights");
                response.EnsureSuccessStatusCode();

                var flights = await response.Content.ReadFromJsonAsync<List<FlightDto>>() ?? new List<FlightDto>();
                return flights;
            }
            catch (Exception ex)
            {
                // In a production app, you would log this error
                Console.WriteLine($"Error getting flights: {ex.Message}");
                throw;
            }
        }

        public async Task<BookingDto?> SearchBookingAsync(string passportNumber, string flightNumber)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/checkin/search?passportNumber={passportNumber}&flightNumber={flightNumber}");

                if (response.IsSuccessStatusCode)
                {
                    var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
                    return booking;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching booking: {ex.Message}");
                throw;
            }
        }

        public async Task<List<SeatDto>> GetAvailableSeatsAsync(string flightNumber)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/checkin/available-seats/{flightNumber}");
                response.EnsureSuccessStatusCode();

                var seats = await response.Content.ReadFromJsonAsync<List<SeatDto>>() ?? new List<SeatDto>();
                return seats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting available seats: {ex.Message}");
                throw;
            }
        }

        public async Task<(bool Success, string Message, byte[]? BoardingPassPdf)> ProcessCheckInAsync(CheckInRequestDto request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/checkin/process", content);

                if (response.IsSuccessStatusCode)
                {
                    var checkInResponse = await response.Content.ReadFromJsonAsync<CheckInResponseDto>();

                    if (checkInResponse != null)
                    {
                        return (checkInResponse.Success, checkInResponse.Message, checkInResponse.BoardingPassPdf);
                    }
                }

                // If we get here, something went wrong
                var errorContent = await response.Content.ReadAsStringAsync();

                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ErrorResponseDto>(errorContent);
                    return (false, errorResponse?.Error ?? "Unknown error", null);
                }
                catch
                {
                    return (false, errorContent, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing check-in: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<bool> UpdateFlightStatusAsync(string flightNumber, string newStatus)
        {
            try
            {
                var request = new UpdateFlightStatusDto { NewStatus = newStatus };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync($"api/flights/{flightNumber}/status", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating flight status: {ex.Message}");
                throw;
            }
        }
    }
}