using UnityEngine;
/// <summary>
/// 主动依赖注入脚本
/// </summary>
public class PlayerInstaller : MonoBehaviour
{
    [Header("玩家需求组件")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerActionBuffer actionBuffer;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private PlayerStateController playerStateController;
    [SerializeField] private PlayerCameraOrbitTarget playerCameraOrbitTarget;
    private void Awake()
    {
        //调用依赖注入逻辑
        ResolveActionBuffer();
        inputReader.Init(actionBuffer);
        playerMotor.Init(inputReader);
        playerCameraOrbitTarget.Init(inputReader);
        playerStateController.Init(inputReader, actionBuffer);
    }

    private void ResolveActionBuffer()
    {
        if (actionBuffer != null)
        {
            return;
        }

        actionBuffer = inputReader.GetComponent<PlayerActionBuffer>();
        if (actionBuffer == null)
        {
            actionBuffer = inputReader.gameObject.AddComponent<PlayerActionBuffer>();
        }
    }
}
