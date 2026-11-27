using Digimezzo.Foundation.Core.Settings;
using EchoStem.Core.Base;
using SQLite;
using System;
using System.IO;

namespace EchoStem.Data
{
    public class SQLiteConnectionFactory : ISQLiteConnectionFactory
    {
        public string DatabaseFile => Path.Combine(SettingsClient.ApplicationFolder(), ProductInformation.ApplicationName + ".db");
        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(this.DatabaseFile) { BusyTimeout = new TimeSpan(0, 0, 0, 10) };
        }
    }
}
