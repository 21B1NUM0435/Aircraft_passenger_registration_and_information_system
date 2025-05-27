namespace FlightSystem.Desktop
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _signalRService?.Dispose();
                _httpClient?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}