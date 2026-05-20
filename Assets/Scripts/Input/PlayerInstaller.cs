using UnityEngine;
/// <summary>
/// 主动依赖注入脚本
/// </summary>
public class PlayerInstaller : MonoBehaviour
{
    [Header("玩家需求组件")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private Transform cameraTransform;

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("PlayerSceneContext: inputReader is not assigned.");
            return;
        }

        if (playerMotor == null)
        {
            Debug.LogError("PlayerSceneContext: playerMotor is not assigned.");
            return;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        //调用依赖注入逻辑
        playerMotor.Init(inputReader, cameraTransform);
    }
}
