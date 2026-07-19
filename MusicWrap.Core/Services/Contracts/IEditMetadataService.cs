using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Contracts
{
    public interface IEditMetadataService
    {
        void OpenMetadataWindow(List<int> trackIds);
        event EventHandler<List<int>>? ItemsChanged;
    }
}
