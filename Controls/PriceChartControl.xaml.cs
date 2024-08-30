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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

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
        private readonly ObservableCollection<Sale> _sales;
        private readonly CancellationTokenSource _masterCTS;

        public PriceChartControl(int proto, string quality, int days, Token token)
        {
            InitializeComponent();
            _proto = proto;
            _quality = quality;
            _days = days;
            _token = token;
            _orders = new();
            _sales = new();
            _masterCTS = new();
            SalesListView.ItemsSource = _sales;
            tbTitle.Text = $"Sale History ({token.Name})";
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

            string baseUrlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=desc&include_fees=true&order_by=updated_at&updated_min_timestamp={minTimestamp}&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=filled";
            string urlOrderBook = baseUrlOrderBook;
            Task<Dictionary<string, Token>> tokenInfo = Wallet.FetchTokens();
            while (remaining)
            {
                string ordersString;
                try
                {
                    ordersString = await ResourceManager.Client.GetStringAsync(urlOrderBook, _masterCTS.Token);
                } catch (OperationCanceledException)
                {
                    break;
                }
                // Extract orders from the data returned by the server.
                JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(ordersString);
                if (jsonOrders == null)
                    break;
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
                            string convertedPrice = "";
                            if ((await tokenInfo).ContainsKey(_token.Name))
                            {
                                decimal? conversionFactor = (await tokenInfo)[_token.Name].Value;
                                convertedPrice = conversionFactor == null ? "" : $"(${(conversionFactor.Value * or.PriceTotal()).ToString("0.00")})";
                            }
                            _sales.Add(new Sale(or.TimeStamp.Value.ToLocalTime().ToString("MMM-dd HH:mm"), $"{or.PriceTotal().ToString("0.000000").Substring(0, 8)} {_token.Name} {convertedPrice}"));
                        }
                        catch (NullReferenceException)
                        {
                        }
                    }
                    DrawChart();
                }
                if (_orders.Count == 0)
                {
                    tbMonthVolume.Text = "-";
                    tbMonthAverage.Text = "-";
                    tbMonthChange.Text = "-";
                    tbWeekVolume.Text = "-";
                    tbWeekAverage.Text = "-";
                    tbWeekChange.Text = "-";
                }
                else
                {
                    // Monthy change.
                    decimal changeMonth = (_orders[0].PriceTotal() - _orders[_orders.Count - 1].PriceTotal()) / _orders[_orders.Count - 1].PriceTotal() * 100;
                    tbMonthVolume.Text = $"{_orders.Count} sales";
                    tbMonthAverage.Text = $"{_orders.Average(x => x.PriceTotal()).ToString("0.000000").Substring(0, 8)} {_token.Name}";
                    tbMonthChange.Text = $"{changeMonth.ToString("0.00")}%";
                    if (changeMonth >= 0)
                        tbMonthChange.Foreground = Brushes.Green;
                    else
                        tbMonthChange.Foreground = Brushes.Red;

                    // Weekly change
                    List<Order> ordersWeek = _orders.Where(x => x.TimeStamp != null && x.TimeStamp > currentTime.AddDays(-7)).ToList();
                    if (ordersWeek.Count == 0)
                    {
                        tbWeekVolume.Text = "-";
                        tbWeekAverage.Text = "-";
                        tbWeekChange.Text = "-";
                    }
                    else
                    {
                        decimal changeWeek = (ordersWeek[0].PriceTotal() - ordersWeek[ordersWeek.Count - 1].PriceTotal()) / ordersWeek[ordersWeek.Count - 1].PriceTotal() * 100;
                        tbWeekVolume.Text = $"{ordersWeek.Count} sales";
                        tbWeekAverage.Text = $"{ordersWeek.Average(x => x.PriceTotal()).ToString("0.000000").Substring(0, 8)} {_token.Name}";
                        tbWeekChange.Text = $"{changeWeek.ToString("0.00")}%";
                        if (changeWeek >= 0)
                            tbWeekChange.Foreground = Brushes.Green;
                        else
                            tbWeekChange.Foreground = Brushes.Red;
                    }
                }
                urlOrderBook = baseUrlOrderBook + $"&cursor={cursor}";
            }
            Console.WriteLine("Done");
            spinner.Visibility = Visibility.Collapsed;
        }

        private void DrawChart()
        {
            canvasChart.Children.Clear();
            //canvasChart.Height = controlGrid.Height / 2;
            //canvasChart.Width = controlGrid.Width - 10;
            double canvasWidth = canvasChart.ActualWidth - 1;
            double canvasHeight = canvasChart.ActualHeight - 1;
            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                Console.WriteLine($"{canvasWidth} x {canvasChart.Height}");
                return;
            }
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
                Canvas.SetBottom(circle, yPos - circleSize / 2);
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
            // Click occurred outside buyGrid.
            _masterCTS.Cancel();
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
            controlGrid.Height = height - 10;
            controlGrid.Width = width - 10;
        }

        private void canvasChart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _masterCTS.Cancel();
            this.Visibility = Visibility.Collapsed;
        }
    }

    public class Sale
    {
        public string DateTime { get; set; }
        public string Price { get; set; }

        public Sale(string dateTime, string price)
        {
            DateTime = dateTime;
            Price = price;
        }
    }
}
