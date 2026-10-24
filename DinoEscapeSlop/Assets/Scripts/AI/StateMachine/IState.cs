public interface IState
{
    void Enter(); 
    void Update(); 
    void Exit(); 
}

/// <summary>
/// Interfaz para estados con nivel de prioridad.
/// Estados con mayor prioridad no pueden ser interrumpidos por estados de menor prioridad en la StateMachine.
/// </summary>
public interface IPriorityState : IState
{
    int Priority { get; }
}
