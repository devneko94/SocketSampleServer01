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
        #region プロパティ
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
        public string OutputText
        {
            get => _outputText;
            set
            {
                _outputText = value;
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
        #endregion

        #region フィールド
        /// <summary>
        /// MyTcpListenerオブジェクト
        /// </summary>
        private MyTcpListener _tcpListener = null;

        /// <summary>
        /// ホスト名
        /// </summary>
        private string _targetHostName = "127.0.0.1";

        /// <summary>
        /// ポート番号
        /// </summary>
        private int _targetPortNum = 8001;

        /// <summary>
        /// 受信テキスト
        /// </summary>
        private string _outputText = string.Empty;

        /// <summary>
        /// 実行中フラグ
        /// </summary>
        private bool _isRunning = false;
        #endregion

        #region イベント
        /// <summary>
        /// プロパティ変更通知イベント
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region コンストラクタ
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            // .NETCore標準のエンコードにShift_JISが含まれないため、使用する場合は下記が必要らしい
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        #endregion

        #region プライベートメソッド
        /// <summary>
        /// プロパティ変更通知
        /// </summary>
        /// <param name="propertyName"></param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 電文送受信
        /// </summary>
        private void Main()
        {
            // Listen状態の間はループ
            while (_tcpListener.Listened)
            {
                try
                {
                    // 接続失敗した場合はコンティニュー
                    if (!_tcpListener.WaitConnect()) { continue; }

                    // クライアントから送られたデータを読み取る
                    byte[] buffer = _tcpListener.Read((buf) => true, 15000);
                    string recieveText = Encoding.GetEncoding("Shift_JIS").GetString(buffer);
                    OutputText += $"受信:{recieveText + Environment.NewLine}";

                    // 読み取ったデータを加工して返信
                    string sendText = "Re: " + recieveText;
                    byte[] data = Encoding.GetEncoding("Shift_JIS").GetBytes(sendText);
                    if (_tcpListener.Write(data))
                    {
                        OutputText += $"返信:{sendText + Environment.NewLine}";
                    }
                    else
                    {
                        OutputText += "返信失敗" + Environment.NewLine;
                    }

                    // 接続解除
                    _tcpListener.Disconnect();
                }
                catch
                {
                    // 例外発生した場合は再起動
                    _tcpListener.Restart();
                }
            }
        }

        /// <summary>
        /// 起動ボタン押下
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnRunServer_Click(object sender, RoutedEventArgs e)
        {
            _tcpListener = MyTcpListener.Create(TargetHostName, _targetPortNum);
            _tcpListener.Start();
            IsRunning = true;

            Task.Run(() => { Main(); });
        }

        /// <summary>
        /// 停止ボタン押下
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            _tcpListener?.Stop();
            IsRunning = false;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            _tcpListener?.Stop();
            _tcpListener = null;
            IsRunning = false;
        }
        #endregion

        #region 内部クラス
        /// <summary>
        /// TcpListenerラッパークラス
        /// </summary>
        private class MyTcpListener: TcpListener
        {
            #region フィールド
            /// <summary>
            /// TcpClientオブジェクト
            /// </summary>
            private TcpClient _tcpClient;
            #endregion

            #region プロパティ
            /// <summary>
            /// Listen状態フラグ
            /// </summary>
            public bool Listened { get; private set; }
            #endregion

            #region コンストラクタ
            /// <summary>
            /// コンストラクタ(IPEndPoint)
            /// </summary>
            /// <param name="localIEP">IPEndPoint</param>
            public MyTcpListener(IPEndPoint localIEP): base(localIEP) { }
            #endregion

            #region クラスメソッド
            /// <summary>
            /// インスタンス作成
            /// </summary>
            /// <param name="addr">ホストアドレス</param>
            /// <param name="port">ポート番号</param>
            /// <returns>MyTcpListenerインスタンス</returns>
            public static MyTcpListener Create(string addr, int port)
            {
                if(!IPAddress.TryParse(addr, out IPAddress ipAddress))
                {
                    throw new Exception($"不正なIPアドレスです。[{addr}]");
                }

                return new MyTcpListener(new IPEndPoint(ipAddress, port));
            }
            #endregion

            #region パブリックメソッド
            /// <summary>
            /// 起動
            /// </summary>
            public new void Start()
            {
                if (this.Active) { return; }

                base.Start();
                this.Listened = true;
            }

            /// <summary>
            /// 停止
            /// </summary>
            public new void Stop()
            {
                if (!this.Active) { return; }

                this.Disconnect();
                base.Stop();
                this.Listened = false;
            }

            /// <summary>
            /// 再起動
            /// </summary>
            public void Restart()
            {
                if (!this.Active) { return; }

                this.Disconnect();
                base.Stop();
                base.Start();
            }

            /// <summary>
            /// 接続待機
            /// </summary>
            /// <returns></returns>
            public bool WaitConnect()
            {
                if(!this.Active || _tcpClient != null) { return false; }

                try
                {
                    _tcpClient = this.AcceptTcpClient();
                    return true;
                }
                catch(SocketException se)
                {
                    return false;
                }
            }

            /// <summary>
            /// 切断
            /// </summary>
            public void Disconnect()
            {
                if (!this.Active || _tcpClient == null) { return; }

                if (_tcpClient.Connected)
                {
                    NetworkStream ns = _tcpClient.GetStream();
                    ns?.Close();
                    _tcpClient.Close();
                }
                _tcpClient = null;
            }

            /// <summary>
            /// 読み取り
            /// </summary>
            /// <param name="isComplete">完了フラグ</param>
            /// <param name="ms">タイムアウト時間(ミリ秒)</param>
            /// <returns>読み取り結果</returns>
            public byte[] Read(Func<byte[], bool> isComplete, int ms)
            {
                if(!this.Active || _tcpClient == null) { return Array.Empty<byte>(); }

                try
                {
                    byte[] buffer = new byte[256];
                    NetworkStream ns = _tcpClient.GetStream();
                    ns.ReadTimeout = ms;

                    while (true)
                    {
                        int size = ns.Read(buffer, 0, buffer.Length);

                        if (size == 0 || _tcpClient == null)
                        {
                            this.Disconnect();
                            return Array.Empty<byte>();
                        }

                        if (isComplete(buffer))
                        {
                            break;
                        }
                    }
                    return buffer;
                }
                catch (Exception e)
                {
                    if (!this.Listened) { return Array.Empty<byte>(); }
                    throw e;
                }
            }

            /// <summary>
            /// 書き込み
            /// </summary>
            /// <param name="data">書き込みデータ</param>
            /// <returns>書き込み成否</returns>
            public bool Write(byte[] data)
            {
                if(!this.Active || _tcpClient == null) { return false; }

                NetworkStream ns = _tcpClient.GetStream();
                ns.Write(data, 0, data.Length);
                return true;
            }
            #endregion
        }
        #endregion
    }
}
