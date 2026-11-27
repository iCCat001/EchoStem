namespace EchoStem.Core.Audio
{
    public interface IPlayerFactory
    {
       IPlayer Create(bool hasMediaFoundationSupport);
    }
}