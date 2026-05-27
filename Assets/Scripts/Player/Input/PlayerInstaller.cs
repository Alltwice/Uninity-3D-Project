using UnityEngine;
/// <summary>
/// 主动依赖注入脚本
/// </summary>
public class PlayerInstaller : MonoBehaviour
{
    [Header("玩家需求组件")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private PlayerStateController playerStateController;
    [SerializeField] private PlayerCameraOrbitTarget playerCameraOrbitTarget;
    private void Start()
    {
        //调用依赖注入逻辑
        playerMotor.Init(inputReader);
        playerCameraOrbitTarget.Init(inputReader);
        playerStateController.Init(inputReader);
    }
}
