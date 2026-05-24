using UnityEngine;
/// <summary>
/// 将玩家数据传入Animator参数
/// </summary>
public class PlayerAnimationDriver : MonoBehaviour
{
    [Header("引用")] 
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMotor motor;
    
}
