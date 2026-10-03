using System;
using System.IO;
using UnityEngine;

namespace FactoryTask
{
    // Locally stored, pseudonymous setup values. These are not clinical measurements.
    public sealed class WorkerProfile : MonoBehaviour
    {
        [Serializable] sealed class SavedProfile
        {
            public int version=1;
            public string id;
            public int heightCm;
            public int reachCm;
            public bool heightEntered;
            public bool reachEntered;
        }

        public UserCalibration calibration;
        public string Id { get; private set; }
        public int HeightCm { get; private set; }
        public int ReachCm { get; private set; }
        public bool HeightEntered { get; private set; }
        public bool ReachEntered { get; private set; }
        public bool Complete => HeightEntered && ReachEntered;
        public static string FilePath => Path.Combine(ManufacturingDataCollector.DataDirectory,"worker_profile_v1.json");

        void Awake()
        {
            try
            {
                if(File.Exists(FilePath))
                {
                    var saved=JsonUtility.FromJson<SavedProfile>(File.ReadAllText(FilePath));
                    if(saved!=null&&saved.version==1&&!string.IsNullOrEmpty(saved.id))
                    {
                        Id=saved.id;HeightCm=Mathf.Clamp(saved.heightCm,130,210);
                        ReachCm=Mathf.Clamp(saved.reachCm,40,100);
                        HeightEntered=saved.heightEntered;ReachEntered=saved.reachEntered;
                    }
                }
            }
            catch(Exception error){Debug.LogWarning("Worker profile ignored: "+error.Message);}
            if(string.IsNullOrEmpty(Id))Id=Guid.NewGuid().ToString("N").Substring(0,12);
        }

        public void SeedFromCalibration()
        {
            if(calibration==null||!calibration.IsCalibrated)return;
            if(HeightCm==0)
                HeightCm=Mathf.Clamp(Mathf.RoundToInt((calibration.BaselineHead.y-calibration.standingPoint.position.y+.12f)*100),130,210);
            if(ReachCm==0)ReachCm=Mathf.Clamp(Mathf.RoundToInt(HeightCm*.45f),40,100);
        }

        public void SetHeight(int centimetres)
        {
            HeightCm=Mathf.Clamp(centimetres,130,210);HeightEntered=true;
            if(!ReachEntered)ReachCm=Mathf.Clamp(Mathf.RoundToInt(HeightCm*.45f),40,100);
            Save();
        }

        public void SetReach(int centimetres)
        {
            ReachCm=Mathf.Clamp(centimetres,40,100);ReachEntered=true;Save();
        }

        void Save()
        {
            try
            {
                Directory.CreateDirectory(ManufacturingDataCollector.DataDirectory);
                File.WriteAllText(FilePath,JsonUtility.ToJson(new SavedProfile
                {
                    id=Id,heightCm=HeightCm,reachCm=ReachCm,
                    heightEntered=HeightEntered,reachEntered=ReachEntered
                },true));
            }
            catch(Exception error){Debug.LogWarning("Worker profile save failed: "+error.Message);}
        }
    }
}
