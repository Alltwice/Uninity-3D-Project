using UnityEngine;

/// <summary>
/// 注入需要共享的输入与缓冲接口。
/// </summary>
public class PlayerInstaller : MonoBehaviour
{
    [Header("玩家依赖")]
    [SerializeField] private PlayerInputReader playerInputReader;
    [SerializeField] private PlayerActionBuffer actionBuffer;
    [SerializeField] private PlayerStateController playerStateController;
    [SerializeField] private PlayerCameraOrbitTarget playerCameraOrbitTarget;

    private void Awake()
    {
        playerInputReader.Init(actionBuffer);
        playerCameraOrbitTarget.Init(playerInputReader);
        playerStateController.Init(playerInputReader, actionBuffer);
    }
}
