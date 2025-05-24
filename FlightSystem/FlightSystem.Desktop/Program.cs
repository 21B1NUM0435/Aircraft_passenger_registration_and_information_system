namespace FlightSystem.Desktop;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Enable visual styles and high DPI support
        ApplicationConfiguration.Initialize();

        Console.WriteLine("🚀 Starting Flight Check-In Desktop Application");
        Console.WriteLine("🔌 Server URL: https://localhost:5001");

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Application error: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Console.WriteLine($"❌ Application error: {ex.Message}");
        }

        Console.WriteLine("👋 Application closed");
    }
}