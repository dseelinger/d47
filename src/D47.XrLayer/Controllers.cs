using System.Text;

namespace D47.XrLayer;

/// <summary>
/// The layer's action set in one Elite session. When Elite attaches first, the set joins Elite's attach and every
/// sync Elite makes; when Elite has not attached by the first frame after the session is focused, the layer attaches
/// the set and syncs it once a frame. While pointing is off the layer makes no action call. Elite's calls only change
/// fields here; <see cref="Frame"/>, called from xrEndFrame, is the only writer of the published hands.
/// </summary>
internal sealed unsafe class Controllers
{
    private const int MostActionSets = 64;

    private const int Waiting = 0;
    private const int Attaching = 1;
    private const int Joined = 2;
    private const int Own = 3;
    private const int EliteOnly = 4;
    private const int Off = 5;

    private readonly ILayerCalls calls;
    private readonly SuggestedBindings suggested;
    private readonly ulong instance;
    private readonly ulong session;
    private readonly ulong[] hands = new ulong[XrBlock.HandCount];
    private readonly ulong[] aimSpaces = new ulong[XrBlock.HandCount];
    private readonly ulong[] gripSpaces = new ulong[XrBlock.HandCount];
    private int state;
    private volatile bool pointing;
    private volatile bool focused;
    private volatile bool eliteSynced;
    private volatile int eliteSyncResult;
    private volatile XrControllerRefusal refusal;
    private readonly ulong[] actions = new ulong[ControllerProfiles.Actions.Length];
    private ulong actionSet;

    public Controllers(ILayerCalls calls, SuggestedBindings suggested, ulong instance, ulong session)
    {
        this.calls = calls;
        this.suggested = suggested;
        this.instance = instance;
        this.session = session;
    }

    /// <summary>The LOCAL space the hands are located in. 0 until the session has one; while 0 every Elite call passes through.</summary>
    public ulong LocalSpace { get; set; }

    /// <summary>Notes an XrEventDataSessionStateChanged for this session.</summary>
    public void StateChanged(int sessionState)
    {
        if (sessionState == XrConstant.SessionStateFocused)
        {
            focused = true;
        }
    }

    /// <summary>Elite's xrAttachSessionActionSets, with the layer's set added while pointing is on and nothing has attached yet.</summary>
    public int EliteAttach(XrSessionActionSetsAttachInfo* info, bool pointingNow)
    {
        pointing = pointingNow;
        if (LocalSpace == 0 || !pointingNow)
        {
            var passed = calls.AttachSessionActionSets(session, info);
            if (passed >= XrResult.Success)
            {
                Interlocked.CompareExchange(ref state, EliteOnly, Waiting);
            }

            if (Volatile.Read(ref state) is Own or Attaching)
            {
                refusal = XrControllerRefusal.EliteAttachedAfterLayer;
            }

            return passed;
        }

        var before = Interlocked.CompareExchange(ref state, Attaching, Waiting);
        if (before != Waiting)
        {
            var passed = calls.AttachSessionActionSets(session, info);
            if (before is Own or Attaching)
            {
                refusal = XrControllerRefusal.EliteAttachedAfterLayer;
            }

            return passed;
        }

        var count = info is null ? 0u : info->CountActionSets;
        bool ready;
        try
        {
            ready = info is not null && count < MostActionSets && (count == 0 || info->ActionSets is not null) && Prepare();
        }
        catch (Exception)
        {
            ready = false;
        }

        if (!ready)
        {
            refusal = XrControllerRefusal.SetupFailed;
            Volatile.Write(ref state, Off);
            return calls.AttachSessionActionSets(session, info);
        }

        var sets = stackalloc ulong[(int)count + 1];
        for (var i = 0; i < count; i++)
        {
            sets[i] = info->ActionSets[i];
        }

        sets[count] = actionSet;
        var joined = *info;
        joined.CountActionSets = count + 1;
        joined.ActionSets = sets;
        var result = calls.AttachSessionActionSets(session, &joined);
        if (result >= XrResult.Success)
        {
            CreateSpaces();
            Volatile.Write(ref state, Joined);
            return result;
        }

        refusal = XrControllerRefusal.SetupFailed;
        Volatile.Write(ref state, Off);
        return calls.AttachSessionActionSets(session, info);
    }

    /// <summary>Elite's xrSyncActions, with the layer's set added to the active sets while it is joined and pointing is on.</summary>
    public int EliteSync(XrActionsSyncInfo* info)
    {
        var count = info is null ? 0u : info->CountActiveActionSets;
        if (Volatile.Read(ref state) != Joined || !pointing || info is null
            || count >= MostActionSets || (count > 0 && info->ActiveActionSets is null))
        {
            return calls.SyncActions(session, info);
        }

        var sets = stackalloc XrActiveActionSet[(int)count + 1];
        for (var i = 0; i < count; i++)
        {
            sets[i] = info->ActiveActionSets[i];
        }

        sets[count] = new XrActiveActionSet { ActionSet = actionSet };
        var joined = *info;
        joined.CountActiveActionSets = count + 1;
        joined.ActiveActionSets = sets;
        var result = calls.SyncActions(session, &joined);
        if (result >= XrResult.Success)
        {
            eliteSyncResult = result;
            eliteSynced = true;
            return result;
        }

        eliteSynced = false;
        var alone = calls.SyncActions(session, info);
        if (alone >= XrResult.Success)
        {
            refusal = XrControllerRefusal.SetupFailed;
            Volatile.Write(ref state, Off);
        }

        return alone;
    }

    /// <summary>Attaches the layer's own set when due, syncs it, and writes both hands and the refusal into <paramref name="published"/>.</summary>
    public void Frame(long time, bool pointingNow, ref XrLayerHalf published)
    {
        published.Hands = default;
        try
        {
            ReadHands(time, pointingNow, ref published.Hands);
        }
        catch (Exception)
        {
            published.Hands = default;
            refusal = XrControllerRefusal.Faulted;
            Volatile.Write(ref state, Off);
        }

        published.ControllerRefusal = refusal;
    }

    /// <summary>Destroys the action spaces and the action set. Called before the session is destroyed.</summary>
    public void Destroy()
    {
        Volatile.Write(ref state, Off);
        for (var i = 0; i < XrBlock.HandCount; i++)
        {
            if (aimSpaces[i] != 0)
            {
                calls.DestroySpace(aimSpaces[i]);
                aimSpaces[i] = 0;
            }

            if (gripSpaces[i] != 0)
            {
                calls.DestroySpace(gripSpaces[i]);
                gripSpaces[i] = 0;
            }
        }

        if (actionSet != 0)
        {
            calls.DestroyActionSet(actionSet);
            actionSet = 0;
        }
    }

    private void ReadHands(long time, bool pointingNow, ref XrHands published)
    {
        pointing = pointingNow;
        if (!pointingNow)
        {
            eliteSynced = false;
            return;
        }

        if (LocalSpace == 0)
        {
            return;
        }

        var current = Volatile.Read(ref state);
        if (current == EliteOnly && refusal == XrControllerRefusal.None)
        {
            refusal = XrControllerRefusal.PointingTurnedOnAfterEliteAttached;
        }

        if (current == Waiting && focused)
        {
            current = AttachOwn();
        }

        int synced;
        if (current == Own)
        {
            var active = new XrActiveActionSet { ActionSet = actionSet };
            var info = new XrActionsSyncInfo { Type = XrType.ActionsSyncInfo, CountActiveActionSets = 1, ActiveActionSets = &active };
            synced = calls.SyncActions(session, &info);
        }
        else if (current == Joined && eliteSynced)
        {
            synced = eliteSyncResult;
        }
        else
        {
            return;
        }

        if (synced != XrResult.Success)
        {
            return;
        }

        for (var i = 0; i < XrBlock.HandCount; i++)
        {
            published[i] = ReadHand(i, time);
        }
    }

    private int AttachOwn()
    {
        if (Interlocked.CompareExchange(ref state, Attaching, Waiting) != Waiting)
        {
            return Volatile.Read(ref state);
        }

        var set = 0ul;
        var attached = false;
        if (Prepare())
        {
            set = actionSet;
            var info = new XrSessionActionSetsAttachInfo { Type = XrType.SessionActionSetsAttachInfo, CountActionSets = 1, ActionSets = &set };
            attached = calls.AttachSessionActionSets(session, &info) >= XrResult.Success;
        }

        if (!attached)
        {
            refusal = XrControllerRefusal.SetupFailed;
            Volatile.Write(ref state, Off);
            return Off;
        }

        CreateSpaces();
        Volatile.Write(ref state, Own);
        return Own;
    }

    /// <summary>Creates the action set and its actions and suggests each profile's bindings, Elite's included. False when any call fails.</summary>
    private bool Prepare()
    {
        if (!Path("/user/hand/left"u8, out hands[0]) || !Path("/user/hand/right"u8, out hands[1]))
        {
            return false;
        }

        var setInfo = new XrActionSetCreateInfo { Type = XrType.ActionSetCreateInfo };
        Write(setInfo.ActionSetName, XrConstant.MaxActionSetNameSize, "d47"u8);
        Write(setInfo.LocalizedActionSetName, XrConstant.MaxLocalizedActionSetNameSize, "D47"u8);
        ulong set = 0;
        if (calls.CreateActionSet(instance, &setInfo, &set) < XrResult.Success || set == 0)
        {
            return false;
        }

        actionSet = set;
        for (var i = 0; i < actions.Length; i++)
        {
            var (name, localized, type) = ControllerProfiles.Actions[i];
            if (!CreateAction(name, localized, type, out actions[i]))
            {
                return false;
            }
        }

        foreach (var profile in ControllerProfiles.All)
        {
            if (!Suggest(profile))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Suggests the profile's bindings after Elite's own for it.</summary>
    private bool Suggest(ControllerProfile table)
    {
        if (!Path(table.Path, out var profile))
        {
            return false;
        }

        var elites = suggested.For(instance, profile);
        if (elites is null)
        {
            return false;
        }

        var bindings = new XrActionSuggestedBinding[elites.Length + table.Bindings.Length];
        elites.CopyTo(bindings, 0);
        var at = elites.Length;
        foreach (var (action, binding) in table.Bindings)
        {
            if (!Path(binding, out var path))
            {
                return false;
            }

            bindings[at++] = new XrActionSuggestedBinding { Action = actions[(int)action], Binding = path };
        }

        fixed (XrActionSuggestedBinding* all = bindings)
        {
            var suggestion = new XrInteractionProfileSuggestedBinding
            {
                Type = XrType.InteractionProfileSuggestedBinding,
                InteractionProfile = profile,
                CountSuggestedBindings = (uint)bindings.Length,
                SuggestedBindings = all,
            };
            return calls.SuggestInteractionProfileBindings(instance, &suggestion) >= XrResult.Success;
        }
    }

    private bool CreateAction(string name, string localized, int type, out ulong action)
    {
        action = 0;
        var subactions = stackalloc ulong[XrBlock.HandCount];
        for (var i = 0; i < XrBlock.HandCount; i++)
        {
            subactions[i] = hands[i];
        }

        var info = new XrActionCreateInfo
        {
            Type = XrType.ActionCreateInfo,
            ActionType = type,
            CountSubactionPaths = XrBlock.HandCount,
            SubactionPaths = subactions,
        };
        Write(info.ActionName, XrConstant.MaxActionNameSize, Encoding.UTF8.GetBytes(name));
        Write(info.LocalizedActionName, XrConstant.MaxLocalizedActionNameSize, Encoding.UTF8.GetBytes(localized));
        ulong created = 0;
        if (calls.CreateAction(actionSet, &info, &created) < XrResult.Success || created == 0)
        {
            return false;
        }

        action = created;
        return true;
    }

    private bool Path(string text, out ulong path) => Path(Encoding.UTF8.GetBytes(text), out path);

    private bool Path(ReadOnlySpan<byte> text, out ulong path)
    {
        Span<byte> terminated = stackalloc byte[text.Length + 1];
        text.CopyTo(terminated);
        terminated[^1] = 0;
        ulong found = 0;
        fixed (byte* pathString = terminated)
        {
            path = calls.StringToPath(instance, pathString, &found) < XrResult.Success ? 0 : found;
        }

        return path != 0;
    }

    private void CreateSpaces()
    {
        for (var i = 0; i < XrBlock.HandCount; i++)
        {
            aimSpaces[i] = CreateSpace(actions[(int)LayerAction.Aim], hands[i]);
            gripSpaces[i] = CreateSpace(actions[(int)LayerAction.Grip], hands[i]);
        }
    }

    private ulong CreateSpace(ulong action, ulong hand)
    {
        var info = new XrActionSpaceCreateInfo
        {
            Type = XrType.ActionSpaceCreateInfo,
            Action = action,
            SubactionPath = hand,
            PoseInActionSpace = new XrPosef { OrientationW = 1f },
        };
        ulong space = 0;
        return calls.CreateActionSpace(session, &info, &space) < XrResult.Success ? 0 : space;
    }

    private XrHand ReadHand(int index, long time)
    {
        if (!Locate(aimSpaces[index], time, out var aim) || !Locate(gripSpaces[index], time, out var grip))
        {
            return default;
        }

        var hand = hands[index];
        var buttons = XrButtons.None;
        foreach (var (action, bit) in ControllerProfiles.Buttons)
        {
            if (Held(actions[(int)action], hand))
            {
                buttons |= bit;
            }
        }

        var (x, y) = Stick(actions[(int)LayerAction.Stick], hand);
        return new XrHand
        {
            Aim = aim,
            Grip = grip,
            Valid = 1,
            TriggerValue = Value(actions[(int)LayerAction.Trigger], hand),
            GripValue = Value(actions[(int)LayerAction.Squeeze], hand),
            Buttons = buttons,
            StickX = x,
            StickY = y,
        };
    }

    private bool Locate(ulong space, long time, out XrBlockPose pose)
    {
        pose = default;
        if (space == 0)
        {
            return false;
        }

        var location = new XrSpaceLocation { Type = XrType.SpaceLocation };
        const ulong bothValid = XrConstant.LocationOrientationValid | XrConstant.LocationPositionValid;
        if (calls.LocateSpace(space, LocalSpace, time, &location) < XrResult.Success
            || (location.LocationFlags & bothValid) != bothValid)
        {
            return false;
        }

        var located = location.Pose;
        pose = new XrBlockPose
        {
            OrientationX = located.OrientationX,
            OrientationY = located.OrientationY,
            OrientationZ = located.OrientationZ,
            OrientationW = located.OrientationW,
            PositionX = located.PositionX,
            PositionY = located.PositionY,
            PositionZ = located.PositionZ,
        };
        return true;
    }

    private float Value(ulong action, ulong hand)
    {
        var info = new XrActionStateGetInfo { Type = XrType.ActionStateGetInfo, Action = action, SubactionPath = hand };
        var value = new XrActionStateFloat { Type = XrType.ActionStateFloat };
        return calls.GetActionStateFloat(session, &info, &value) >= XrResult.Success
            && value.IsActive != 0 && float.IsFinite(value.CurrentState)
            ? value.CurrentState
            : 0f;
    }

    private bool Held(ulong action, ulong hand)
    {
        var info = new XrActionStateGetInfo { Type = XrType.ActionStateGetInfo, Action = action, SubactionPath = hand };
        var state = new XrActionStateBoolean { Type = XrType.ActionStateBoolean };
        return calls.GetActionStateBoolean(session, &info, &state) >= XrResult.Success
            && state.IsActive != 0 && state.CurrentState != 0;
    }

    private (float X, float Y) Stick(ulong action, ulong hand)
    {
        var info = new XrActionStateGetInfo { Type = XrType.ActionStateGetInfo, Action = action, SubactionPath = hand };
        var state = new XrActionStateVector2f { Type = XrType.ActionStateVector2f };
        return calls.GetActionStateVector2f(session, &info, &state) >= XrResult.Success
            && state.IsActive != 0 && float.IsFinite(state.CurrentStateX) && float.IsFinite(state.CurrentStateY)
            ? (Math.Clamp(state.CurrentStateX, -1f, 1f), Math.Clamp(state.CurrentStateY, -1f, 1f))
            : (0f, 0f);
    }

    private static void Write(byte* target, int size, ReadOnlySpan<byte> text) =>
        text[..Math.Min(text.Length, size - 1)].CopyTo(new Span<byte>(target, size));
}
