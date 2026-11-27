using System.ServiceModel;

namespace EchoStem.Services.Command
{
    [ServiceContract(Namespace = "http://EchoStem.CommandService")]
    public interface ICommandService
    {
        [OperationContract()]
        void ShowMainWindowCommand();
    }
}
