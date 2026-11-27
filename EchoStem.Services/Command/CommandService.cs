using EchoStem.Core.Prism;
using EchoStem.Services.Command;
using System.ServiceModel;

namespace EchoStem.Services.Command
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class CommandService : ICommandService
    {
        public void ShowMainWindowCommand()
        {
            ApplicationCommands.ShowMainWindowCommand.Execute(null);
        }
    }
}
