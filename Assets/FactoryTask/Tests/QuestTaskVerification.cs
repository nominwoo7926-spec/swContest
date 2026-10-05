#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        Check(Mathf.Approximately(AvatarLoadHeatmap.MapColor(0),0)&&Mathf.Approximately(AvatarLoadHeatmap.MapColor(30),.5f)&&Mathf.Approximately(AvatarLoadHeatmap.MapColor(60),1)&&Mathf.Approximately(AvatarLoadHeatmap.MapColor(100),1),"Heatmap colour reaches full risk at the fatigue threshold of 60 (raw scores unchanged)");
        var line=new[]{new Vector3(.3f,0,1.6f),new Vector3(.4f,0,1.8f),new Vector3(.5f,0,2f)};
        WorkSessionController.FitLoadModel(line,0,out float alpha,out float beta);
        Check(Mathf.Abs(alpha-2)<.001f&&Mathf.Abs(beta-1)<.001f,"Regression recovers r = 2 v + 1 from window data");
        float slower=WorkSessionController.OptimalBeltSpeed(5,.45f,1,6,1,.15f),faster=WorkSessionController.OptimalBeltSpeed(-5,.45f,1,6,1,.15f);
        Check(slower<.45f&&slower>.3f&&faster>.45f&&Mathf.Approximately(WorkSessionController.OptimalBeltSpeed(0,.45f,1,6,1,.15f),.45f),"Cost minimum trades a little deadline slack for lower expected RULA");
        Check(WorkSessionController.OptimalBeltSpeed(5,.45f,1,6,10,.15f)>slower,"A larger lateness penalty keeps the speed closer to the required speed");
        Check(RulaAssessment.Combine(1,1)==1&&RulaAssessment.Combine(8,7)==7,"RULA Table C endpoints match the published worksheet");
        Check(RulaAssessment.LookupA(1,1,1,1)==1&&RulaAssessment.LookupB(1,1,1)==1,"RULA posture tables accept neutral posture");
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
    // The free part furthest along the line that has come off the belt onto the pickup table.
    ConveyorPart ArrivedPart()
    {
        float tableStart=GameObject.Find("Pickup_Table").GetComponent<Collider>().bounds.min.x;
        return task.pool.Parts.Where(p=>p.State==PartState.Conveying&&p.Shape.bounds.center.x>tableStart).OrderByDescending(p=>p.Body.position.x).FirstOrDefault();
    }
    string worstPair="";
    float MaxPartPenetration()
    {
        var active=task.pool.Parts.Where(p=>p.gameObject.activeInHierarchy).ToArray();float worst=0;worstPair="";
        for(int i=0;i<active.Length;i++)for(int j=i+1;j<active.Length;j++)
        {
            var a=active[i].Shape;var b=active[j].Shape;
            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>worst)
            {worst=depth;worstPair=active[i].State+"@"+a.transform.position.ToString("F2")+" / "+active[j].State+"@"+b.transform.position.ToString("F2");}
        }
        return worst;
    }
    // Move one hand through waypoints like a real carry, so the held part travels by physics.
    IEnumerator Carry(bool left,bool grip,params Vector3[] path)
    {
        for(int i=1;i<path.Length;i++)
            for(float t=0;t<1;t+=Time.deltaTime/.35f)
            {
                Vector3 p=Vector3.Lerp(path[i-1],path[i],t);
                if(left)Pose(p,rightRest,grip);else Pose(leftRest,p,false,grip);
                yield return null;
            }
        if(left)Pose(path[path.Length-1],rightRest,grip);else Pose(leftRest,path[path.Length-1],false,grip);
    }
    IEnumerator Run()
    {
        RunMathChecks();tracking=FindFirstObjectByType<XRTrackingProvider>();calibration=FindFirstObjectByType<UserCalibration>();task=FindFirstObjectByType<XRGrabTaskTracker>();estimator=FindFirstObjectByType<BodyLoadEstimator>();controller=FindFirstObjectByType<ConveyorController>();
        avatar=FindFirstObjectByType<FullBodyAvatarIK>();recording=FindFirstObjectByType<VRSessionRecorder>();spectator=FindFirstObjectByType<SpectatorCameraController>();
        if(recording!=null)recording.autoRecord=false;
        // The per-hand checks below use single-hand grabs; two-handed carrying is verified separately.
        task.requireBothHands=false;
        head=calibration.standingPoint.position+Vector3.up*1.65f;leftRest=head+new Vector3(-.2f,-.65f,-.05f);rightRest=head+new Vector3(.2f,-.65f,-.05f);
        Pose(leftRest,rightRest);float limit=Time.realtimeSinceStartup+8;
        while(!calibration.IsCalibrated&&Time.realtimeSinceStartup<limit)yield return null;
        Pass(calibration.IsCalibrated,"Stable tracked head and hands automatically calibrate");
        Pass(Vector3.Distance(calibration.BaselineHead,head)<.05f,"Standing origin and baseline are aligned to task");
        var rula=FindFirstObjectByType<RulaAssessment>();Pass(rula!=null&&rula.avatar==avatar,"RULA assessment is wired to the tracked avatar");
        if(recording!=null)
        {
            Directory.CreateDirectory("Documentation/Avatar");sessionPath=Path.GetFullPath("Documentation/Avatar/VerifiedSession_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".fvr");
            Pass(recording.StartRecording(sessionPath),"Binary session recording starts after calibration");
            var humanoid=avatar.modelRoot.GetComponentInChildren<Animator>();
            Pass(humanoid.avatar.isHuman&&humanoid.avatar.isValid,"Real skinned worker uses a valid Humanoid avatar");
            Pass(!spectator.spectatorCamera.GetUniversalAdditionalCameraData().allowXRRendering&&spectator.spectatorCamera.targetTexture!=null,"Spectator output is isolated from headset rendering");
            avatar.SolvePose(.033f);initialLeftFoot=avatar.leftLeg.end.position;initialRightFoot=avatar.rightLeg.end.position;
            Pass(Mathf.Abs(avatar.LeftSoleY-calibration.standingPoint.position.y)<.03f&&Mathf.Abs(avatar.RightSoleY-calibration.standingPoint.position.y)<.03f,"Avatar soles are aligned to the calibrated floor");
        }
        Time.timeScale=4;
        // Control panel: nothing runs until red; green and blue are exclusive; red restarts cleanly.
        var session=FindFirstObjectByType<WorkSessionController>();var lineHeight=FindFirstObjectByType<LineHeightAdjuster>();
        yield return new WaitForSeconds(1);
        Pass(session.State==WorkSessionController.RunState.Waiting&&task.pool.SpawnedCount==0,"The line waits for the red button");
        session.SelectMode(WorkSessionController.Mode.Optimized);bool blueSelected=session.SelectedMode==WorkSessionController.Mode.Optimized;
        session.SelectMode(WorkSessionController.Mode.Baseline);
        Pass(blueSelected&&session.SelectedMode==WorkSessionController.Mode.Baseline,"Green and blue select one exclusive mode");
        float tableBefore=lineHeight.TableTopHeight,beltBefore=controller.driveSurfaces[0].bounds.max.y;
        lineHeight.SetTargetOffset(.1f);float until=Time.time+20;while(lineHeight.Moving&&Time.time<until)yield return null;
        Pass(Mathf.Abs(lineHeight.TableTopHeight-tableBefore-.1f)<.005f&&Mathf.Abs(controller.driveSurfaces[0].bounds.max.y-beltBefore-.1f)<.005f,"Raising the line moves belt and table together");
        session.StartRun();yield return new WaitForSeconds(6);
        Pass(Mathf.Abs(lineHeight.Offset)<.001f&&task.pool.SpawnedCount>=2,"Red starts a run at the default height and supplies parts");
        session.StartRun();
        Pass(task.pool.SpawnedCount==0&&task.pool.Parts.All(p=>!p.gameObject.activeSelf)&&task.CompletedTotal==0,"Red during a run clears every part and count and restarts");
        Pass(task.pool.spawnLimit==100&&task.pool.finalPartMaterial!=null,"Quota is 100 parts and the last one has its own colour");
        until=Time.time+50;
        while(ArrivedPart()==null&&Time.time<until)yield return null;yield return new WaitForSeconds(1);
        Pass(ArrivedPart()!=null&&!ArrivedPart().Body.isKinematic,"Belt friction pushes dynamic parts off the conveyor end onto the table");
        int supplied=task.pool.SpawnedCount;yield return new WaitForSeconds(4);
        Pass(task.pool.SpawnedCount>supplied,"Supply continues while parts accumulate");
        Pass(MaxPartPenetration()<.01f,"Parts collide instead of interpenetrating (max "+MaxPartPenetration().ToString("F3")+" m)");
        ConveyorPart first=ArrivedPart();Vector3 firstPosition=first.Body.position;
        Pose(firstPosition,firstPosition,true,true);yield return new WaitForSeconds(.18f);
        Pass(task.LeftHeld==first&&first.Holder==HandSide.Left&&task.RightHeld!=first,"Simultaneous grips cannot own the same part twice");
        Pass(first.State==PartState.Held&&!first.Body.isKinematic&&!first.Body.useGravity,"Held part stays a dynamic body driven toward the hand");
        Vector3 working=head+new Vector3(-.5f,-.2f,.6f);Pose(working,rightRest,true);yield return new WaitForSeconds(3);
        Pass(task.LeftHeld==first&&Vector3.Distance(first.Shape.ClosestPoint(tracking.leftHand.position),tracking.leftHand.position)<.06f,"Held part follows a fast hand move and stays seated in the palm");
        Pass(estimator.Score(0)>estimator.Score(1)+5&&estimator.Score(2)>estimator.Score(3)+5&&estimator.Score(4)>estimator.Score(5)+5,"Left-hand work raises left shoulder, arm and wrist independently");
        CaptureRunningTask();
        if(avatar!=null)
        {
            avatar.SolvePose(.033f);avatar.GetComponent<AvatarLoadHeatmap>().Apply();
            CaptureSpectator("Spectator_LeftWork");
            Pass(avatar.LeftGripError<.20f,"Avatar preserves anatomical arm length and bounds unreachable controller error (error="+avatar.LeftGripError.ToString("F3")+" m, root="+avatar.modelRoot.position.ToString("F2")+", shoulder="+avatar.leftArm.upper.position.ToString("F2")+", hand="+tracking.leftHand.position.ToString("F2")+")");
            Pass(Vector3.Distance(avatar.leftGripAnchor.position,first.Shape.ClosestPoint(avatar.leftGripAnchor.position))<.20f,"Avatar palm remains close to the held part without stretching bones");
            float scale=avatar.modelRoot.localScale.x;
            Pass(Vector3.Distance(avatar.head.position+tracking.head.rotation*avatar.eyeOffset*scale,tracking.head.position)<.01f,"Avatar eye anchor follows the tracked HMD pose");
            var block=new MaterialPropertyBlock();avatar.GetComponent<AvatarLoadHeatmap>().surfaces[0].GetPropertyBlock(block);
            var heat=block.GetVector("_LoadsA");Pass(heat.x>heat.y&&heat.z>heat.w,"Surface heat shader receives independent left/right loads");
        }
        float heldTime=first.HoldSeconds;Pose(working,rightRest,true,false,false);yield return new WaitForSeconds(.5f);
        Pass(first.State==PartState.Held&&Mathf.Abs(first.HoldSeconds-heldTime)<.1f,"Tracking loss freezes held part and hold timer");
        Pose(working,rightRest,true);yield return new WaitForSeconds(.15f);
        yield return Carry(true,true,working,head+new Vector3(0,-1.3f,-.45f));Pose(head+new Vector3(0,-1.3f,-.45f),rightRest,false);yield return new WaitForSeconds(1);
        Pass(first.State==PartState.Dropped&&task.CompletedTotal==0,"Floor drop does not count as completion");
        Pose(first.Body.position,rightRest,true);yield return new WaitForSeconds(.18f);
        Pass(task.LeftHeld==first,"Dropped part can be grabbed again");
        Vector3 inside=task.completionVolume.transform.TransformPoint(task.completionVolume.center);
        Vector3 aboveBin=inside+Vector3.up*.45f;
        yield return Carry(true,true,first.Body.position,head+new Vector3(0,-.25f,-.1f),aboveBin,inside);yield return new WaitForSeconds(1.5f);
        Pass(task.CompletedTotal==0,"A held part inside the box is not counted");
        float beforeRelease=estimator.Score(4);Pose(inside,rightRest,false);yield return new WaitForSeconds(.8f);
        Pass(task.CompletedLeft==1&&task.CompletedRight==0&&task.LeftHeld==null,"Released part inside completion box records left-hand task");
        Pass(first.Completed&&!first.Free&&task.BinnedCount==1,"Completed part stays in the bin and cannot be re-counted");
        float afterRelease=estimator.Score(4);yield return new WaitForSeconds(2);
        Pass(estimator.Score(4)>0&&estimator.Score(4)<afterRelease&&afterRelease<beforeRelease,"Post-release load persists and decays gradually");
        until=Time.time+15;while(ArrivedPart()==null&&Time.time<until)yield return null;
        var second=ArrivedPart();Pose(leftRest,second.Body.position,false,true);yield return new WaitForSeconds(.2f);
        Pass(task.RightHeld==second,"Right Grip can pick the next part");
        Pose(leftRest,head+new Vector3(.55f,-.2f,.6f),false,true);yield return new WaitForSeconds(3);
        Pass(estimator.Score(1)>10&&estimator.Score(3)>10&&estimator.Score(5)>10,"Right-hand work drives right-side scores");
        if(avatar!=null){avatar.SolvePose(.033f);CaptureSpectator("Spectator_RightWork");}
        yield return Carry(false,true,head+new Vector3(.55f,-.2f,.6f),aboveBin,inside);yield return new WaitForSeconds(.2f);Pose(leftRest,inside);yield return new WaitForSeconds(.8f);
        Pass(task.CompletedRight==1&&task.CompletedTotal==2,"Right completion is recorded separately");
        Pass(estimator.RecentCount(HandSide.Left,Time.time)==1&&estimator.RecentCount(HandSide.Right,Time.time)==1,"Recent repetition history is isolated by hand");
        Pass(estimator.RecentCount(HandSide.Left,Time.time+61)==0,"Repetition history expires after 60 seconds");
        task.requireBothHands=true;
        until=Time.time+15;while(ArrivedPart()==null&&Time.time<until)yield return null;
        var pair=ArrivedPart();Vector3 centre=pair.Shape.bounds.center,across=Vector3.right*pair.HalfHeight;
        Pose(centre-across,centre+across,true,false);yield return new WaitForSeconds(.2f);
        Pass(task.LeftHeld==null&&task.RightHeld==null,"A single grip no longer picks up a part");
        // Parts on a busy table get nudged by the next ones: aim at where this one is now.
        centre=pair.Shape.bounds.center;Pose(centre-across,centre+across,false,false);yield return null;
        Pose(centre-across,centre+across,true,true);yield return new WaitForSeconds(.2f);
        Pass(task.CarriedWithBothHands&&task.LeftHeld==pair&&Mathf.Approximately(task.HeldWeight,pair.weightKg)&&Mathf.Approximately(task.HandWeight(HandSide.Left),pair.weightKg*.5f),"Both grips on one part carry it together and split its weight between the arms (held "+(task.LeftHeld!=null?task.LeftHeld.name:"none")+"/"+(task.RightHeld!=null?task.RightHeld.name:"none")+", target "+pair.name+" "+pair.State+", palms "+Vector3.Distance(pair.Shape.ClosestPoint(task.Palm(HandSide.Left)),task.Palm(HandSide.Left)).ToString("F3")+"/"+Vector3.Distance(pair.Shape.ClosestPoint(task.Palm(HandSide.Right)),task.Palm(HandSide.Right)).ToString("F3")+")");
        Pose(centre-across+Vector3.up*.2f,centre+across+Vector3.up*.2f,true,true);yield return new WaitForSeconds(.4f);
        Pass(task.LeftHeld==pair&&pair.Body.position.y>centre.y+.1f,"A two-handed carry lifts the part between the palms");
        Pose(centre-across+Vector3.up*.2f,centre+across+Vector3.up*.2f,true,false);yield return new WaitForSeconds(.2f);
        Pass(task.LeftHeld==null&&task.RightHeld==null&&pair.State==PartState.Dropped,"Opening either hand releases the part");
        Pose(leftRest,rightRest);yield return new WaitForSeconds(.5f);
        var director=FindFirstObjectByType<ThirdPersonAvatarDirector>();
        if(director!=null&&spectator!=null)
        {
            until=Time.time+15;while(director.isPerformingAction&&Time.time<until)yield return null;
            // Real time, and the animator (with its IK pass) evaluated right before each capture,
            // so the captured pose matches the carried part exactly.
            float previousScale=Time.timeScale;Time.timeScale=1;
            var dummyAnimator=director.GetComponent<Animator>();
            for(int cycle=0;cycle<3;cycle++)
            {
                director.TriggerPickAndPlaceSequence();
                if(cycle==0)Pass(director.isPerformingAction,"Spectator dummy starts when a part is picked up");
                until=Time.time+10;while(director.CurrentStage!=ThirdPersonAvatarDirector.Stage.Carry&&Time.time<until)yield return null;
                if(cycle==0){yield return new WaitForSeconds(.35f);dummyAnimator.Update(0);CaptureSpectator("Dummy_1_Carry");}
                until=Time.time+10;while(director.CurrentStage!=ThirdPersonAvatarDirector.Stage.Hold&&Time.time<until)yield return null;
                if(cycle==0)
                {
                    yield return new WaitForSeconds(1);dummyAnimator.Update(0);CaptureSpectator("Dummy_2_Hold");
                    Pass(director.CurrentStage==ThirdPersonAvatarDirector.Stage.Hold,"Dummy holds its part above the bin until the real part is released");
                }
                director.NotifyRelease();
                until=Time.time+10;while(director.CurrentStage!=ThirdPersonAvatarDirector.Stage.Return&&Time.time<until)yield return null;
                if(cycle==0){dummyAnimator.Update(0);CaptureSpectator("Dummy_3_Released");}
                until=Time.time+10;while(director.isPerformingAction&&Time.time<until)yield return null;
            }
            yield return new WaitForSeconds(1);dummyAnimator.Update(0);CaptureSpectator("Dummy_4_Bin");
            Time.timeScale=previousScale;
            Pass(!director.isPerformingAction,"Dummy sequence finishes and returns to idle");
            Pass(director.ShownInBin>=1&&director.ShownInBin<=2,"Third-person bin shows at most two placed parts ("+director.ShownInBin+")");
            Pass(FindFirstObjectByType<BodyLoadVisualizer>().figure.texture!=null,"Load panel shows the worker figure");
        }
        yield return new WaitForSeconds(4);
        Pass(task.BinnedCount==2&&task.pool.Parts.All(p=>!p.gameObject.activeSelf||p.weightKg==5),"Both completed parts rest in the bin; every part is the single 5 kg size");
        Pass(task.pool.Parts.Count(p=>p.State==PartState.Conveying)>3,"Line keeps supplying parts");
        float accumulated=MaxPartPenetration();
        Pass(accumulated<.01f,"Accumulated parts still do not interpenetrate (max "+accumulated.ToString("F3")+" m "+worstPair+")");
        tracking.Simulate(head+new Vector3(0,-.22f,.23f),leftRest,rightRest);yield return new WaitForSeconds(1.5f);
        Pass(estimator.Score(6)>25,"Tracked head lowering and forward movement raise torso load in Play mode");
        if(recording!=null)
        {
            avatar.SolvePose(.033f);CaptureSpectator("Spectator_Bending");
            Pass(Vector3.Distance(avatar.leftLeg.end.position,initialLeftFoot)<.035f&&Vector3.Distance(avatar.rightLeg.end.position,initialRightFoot)<.035f,"Feet remain planted while the tracked head bends forward");
            rula.Evaluate();Pass(rula.IsValid&&rula.WorstGrandScore>=1&&rula.WorstGrandScore<=7,"RULA produces a bounded full-body grand score");
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
