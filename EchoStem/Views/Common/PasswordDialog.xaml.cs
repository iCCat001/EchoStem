using System.Windows.Controls;

namespace EchoStem.Views.Common
{
    public partial class PasswordDialog : UserControl
    {
        public PasswordDialog()
        {
            InitializeComponent();

            this.Loaded += (_, __) => this.PasswordInput.Focus();
        }

        public string Password => this.PasswordInput.Password;
    }
}
