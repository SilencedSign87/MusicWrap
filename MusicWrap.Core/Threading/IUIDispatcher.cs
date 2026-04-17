using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Threading
{
    public interface IUIDispatcher
    {
        bool CanAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
    }
}
