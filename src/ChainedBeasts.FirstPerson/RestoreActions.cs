#if TC_EMBEDDED_CAMERA
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#endif
#if TC_EMBEDDED_CAMERA
namespace TermsAndContusions.FirstPerson;
#else
namespace ChainedBeasts.FirstPerson;
#endif

// Register BEFORE each mutation. Cleanup runs in reverse order even if an object was destroyed.
internal sealed class RestoreActions : IDisposable
{
    private readonly List<Action> _actions = new();
    private readonly Action<Exception> _onError;
    internal RestoreActions(Action<Exception> onError) => _onError = onError;
    internal void Add(Action action) => _actions.Add(action);
    public void Dispose()
    {
        var actions = _actions.ToArray();
        _actions.Clear();
        for (var i = actions.Length - 1; i >= 0; i--)
        {
            try { actions[i](); }
            catch (Exception error) { _onError(error); }
        }
    }
}
