using System;
using System.IO;
using UnityEngine;

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
        const int Magic = 0x46565232, Version = 1, HeaderBytes = 16;
        BinaryWriter writer;
        BinaryReader reader;
        float started, nextSample, nextFlush, toggleHeld, playbackClock;
        bool toggleConsumed, autoStarted, paused;
        int capacity, frameBytes;
        Frame a, b;
        readonly float[] mixedScores = new float[7];
        public static string SessionDirectory => Path.Combine(Application.persistentDataPath,"FactorySessions");

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
            public Frame(int count){pp=new Vector3[count];pq=new Quaternion[count];state=new byte[count];kind=new byte[count];}
        }
        void Update()
        {
            if(IsPlayback){TickPlayback(Time.unscaledDeltaTime);return;}
            if(paused)return;
            if(autoRecord&&!autoStarted&&calibration.IsCalibrated){autoStarted=true;StartRecording();}
            if(!tracking.RecordToggle){toggleHeld=0;toggleConsumed=false;}
            else if(!toggleConsumed)toggleHeld+=Time.unscaledDeltaTime;
            if(toggleHeld>=1&&!toggleConsumed)
            {
                toggleConsumed=true;autoStarted=true;
                if(IsRecording)StopRecording();else if(calibration.IsCalibrated)StartRecording();
            }
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
                var p=pool.Parts[i];writer.Write((byte)p.State);writer.Write((byte)(p.weightKg<=1?0:p.weightKg<=3?1:2));Write(p.transform);
            }
        }
        void Write(Transform t){Write(t.position);var q=t.rotation;writer.Write(q.x);writer.Write(q.y);writer.Write(q.z);writer.Write(q.w);}
        void Write(Vector3 v){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
        Vector3 ReadVector()=>new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        Quaternion ReadRotation()=>new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        public bool BeginPlayback(string path)
        {
            StopRecording();ClosePlayback();autoStarted=true;
            try
            {
                reader=new BinaryReader(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,65536));
                if(reader.ReadInt32()!=Magic||reader.ReadInt32()!=Version)throw new InvalidDataException("Unsupported Factory session file");
                capacity=reader.ReadInt32();int rate=reader.ReadInt32();pool.Initialize();
                if(capacity!=pool.Parts.Length||capacity<1||capacity>16||rate!=Rate)throw new InvalidDataException("Session pool/version differs from this scene");
                frameBytes=156+capacity*30;
                if(reader.BaseStream.Length<HeaderBytes+frameBytes*2)throw new InvalidDataException("Session needs at least two complete frames");
                long completeFrames=(reader.BaseStream.Length-HeaderBytes)/frameBytes;
                reader.BaseStream.Position=HeaderBytes+(completeFrames-1)*frameBytes;Duration=reader.ReadSingle();
                reader.BaseStream.Position=HeaderBytes;a=new Frame(capacity);b=new Frame(capacity);ReadFrame(a);ReadFrame(b);
                CurrentPath=path;LastError=null;PlaybackPaused=false;playbackClock=a.time;
                conveyor.enabled=false;task.enabled=false;estimator.enabled=false;calibration.enabled=false;
                avatar.ResetPlant();ApplyFrame(0);return true;
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
            return true;
        }
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
        }
        void ClosePlayback(){reader?.Dispose();reader=null;}
        void OnApplicationPause(bool value){paused=value;if(value)StopRecording();else if(autoRecord&&!IsPlayback)autoStarted=false;}
        void OnDisable(){StopRecording();ClosePlayback();}
        void OnApplicationQuit(){StopRecording();ClosePlayback();}
    }
}
