using System.Threading.Tasks;

namespace EchoStem.Services.Lifetime
{
    public interface ILifetimeService
    {
        bool MustPerformClosingTasks { get; }

        Task PerformClosingTasksAsync();
    }
}