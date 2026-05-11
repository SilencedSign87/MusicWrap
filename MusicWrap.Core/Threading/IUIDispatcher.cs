namespace MusicWrap.Core.Threading
{
    public interface IUIDispatcher
    {
        bool CanAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
    }
}
