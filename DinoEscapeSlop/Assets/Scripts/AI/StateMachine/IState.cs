public interface IState
{
    void Enter();
    void Update();
    void Exit();
}

public interface IPriorityState : IState
{
    int Priority { get; }
}
