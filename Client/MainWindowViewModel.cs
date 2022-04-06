using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System.ComponentModel;
using System.Windows.Input;

namespace Client
{
    internal class MainWindowModel : NotificationObject
    {
        public string? HostName { get; set; } = "127.0.0.1";
        public int? HostPort { get; set; } = 9090;
        public string? UserName { get; set; } = "wentianbu";
        public SecureString? UserPassword { private get; set; }

        private string? consoleOutput;
        public string? ConsoleOutput
        {
            get { return consoleOutput; }
            set
            {
                consoleOutput = value;
                RaisePropertyChanged("ConsoleOutput");
            }
        }

        private bool IsLoggedIn = false;
        public string? IsLoggedInString { get; set; } = "未登录";



    }

    internal abstract class NotificationObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    internal class LoginCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) 
        {
            if (parameter == null) return false;
            bool isLoggedIn = (bool)parameter;
            return !isLoggedIn; 
        }
        public void Execute(object? parameter)
        {
            
        }
    }

}
