using System.Runtime.InteropServices;

namespace Greenshot.Platform.Mac;

public enum GlobalHotKeyAction
{
    CaptureRegion,
    CaptureScreen,
    CaptureLastRegion,
    CaptureWindow
}

public readonly record struct GlobalHotKeyDefinition(uint KeyCode, uint Modifiers);

public sealed class GlobalHotKeySettings
{
    public GlobalHotKeyDefinition CaptureRegion { get; init; } = new(15, CommandKey | ShiftKey); // R
    public GlobalHotKeyDefinition CaptureScreen { get; init; } = new(1, CommandKey | ShiftKey); // S
    public GlobalHotKeyDefinition CaptureLastRegion { get; init; } = new(37, CommandKey | ShiftKey); // L
    public GlobalHotKeyDefinition CaptureWindow { get; init; } = new(13, CommandKey | ShiftKey); // W

    public const uint CommandKey = 1u << 8;
    public const uint ShiftKey = 1u << 9;

    public static GlobalHotKeySettings Default { get; } = new();
}

public sealed class GlobalHotKeyService : IDisposable
{
    private const uint KeyboardEventClass = 0x6B657962; // 'keyb'
    private const uint HotKeyPressedEventKind = 6;
    private const uint DirectObjectParameter = 0x2D2D2D2D; // '----'
    private const uint HotKeyIdParameterType = 0x686B6964; // 'hkid'

    private readonly GlobalHotKeySettings _settings;
    private readonly EventHandlerProc _eventHandler;
    private readonly List<IntPtr> _hotKeyReferences = [];
    private readonly Dictionary<uint, GlobalHotKeyAction> _actions = [];
    private IntPtr _eventHandlerReference;
    private bool _started;

    public GlobalHotKeyService(GlobalHotKeySettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _eventHandler = HandleHotKeyEvent;
    }

    public event EventHandler<GlobalHotKeyAction>? HotKeyPressed;
    public event EventHandler<Exception>? RegistrationFailed;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        try
        {
            var eventTypes = new[]
            {
                new EventTypeSpec(KeyboardEventClass, HotKeyPressedEventKind)
            };
            var status = InstallEventHandler(
                GetEventDispatcherTarget(),
                _eventHandler,
                1,
                eventTypes,
                IntPtr.Zero,
                out _eventHandlerReference);
            ThrowIfFailed(status, "InstallEventHandler");

            Register(_settings.CaptureRegion, 1, GlobalHotKeyAction.CaptureRegion);
            Register(_settings.CaptureScreen, 2, GlobalHotKeyAction.CaptureScreen);
            Register(_settings.CaptureLastRegion, 3, GlobalHotKeyAction.CaptureLastRegion);
            Register(_settings.CaptureWindow, 4, GlobalHotKeyAction.CaptureWindow);
            _started = true;
        }
        catch (Exception exception)
        {
            RegistrationFailed?.Invoke(this, exception);
            Dispose();
        }
    }

    private void Register(GlobalHotKeyDefinition definition, uint id, GlobalHotKeyAction action)
    {
        var hotKeyId = new EventHotKeyID(0x47534D43, id); // 'GSMC'
        var status = RegisterEventHotKey(
            definition.KeyCode,
            definition.Modifiers,
            hotKeyId,
            GetEventDispatcherTarget(),
            0,
            out var reference);
        ThrowIfFailed(status, "RegisterEventHotKey");
        _hotKeyReferences.Add(reference);
        _actions[id] = action;
    }

    private int HandleHotKeyEvent(IntPtr nextHandler, IntPtr eventRef, IntPtr userData)
    {
        if (GetEventParameter(
                eventRef,
                DirectObjectParameter,
                HotKeyIdParameterType,
                IntPtr.Zero,
                (uint)Marshal.SizeOf<EventHotKeyID>(),
                IntPtr.Zero,
                out EventHotKeyID hotKeyId) == 0 &&
            _actions.TryGetValue(hotKeyId.Id, out var action))
        {
            HotKeyPressed?.Invoke(this, action);
        }

        return 0;
    }

    public void Dispose()
    {
        foreach (var reference in _hotKeyReferences)
        {
            UnregisterEventHotKey(reference);
        }

        _hotKeyReferences.Clear();
        _actions.Clear();
        if (_eventHandlerReference != IntPtr.Zero)
        {
            RemoveEventHandler(_eventHandlerReference);
            _eventHandlerReference = IntPtr.Zero;
        }

        _started = false;
        GC.SuppressFinalize(this);
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status != 0)
        {
            throw new InvalidOperationException($"{operation} failed with macOS status {status}.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct EventTypeSpec(uint eventClass, uint eventKind)
    {
        public readonly uint EventClass = eventClass;
        public readonly uint EventKind = eventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct EventHotKeyID(uint signature, uint id)
    {
        public readonly uint Signature = signature;
        public readonly uint Id = id;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EventHandlerProc(IntPtr nextHandler, IntPtr eventRef, IntPtr userData);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern IntPtr GetEventDispatcherTarget();

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int InstallEventHandler(
        IntPtr target,
        EventHandlerProc handler,
        uint numTypes,
        EventTypeSpec[] eventTypes,
        IntPtr userData,
        out IntPtr handlerReference);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int RemoveEventHandler(IntPtr handlerReference);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int RegisterEventHotKey(
        uint keyCode,
        uint modifiers,
        EventHotKeyID hotKeyId,
        IntPtr target,
        uint options,
        out IntPtr hotKeyReference);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int UnregisterEventHotKey(IntPtr hotKeyReference);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int GetEventParameter(
        IntPtr eventRef,
        uint parameterName,
        uint parameterType,
        IntPtr actualType,
        uint size,
        IntPtr actualSize,
        out EventHotKeyID data);
}
