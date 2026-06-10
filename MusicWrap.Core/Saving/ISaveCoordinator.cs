using MusicWrap.Data.Infrastructure.Saving;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Saving
{
    public interface ISaveCoordinator
    {
        void Enqueue(SaveKind kind);
        Task FlushAsync(CancellationToken ct = default);
    }
}
