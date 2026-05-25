using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace MusicWrap.UI.Shared.Services
{
    public interface ICommandProvider
    {
        int Priority { get; }
        IEnumerable<CommandItem> GetCommands(string query);
    }
    public record CommandItem(
        string Title,
        string? Description,
        string? Category,
        ICommand Command,
        object? CommandParameter = null
        );
    public class CommandPaletteService
    {
        private readonly List<WeakReference<ICommandProvider>> _providers = [];
        public bool IsOpen { get; private set; }
        public string Query { get; private set; } = string.Empty;

        public void Register(ICommandProvider provider)
        {
            _providers.RemoveAll(wr => !wr.TryGetTarget(out _));
            _providers.Add(new WeakReference<ICommandProvider>(provider));
        }
        public void Unregister(ICommandProvider provider)
        {
            _providers.RemoveAll(r =>
            !r.TryGetTarget(out var target) || ReferenceEquals(target, provider)
            );
        }
        public void Open(string InitialQuery = "")
        {
            Query = InitialQuery;
            IsOpen = true;
            QueryChanged?.Invoke(this, Query);
        }
        public void Close()
        {
            IsOpen = false;
        }
        public IEnumerable<CommandItem> Search(string query)
        {
            return _providers
                .Select(r => r.TryGetTarget(out var p) ? p : null)
                .OfType<ICommandProvider>()
                .OrderByDescending(p => p.Priority)
                .SelectMany(p => p.GetCommands(query));
        }

        public event EventHandler<string>? QueryChanged;
    }
}
