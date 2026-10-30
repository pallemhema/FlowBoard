namespace FlowBoard.Web.Services.UI;

public class ToastService
{
    public event Action? OnChange;

    public string? Message { get; private set; }

    public ToastType Type { get; private set; }

    public void Success(string message)
    {
        Show(message, ToastType.Success);
    }

    public void Error(string message)
    {
        Show(message, ToastType.Error);
    }

    public void Warning(string message)
    {
        Show(message, ToastType.Warning);
    }

    public void Info(string message)
    {
        Show(message, ToastType.Info);
    }

    public void Clear()
    {
        Message = null;

        OnChange?.Invoke();
    }

    private void Show(
        string message,
        ToastType type)
    {
        Message = message;

        Type = type;

        OnChange?.Invoke();
    }
}

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}