/// <summary>
/// 玩家状态基类。转换判断只读取状态，副作用由状态生命周期方法执行
/// </summary>
public abstract class PlayerStateBase
{
    protected readonly PlayerContext Context;

    protected PlayerStateBase(PlayerContext context)
    {
        Context = context;
    }
    /// <summary>
    /// 判断输入意图提前执行
    /// </summary>
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
    /// <summary>
    /// 判断执行结果后执行
    /// </summary>
    public virtual PlayerStateTransitionRequest EvaluateResultTransition()
    {
        return null;
    }

    public virtual void Exit(PlayerStateTransition transition)
    {
    }
}
