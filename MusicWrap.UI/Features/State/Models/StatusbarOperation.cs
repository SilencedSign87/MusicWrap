namespace MusicWrap.UI.Features.State.Models
{
    public class StatusbarOperation : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public string OperationId { get; }

        public StatusbarOperation(string operationId, Action onDispose)
        {
            OperationId = operationId;
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}
