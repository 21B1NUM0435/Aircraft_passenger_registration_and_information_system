using System.Drawing;
using ZXing;
using ZXing.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZXing.QrCode;

namespace FlightManagementSystem.WinApp.Services
{
    public class PrintService
    {
        [Obsolete]
        public static void PrintBoardingPass(string passengerName, string flightNumber, string origin, string destination,
            string gate, DateTime departureTime, string seatNumber, string bookingReference)
        {
            // In a real application, this would actually print the boarding pass to a printer
            // For this example, we'll just save it as a PDF

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"BoardingPass_{bookingReference}.pdf");

            GenerateBoardingPassPdf(passengerName, flightNumber, origin, destination,
                gate, departureTime, seatNumber, bookingReference)
                .GeneratePdf(path);

            // Open the file with the default viewer
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        [Obsolete]
        public static QuestPDF.Fluent.Document GenerateBoardingPassPdf(string passengerName, string flightNumber, string origin,
            string destination, string gate, DateTime departureTime, string seatNumber, string bookingReference)
        {
            return QuestPDF.Fluent.Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A5.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content()
                        .PaddingVertical(10)
                        .Grid(grid =>
                        {
                            grid.Columns(12);

                            // Airline Logo and Header
                            grid.Item(12)
                                .BorderBottom(1)
                                .PaddingBottom(10)
                                .Row(row =>
                                {
                                    row.RelativeItem(4)
                                        .Text("Mongolian Airlines")
                                        .FontSize(20)
                                        .Bold();

                                    row.RelativeItem(8)
                                        .AlignRight()
                                        .Text("BOARDING PASS")
                                        .FontSize(16)
                                        .Bold();
                                });

                            // Flight Info
                            grid.Item(8)
                                .PaddingTop(15)
                                .Grid(flightGrid =>
                                {
                                    flightGrid.Columns(2);

                                    // Passenger Info
                                    flightGrid.Item(2)
                                        .PaddingBottom(10)
                                        .Row(r =>
                                        {
                                            r.RelativeItem()
                                                .Text("PASSENGER:")
                                                .Bold();

                                            r.RelativeItem(3)
                                                .Text(passengerName);
                                        });

                                    // Flight Number
                                    flightGrid.Item(1)
                                        .PaddingBottom(10)
                                        .Row(r =>
                                        {
                                            r.RelativeItem()
                                                .Text("FLIGHT:")
                                                .Bold();

                                            r.RelativeItem()
                                                .Text(flightNumber);
                                        });

                                    // Seat
                                    flightGrid.Item(1)
                                        .PaddingBottom(10)
                                        .Row(r =>
                                        {
                                            r.RelativeItem()
                                                .Text("SEAT:")
                                                .Bold();

                                            r.RelativeItem()
                                                .Text(seatNumber);
                                        });

                                    // From/To
                                    flightGrid.Item(2)
                                        .PaddingBottom(5)
                                        .Grid(g =>
                                        {
                                            g.Columns(2);

                                            g.Item(1)
                                                .Text("FROM:")
                                                .Bold();

                                            g.Item(1)
                                                .Text("TO:")
                                                .Bold();

                                            g.Item(1)
                                                .Text(origin);

                                            g.Item(1)
                                                .Text(destination);
                                        });

                                    // Date/Time
                                    flightGrid.Item(1)
                                        .PaddingBottom(10)
                                        .Row(r =>
                                        {
                                            r.RelativeItem()
                                                .Text("DATE:")
                                                .Bold();

                                            r.RelativeItem(2)
                                                .Text(departureTime.ToString("dd MMM yyyy"));
                                        });

                                    // Time
                                    flightGrid.Item(1)
                                        .PaddingBottom(10)
                                        .Row(r =>
                                        {
                                            r.RelativeItem()
                                                .Text("TIME:")
                                                .Bold();

                                            r.RelativeItem()
                                                .Text(departureTime.ToString("HH:mm"));
                                        });

                                    // Gate
                                    flightGrid.Item(1)
                                        .PaddingBottom(10)
                                        .Row(r =>
                                        {
                                            r.RelativeItem()
                                                .Text("GATE:")
                                                .Bold();

                                            r.RelativeItem()
                                                .Text(gate);
                                        });

                                    // Boarding Time
                                    flightGrid.Item(1)
                                        .PaddingBottom(10)
                                        .Row(r =>
                                        {
                                            r.RelativeItem()
                                                .Text("BOARDING:")
                                                .Bold();

                                            r.RelativeItem(2)
                                                .Text(departureTime.AddMinutes(-30).ToString("HH:mm"));
                                        });
                                });

                            // Barcode and Reference
                            grid.Item(4)
                                .PaddingTop(15)
                                .AlignCenter()
                                .AlignMiddle()
                                .Column(column =>
                                {
                                    // Barcode
                                    var barcodeImage = GenerateBarcode(bookingReference);
                                    column.Item().Height(100).Image(barcodeImage);

                                    // Booking Reference
                                    column.Item().AlignCenter().Text(bookingReference).FontSize(12).Bold();
                                });

                            // Footer
                            grid.Item(12)
                                .BorderTop(1)
                                .PaddingTop(10)
                                .AlignCenter()
                                .Text(text =>
                                {
                                    text.Span("Please be at the gate at least 30 minutes before departure. ");
                                    text.Span("Thank you for flying with Mongolian Airlines!");
                                });
                        });
                });
            });
        }

        private static byte[] GenerateBarcode(string data)
        {
            var writer = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 80,
                    Width = 250,
                    Margin = 0
                }
            };

            using var bitmap = writer.Write(data);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
    }
}