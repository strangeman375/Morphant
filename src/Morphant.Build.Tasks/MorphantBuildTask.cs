using Microsoft.Build.Framework;

namespace Morphant.Build.Tasks;

public abstract class MorphantBuildTask : ICancelableTask
{
    private readonly CancellationTokenSource cancellation = new();
    protected CancellationToken CancellationToken => cancellation.Token;
    public void Cancel() => cancellation.Cancel();

    public IBuildEngine BuildEngine { get; set; } = null!;

    public ITaskHost? HostObject { get; set; }

    public bool Execute()
    {
        try
        {
            CancellationToken.ThrowIfCancellationRequested();
            ExecuteCore();
            return true;
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SnapshotException exception)
        {
            LogError(exception.Code, exception.Message);
            return false;
        }
        catch (Exception exception)
        {
            LogError(
                "MORPHANTMSB999",
                "Unexpected Morphant Git snapshot failure: " + exception);
            return false;
        }
    }

    protected abstract void ExecuteCore();

    protected void LogMessage(string message)
    {
        BuildEngine.LogMessageEvent(new BuildMessageEventArgs(
            message,
            string.Empty,
            GetType().Name,
            MessageImportance.Normal));
    }

    private void LogError(string code, string message)
    {
        BuildEngine.LogErrorEvent(new BuildErrorEventArgs(
            "MorphantGitSnapshot",
            code,
            BuildEngine.ProjectFileOfTaskNode,
            0,
            0,
            0,
            0,
            message,
            string.Empty,
            GetType().Name));
    }
}

internal sealed class SnapshotException : Exception
{
    public SnapshotException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
