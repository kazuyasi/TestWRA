using Microsoft.Maker.Firmata;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace TestWRA
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        RemoteDevice arduino;
        UsbSerial usbSerial;
        UwpFirmata firmata;

        public MainPage()
        {
            this.InitializeComponent();

        }

        private async void grid1_Loaded(object sender, RoutedEventArgs e)
        {
            var devices = await UsbSerial.listAvailableDevicesAsync();

            int idx = -1;

            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Name.StartsWith("Arduino"))
                {
                    idx = i;
                }
            }

            if (idx != -1)
            {
                usbSerial = new UsbSerial(devices[idx]);
                firmata = new UwpFirmata();
                arduino = new RemoteDevice(firmata);
                firmata.begin(usbSerial);
                //arduino.DeviceReady += OnDeviceReady;
                //usbSerial.ConnectionEstablished += OnDeviceReady;
                firmata.FirmataConnectionReady += OnDeviceReady;
                //arduino.SysexMessageReceived += DataRecieved;
                firmata.SysexMessageReceived += Firmata_SysexMessageReceived;
                firmata.PinCapabilityResponseReceived += Firmata_PinCapabilityResponseReceived;
                firmata.DigitalPortValueUpdated += Firmata_DigitalPortValueUpdated;
                usbSerial.begin(57600, SerialConfig.SERIAL_8N1);
                
            }
        }

        private void Firmata_DigitalPortValueUpdated(UwpFirmata caller, CallbackEventArgs argv)
        {
            
            byte port = argv.getPort();
        }

        private void Firmata_PinCapabilityResponseReceived(UwpFirmata caller, SysexCallbackEventArgs argv)
        {
            byte cmd = argv.getCommand();
        }

        int rcvCmd = -1;
        byte[] rcvData = null; 

        /// <summary>
        /// SYSEXメッセージをクリアする。
        /// </summary>
        private void ClearSysemMessage()
        {
            rcvCmd = -1;
            rcvData = null;
        }

        private void GetSysexMessage(SysexCommand command, byte[] sndMessage)
        {
            ClearSysemMessage();

            //返信専用コマンドは受信待ちせずに終了する。
            switch (command)
            {
                case SysexCommand.I2C_REPLY:
                case SysexCommand.PIN_STATE_RESPONSE:
                case SysexCommand.CAPABILITY_RESPONSE:
                case SysexCommand.ANALOG_MAPPING_RESPONSE:
                    return;
                default:
                    break;
            }

            firmata.sendSysex(command, sndMessage.AsBuffer());

            Task.Delay(100);

            //タイムアウトを監視しながら受信をまつ
            DateTime startTime = DateTime.Now;
            while (rcvCmd == -1)
            {
                if ((DateTime.Now - startTime).Milliseconds > 100)
                {
                    GetSysexMessage(command, sndMessage);
                }
            }
        }

        private void GetSysexMessage(SysexCommand command)
        {
            ClearSysemMessage();

            //返信専用コマンドは受信待ちせずに終了する。
            switch (command)
            {
                case SysexCommand.I2C_REPLY:
                case SysexCommand.PIN_STATE_RESPONSE:
                case SysexCommand.CAPABILITY_RESPONSE:
                case SysexCommand.ANALOG_MAPPING_RESPONSE:
                    return;
                default:
                    break;
            }
            firmata.sendSysex(command, new byte[] { }.AsBuffer());

            Task.Delay(100);

            //タイムアウトを監視しながら受信をまつ
            DateTime startTime = DateTime.Now;
            while (rcvCmd == -1)
            {
                if((DateTime.Now - startTime).Milliseconds > 100)
                {
                    GetSysexMessage(command);
                }
            }
        }

        private void Firmata_SysexMessageReceived(UwpFirmata caller, SysexCallbackEventArgs argv)
        {
            rcvCmd = Convert.ToInt32(argv.getCommand());
            rcvData = argv.getDataBuffer().ToArray();
        }

        private async void StringRecieved(string msg)
        {
            await new MessageDialog(msg).ShowAsync();
        }

        private async void ConnectionFaild(string msg)
        {
            await new MessageDialog(msg).ShowAsync();
        }

        private async void OnDeviceReady()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //ユーザーインターフェースを操作する
                textBlock0.Text = "Device";
                textBlock.Text = "Started.";
            });
        }

        /// <summary>
        /// 特定の値から指定ビットを取り出す。
        /// </summary>
        /// <param name="value">値</param>
        /// <param name="startBit">開始ビット</param>
        /// <param name="endBit">終了ビット</param>
        /// <returns>ぬきだしたビット</returns>
        private byte Bits(int value,  byte startBit, byte endBit )
        {
            if (startBit > endBit) throw new ArgumentException();
            if ((endBit - startBit) > 7) throw new ArgumentException();

            byte ret = (byte)(value >> startBit);

            byte mask = 0;
            switch(endBit - startBit)
            {
                case 0: mask = 0b00000001; break;
                case 1: mask = 0b00000011; break;
                case 2: mask = 0b00000111; break;
                case 3: mask = 0b00001111; break;
                case 4: mask = 0b00011111; break;
                case 5: mask = 0b00111111; break;
                case 6: mask = 0b01111111; break;
                case 7: mask = 0b11111111; break;
            }

            ret &= mask;

            return ret;
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            //GetSysexMessage(SysexCommand.REPORT_FIRMWARE);

            //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    //ユーザーインターフェースを操作する
            //    textBlock0.Text =  rcvData[0].ToString() + "." + rcvData[1].ToString();
            //    textBlock.Text = ExtractStringFromSysexMessage(2);
            //});

            GetSysexMessage(SysexCommand.PIN_STATE_QUERY, new byte[] { 3 });

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //ユーザーインターフェースを操作する
                textBlock0.Text = ((PinMode)rcvData[1]).ToString();
                textBlock.Text = rcvData[2].ToString();
            });
        }

        /// <summary>
        /// 受信したSYSEXメッセージから文字列を抽出する。
        /// </summary>
        /// <param name="idx">読み取り開始インデックス</param>
        /// <returns>抽出した文字列</returns>
        private string ExtractStringFromSysexMessage(int idx)
        {
            string nm = string.Empty;

            for (int i = idx; i < rcvData.Length; i += 2)
            {
                nm += (char)(rcvData[i] + ((1 & rcvData[i + 1]) << 7));
            }

            return nm;
        }

        private async void button0_Click(object sender, RoutedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //ユーザーインターフェースを操作する
                textBlock0.Text = "hoge";
                textBlock.Text = "fuga";
            });
        }
    }
}
