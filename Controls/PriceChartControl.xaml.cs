﻿using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;

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
            _ = setup();
        }

        private async Task setup()
        {
            await FetchOrders();
            await DrawChart();
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
                }
                urlOrderBook = baseUrlOrderBook + $"&cursor={cursor}";
            }
            Console.WriteLine(_orders.Count);
        }

        private async Task DrawChart()
        {
            if (_orders.Count == 0)
                return;
            decimal minPrice = decimal.MaxValue;
            decimal maxPrice = decimal.MinValue;
            DateTime minTime = _orders[_orders.Count - 1].TimeStamp!.Value;
            DateTime maxTime = _orders[0].TimeStamp!.Value;

            foreach (Order order in _orders)
            {
                if (order.PriceTotal() < minPrice) minPrice = order.PriceTotal();
                if (order.PriceTotal() > maxPrice) maxPrice = order.PriceTotal();
            }
            Console.WriteLine($"Price range between {minTime.ToLocalTime().ToString("yyyy-MM-dd")} and {maxTime.ToLocalTime().ToString("yyyy-MM-dd")} was: {minPrice} - {maxPrice} {_token.Name}");
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
