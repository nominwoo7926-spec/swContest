using UnityEngine;

namespace ErgoContest
{
    public abstract class ErgoInputProvider : MonoBehaviour
    {
        public abstract ErgoInputFrame CurrentFrame { get; }
        public abstract Transform HandTarget { get; }
        public abstract Transform HeadTarget { get; }
        public abstract string InputModeLabel { get; }
    }
}
