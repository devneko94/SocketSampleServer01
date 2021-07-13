using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SocketSampleServer01
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// ホスト名
        /// </summary>
        public string TargetHostName
        {
            get => _targetHostName;
            set
            {
                _targetHostName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ポート番号
        /// </summary>
        public int TargetPortNum
        {
            get => _targetPortNum;
            set
            {
                _targetPortNum = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 受信テキスト
        /// </summary>
        public string RecieveText
        {
            get => _recieveText;
            set
            {
                _recieveText = value;
                OnPropertyChanged();
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// TcpListenerオブジェクト
        /// </summary>
        private TcpListener _tcpListener = null;

        /// <summary>
        /// ホスト名
        /// </summary>
        private string _targetHostName = string.Empty;

        /// <summary>
        /// ポート番号
        /// </summary>
        private int _targetPortNum = 8001;

        /// <summary>
        /// 受信テキスト
        /// </summary>
        private string _recieveText = string.Empty;

        /// <summary>
        /// 実行中フラグ
        /// </summary>
        private bool _isRunning = false;

        /// <summary>
        /// プロパティ変更通知イベント
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            TargetHostName = GetLocalIPAddress();
        }

        /// <summary>
        /// ローカルIPアドレス取得
        /// </summary>
        /// <returns>ローカルIPアドレス</returns>
        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>
        /// TCP起動
        /// </summary>
        /// <param name="hostName">ホスト名</param>
        /// <param name="portNum">ポート番号</param>
        /// <returns>接続可否</returns>
        private bool StartTcpServer()
        {
            try
            {
                IPAddress iPAddress = IPAddress.Parse(TargetHostName);
                _tcpListener = new TcpListener(iPAddress, TargetPortNum);
                _tcpListener.Start();
                IsRunning = true;
                return true;
            }
            catch (Exception e)
            {
                if (IsRunning)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(e.Message);
                    });
                }
                IsRunning = false;
                return false;
            }
        }

        /// <summary>
        /// TCP切断
        /// </summary>
        private void CloseTcpClient()
        {
            _tcpListener?.Stop();
        }

        /// <summary>
        /// メッセージ受信
        /// </summary>
        /// <param name="sendText">送信テキスト</param>
        /// <returns>受信テキスト</returns>
        private async Task<string> RecieveMessage()
        {
            if (!_tcpListener.Pending())
            {
                await Task.Delay(500);
                return string.Empty;
            }

            string recieveMsg = string.Empty;
            TcpClient tcpClient = _tcpListener.AcceptTcpClient();

            using (NetworkStream ns = tcpClient.GetStream())
            {
                ns.ReadTimeout = 1000;
                ns.WriteTimeout = 1000;

                recieveMsg = RecieveTcp(ns);
                ReplyTcp(ns, recieveMsg);

                ns.Close();
            }

            if(recieveMsg.Trim() == "END")
            {
                IsRunning = false;
            }

            return recieveMsg;
        }

        /// <summary>
        /// TCP受信
        /// </summary>
        /// <param name="ns">NetworkStream</param>
        /// <returns>受信テキスト</returns>
        private string RecieveTcp(NetworkStream ns)
        {
            string recieveMsg = string.Empty;

            using (MemoryStream ms = new MemoryStream())
            {
                byte[] resBytes = new byte[256];
                int resSize = 0;

                do
                {
                    resSize = ns.Read(resBytes, 0, resBytes.Length);

                    if (resSize == 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("データなし");
                        });
                        break;
                    }

                    ms.Write(resBytes, 0, resSize);

                } while (ns.DataAvailable || resBytes[resSize - 1] != '\n');

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                recieveMsg = Encoding.GetEncoding("Shift_JIS").GetString(ms.GetBuffer());

                ms.Close();
            }

            return recieveMsg;
        }

        /// <summary>
        /// TCP返信
        /// </summary>
        /// <param name="ns">NetworkStream</param>
        /// <param name="replyMsg">返信テキスト</param>
        private void ReplyTcp(NetworkStream ns, string replyMsg)
        {
            replyMsg = "Re:" + replyMsg;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            byte[] replyBytes = Encoding.GetEncoding("Shift_JIS").GetBytes(replyMsg + '\n');

            ns.Write(replyBytes, 0, replyBytes.Length);
        }

        /// <summary>
        /// 起動ボタン押下イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnRunServer_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(async () =>
            {
                do
                {
                    if (StartTcpServer())
                    {
                        if(await RecieveMessage() != string.Empty)
                        {
                            RecieveText += await RecieveMessage() + Environment.NewLine;
                        }
                    }

                    CloseTcpClient();
                } while (IsRunning);
            });
        }

        /// <summary>
        /// 停止ボタン押下イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            IsRunning = false;
        }

        /// <summary>
        /// プロパティ変更通知イベントハンドラ
        /// </summary>
        /// <param name="propertyName"></param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
