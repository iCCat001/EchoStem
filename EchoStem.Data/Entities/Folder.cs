using SQLite;

namespace EchoStem.Data.Entities
{
    public class Folder
    {
        [PrimaryKey(), AutoIncrement()]
        public long FolderID { get; set; }

        public string Path { get; set; }

        public string SafePath { get; set; }

        public long ShowInCollection { get; set; }
    }
}
