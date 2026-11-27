using SQLite;

namespace EchoStem.Data
{
    public interface ISQLiteConnectionFactory
    {
        string DatabaseFile { get; }
        SQLiteConnection GetConnection();
    }
}
