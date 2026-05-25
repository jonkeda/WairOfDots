namespace WairOfDots.Core;

public sealed record ExternalControllerRequest(
    string AdapterKind,
    string ModelPath,
    string ControllerId,
    GeneralPerception Perception);

public sealed record ExternalControllerResponse(
    bool Success,
    GeneralAction? Action,
    string Error = "");

public sealed record OnnxControllerManifest(
    string ModelPath,
    string InputSchema,
    string OutputSchema,
    string FallbackControllerId);

public interface IExternalGeneralControllerAdapter
{
    string AdapterKind { get; }
    ExternalControllerResponse Decide(ExternalControllerRequest request);
}

public sealed class FallbackExternalGeneralController : IGeneralController
{
    private readonly IExternalGeneralControllerAdapter _adapter;
    private readonly IGeneralController _fallback;
    private readonly string _modelPath;

    public FallbackExternalGeneralController(
        IExternalGeneralControllerAdapter adapter,
        IGeneralController fallback,
        string modelPath)
    {
        _adapter = adapter;
        _fallback = fallback;
        _modelPath = modelPath;
    }

    public string ControllerId => $"{_adapter.AdapterKind}:{_fallback.ControllerId}";

    public GeneralAction Decide(GeneralPerception perception)
    {
        var response = _adapter.Decide(new ExternalControllerRequest(
            _adapter.AdapterKind,
            _modelPath,
            _fallback.ControllerId,
            perception));

        return response.Success && response.Action != null
            ? response.Action
            : _fallback.Decide(perception);
    }
}

public sealed class DelegateExternalGeneralControllerAdapter : IExternalGeneralControllerAdapter
{
    private readonly Func<ExternalControllerRequest, ExternalControllerResponse> _handler;

    public DelegateExternalGeneralControllerAdapter(
        string adapterKind,
        Func<ExternalControllerRequest, ExternalControllerResponse> handler)
    {
        AdapterKind = adapterKind;
        _handler = handler;
    }

    public string AdapterKind { get; }

    public ExternalControllerResponse Decide(ExternalControllerRequest request)
        => _handler(request);
}

public sealed class TimeoutExternalGeneralControllerAdapter : IExternalGeneralControllerAdapter
{
    private readonly IExternalGeneralControllerAdapter _inner;
    private readonly TimeSpan _timeout;

    public TimeoutExternalGeneralControllerAdapter(IExternalGeneralControllerAdapter inner, TimeSpan timeout)
    {
        _inner = inner;
        _timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : timeout;
    }

    public string AdapterKind => _inner.AdapterKind;

    public ExternalControllerResponse Decide(ExternalControllerRequest request)
    {
        var task = Task.Run(() => _inner.Decide(request));
        return task.Wait(_timeout)
            ? task.Result
            : new ExternalControllerResponse(false, null, $"timeout={_timeout.TotalMilliseconds:0}ms");
    }
}

public static class ExternalControllerManifests
{
    public static OnnxControllerManifest CreateGeneralOnnxManifest(string modelPath, string fallbackControllerId)
        => new(
            modelPath,
            "GeneralPerception: legacy observation, economy, regions, general security",
            "GeneralAction: directive, target city/region, strategy mode, budget, purchase intent",
            fallbackControllerId);

    public static IReadOnlyList<string> Validate(OnnxControllerManifest manifest)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.ModelPath))
            errors.Add("ModelPath is required.");
        if (string.IsNullOrWhiteSpace(manifest.InputSchema))
            errors.Add("InputSchema is required.");
        if (string.IsNullOrWhiteSpace(manifest.OutputSchema))
            errors.Add("OutputSchema is required.");
        if (string.IsNullOrWhiteSpace(manifest.FallbackControllerId))
            errors.Add("FallbackControllerId is required.");

        return errors;
    }
}
