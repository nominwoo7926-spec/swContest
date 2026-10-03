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
        var longer=input;longer.holdSeconds=25;var moving=input;moving.travelMetres=4;var repeating=input;repeating.repeats=10;var high=input;high.handAboveShoulder=.4f;var low=input;low.handAboveShoulder=-.85f;
        Check(BodyLoadEstimator.Evaluate(heavier).x>baseline.x&&BodyLoadEstimator.Evaluate(heavier).z>baseline.z,"5 kg increases shoulder and wrist versus 1 kg");
        Check(BodyLoadEstimator.Evaluate(far).x>baseline.x&&BodyLoadEstimator.Evaluate(far).y>baseline.y,"Farther reach increases shoulder and arm");
        Check(BodyLoadEstimator.Evaluate(longer).y>baseline.y&&BodyLoadEstimator.Evaluate(longer).z>baseline.z,"Longer holding increases arm and wrist");
        Check(BodyLoadEstimator.Evaluate(moving).z>baseline.z,"Hand travel increases wrist");
        Check(BodyLoadEstimator.Evaluate(repeating).y>baseline.y,"Recent repetition increases arm");
        Check(BodyLoadEstimator.Evaluate(high).x>baseline.x,"Higher hands increase shoulder");
        Check(BodyLoadEstimator.Evaluate(low).x>baseline.x&&BodyLoadEstimator.Evaluate(low).y>baseline.y,"Low table reach increases shoulder and arm load");
        Check(BodyLoadEstimator.EvaluateTorso(.3f,.3f)>BodyLoadEstimator.EvaluateTorso(.05f,.05f),"Head drop and forward displacement increase torso");
        Check(BodyLoadEstimator.EvaluateTorso(10,10)==100&&BodyLoadEstimator.EvaluateTorso(-1,-1)==0,"Torso is bounded 0 to 100");
        Vector3 buttonPoint=new Vector3(0,1,-3.91f);
        Check(VRButtonTouch.IsInReach(buttonPoint+new Vector3(0,0,-.15f),buttonPoint),"Controller origin behind its tip activates the touched panel button");
        Check(!VRButtonTouch.IsInReach(buttonPoint+new Vector3(.22f,0,-.15f),buttonPoint),"Adjacent button is not activated by the same controller pose");
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
        var partVisual=FindFirstObjectByType<PartSpawner>().prefab;
        var loadPanel=GameObject.Find("Relative_Load_Panel");
        var completionBin=GameObject.Find("Completion_Box");
        Pass(loadPanel!=null&&completionBin!=null&&loadPanel.transform.parent==completionBin.transform&&
            !loadPanel.transform.IsChildOf(tracking.head)&&loadPanel.transform.position.x>completionBin.transform.position.x+.6f,
            "Live load report stays fixed to the right of the completion bin");
        Pass(partVisual.weightLabel!=null&&partVisual.transform.Find("Top_Insert")==null&&partVisual.transform.Find("Fastener")==null,
            "Parts show weight without button-like black inserts or metal studs");
        if(recording!=null)recording.autoRecord=false;
        head=calibration.standingPoint.position+Vector3.up*1.65f;leftRest=head+new Vector3(-.2f,-.65f,-.05f);rightRest=head+new Vector3(.2f,-.65f,-.05f);
        Pose(leftRest,rightRest);float limit=Time.realtimeSinceStartup+8;
        while(!calibration.IsCalibrated&&Time.realtimeSinceStartup<limit)yield return null;
        Pass(calibration.IsCalibrated,"Stable tracked head and hands automatically calibrate");
        Pass(Vector3.Distance(calibration.BaselineHead,head)<.05f,"Standing origin and baseline are aligned to task");
        var speedButtons=FindFirstObjectByType<ConveyorSpeedControls>();
        var autoButton=FindFirstObjectByType<AdaptiveErgonomicsAgent>();
        var profile=FindFirstObjectByType<WorkerProfile>();
        var personal=FindFirstObjectByType<PersonalizationConsole>();
        Pass(profile!=null&&personal!=null&&personal.buttons.Length==5&&autoButton.approvalRequired&&
            !personal.menuConsole.activeSelf&&personal.workConsole.activeSelf,
            "An optional five-button profile, feedback and approval menu starts hidden");
        tracking.Simulate(head,leftRest,rightRest,recalibrate:true);yield return null;
        Pose(leftRest,rightRest);yield return null;
        Pass(personal.menuConsole.activeSelf&&!personal.workConsole.activeSelf&&
            personal.reportLabel.text.Contains("WORK REPORT"),
            "Short Y opens the report directly without duplicate work buttons");
        CapturePersonalMenu(personal,"PersonalReportView");
        var collector=FindFirstObjectByType<ManufacturingDataCollector>();
        float sampleLimit=Time.realtimeSinceStartup+3;
        while(collector.PendingSamples==0&&Time.realtimeSinceStartup<sampleLimit)yield return null;
        Pass(collector.PendingSamples>0,"Tracked sensor rows accumulate before a subjective score is entered");
        Pose(personal.buttons[2].position-personal.buttons[2].forward*.15f,rightRest);yield return null;
        Pass(personal.reportLabel.text.Contains("PROFILE: HEIGHT"),"Report opens the optional height input");
        CapturePersonalMenu(personal,"PersonalizationView");
        Pose(leftRest,rightRest);yield return null;
        Vector3 menuNext=personal.buttons[4].position-personal.buttons[4].forward*.15f;
        Pose(menuNext,rightRest);yield return null;
        Pass(personal.reportLabel.text.Contains("ARM REACH"),"Profile menu advances from height to reach");
        Pose(leftRest,rightRest);yield return null;
        Pose(menuNext,rightRest);yield return null;
        Pass(personal.reportLabel.text.Contains("WORK REPORT"),"Reach page returns to the report");
        Pose(leftRest,rightRest);yield return null;
        Pose(personal.buttons[3].position-personal.buttons[3].forward*.15f,rightRest);yield return null;
        Pass(personal.reportLabel.text.Contains("AFTER A WORK BLOCK"),"Report opens real discomfort feedback");
        Pose(leftRest,rightRest);yield return null;
        Pose(personal.buttons[2].position-personal.buttons[2].forward*.15f,rightRest);yield return null;
        Pass(personal.reportLabel.text.Contains("FEEDBACK SAVED: 3")&&
            File.Exists(collector.LastSavedPath)&&File.ReadLines(collector.LastSavedPath).First().Contains("profile_id"),
            "VR score 3 labels actual sampled rows in the v3 CSV and opens the report");
        Pose(leftRest,rightRest);yield return null;
        tracking.Simulate(head,leftRest,rightRest,recalibrate:true);yield return null;
        Pose(leftRest,rightRest);yield return null;
        Pass(!personal.menuConsole.activeSelf&&personal.workConsole.activeSelf,
            "Short Y returns to the five normal work buttons");
        var allButtons=new List<Transform>{speedButtons.slowerButton,speedButtons.fasterButton};
        Pass(allButtons.Count==2&&allButtons.TrueForAll(button=>Mathf.Abs(button.position.x-head.x)<=.60f&&
            button.position.z<head.z-.45f&&button.gameObject.layer==28),
            "Only two belt-speed buttons sit behind the worker and outside the spectator view");
        Pass(FindFirstObjectByType<TableHeightControls>()==null&&FindFirstObjectByType<VRFeedbackControls>()==null&&
            autoButton.toggleButton==null&&autoButton.automatic,"Automatic table and agent remain on without extra buttons");
        autoButton.automatic=false;
        Pass(task.pool.Mode==PartSpawner.SupplyMode.FiveKg&&FindFirstObjectByType<PartSelectionControls>()==null,
            "Supply is fixed at 5 kg with no weight-selection controls");
        Pass(speedButtons.slowerButton.localScale.x<.2f,
            "VR control buttons retain their authored physical size after Update");
        task.pool.SetMode(PartSpawner.SupplyMode.OneKg);
        Pass(task.pool.Mode==PartSpawner.SupplyMode.FiveKg,"Legacy weight changes cannot override the 5 kg supply");
        Pass(speedButtons.speedLabel!=null&&speedButtons.speedLabel.text.Contains("m/s"),"Belt speed is displayed in metres per second");
        float initialSpeed=controller.Speed;
        Pose(speedButtons.fasterButton.position-speedButtons.fasterButton.forward*.15f,rightRest);yield return null;
        Pass(controller.Speed>initialSpeed,"Controller tip contact increases belt speed");
        controller.AdjustSpeed(initialSpeed-controller.Speed,false);
        Pose(leftRest,rightRest);yield return null;
        if(recording!=null)
        {
            Directory.CreateDirectory("Documentation/Avatar");sessionPath=Path.GetFullPath("Documentation/Avatar/VerifiedSession_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".fvr");
            Pass(recording.StartRecording(sessionPath),"Binary session recording starts after calibration");
            var humanoid=avatar.modelRoot.GetComponentInChildren<Animator>();
            Pass(humanoid.avatar.isHuman&&humanoid.avatar.isValid,"Real skinned worker uses a valid Humanoid avatar");
            Pass(!spectator.spectatorCamera.GetUniversalAdditionalCameraData().allowXRRendering&&spectator.spectatorCamera.targetTexture!=null,"Spectator output is isolated from headset rendering");
            avatar.SolvePose(.033f);initialLeftFoot=avatar.leftLeg.end.position;initialRightFoot=avatar.rightLeg.end.position;
            Pass(Mathf.Abs(avatar.LeftSoleHeight-calibration.standingPoint.position.y)<.025f&&
                Mathf.Abs(avatar.RightSoleHeight-calibration.standingPoint.position.y)<.025f,
                "Avatar shoe soles start on the calibrated floor");
        }
        Time.timeScale=4;
        float until=Time.time+90;
        while((task.queue.Count<task.queue.Capacity||task.queue.Front.State!=PartState.Waiting)&&Time.time<until)yield return null;
        Pass(task.queue.Count==12&&task.queue.Front.State==PartState.Waiting,"Conveyor fills the bounded FIFO accumulation queue");
        Pass(task.pool.Parts.All(part=>part.State==PartState.Pooled||
            (Mathf.Abs(part.weightKg-5f)<.01f&&part.weightLabel.text=="5 kg")),
            "Every supplied box is visibly labeled 5 kg");
        var beltSurface=GameObject.Find("Production_Line_A").GetComponentsInChildren<Renderer>();
        var beltTop=Array.Find(beltSurface,r=>r.name=="Upper_Belt");
        var pickupTop=GameObject.Find("Pickup_Table").GetComponent<Renderer>();
        var binBase=GameObject.Find("Box_Base").GetComponent<Renderer>();
        Pass(pickupTop.bounds.min.x-beltTop.bounds.max.x>.10f&&binBase.bounds.min.x-pickupTop.bounds.max.x>.10f&&
            Mathf.Abs(pickupTop.bounds.center.z-beltTop.bounds.center.z)<.05f&&Mathf.Abs(binBase.bounds.center.z-beltTop.bounds.center.z)<.05f,
            "Belt, pickup table and completion bin form a clear left-to-right line");
        Pass(task.queue.tableSlotCount==3&&task.queue.slots.Take(3).All(slot=>slot.IsChildOf(FindFirstObjectByType<AdjustableWorktable>().movingTop))&&
            task.queue.slots.Skip(3).All(slot=>!slot.IsChildOf(FindFirstObjectByType<AdjustableWorktable>().movingTop)),
            "Queued parts accumulate on the belt while three pickup parts follow the moving table");
        Pass(task.queue.Front.Shape.bounds.min.y>=pickupTop.bounds.max.y-.015f,
            "Waiting part rests above the pickup surface without being half buried");
        int supplied=task.pool.SpawnedCount;yield return new WaitForSeconds(4);
        Pass(task.pool.SpawnedCount==supplied,"Full queue applies backpressure without instantiation");
        ConveyorPart first=task.queue.Front;Vector3 firstPosition=first.Body.position;
        var table=FindFirstObjectByType<AdjustableWorktable>();
        var agent=FindFirstObjectByType<AdaptiveErgonomicsAgent>();
        agent.automatic=true;
        table.SetTargetHeight(1.20f);
        float realLimit=Time.realtimeSinceStartup+8;
        while(!agent.HasProposal&&Time.realtimeSinceStartup<realLimit)yield return null;
        Pass(agent.HasProposal&&Mathf.Abs(table.TargetHeight-1.20f)<.01f,
            "Agent proposes a safer table setting without moving equipment before approval");
        Pass(agent.ApproveProposal()&&table.TargetHeight<1.15f,
            "Approval applies the proposed virtual table target");
        Pass(agent.PendingOutcomeCount>0,"Agent retains a pending verification for its physical table action");
        tracking.Simulate(head,leftRest,rightRest,false,false,false);agent.ObserveAndAct(1);
        Pass(agent.PendingOutcomeCount==0&&agent.Decision=="SENSOR WAIT","Sensor loss invalidates unverifiable action outcomes");
        tracking.Simulate(head,leftRest,rightRest);
        agent.automatic=false;
        table.SetTargetHeight(.55f,true);
        until=Time.time+10;while((table.Moving||Mathf.Abs(first.Body.position.y-(table.Height+first.HalfHeight+.055f))>.04f)&&Time.time<until)yield return null;
        Pass(table.Height<.57f&&first.Body.position.y<firstPosition.y-.24f,"Lowering the table moves the solid surface and queued pickup part");
        Pass(first.Shape.bounds.min.y>=pickupTop.bounds.max.y-.015f,
            "Lowest table position keeps the waiting part fully visible above the surface");
        Vector3 standingHeightHand=new Vector3(first.Body.position.x,head.y-.25f,first.Body.position.z);
        Pose(standingHeightHand,standingHeightHand,true,true);yield return new WaitForSeconds(.18f);
        Pass(task.LeftHeld==null&&task.RightHeld==null,"A hand left at standing-height cannot grab the lowered part");
        Pose(leftRest,rightRest);yield return new WaitForSeconds(.12f);
        Vector3 lowered=first.Body.position;Pose(lowered,rightRest,true,false);yield return new WaitForSeconds(.18f);
        Pass(task.LeftHeld==null&&task.RightHeld==null,"One Grip cannot start a pickup even when touching the part");
        Pose(leftRest,rightRest);yield return new WaitForSeconds(.12f);
        Pose(lowered,lowered,true,true);yield return new WaitForSeconds(.18f);
        Pass(task.LeftHeld==first&&first.Holder==HandSide.Left&&task.RightHeld!=first,"Simultaneous grips cannot own the same part twice");
        Pass(task.queue.Front!=first,"Removing the first item advances the next FIFO item");
        Vector3 working=head+new Vector3(-.5f,-.2f,.6f);Pose(working,rightRest,true);yield return new WaitForSeconds(3);
        Pass(estimator.Score(0)>estimator.Score(1)+5&&estimator.Score(2)>estimator.Score(3)+5&&estimator.Score(4)>estimator.Score(5)+5,"Left-hand work raises left shoulder, arm and wrist independently");
        CaptureRunningTask();
        if(avatar!=null)
        {
            avatar.SolvePose(.033f);avatar.GetComponent<AvatarLoadHeatmap>().Apply();
            CaptureSpectator("Spectator_LeftWork");
            Pass(avatar.LeftGripError<.01f,"Avatar grip anchor follows the held part within reachable arm range (error="+avatar.LeftGripError.ToString("F3")+" m)");
            Pass(avatar.LeftWristBend<=95.1f&&avatar.RightWristBend<=95.1f,
                "Avatar wrists remain inside the visual bend limit");
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
        Pose(first.Body.position,rightRest,true,true);yield return new WaitForSeconds(.18f);
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
        var second=task.queue.Front;Pose(leftRest,second.Body.position,true,true);yield return new WaitForSeconds(.2f);
        Pass(task.RightHeld==second,"Right Grip can pick the next part");
        Pose(leftRest,head+new Vector3(.55f,-.2f,.6f),false,true);yield return new WaitForSeconds(3);
        Pass(estimator.Score(1)>10&&estimator.Score(3)>10&&estimator.Score(5)>10,"Right-hand work drives right-side scores");
        if(avatar!=null){avatar.SolvePose(.033f);CaptureSpectator("Spectator_RightWork");}
        Pose(leftRest,inside,false,true);yield return new WaitForSeconds(.2f);Pose(leftRest,inside);yield return new WaitForSeconds(.8f);
        Pass(task.CompletedRight==1&&task.CompletedTotal==2,"Right completion is recorded separately");
        Pass(estimator.RecentCount(HandSide.Left,Time.time)==1&&estimator.RecentCount(HandSide.Right,Time.time)==1,"Recent repetition history is isolated by hand");
        Pass(estimator.RecentCount(HandSide.Left,Time.time+61)==0,"Repetition history expires after 60 seconds");
        yield return new WaitForSeconds(4);
        Pass(task.pool.ReusedCount>0&&task.pool.Parts.Length==16,"Pool reuses instances and stays bounded at sixteen");
        Pass(task.queue.Count<=12,"Queue capacity stays bounded throughout the task");
        float speedBeforeLoss=controller.Speed;
        tracking.Simulate(head,leftRest,rightRest,false,false,false);agent.ObserveAndAct(1);
        Pass(agent.Decision=="SENSOR WAIT"&&Mathf.Approximately(controller.Speed,speedBeforeLoss),"Sensor loss stops automatic intervention and reports why");
        tracking.Simulate(head,leftRest,rightRest);
        tracking.Simulate(head+new Vector3(0,-.22f,.23f),leftRest,rightRest);yield return new WaitForSeconds(1.5f);
        Pass(estimator.Score(6)>25,"Tracked head lowering and forward movement raise torso load in Play mode");
        if(recording!=null)
        {
            avatar.SolvePose(.033f);CaptureSpectator("Spectator_Bending");
            Pass(Vector3.Distance(avatar.leftLeg.end.position,initialLeftFoot)<.035f&&Vector3.Distance(avatar.rightLeg.end.position,initialRightFoot)<.035f,"Feet remain planted while the tracked head bends forward");
            recording.StopRecording();Pass(recording.FramesWritten>100,"Head, hands, parts, scores and task counts are streamed to a bounded binary recording");
            float recordedTableHeight=table.Height,recordedBeltSpeed=controller.Speed;
            table.ApplyRecordedHeight(AdjustableWorktable.MinHeight);
            controller.ApplyRecordedSpeed(ConveyorController.MinSpeed);
            Pass(recording.BeginPlayback(sessionPath),"Recorded session reopens for third-person playback");
            Pass(recording.HasRecordedEquipment,"New session format contains changing table height and belt speed");
            Pass(!task.enabled&&!controller.enabled&&!estimator.enabled&&tracking.ExternalPlayback,"Playback disables live simulation and XR pose writes");
            recording.TickPlayback(recording.Duration+1);avatar.SolvePose(.033f);CaptureSpectator("Spectator_Replay");
            Pass(Mathf.Abs(table.Height-recordedTableHeight)<.02f&&Mathf.Abs(controller.Speed-recordedBeltSpeed)<.02f,
                "Playback restores the recorded physical table surface and belt speed rather than burying parts below a static table");
            Pass(task.CompletedTotal==2,"Recorded left/right task counts survive playback");
            Pass(estimator.Score(6)>25,"Recorded torso load survives playback");
            Pass(Vector3.Distance(tracking.head.position,head+new Vector3(0,-.22f,.23f))<.03f,"Recorded head trajectory survives playback");
            recording.RestartPlayback();Pass(recording.PlaybackSeconds<.2f,"Recording restarts from its first frame");
            Vector3 lowHead=calibration.standingPoint.position+Vector3.up*1.10f;
            tracking.ApplyRecordedPose(lowHead,Quaternion.identity,lowHead+new Vector3(-.2f,-.35f,.25f),Quaternion.identity,
                lowHead+new Vector3(.2f,-.35f,.25f),Quaternion.identity,7);
            calibration.ApplyRecordedCalibration(lowHead,calibration.standingPoint.forward);
            avatar.ResetPlant();avatar.SolvePose(.033f);
            Pass(Mathf.Abs(avatar.LeftSoleHeight-calibration.standingPoint.position.y)<.04f&&
                Mathf.Abs(avatar.RightSoleHeight-calibration.standingPoint.position.y)<.04f,
                "Both shoe soles remain on the floor with a 1.10 m tracked head height");
            Vector3 leftLegOffset=avatar.leftLeg.end.position-avatar.hips.position;
            Vector3 rightLegOffset=avatar.rightLeg.end.position-avatar.hips.position;
            Pass(new Vector2(leftLegOffset.x,leftLegOffset.z).magnitude<.30f&&
                new Vector2(rightLegOffset.x,rightLegOffset.z).magnitude<.30f,
                "Feet remain below the corrected pelvis rather than stretching toward the old work position");
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
    void CapturePersonalMenu(PersonalizationConsole menu,string name)
    {
        ShaderUtil.allowAsyncCompilation=false;
        var camera=Camera.main;var oldRotation=camera.transform.rotation;
        camera.transform.LookAt(menu.menuConsole.transform.position+Vector3.up*1.12f,Vector3.up);
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
        File.WriteAllBytes("Documentation/QuestTask/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;camera.transform.rotation=oldRotation;
        rt.Release();Destroy(rt);Destroy(image);
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
