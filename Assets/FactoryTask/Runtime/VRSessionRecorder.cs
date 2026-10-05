using System;
using System.IO;
using Meta.XR.Movement.Retargeting;
using UnityEngine;
using static Meta.XR.Movement.MSDKUtility;

namespace FactoryTask
{
    // Fixed-size binary frames, streaming I/O, bounded buffers. No JSON/string allocation per sample.
    [DefaultExecutionOrder(50)]
    public sealed class VRSessionRecorder : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public BodyLoadEstimator estimator;
        public XRGrabTaskTracker task;
        public PartSpawner pool;
        public ConveyorController conveyor;
        public FullBodyAvatarIK avatar;
        public MovementBodyPoseStream bodySource;
        public CharacterRetargeter bodyRetargeter;
        public LineHeightAdjuster line;
        public bool autoRecord = true;
        public bool IsRecording => writer != null;
        public bool IsPlayback => reader != null;
        public bool PlaybackPaused { get; set; }
        public string CurrentPath { get; private set; }
        public string LastError { get; private set; }
        public float PlaybackSeconds => playbackClock;
        public int FramesWritten { get; private set; }
        public float Duration { get; private set; }
        // 녹화 중이면 현재 경과 시간, 재생/정지 중이면 파일에서 읽은 Duration 반환
        public float RecordingDuration => IsRecording ? (Time.unscaledTime - started) : Duration;
        public const int Rate = 30;
        const int Magic = 0x46565232, Version = 4, LegacyVersion = 1, HeaderBytes = 16;
        // Belt speed of the frame being replayed (v4 recordings; 0 for older files).
        public float PlaybackBeltSpeed { get; private set; }
        const int TransformBytes = 28;
        BinaryWriter writer;
        BinaryReader reader;
        float started, nextSample, nextFlush, toggleHeld, playbackClock;
        bool toggleConsumed, paused, movementPlaybackActive;
        int capacity, frameBytes, fileVersion;
        Frame a, b;
        readonly float[] mixedScores = new float[7];
        readonly NativeTransform[] capturePose = new NativeTransform[MovementBodyPoseStream.JointCount];
        readonly NativeTransform[] captureTPose = new NativeTransform[MovementBodyPoseStream.JointCount];
        readonly NativeTransform[] mixedPose = new NativeTransform[MovementBodyPoseStream.JointCount];
        readonly NativeTransform[] mixedTPose = new NativeTransform[MovementBodyPoseStream.JointCount];
        public static string SessionDirectory => Path.Combine(Application.persistentDataPath,"FactorySessions");

        void Awake()
        {
            bool movement=bodySource!=null&&bodyRetargeter!=null;
            if(bodyRetargeter!=null){avatarRootScale=bodyRetargeter.transform.localScale;bodyRetargeter.enabled=movement;}
            // Start on IK until the first valid body pose arrives.
            if(avatar!=null)avatar.enabled=true;
        }

        sealed class Frame
        {
            public float time,weight;
            public int flags,left,right;
            public Vector3 hp,lp,rp,baseline,forward;
            public Quaternion hq,lq,rq;
            public readonly float[] scores = new float[7];
            public readonly Vector3[] pp;
            public readonly Quaternion[] pq;
            public readonly byte[] state,kind;
            public bool bodyValid;
            public float lineOffset,beltSpeed;
            public readonly NativeTransform[] bodyPose = new NativeTransform[MovementBodyPoseStream.JointCount];
            public readonly NativeTransform[] bodyTPose = new NativeTransform[MovementBodyPoseStream.JointCount];
            public Frame(int count){pp=new Vector3[count];pq=new Quaternion[count];state=new byte[count];kind=new byte[count];}
        }
        // Live driver choice: Movement retargeting while Quest body tracking delivers a valid pose,
        // otherwise (permission denied, start failure, tracking loss) the three-point IK avatar.
        float bodyInvalidSeconds=1;
        Vector3 avatarRootScale=Vector3.one;
        void UpdateLiveDriver()
        {
            if(bodySource==null||bodyRetargeter==null||avatar==null)return;
            bodyInvalidSeconds=bodySource.IsPoseValid()?0:bodyInvalidSeconds+Time.unscaledDeltaTime;
            bool useIk=bodyInvalidSeconds>.5f;
            if(!bodyRetargeter.enabled)bodyRetargeter.enabled=true;
            if(avatar.enabled!=useIk){avatar.enabled=useIk;if(useIk)avatar.ResetPlant();}
            if(useIk)RestoreAvatarScale();
        }
        // CharacterRetargeter hides its character (root scale 0) until a valid body pose arrives,
        // and only rescales it while it is applying poses. The IK avatar must stay visible.
        void RestoreAvatarScale()
        {
            if(bodyRetargeter!=null&&bodyRetargeter.transform.localScale!=avatarRootScale)bodyRetargeter.transform.localScale=avatarRootScale;
        }
        void Update()
        {
            if(IsPlayback){TickPlayback(Time.unscaledDeltaTime);return;}
            UpdateLiveDriver();
            if(paused)return;
            // Keep a recording running whenever one should be: after a pause (headset off, system menu,
            // screen capture) or a failed start, retry every couple of seconds instead of only once.
            if(autoRecord&&!userStopped&&!IsRecording&&calibration.IsCalibrated&&Time.unscaledTime>=nextAutoStart)
            {nextAutoStart=Time.unscaledTime+2;StartRecording();}
            if(!tracking.RecordToggle){toggleHeld=0;toggleConsumed=false;}
            else if(!toggleConsumed)toggleHeld+=Time.unscaledDeltaTime;
            if(toggleHeld>=1&&!toggleConsumed)
            {
                toggleConsumed=true;
                if(IsRecording){StopRecording();userStopped=true;}else if(calibration.IsCalibrated){userStopped=false;StartRecording();}
            }
        }
        bool userStopped;
        float nextAutoStart;
        // A new work run gets its own file: one run, one recording.
        public void RestartRecording()
        {
            if(!autoRecord||IsPlayback)return;
            StopRecording();userStopped=false;nextAutoStart=0;
            if(calibration.IsCalibrated&&!paused)StartRecording();
        }
        void LateUpdate()
        {
            if(!IsRecording||Time.unscaledTime<nextSample)return;
            nextSample=Mathf.Max(nextSample+1f/Rate,Time.unscaledTime);
            try
            {
                WriteFrame();FramesWritten++;
                if(Time.unscaledTime>=nextFlush){writer.Flush();nextFlush=Time.unscaledTime+1;}
            }
            catch(Exception e){LastError=e.Message;StopRecording();Debug.LogError("Session recording stopped: "+LastError);}
        }
        public bool StartRecording(string path=null)
        {
            if(IsPlayback||IsRecording||!calibration.IsCalibrated)return false;
            try
            {
                Directory.CreateDirectory(SessionDirectory);
                CurrentPath=path??Path.Combine(SessionDirectory,"Factory_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".fvr");
                pool.Initialize();capacity=pool.Parts.Length;estimator?.ResetSession();
                writer=new BinaryWriter(new FileStream(CurrentPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read,65536));
                writer.Write(Magic);writer.Write(Version);writer.Write(capacity);writer.Write(Rate);
                fileVersion=Version;
                FramesWritten=0;LastError=null;started=nextSample=Time.unscaledTime;nextFlush=started+1;return true;
            }
            catch(Exception e){LastError=e.Message;StopRecording();return false;}
        }
        public void StopRecording()
        {
            if(writer==null)return;
            Duration=Time.unscaledTime-started;
            try{writer.Flush();writer.Dispose();}catch(Exception e){LastError=e.Message;}finally{writer=null;}
        }
        void WriteFrame()
        {
            writer.Write(Time.unscaledTime-started);
            int flags=(tracking.HeadTracked?1:0)|(tracking.LeftTracked?2:0)|(tracking.RightTracked?4:0)|(tracking.LeftGrip?8:0)|(tracking.RightGrip?16:0);
            writer.Write(flags);
            Write(tracking.head);Write(tracking.leftHand);Write(tracking.rightHand);
            Write(calibration.BaselineHead);Write(calibration.Forward);
            for(int i=0;i<7;i++)writer.Write(estimator.Score(i));
            writer.Write(task.HeldWeight);writer.Write(task.CompletedLeft);writer.Write(task.CompletedRight);
            for(int i=0;i<capacity;i++)
            {
                var p=pool.Parts[i];writer.Write((byte)p.State);writer.Write((byte)p.RecordedKind);Write(p.transform);
            }
            bool bodyValid=bodySource!=null&&bodySource.CaptureLivePose(capturePose,captureTPose);
            writer.Write(bodyValid);
            for(int i=0;i<MovementBodyPoseStream.JointCount;i++)Write(capturePose[i]);
            for(int i=0;i<MovementBodyPoseStream.JointCount;i++)Write(captureTPose[i]);
            writer.Write(line!=null?line.Offset:0f);
            writer.Write(conveyor!=null?conveyor.CurrentSpeed:0f);
        }
        void Write(NativeTransform t){Write(t.Position);var q=t.Orientation;writer.Write(q.x);writer.Write(q.y);writer.Write(q.z);writer.Write(q.w);}
        void Write(Transform t){Write(t.position);var q=t.rotation;writer.Write(q.x);writer.Write(q.y);writer.Write(q.z);writer.Write(q.w);}
        void Write(Vector3 v){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
        Vector3 ReadVector()=>new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        Quaternion ReadRotation()=>new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        public bool BeginPlayback(string path)
        {
            StopRecording();ClosePlayback();userStopped=true;
            try
            {
                reader=new BinaryReader(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,65536));
                if(reader.ReadInt32()!=Magic)throw new InvalidDataException("Unsupported Factory session file");
                fileVersion=reader.ReadInt32();
                if(fileVersion<LegacyVersion||fileVersion>Version)throw new InvalidDataException("Unsupported Factory session version");
                capacity=reader.ReadInt32();int rate=reader.ReadInt32();pool.Initialize();
                // Sessions recorded with a smaller pool still replay; the spare parts stay hidden.
                if(capacity>pool.Parts.Length||capacity<1||capacity>16||rate!=Rate)throw new InvalidDataException("Session pool/version differs from this scene");
                for(int i=capacity;i<pool.Parts.Length;i++)pool.Parts[i].ApplyRecordedState(PartState.Pooled,0,Vector3.zero,Quaternion.identity);
                frameBytes=156+capacity*30+(fileVersion>=2?1+MovementBodyPoseStream.JointCount*TransformBytes*2:0)+(fileVersion>=3?4:0)+(fileVersion>=4?4:0);
                if(reader.BaseStream.Length<HeaderBytes+frameBytes*2)throw new InvalidDataException("Session needs at least two complete frames");
                long completeFrames=(reader.BaseStream.Length-HeaderBytes)/frameBytes;
                reader.BaseStream.Position=HeaderBytes+(completeFrames-1)*frameBytes;Duration=reader.ReadSingle();
                reader.BaseStream.Position=HeaderBytes;a=new Frame(capacity);b=new Frame(capacity);ReadFrame(a);ReadFrame(b);
                CurrentPath=path;LastError=null;PlaybackPaused=false;playbackClock=a.time;
                conveyor.enabled=false;task.enabled=false;estimator.enabled=false;calibration.enabled=false;
                ConfigurePlaybackDriver(fileVersion>=2);avatar.ResetPlant();ApplyFrame(0);return true;
            }
            catch(Exception e){LastError=e.Message;ClosePlayback();return false;}
        }
        bool ReadFrame(Frame f)
        {
            if(reader.BaseStream.Position+frameBytes>reader.BaseStream.Length)return false;
            f.time=reader.ReadSingle();f.flags=reader.ReadInt32();
            f.hp=ReadVector();f.hq=ReadRotation();f.lp=ReadVector();f.lq=ReadRotation();f.rp=ReadVector();f.rq=ReadRotation();
            f.baseline=ReadVector();f.forward=ReadVector();for(int i=0;i<7;i++)f.scores[i]=reader.ReadSingle();
            f.weight=reader.ReadSingle();f.left=reader.ReadInt32();f.right=reader.ReadInt32();
            for(int i=0;i<capacity;i++){f.state[i]=reader.ReadByte();f.kind[i]=reader.ReadByte();f.pp[i]=ReadVector();f.pq[i]=ReadRotation();}
            if(fileVersion>=2)
            {
                f.bodyValid=reader.ReadBoolean();
                for(int i=0;i<MovementBodyPoseStream.JointCount;i++)f.bodyPose[i]=ReadNativeTransform();
                for(int i=0;i<MovementBodyPoseStream.JointCount;i++)f.bodyTPose[i]=ReadNativeTransform();
            }
            f.lineOffset=fileVersion>=3?reader.ReadSingle():0;
            f.beltSpeed=fileVersion>=4?reader.ReadSingle():0;
            return true;
        }
        NativeTransform ReadNativeTransform(){Vector3 p=ReadVector();return new NativeTransform(ReadRotation(),p);}
        public void RestartPlayback()
        {
            if(!IsPlayback)return;reader.BaseStream.Position=HeaderBytes;ReadFrame(a);ReadFrame(b);playbackClock=a.time;PlaybackPaused=false;avatar.ResetPlant();ApplyFrame(0);
        }
        public void TickPlayback(float dt)
        {
            if(!IsPlayback)return;
            if(!PlaybackPaused)playbackClock=Mathf.Min(Duration,playbackClock+dt);
            while(playbackClock>b.time)
            {
                if(reader.BaseStream.Position+frameBytes>reader.BaseStream.Length){PlaybackPaused=true;break;}
                var swap=a;a=b;b=swap;ReadFrame(b);
            }
            ApplyFrame(Mathf.InverseLerp(a.time,b.time,playbackClock));
            if(playbackClock>=Duration)PlaybackPaused=true;
        }
        void ApplyFrame(float t)
        {
            Frame discrete=t>=1?b:a;
            tracking.ApplyRecordedPose(Vector3.Lerp(a.hp,b.hp,t),Quaternion.Slerp(a.hq,b.hq,t),Vector3.Lerp(a.lp,b.lp,t),Quaternion.Slerp(a.lq,b.lq,t),Vector3.Lerp(a.rp,b.rp,t),Quaternion.Slerp(a.rq,b.rq,t),discrete.flags);
            calibration.ApplyRecordedCalibration(discrete.baseline,discrete.forward);
            for(int i=0;i<7;i++)mixedScores[i]=Mathf.Lerp(a.scores[i],b.scores[i],t);
            estimator.ApplyRecordedScores(mixedScores);task.ApplyRecordedTask(discrete.weight,discrete.left,discrete.right);
            for(int i=0;i<capacity;i++)
            {
                // Never interpolate a pooled item across the factory when its pool slot is reused.
                bool continuous=a.state[i]!=0&&b.state[i]!=0&&a.kind[i]==b.kind[i]&&Vector3.Distance(a.pp[i],b.pp[i])<1;
                pool.Parts[i].ApplyRecordedState((PartState)discrete.state[i],discrete.kind[i],continuous?Vector3.Lerp(a.pp[i],b.pp[i],t):discrete.pp[i],continuous?Quaternion.Slerp(a.pq[i],b.pq[i],t):discrete.pq[i]);
            }
            task.UpdateSpectatorVisibility();
            if(line!=null)line.SetOffsetImmediate(discrete.lineOffset);
            PlaybackBeltSpeed=Mathf.Lerp(a.beltSpeed,b.beltSpeed,t);
            if(conveyor!=null)conveyor.ScrollBelt(PlaybackBeltSpeed,Time.unscaledDeltaTime);
            if(fileVersion>=2&&bodySource!=null)
            {
                bool bodyAvailable=a.bodyValid||b.bodyValid;
                ConfigurePlaybackDriver(bodyAvailable);
                for(int i=0;i<MovementBodyPoseStream.JointCount;i++)
                {
                    mixedPose[i]=Blend(a.bodyPose[i],b.bodyPose[i],t);
                    mixedTPose[i]=Blend(a.bodyTPose[i],b.bodyTPose[i],t);
                }
                bodySource.SetReplayPose(mixedPose,mixedTPose,bodyAvailable);
            }
        }
        static NativeTransform Blend(NativeTransform x,NativeTransform y,float t)=>new NativeTransform(Quaternion.Slerp(x.Orientation,y.Orientation,t),Vector3.Lerp(x.Position,y.Position,t));
        void ConfigurePlaybackDriver(bool hasBody)
        {
            bool movement=hasBody&&bodySource!=null&&bodyRetargeter!=null;
            if(bodySource!=null&&movement!=movementPlaybackActive){if(movement)bodySource.BeginReplay();else bodySource.EndReplay();}
            movementPlaybackActive=movement;
            if(bodyRetargeter!=null)bodyRetargeter.enabled=movement;
            if(avatar!=null)avatar.enabled=!movement;
            if(!movement)RestoreAvatarScale();
        }
        void ClosePlayback(){reader?.Dispose();reader=null;if(bodySource!=null)bodySource.EndReplay();movementPlaybackActive=false;bodyInvalidSeconds=1;if(bodyRetargeter!=null)bodyRetargeter.enabled=true;if(avatar!=null)avatar.enabled=true;}
        void OnApplicationPause(bool value){paused=value;if(value)StopRecording();else nextAutoStart=0;}
        // Regaining focus also means the app is running again, even if the resume callback was missed.
        void OnApplicationFocus(bool focused){if(focused&&paused){paused=false;nextAutoStart=0;}}
        void OnDisable(){StopRecording();ClosePlayback();}
        void OnApplicationQuit(){StopRecording();ClosePlayback();}
    }
}
