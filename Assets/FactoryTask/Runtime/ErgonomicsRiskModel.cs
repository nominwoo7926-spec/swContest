using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FactoryTask
{
    public sealed class ErgonomicsRiskModel : MonoBehaviour
    {
        [Serializable] sealed class Model
        {
            public int version=0;public string[] features=Array.Empty<string>();
            public float[] mean=Array.Empty<float>(),std=Array.Empty<float>(),weights=Array.Empty<float>();
            public float bias=0,validationAccuracy=0;
        }
        Model model;
        public bool IsLoaded=>model!=null;
        public float ValidationAccuracy=>model==null?0:model.validationAccuracy;
        IEnumerator Start()
        {
            string path=Path.Combine(Application.streamingAssetsPath,"ErgonomicsModel.json");
            string json=null;
            if(path.Contains("://")||path.StartsWith("jar:"))
            {
                using(var request=UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    if(request.result==UnityWebRequest.Result.Success)json=request.downloadHandler.text;
                }
            }
            else if(File.Exists(path))json=File.ReadAllText(path);
            if(string.IsNullOrEmpty(json))yield break;
            try
            {
                var candidate=JsonUtility.FromJson<Model>(json);
                int count=candidate==null?0:candidate.version==1?8:candidate.version==2?9:candidate.version==3?11:0;
                if(count>0&&candidate.weights!=null&&candidate.weights.Length==count&&candidate.mean!=null&&candidate.mean.Length==count&&
                   candidate.std!=null&&candidate.std.Length==count)model=candidate;
            }catch(Exception e){Debug.LogWarning("Ergonomics model ignored: "+e.Message);}
        }
        public bool TryPredict(float[] features,out float risk)
        {
            risk=0;if(model==null||features==null||features.Length<model.weights.Length)return false;
            float z=model.bias;for(int i=0;i<model.weights.Length;i++)z+=model.weights[i]*(features[i]-model.mean[i])/Mathf.Max(.0001f,model.std[i]);
            risk=1/(1+Mathf.Exp(-Mathf.Clamp(z,-20,20)));return true;
        }
    }
}
