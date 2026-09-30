using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FactoryTask
{
    public sealed class SpectatorCameraController : MonoBehaviour
    {
        public Camera spectatorCamera;
        public RenderTexture output;
        public GameObject spectatorPanel;
        public VRSessionRecorder session;
        public bool EnabledForCapture { get; private set; }
        void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            SetCaptureEnabled(false);
#else
            SetCaptureEnabled(true);
#endif
        }
        public void SetCaptureEnabled(bool value)
        {
            EnabledForCapture=value;
            spectatorCamera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            spectatorCamera.targetTexture=output;spectatorCamera.enabled=value;
            if(spectatorPanel!=null)spectatorPanel.SetActive(value);
        }
#if !UNITY_ANDROID || UNITY_EDITOR
        void OnGUI()
        {
            if(!EnabledForCapture||output==null)return;
            // A desktop preview. The dedicated Editor Spectator window is the reliable OBS source during XR.
            float width=Screen.width,height=width*output.height/output.width;
            if(height>Screen.height){height=Screen.height;width=height*output.width/output.height;}
            GUI.DrawTexture(new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height),output,ScaleMode.ScaleToFit,false);
        }
#endif
    }
}
