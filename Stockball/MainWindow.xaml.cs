using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Stockball
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _http = new HttpClient();
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private string _stockCode = "sh600519";
        private double _baseFontSize = 14;

        // 配置文件路径（放在 exe 同目录）
        private readonly string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "stockball.config");

        public MainWindow()
        {
            InitializeComponent();
            _http.DefaultRequestHeaders.Add("Referer", "https://finance.sina.com.cn");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            LoadConfig();  // 启动时读取上次的设置
            ApplyFontSize();

            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += async (s, e) => await RefreshAsync();
            _timer.Start();
            Loaded += async (s, e) => await RefreshAsync();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var lines = File.ReadAllLines(_configPath);
                    if (lines.Length >= 1) _stockCode = lines[0];
                    if (lines.Length >= 2 && double.TryParse(lines[1], out var fontSize))
                        _baseFontSize = fontSize;
                }
            }
            catch { /* 读取失败就用默认值 */ }
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllLines(_configPath, new[] {
                    _stockCode,
                    _baseFontSize.ToString()
                });
            }
            catch { /* 保存失败不影响使用 */ }
        }

        private void ApplyFontSize()
        {
            NameText.FontSize = _baseFontSize * 0.8;
            PriceText.FontSize = _baseFontSize * 1.1;
            ChangeText.FontSize = _baseFontSize * 0.9;
        }

        private async Task RefreshAsync()
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(
                    $"https://hq.sinajs.cn/list={_stockCode}");
                var text = Encoding.GetEncoding("GBK").GetString(bytes);
                var start = text.IndexOf('"') + 1;
                var end = text.LastIndexOf('"');
                if (end <= start) { NameText.Text = "无数据"; return; }

                var parts = text.Substring(start, end - start).Split(',');
                if (parts.Length < 4) { NameText.Text = "无数据"; return; }

                string name = parts[0];
                double prevClose = double.Parse(parts[2]);
                double price = double.Parse(parts[3]);
                double changePct = prevClose == 0 ? 0 : (price - prevClose) / prevClose * 100;

                NameText.Text = name;
                PriceText.Text = price.ToString("F2");
                ChangeText.Text = (changePct >= 0 ? "+" : "") + changePct.ToString("F2") + "%";

                var color = changePct >= 0
                    ? Color.FromRgb(0xE0, 0x30, 0x30)
                    : Color.FromRgb(0x18, 0xA0, 0x50);
                var brush = new SolidColorBrush(color);
                NameText.Foreground = brush;
                PriceText.Foreground = brush;
                ChangeText.Foreground = brush;
            }
            catch
            {
                NameText.Text = "网络错误";
            }
        }

        private void FontBigger_Click(object sender, RoutedEventArgs e)
        {
            _baseFontSize += 2;
            ApplyFontSize();
            SaveConfig();  // 保存设置
        }

        private void FontSmaller_Click(object sender, RoutedEventArgs e)
        {
            if (_baseFontSize > 6) _baseFontSize -= 2;
            ApplyFontSize();
            SaveConfig();  // 保存设置
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private async void ChangeStock_Click(object sender, RoutedEventArgs e)
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "输入股票代码（如 sh600519、sz000001）：", "切换股票", _stockCode);
            if (!string.IsNullOrWhiteSpace(input))
            {
                _stockCode = input.Trim();
                SaveConfig();  // 保存设置
                await RefreshAsync();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}