using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stockball
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _http = new HttpClient();
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        private List<string> _stockCodes = new List<string> { "sh600519" };
        private double _baseFontSize = 14;

        private readonly List<(TextBlock name, TextBlock price, TextBlock change)> _rows
            = new List<(TextBlock, TextBlock, TextBlock)>();

        private readonly string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "stockball.config");

        public MainWindow()
        {
            InitializeComponent();
            _http.DefaultRequestHeaders.Add("Referer", "https://finance.sina.com.cn");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            LoadConfig();
            BuildRows();

            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += async (s, e) => await RefreshAsync();
            _timer.Start();
            Loaded += async (s, e) => await RefreshAsync();
        }

        private void BuildRows()
        {
            StockList.Children.Clear();
            _rows.Clear();

            foreach (var _ in _stockCodes)
            {
                var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };

                var name = new TextBlock
                {
                    Text = "加载中",
                    Foreground = Brushes.White,
                    FontSize = _baseFontSize * 0.8,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var price = new TextBlock
                {
                    Text = "--",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = _baseFontSize * 1.1,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var change = new TextBlock
                {
                    Text = "--",
                    Foreground = Brushes.White,
                    FontSize = _baseFontSize * 0.9,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                panel.Children.Add(name);
                panel.Children.Add(price);
                panel.Children.Add(change);
                StockList.Children.Add(panel);

                _rows.Add((name, price, change));
            }
        }

        private void ApplyFontSize()
        {
            foreach (var row in _rows)
            {
                row.name.FontSize = _baseFontSize * 0.8;
                row.price.FontSize = _baseFontSize * 1.1;
                row.change.FontSize = _baseFontSize * 0.9;
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var lines = File.ReadAllLines(_configPath);
                    if (lines.Length >= 1 && !string.IsNullOrWhiteSpace(lines[0]))
                    {
                        _stockCodes = lines[0]
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => s.Length > 0)
                            .Take(3)
                            .ToList();
                    }
                    if (lines.Length >= 2 && double.TryParse(lines[1], out var fontSize))
                        _baseFontSize = fontSize;
                }
            }
            catch { }

            if (_stockCodes.Count == 0)
                _stockCodes.Add("sh600519");
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllLines(_configPath, new[] {
                    string.Join(",", _stockCodes),
                    _baseFontSize.ToString()
                });
            }
            catch { }
        }

        private async Task RefreshAsync()
        {
            for (int i = 0; i < _stockCodes.Count && i < _rows.Count; i++)
            {
                await RefreshOneAsync(_stockCodes[i], _rows[i]);
            }
        }

        private async Task RefreshOneAsync(string code,
            (TextBlock name, TextBlock price, TextBlock change) row)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(
                    $"https://hq.sinajs.cn/list={code}");
                var text = Encoding.GetEncoding("GBK").GetString(bytes);
                var start = text.IndexOf('"') + 1;
                var end = text.LastIndexOf('"');
                if (end <= start) { row.name.Text = "无数据"; return; }

                var parts = text.Substring(start, end - start).Split(',');
                if (parts.Length < 4) { row.name.Text = "无数据"; return; }

                string name = parts[0];
                double prevClose = double.Parse(parts[2]);
                double price = double.Parse(parts[3]);
                double changePct = prevClose == 0 ? 0 : (price - prevClose) / prevClose * 100;

                row.name.Text = name;
                row.price.Text = price.ToString("F2");
                row.change.Text = (changePct >= 0 ? "+" : "") + changePct.ToString("F2") + "%";

                var color = changePct >= 0
                    ? Color.FromRgb(0xE0, 0x30, 0x30)
                    : Color.FromRgb(0x18, 0xA0, 0x50);
                var brush = new SolidColorBrush(color);
                row.name.Foreground = brush;
                row.price.Foreground = brush;
                row.change.Foreground = brush;
            }
            catch
            {
                row.name.Text = "网络错误";
            }
        }

        // Ctrl + 滚轮调整字体
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    _baseFontSize += 1;
                else if (_baseFontSize > 6)
                    _baseFontSize -= 1;

                ApplyFontSize();
                SaveConfig();
                e.Handled = true;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private async void ChangeStock_Click(object sender, RoutedEventArgs e)
        {
            var current = string.Join(",", _stockCodes);
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "输入股票代码，用英文逗号分隔，最多3只：\n例如 sh600519,sz000001,sh601318",
                "设置股票", current);

            if (!string.IsNullOrWhiteSpace(input))
            {
                var codes = input
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .Take(3)
                    .ToList();

                if (codes.Count > 0)
                {
                    _stockCodes = codes;
                    SaveConfig();
                    BuildRows();
                    await RefreshAsync();
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}