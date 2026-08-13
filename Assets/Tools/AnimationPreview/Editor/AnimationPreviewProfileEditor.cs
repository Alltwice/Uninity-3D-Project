using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ProjectTools.AnimationPreview
{
    [CustomEditor(typeof(AnimationPreviewProfile))]
    internal sealed class AnimationPreviewProfileEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            Button openButton = new Button(() => AnimationPreviewWindow.Open((AnimationPreviewProfile)target)) { text = "打开模型动画预览器" };
            openButton.style.height = 30f;
            openButton.style.marginTop = 8f;
            root.Add(openButton);
            root.Add(new HelpBox("Profile 只保存预览配置；模型和动画播放发生在独立 EditorWindow 中，不会修改场景或源资源。", HelpBoxMessageType.Info));
            return root;
        }
    }
}
