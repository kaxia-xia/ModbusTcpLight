using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ModbusTcpFull;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModbusTcpLight
{
    public sealed partial class MainWindow : Window
    {
        // 定义LED灯的两种状态颜色：熄灭（灰色）、点亮（绿色）
        private readonly SolidColorBrush _ledOffColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204));
        private readonly SolidColorBrush _ledOnColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 200, 0));
        private ModbusTcpMaster? _modbusMaster;
        private readonly List<Ellipse> _leds;
        private bool _isConnected = false;
        private string _ip = "";
        private Task? _receiveTask;

        public MainWindow()
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            _leds = [Led1, Led2, Led3, Led4, Led5, Led6, Led7, Led8];
            _leds.ForEach(x => {
                x.Fill = _ledOffColor;
                x.IsHitTestVisible = false;
            });
            StatusTextBlock.Text = "未连接";
        }

        private void Led_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Ellipse led)
            {
                if (led.Fill == _ledOffColor)
                {
                    switch (led.Name)
                    {
                        case "Led1":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 1, true);
                            break;
                        case "Led2":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 2, true);
                            break;
                        case "Led3":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 3, true);
                            break;
                        case "Led4":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 4, true);
                            break;
                        case "Led5":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 5, true);
                            break;
                        case "Led6":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 6, true);
                            break;
                        case "Led7":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 7, true);
                            break;
                        case "Led8":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 8, true);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    switch (led.Name)
                    {
                        case "Led1":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 1, false);
                            break;
                        case "Led2":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 2, false);
                            break;
                        case "Led3":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 3, false);
                            break;
                        case "Led4":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 4, false);
                            break;
                        case "Led5":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 5, false);
                            break;
                        case "Led6":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 6, false);
                            break;
                        case "Led7":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 7, false);
                            break;
                        case "Led8":
                            _ = _modbusMaster?.WriteSingleCoilAsync(0, 8, false);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            _ip = TxtIp.Text.Trim();
            if (_ip == null || _ip == "")
            {
                StatusTextBlock.Text = "请输入Modbus服务器IP地址";
                return;
            }
            _modbusMaster = new ModbusTcpMaster(_ip);
            if (_modbusMaster == null)
            {
                StatusTextBlock.Text = "无法创建Modbus主站实例";
                return;
            }
            _modbusMaster?.ConnectAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusTextBlock.Text = "连接Modbus服务器失败：" + t.Exception?.GetBaseException().Message;
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusTextBlock.Text = "已连接到Modbus服务器";
                        _isConnected = true;
                        ConnectButton.IsEnabled = false;
                        DisconnectButton.IsEnabled = true;
                        AllLedsOn.IsEnabled = true;
                        _leds.ForEach(x => x.IsHitTestVisible = true);
                        _receiveTask = new Task(() =>
                        {
                            while (_isConnected)
                            {
                                var res = _modbusMaster?.ReadCoilsAsync(1, 1, 8).Result;
                                for (int i = 0; i < res?.Length; i++)
                                {
                                    var led = _leds[i];
                                    var isOn = res[i];
                                    _ = DispatcherQueue.TryEnqueue(() =>
                                    {
                                        led.Fill = isOn ? _ledOnColor : _ledOffColor;
                                    });
                                }
                                Task.Delay(200).Wait();
                            }
                        });
                        _receiveTask.Start();
                    });
                }
            });
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            _isConnected = false;
            try
            {
                _receiveTask?.Wait();
                _modbusMaster?.Disconnect();
            }
            finally
            {
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                AllLedsOn.IsEnabled = false;
                StatusTextBlock.Text = "未连接";
                foreach (var led in _leds)
                {
                    led.IsHitTestVisible = false;
                    led.Fill = _ledOffColor;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                _ = _modbusMaster?.WriteMultipleCoilsAsync(0, 1, [true, true, true, true, true, true, true, true]);
            }
        }

        private void AllLedsOff_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                _ = _modbusMaster?.WriteMultipleCoilsAsync(0, 1, [false, false, false, false, false, false, false, false]);
            }
        }
    }
}