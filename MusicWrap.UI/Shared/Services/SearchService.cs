using MusicWrap.Core.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Text;

namespace MusicWrap.UI.Shared.Services
{
    public sealed class SearchService : ISearchQueryProvider
    {
        private string _query = string.Empty;

        private string _activeQuery = string.Empty;
        public string ActiveQuery
        {
            private set { _activeQuery = value; }
            get => _activeQuery;
        }

        public event EventHandler<string>? SearchSubmitted;
        public event EventHandler<string>? QueryChanged;


        public void SetQuery(string query)
        {
            if (_query != query)
            {
                _query = query;
                QueryChanged?.Invoke(this, _query);
            }
        }
        public void Submit()
        {
            ActiveQuery = _query;
            SearchSubmitted?.Invoke(this, _query);
        }
        public void Clear()
        {
            _query = string.Empty;
            ActiveQuery = string.Empty;
            SearchSubmitted?.Invoke(this, string.Empty);
        }
    }
}
