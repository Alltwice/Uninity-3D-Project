/// <summary>
/// 玩家状态基类。转换判断只读取状态，副作用由状态生命周期方法执行。
/// </summary>
public abstract class PlayerStateBase
{
    protected readonly PlayerContext Context;

    protected PlayerStateBase(PlayerContext context)
    {
        Context = context;
    }

    public virtual PlayerStateTransitionRequest EvaluateInputTransition()
    {
        return null;
    }

    public virtual void Enter(PlayerStateTransition transition)
    {
    }

    public virtual void Tick()
    {
    }

    public virtual PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return null;
    }

    public virtual void Exit(PlayerStateTransition transition)
    {
    }
}
