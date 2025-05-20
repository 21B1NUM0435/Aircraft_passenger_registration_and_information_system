namespace FlightManagementSystem.Web.Models.Api
{
    public class PassengerDto
    {
        public string PassengerId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PassportNumber { get; set; } = string.Empty;
    }
}