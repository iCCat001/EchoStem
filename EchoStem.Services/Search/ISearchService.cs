using System;

namespace EchoStem.Services.Search
{
    public interface ISearchService
    {
        string SearchText { get; set; }
        event Action<string> DoSearch;
    }
}
