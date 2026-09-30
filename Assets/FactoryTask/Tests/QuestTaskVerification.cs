#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FactoryTask;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// This entire test harness is excluded from Android/player compilation.
public sealed class QuestTaskVerification : MonoBehaviour
{
    static readonly List<string> math=new List<string>();
    readonly List<string> steps=new List<string>();
    XRTrackingProvider tracking;UserCalibration calibration;XRGrabTaskTracker task;BodyLoadEstimator estimator;ConveyorController controller;
    Vector3 head,leftRest,rightRest;
    FullBodyAvatarIK avatar;
    VRSessionRecorder recording;
    SpectatorCameraController spectator;
    string sessionPath;
    Vector3 initialLeftFoot,initialRightFoot;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Check(bool value,string message){Require(value,message);math.Add("PASS "+message);}
    public static void RunMathChecks()
    {
        math.Clear();var input=new LoadInput{holding=true,horizontalReach=.2f,totalReach=.3f,handAboveShoulder=-.2f,weight=1,holdSeconds=1,travelMetres=.1f,repeats=0};
        Vector3 baseline=BodyLoadEstimator.Evaluate(input);var heavier=input;heavier.weight=5;var far=input;far.horizontalReach=.6f;far.totalReach=.8f;
        var longer=input;longer.holdSeconds=25;var moving=input;moving.travelMetres=4;var repeating=input;repeating.repeats=10;var high=input;high.handAboveShoulder=.4f;
        Check(BodyLoadEstimator.Evaluate(heavier).x>baseline.x&&BodyLoadEstimator.Evaluate(heavier).z>baseline.z,"5 kg increases shoulder and wrist versus 1 kg");
        Check(BodyLoadEstimator.Evaluate(far).x>baseline.x&&BodyLoadEstimator.Evaluate(far).y>baseline.y,"Farther reach increases shoulder and arm");
        Check(BodyLoadEstimator.Evaluate(longer).y>baseline.y&&BodyLoadEstimator.Evaluate(longer).z>baseline.z,"Longer holding increases arm and wrist");
        Check(BodyLoadEstimator.Evaluate(moving).z>baseline.z,"Hand travel increases wrist");
        Check(BodyLoadEstimator.Evaluate(repeating).y>baseline.y,"Recent repetition increases arm");
        Check(BodyLoadEstimator.Evaluate(high).x>baseline.x,"Higher hands increase shoulder");
        Check(BodyLoadEstimator.EvaluateTorso(.3f,.3f)>BodyLoadEstimator.EvaluateTorso(.05f,.05f),"Head drop and forward displacement increase torso");
        Check(BodyLoadEstimator.EvaluateTorso(10,10)==100&&BodyLoadEstimator.EvaluateTorso(-1,-1)==0,"Torso is bounded 0 to 100");
        float decay=BodyLoadEstimator.SmoothLoad(70,0,1);Check(decay>0&&decay<70,"Load decays gradually rather than clearing on release");
        Check(BodyLoadVisualizer.ColorFor(0).g>BodyLoadVisualizer.ColorFor(0).r&&BodyLoadVisualizer.ColorFor(100).r>BodyLoadVisualizer.ColorFor(100).g,"Color endpoints are green and red");
        var yellow=BodyLoadVisualizer.ColorFor(50);Check(yellow.r>.9f&&yellow.g>.7f&&yellow.b<.2f,"Midpoint is yellow");
        input.holding=false;Check(BodyLoadEstimator.Evaluate(input)==Vector3.zero,"Inactive hand has no new loaded target");
        Directory.CreateDirectory("Documentation/QuestTask");File.WriteAllLines("Documentation/QuestTask/MathVerification.txt",math);
    }
    IEnumerator Start()
    {
        IEnumerator run=Run();bool failed=false;
        while(true)
        {
            object current=null;bool more=false;
            try{more=run.MoveNext();if(more)current=run.Current;}
            catch(Exception e){steps.Add("FAIL "+e);failed=true;}
            if(failed||!more)break;yield return current;
        }
        Time.timeScale=1;Directory.CreateDirectory("Documentation/QuestTask");
        File.WriteAllText("Documentation/QuestTask/PlayVerification.txt",(failed?"FAILED":"PASSED")+"\n"+string.Join("\n",steps));
        Debug.Log(failed?"QUEST_PLAY_TEST_FAILED":"QUEST_PLAY_TEST_PASSED");
        if(SessionState.GetBool("QuestTask.BatchTests",false)){SessionState.SetBool("QuestTask.BatchTests",false);SessionState.SetInt("QuestTask.TestExitCode",failed?1:0);}EditorApplication.isPlaying=false;
    }
    void Pass(bool condition,string message){Require(condition,message);steps.Add("PASS "+message);}
    void Pose(Vector3 left,Vector3 right,bool lg=false,bool rg=false,bool tracked=true){tracking.Simulate(head,left,right,lg,rg,tracked);}
    IEnumerator Run()
    {
        RunMathChecks();tracking=FindFirstObjectByType<XRTrackingProvider>();calibration=FindFirstObjectByType<UserCalibration>();task=FindFirstObjectByType<XRGrabTaskTracker>();estimator=FindFirstObjectByType<BodyLoadEstimator>();controller=FindFirstObjectByType<ConveyorController>();
        avatar=FindFirstObjectByType<FullBodyAvatarIK>();recording=FindFirstObjectByType<VRSessionRecorder>();spectator=FindFirstObjectByType<SpectatorCameraController>();
        if(recording!=null)recording.autoRecord=false;
        head=new Vector3(3.55f,1.65f,-4.2f);leftRest=head+new Vector3(-.2f,-.65f,-.05f);rightRest=head+new Vector3(.2f,-.65f,-.05f);
        Pose(leftRest,rightRest);float limit=Time.realtimeSinceStartup+8;
        while(!calibration.IsCalibrated&&Time.realtimeSinceStartup<limit)yield return null;
        Pass(calibration.IsCalibrated,"Stable tracked head and hands automatically calibrate");
        Pass(Vector3.Distance(calibration.BaselineHead,head)<.05f,"Standing origin and baseline are aligned to task");
        if(recording!=null)
        {
            Directory.CreateDirectory("Documentation/Avatar");sessionPath=Path.GetFullPath("Documentation/Avatar/VerifiedSession_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".fvr");
            Pass(recording.StartRecording(sessionPath),"Binary session recording starts after calibration");
            var humanoid=avatar.modelRoot.GetComponentInChildren<Animator>();
            Pass(humanoid.avatar.isHuman&&humanoid.avatar.isValid,"Real skinned worker uses a valid Humanoid avatar");
            Pass(!spectator.spectatorCamera.GetUniversalAdditionalCameraData().allowXRRendering&&spectator.spectatorCamera.targetTexture!=null,"Spectator output is isolated from headset rendering");
            avatar.SolvePose(.033f);initialLeftFoot=avatar.leftLeg.end.position;initialRightFoot=avatar.rightLeg.end.position;
        }
        Time.timeScale=4;
        float until=Time.time+50;
        while((task.queue.Count<4||task.queue.Front.State!=PartState.Waiting)&&Time.time<until)yield return null;
        yield return new WaitForSeconds(12);
        Pass(task.queue.Count==4&&task.queue.Front.State==PartState.Waiting,"Conveyor delivers and parks four FIFO parts");
        int supplied=task.pool.SpawnedCount;yield return new WaitForSeconds(4);
        Pass(task.pool.SpawnedCount==supplied,"Full queue applies backpressure without instantiation");
        ConveyorPart first=task.queue.Front;Vector3 firstPosition=first.Body.position;
        Pose(firstPosition,firstPosition,true,true);yield return new WaitForSeconds(.18f);
        Pass(task.LeftHeld==first&&first.Holder==HandSide.Left&&task.RightHeld!=first,"Simultaneous grips cannot own the same part twice");
        Pass(task.queue.Front!=first&&task.queue.Front.weightKg==3,"Removing the first item advances the next FIFO item");
        Vector3 working=head+new Vector3(-.5f,-.2f,.6f);Pose(working,rightRest,true);yield return new WaitForSeconds(3);
        Pass(estimator.Score(0)>estimator.Score(1)+5&&estimator.Score(2)>estimator.Score(3)+5&&estimator.Score(4)>estimator.Score(5)+5,"Left-hand work raises left shoulder, arm and wrist independently");
        CaptureRunningTask();
        if(avatar!=null)
        {
            avatar.SolvePose(.033f);avatar.GetComponent<AvatarLoadHeatmap>().Apply();
            CaptureSpectator("Spectator_LeftWork");
            Pass(avatar.LeftGripError<.01f,"Avatar grip anchor follows the held part within reachable arm range (error="+avatar.LeftGripError.ToString("F3")+" m)");
            Pass(Vector3.Distance(avatar.leftGripAnchor.position,first.GripWorldPosition)<.012f,"Avatar palm and the part surface grip position agree");
            float scale=avatar.modelRoot.localScale.x;
            Pass(Vector3.Distance(avatar.head.position+tracking.head.rotation*avatar.eyeOffset*scale,tracking.head.position)<.01f,"Avatar eye anchor follows the tracked HMD pose");
            var block=new MaterialPropertyBlock();avatar.GetComponent<AvatarLoadHeatmap>().surfaces[0].GetPropertyBlock(block);
            var heat=block.GetVector("_LoadsA");Pass(heat.x>heat.y&&heat.z>heat.w,"Surface heat shader receives independent left/right loads");
        }
        float heldTime=first.HoldSeconds;Pose(working,rightRest,true,false,false);yield return new WaitForSeconds(.5f);
        Pass(first.State==PartState.Held&&Mathf.Abs(first.HoldSeconds-heldTime)<.1f,"Tracking loss freezes held part and hold timer");
        Pose(working,rightRest,true);yield return new WaitForSeconds(.15f);
        Pose(new Vector3(3.55f,.3f,-4.7f),rightRest,false);yield return new WaitForSeconds(1);
        Pass(first.State==PartState.Dropped&&task.CompletedTotal==0,"Floor drop does not count as completion");
        Pose(first.Body.position,rightRest,true);yield return new WaitForSeconds(.18f);
        Pass(task.LeftHeld==first,"Dropped part can be grabbed again");
        Vector3 inside=task.completionVolume.transform.TransformPoint(task.completionVolume.center);
        Pose(inside,rightRest,true);yield return new WaitForSeconds(.3f);
        Pass(task.CompletedTotal==0,"A held part inside the box is not counted");
        float beforeRelease=estimator.Score(4);Pose(inside,rightRest,false);yield return new WaitForSeconds(.8f);
        Pass(task.CompletedLeft==1&&task.CompletedRight==0&&task.LeftHeld==null,"Released part inside completion box records left-hand task");
        Pass(first.State==PartState.Pooled||first.State==PartState.Conveying,"Completed part returns to the reusable pool");
        float afterRelease=estimator.Score(4);yield return new WaitForSeconds(2);
        Pass(estimator.Score(4)>0&&estimator.Score(4)<afterRelease&&afterRelease<beforeRelease,"Post-release load persists and decays gradually");
        until=Time.time+10;while(task.queue.Front.State!=PartState.Waiting&&Time.time<until)yield return null;
        var second=task.queue.Front;Pose(leftRest,second.Body.position,false,true);yield return new WaitForSeconds(.2f);
        Pass(task.RightHeld==second,"Right Grip can pick the next part");
        Pose(leftRest,head+new Vector3(.55f,-.2f,.6f),false,true);yield return new WaitForSeconds(3);
        Pass(estimator.Score(1)>10&&estimator.Score(3)>10&&estimator.Score(5)>10,"Right-hand work drives right-side scores");
        if(avatar!=null){avatar.SolvePose(.033f);CaptureSpectator("Spectator_RightWork");}
        Pose(leftRest,inside,false,true);yield return new WaitForSeconds(.2f);Pose(leftRest,inside);yield return new WaitForSeconds(.8f);
        Pass(task.CompletedRight==1&&task.CompletedTotal==2,"Right completion is recorded separately");
        Pass(estimator.RecentCount(HandSide.Left,Time.time)==1&&estimator.RecentCount(HandSide.Right,Time.time)==1,"Recent repetition history is isolated by hand");
        Pass(estimator.RecentCount(HandSide.Left,Time.time+61)==0,"Repetition history expires after 60 seconds");
        yield return new WaitForSeconds(4);
        Pass(task.pool.ReusedCount>0&&task.pool.Parts.Length==12,"Pool reuses instances and stays bounded at twelve");
        Pass(task.queue.Count<=4,"Queue capacity stays bounded throughout the task");
        tracking.Simulate(head+new Vector3(0,-.22f,.23f),leftRest,rightRest);yield return new WaitForSeconds(1.5f);
        Pass(estimator.Score(6)>25,"Tracked head lowering and forward movement raise torso load in Play mode");
        if(recording!=null)
        {
            avatar.SolvePose(.033f);CaptureSpectator("Spectator_Bending");
            Pass(Vector3.Distance(avatar.leftLeg.end.position,initialLeftFoot)<.035f&&Vector3.Distance(avatar.rightLeg.end.position,initialRightFoot)<.035f,"Feet remain planted while the tracked head bends forward");
            recording.StopRecording();Pass(recording.FramesWritten>100,"Head, hands, parts, scores and task counts are streamed to a bounded binary recording");
            Pass(recording.BeginPlayback(sessionPath),"Recorded session reopens for third-person playback");
            Pass(!task.enabled&&!controller.enabled&&!estimator.enabled&&tracking.ExternalPlayback,"Playback disables live simulation and XR pose writes");
            recording.TickPlayback(recording.Duration+1);avatar.SolvePose(.033f);CaptureSpectator("Spectator_Replay");
            Pass(task.CompletedTotal==2,"Recorded left/right task counts survive playback");
            Pass(estimator.Score(6)>25,"Recorded torso load survives playback");
            Pass(Vector3.Distance(tracking.head.position,head+new Vector3(0,-.22f,.23f))<.03f,"Recorded head trajectory survives playback");
            recording.RestartPlayback();Pass(recording.PlaybackSeconds<.2f,"Recording restarts from its first frame");
            File.WriteAllText("Documentation/Avatar/LatestVerifiedSession.txt",Path.GetFileName(sessionPath));
            File.WriteAllLines("Documentation/Avatar/PlayVerification.txt",steps);
        }
    }
    void CaptureRunningTask()
    {
        ShaderUtil.allowAsyncCompilation=false;Canvas.ForceUpdateCanvases();var camera=Camera.main;var rotation=camera.transform.localRotation;camera.transform.localRotation=Quaternion.Euler(24,-5,0);
        var rt=new RenderTexture(1600,1000,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Documentation/QuestTask/PlayView.png",image.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;camera.transform.localRotation=rotation;rt.Release();Destroy(rt);Destroy(image);
    }
    void CaptureSpectator(string name)
    {
        ShaderUtil.allowAsyncCompilation=false;Canvas.ForceUpdateCanvases();spectator.spectatorCamera.Render();
        var old=RenderTexture.active;RenderTexture.active=spectator.output;
        var image=new Texture2D(spectator.output.width,spectator.output.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();
        File.WriteAllBytes("Documentation/Avatar/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;Destroy(image);
    }
}
#endif
