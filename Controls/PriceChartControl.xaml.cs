using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;
using System.Drawing.Printing;
using System.Windows.Shapes;
using System.Windows.Media;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for PriceChartControl.xaml
    /// </summary>
    public partial class PriceChartControl : UserControl
    {
        private readonly int _proto;
        private readonly string _quality;
        private readonly int _days;
        private readonly Token _token;
        private readonly List<Order> _orders;

        public PriceChartControl(int proto, string quality, int days, Token token)
        {
            InitializeComponent();
            _proto = proto;
            _quality = quality;
            _days = days;
            _token = token;
            _orders = new();
        }

        private async void setup(object sender, RoutedEventArgs e)
        {
            DrawChart();
            await FetchOrders();
        }

            private async Task FetchOrders()
        {
            // Fetch orders in the IMX global orderbook for the specified card of the specified quality listed in the selected token.
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + _proto + "\"],\"quality\":[\"" + _quality + "\"]}");
            string token_str = _token.Address;
            if (token_str.Equals("ETH"))
                token_str = "&buy_token_type=ETH";
            DateTime currentTime = DateTime.UtcNow;
            string minTimestamp = currentTime.AddDays(-_days).ToString("yyyy-MM-dd") + "T00:00:00Z";
            bool remaining = true;

            List<Order> dayOrders = new List<Order>();
            string baseUrlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=desc&include_fees=true&order_by=updated_at&updated_min_timestamp={minTimestamp}&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=filled";
            string urlOrderBook = baseUrlOrderBook;
            while (remaining)
            {
                string ordersString = await ResourceManager.Client.GetStringAsync(urlOrderBook);
                // Extract orders from the data returned by the server.
                JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(ordersString);
                if (jsonOrders == null)
                    return;
                string? cursor = (string?)jsonOrders.SelectToken("cursor");
                remaining = ((int)(jsonOrders.SelectToken("remaining") ?? 0)) == 1;
                JToken? result = jsonOrders.SelectToken("result");
                if (result != null)
                {
                    foreach (JToken order in result)
                    {
                        try
                        {
                            Order or = new Order(order, _token.Name);
                            if (or.TimeStamp == null)
                                continue;
                            _orders.Add(or);
                            /*Console.WriteLine($"At {or.TimeStamp.Value.ToLocalTime()}, sold for {or.PriceTotal()} {_token.Name}");
                            if (dayOrders.Count == 0 || dayOrders[0].TimeStamp!.Value.ToLocalTime().ToString("yyyy-MM-dd") == or.TimeStamp.Value.ToLocalTime().ToString("yyyy-MM-dd")) 
                            {
                                dayOrders.Add(or);
                            }
                            else
                            {
                                Console.WriteLine($"{dayOrders.Count} sales on {dayOrders[0].TimeStamp!.Value.ToLocalTime().ToString("yyyy-MM-dd")}");
                                dayOrders.Clear();
                                dayOrders.Add(or);
                            }*/
                        }
                        catch (NullReferenceException)
                        {
                        }
                    }
                    DrawChart();
                }
                urlOrderBook = baseUrlOrderBook + $"&cursor={cursor}";
            }
            Console.WriteLine(_orders.Count);
        }

        private void DrawChart()
        {
            canvasChart.Children.Clear();
            double canvasWidth = canvasChart.ActualWidth;
            double canvasHeight = canvasChart.ActualHeight - 1;
            if (canvasWidth == 0)
                return;
            // Render base
            Rectangle rectBase = new Rectangle
            {
                Width = canvasWidth,
                Height = canvasHeight,
                Fill = Brushes.White,
            };
            Canvas.SetLeft(rectBase, 0);
            Canvas.SetBottom(rectBase, 0);
            canvasChart.Children.Add(rectBase);
            // Render outline
            Rectangle rectOutline = new Rectangle
            {
                Width = canvasWidth,
                Height = canvasHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(rectOutline, 0);
            Canvas.SetBottom(rectOutline, 0);
            canvasChart.Children.Add(rectOutline);
            // Render Y-axis
            Line yAxis = new Line
            {
                X1 = 55,
                Y1 = 10,
                X2 = 55,
                Y2 = canvasHeight - 40,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvasChart.Children.Add(yAxis);
            // Render X-axis
            Line xAxis = new Line
            {
                X1 = 55,
                Y1 = canvasHeight - 40,
                X2 = canvasWidth - 10,
                Y2 = canvasHeight - 40,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvasChart.Children.Add(xAxis);
            if (_orders.Count == 0)
                return;
            decimal maxPrice = decimal.MinValue;

            foreach (Order order in _orders)
            {
                if (order.PriceTotal() > maxPrice) maxPrice = order.PriceTotal();
            }
            decimal maxGraph = maxPrice * new decimal(1.1);

            // Draw price labels
            for (int i = 0; i <= 5; i++)
            {
                TextBlock priceLabel = new TextBlock
                {
                    Text = (maxGraph * i / 5).ToString("0.000000").Substring(0, 8),
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 0, 0, -10)
                };
                Canvas.SetLeft(priceLabel, 5);
                Canvas.SetTop(priceLabel, 10 - 5);
                Canvas.SetTop(priceLabel, canvasHeight - (canvasHeight - 53) * i / 5 - 50);
                canvasChart.Children.Add(priceLabel);
            }

            // Draw date labels
            for (int i = 0; i < _days; i++)
            {
                double xPosDate = ((double)(_days - i - 1) / (_days + 1)) * (canvasWidth - 40) + 25;
                TextBlock dateLabel = new TextBlock
                {
                    Text = DateTime.UtcNow.AddDays(-i).ToString("MMM-dd"),
                    Foreground = Brushes.Black,
                    RenderTransform = new RotateTransform(-45),
                };
                Canvas.SetLeft(dateLabel, xPosDate);
                Canvas.SetBottom(dateLabel, -5);
                canvasChart.Children.Add(dateLabel);
            }

            // Draw sales
            DateTime startTime = DateTime.UtcNow.AddDays(-_days);
            DateTime startDay = new DateTime(startTime.Year, startTime.Month, startTime.Day);
            DateTime endDay = startDay.AddDays(_days + 1);

            double? xPosPrev = null;
            double? yPosPrev = null;
            double circleSize = 8;
            foreach (Order order in _orders)
            {
                if (order.TimeStamp == null)
                    continue;
                double priceFraction = ((double)(order.PriceTotal() / maxGraph));
                DateTime timeStamp = order.TimeStamp.Value;
                double timeFraction = (timeStamp - startDay).TotalSeconds / (endDay - startDay).TotalSeconds;

                double xPos = timeFraction * (canvasWidth - 65) + 55;
                double yPos = priceFraction * (canvasHeight - 50) + 40;

                Ellipse circle = new Ellipse
                {
                    Width = circleSize,
                    Height = circleSize,
                    Fill = Brushes.Blue
                };

                Canvas.SetLeft(circle, xPos - circleSize / 2);
                Canvas.SetBottom(circle, yPos - circleSize / 2 + 1);
                canvasChart.Children.Add(circle);

                if (xPosPrev != null && yPosPrev != null)
                {
                    Line line = new Line
                    {
                        X1 = xPosPrev.Value,
                        Y1 = canvasHeight - yPosPrev.Value,
                        X2 = xPos,
                        Y2 = canvasHeight - yPos,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 2
                    };
                    canvasChart.Children.Add(line);
                }
                xPosPrev = xPos;
                yPosPrev = yPos;
            }
        }

        private void UserControl_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Get the position of the mouse click relative to the buyGrid
            Point clickPoint = e.GetPosition(controlGrid);

            // Check if the click occurred on the buyGrid
            if (clickPoint.X >= 0 && clickPoint.X < controlGrid.ActualWidth &&
                clickPoint.Y >= 0 && clickPoint.Y < controlGrid.ActualHeight)
            {
                return;
            }
            // Click occurred outside buyGrid, you can call your function here
            this.Visibility = Visibility.Collapsed;
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double maxWidth = 1050;
            double maxHeight = 700;

            double width = Math.Min(this.ActualWidth, maxWidth);
            double height = width / 1.4;

            if (height > this.ActualHeight)
            {
                height = Math.Min(this.ActualHeight, maxHeight);
                width = height * 1.4;
            }

            this.controlGrid.Height = height - 10;
            this.controlGrid.Width = width - 10;
        }
    }
}
