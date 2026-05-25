using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Contracts
{
    public interface ISearchQueryProvider
    {
        string ActiveQuery { get; }
    }
}
