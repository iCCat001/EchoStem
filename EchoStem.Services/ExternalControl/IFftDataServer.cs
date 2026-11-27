using System.ServiceModel;
using System.Threading.Tasks;

namespace EchoStem.Services.ExternalControl
{
    [ServiceContract(Namespace = nameof(ExternalControl))]
    public interface IFftDataServer
    {
        [OperationContract]
        int GetFftDataSize();

        [OperationContract]
        Task GetFftData();
    }
}