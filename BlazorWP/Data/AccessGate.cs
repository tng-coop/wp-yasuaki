namespace BlazorWP;

public sealed class AccessGate : IAccessGate {
    private volatile TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task WaitAsync() => _tcs.Task;
    public void Pause() {
        Console.WriteLine("AccessGate: paused");
        _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public void Open() {
        Console.WriteLine("AccessGate: opened");
        _tcs.TrySetResult();
    }
}
