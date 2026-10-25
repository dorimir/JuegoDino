using System.Collections.Generic;

public class StateMachine
{
    public IState CurrentState { get; private set; }

    private readonly List<IState> allowedStates = new List<IState>();

    public IReadOnlyList<IState> AllowedStates => allowedStates;

    public int CurrentStatePriority => (CurrentState is IPriorityState p) ? p.Priority : 0;

    public void AllowState(IState state)
    {
        if (state != null && !allowedStates.Contains(state))
        {
            allowedStates.Add(state);
        }
    }

    public bool IsStateAllowed(IState state) => state != null && allowedStates.Contains(state);

    public void Initialize(IState startingState)
    {
        if (startingState == null) return;

        AllowState(startingState);
        CurrentState = startingState;
        CurrentState.Enter();
    }

    public bool ChangeState(IState newState, bool force = false)
    {
        if (newState == null || newState == CurrentState) return false;

        if (allowedStates.Count > 0 && !allowedStates.Contains(newState))
        {
            return false;
        }

        int currentPriority = CurrentStatePriority;
        int newPriority = (newState is IPriorityState p) ? p.Priority : 0;

        if (!force && currentPriority > newPriority)
        {
            return false;
        }

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
        return true;
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}
