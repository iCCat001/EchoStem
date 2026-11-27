using EchoStem.Core.Audio;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EchoStem.Services.Equalizer
{
    public interface IEqualizerService
    {
        Task<List<EqualizerPreset>> GetPresetsAsync();
        Task<EqualizerPreset> GetSelectedPresetAsync();
    }
}
