using UnityEngine;
/// <summary>
/// 主动依赖注入脚本
/// </summary>
public class PlayerInstaller : MonoBehaviour
{
    [Header("玩家需求组件")]
    [SerializeField] private PlayerInputReader playerInputReader;
    [SerializeField] private PlayerActionBuffer actionBuffer;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private PlayerStateController playerStateController;
    [SerializeField] private PlayerCameraOrbitTarget playerCameraOrbitTarget;
    private void Awake()
    {
        //调用依赖注入逻辑
        ResolveActionBuffer();
        playerInputReader.Init(actionBuffer);
        playerMotor.Init(playerInputReader);
        playerCameraOrbitTarget.Init(playerInputReader);
        playerStateController.Init(playerInputReader, actionBuffer);
    }

    private void ResolveActionBuffer()
    {
        if (actionBuffer != null)
        {
            return;
        }

        actionBuffer = playerInputReader.GetComponent<PlayerActionBuffer>();
        if (actionBuffer == null)
        {
            actionBuffer = playerInputReader.gameObject.AddComponent<PlayerActionBuffer>();
        }
    }
}
