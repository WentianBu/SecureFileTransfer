using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
using SecureFileTransfer;
using SecureFileTransfer.Client;
using SecureFileTransfer.Data;
using System.Collections.ObjectModel;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isLoggedIn = false;
        private ushort clientId = 0;
        private SftClient? client = null;

        public string? HostName { get; set; } = "127.0.0.1";
        public int? HostPort { get; set; } = 9090;
        public string? UserName { get; set; } = "wentianbu";

        public string? ServerPath { get; set; } = "/";

        public ObservableCollection<FileList> AllFiles { get; set; } = new ObservableCollection<FileList>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {

            string? hostName = HostNameBox.Text;
            int hostPort;
            try
            {
                hostPort = Convert.ToInt32(HostPortBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("无效的端口号！\n" + ex.Message);
                return;
            }
            string? userName = UserNameBox.Text;
            string? password = PasswordBox.Password;

            if (hostName == null || userName == null || password == null
                || hostPort <= 0 || hostPort > 65535)
            {
                MessageBox.Show("无效的主机信息或登录信息！");
                return;
            }


            try
            {
                SftClientConfig config = new();
                client = new(config, hostName, hostPort);
                isLoggedIn = client.Login(userName, password);
            }
            catch (SocketException ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            if (isLoggedIn)
            {
                MessageBox.Show("登录成功！");
                LoginState.Content = "已登录";
                LoginButton.IsEnabled = false;
                LogoutButton.IsEnabled = true;
                VisitButton.IsEnabled = true;
                ParentButton.IsEnabled = true;
                ServerPathBox.IsEnabled = true;
                UploadButton.IsEnabled = true;
                
                ServerPathBox.Text = "/";
                return;
            }
            else
            {
                MessageBox.Show("登录失败！");
                return;
            }


        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null)
            {
                MessageBox.Show("尚未登录！");
                return;
            }
            client.Bye();
            LoginState.Content = "未登录";
            LoginButton.IsEnabled = true;
            LogoutButton.IsEnabled = false;
            VisitButton.IsEnabled = false;
            ParentButton.IsEnabled = false;
            ServerPathBox.IsEnabled = false;
            UploadButton.IsEnabled = false;
            
            return;
        }

        private void Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            { ((dynamic)this.DataContext).UserPassword = ((PasswordBox)sender).SecurePassword; }
        }

        private void VisitButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !isLoggedIn)
            {
                MessageBox.Show("尚未登录！");
                return;
            }
            ServerPath = ServerPathBox.Text;
            if (ServerPath == null || !ServerPath.StartsWith("/"))
            {
                MessageBox.Show("不合法的路径，请重新输入！");
                return;
            }

            Tuple<bool, SftPacketData?> listResult = client.List(ServerPath);
            if (listResult.Item1)
            {
                SftMetaData? sftMetaData = (SftMetaData?)listResult.Item2;
                if (sftMetaData == null)
                {
                    throw new ArgumentNullException("Metadata packet is null.");
                }
                AllFiles = new();

                foreach (var f in sftMetaData.FileList)
                {
                    AllFiles.Add(new FileList(f.Name, false, f.LastWriteTime, f.Length, f.IsReadOnly));
                }
                foreach (var d in sftMetaData.DirList)
                {
                    AllFiles.Add(new FileList(d.Name, true, d.LastWriteTime, null, null));
                }
                ServerFileList.DataContext = AllFiles;
            }
            else
            {
                SftFailData? sftFailData = (SftFailData?)listResult.Item2;
                MessageBox.Show("列目录失败：" + sftFailData.Message);
            }
            return;
        }

        private void DataGridRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;
            if (row == null) return;
            FileList file = (FileList)row.Item;
            if (file.IsDirectory)
            {
                ServerPath = ServerPath.EndsWith("/") ? ServerPath + file.Name : ServerPath + "/" + file.Name;
                ServerPathBox.Text = ServerPath;
                VisitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else
            {
                string ServerFilePath = ServerPath.EndsWith("/") ? ServerPath + file.Name : ServerPath + "/" + file.Name;
                DownloadFile(ServerFilePath);
            }
            // Some operations with this row
        }

        private void ParentButton_Click(object sender, RoutedEventArgs e)
        {
            ServerPath = ServerPathBox.Text;
            if (ServerPath == "/") return;
            if (ServerPath.EndsWith("/")) ServerPath = ServerPath.TrimEnd('/');
            int i = ServerPath.LastIndexOf('/');
            ServerPath = ServerPath.Remove(i + 1);
            ServerPathBox.Text = ServerPath;
            VisitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private void UploadFile(string RemoteDirPath)
        {
            string LocalFilePath, LocalFileName;
            Microsoft.Win32.OpenFileDialog dialog = new();
            if (dialog.ShowDialog() == true)
            {
                LocalFilePath = dialog.FileName;
                LocalFileName = dialog.SafeFileName;
            }
            else
            {
                return;
            }
            if (!RemoteDirPath.EndsWith('/'))
                RemoteDirPath += "/";
            string RemoteFilePath = RemoteDirPath + LocalFileName;
            if (client.Upload(LocalFilePath, RemoteFilePath))
            {
                EmbeddedConsole.AppendText("上传任务已开始。\n");
            }
            else
            {
                MessageBox.Show("上传任务创建失败!");
            }
        }

        private void DownloadFile(string RemoteFilePath)
        {
            string LocalFilePath;
            Microsoft.Win32.SaveFileDialog dialog = new();
            int i = RemoteFilePath.LastIndexOf('/'); 
            dialog.FileName = RemoteFilePath.Remove(0, i + 1);
            dialog.Filter = "所有文件(*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                LocalFilePath = dialog.FileName;
            }
            else { return; }
            if (client.Download(LocalFilePath, RemoteFilePath))
            {
                EmbeddedConsole.AppendText("下载任务已开始。\n");
            }
            else
            {
                MessageBox.Show("下载任务创建失败！");
            }
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            ServerPath = ServerPathBox.Text;
            UploadFile(ServerPath);
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            ServerPath = ServerPathBox.Text;
            if (!ServerPath.EndsWith('/'))
            {
                ServerPath += '/';
            }
            
        }
    }

    public class FileList
    {
        public string? Name { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime? LastModified { get; set; }
        public long? Length { get; set; }
        public bool? IsReadOnly { get; set; }

        public FileList(string? name, bool isDirectory, DateTime? lastModified, long? length, bool? isReadOnly)
        {
            Name = name;
            IsDirectory = isDirectory;
            LastModified = lastModified;
            Length = length;
            IsReadOnly = isReadOnly;
        }
    }

    public class TaskList
    {
        public enum Direction {
            Upload, Download
        }
        public enum State { Running, Finished }

        public string? LocalPath { get; set; }
        public string? RemotePath { get; set; }
        public Direction TransferDirection { get; set; }
        public long? Length { get; set; }
        public State TaskState { get; set; }
    }


}
