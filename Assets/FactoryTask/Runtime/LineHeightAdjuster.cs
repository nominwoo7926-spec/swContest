using UnityEngine;

namespace FactoryTask
{
    // Raises or lowers the whole work line together (belt, end drum, pickup table and bin), the way a
    // height-adjustable conveyor station would, so parts still pass from the belt onto the table.
    // Legs stretch to stay on the floor; floor plates and adjusters stay where they are.
    public sealed class LineHeightAdjuster : MonoBehaviour
    {
        [Tooltip("Moved up/down with the line.")]
        public Transform[] moved;
        [Tooltip("Vertical legs: bottom stays on the floor, top follows the line.")]
        public Transform[] stretched;
        [Tooltip("Children of moved objects that must stay on the floor (plates, adjusters).")]
        public Transform[] fixedToFloor;
        public Collider tableTop;
        public float minOffset = -.3f, maxOffset = .3f;
        [Tooltip("Lifting speed in m/s, like an electric height-adjustable workstation.")]
        public float speed = .08f;

        public float Offset { get; private set; }
        public float TargetOffset { get; private set; }
        public bool Moving => !Mathf.Approximately(Offset, TargetOffset);
        public float TableTopHeight => tableTop != null ? tableTop.bounds.max.y : 0;
        [Tooltip("Table top height with no offset, stored by scene setup. Collider bounds read in Awake can still be empty, which once made the line jump to its limit.")]
        public float baseTableTop;
        // Table height with no offset applied.
        public float BaseTableTopHeight => baseTableTop > 0 ? baseTableTop : capturedTableTop;
        float capturedTableTop;

        Vector3[] movedBase, fixedBase, stretchBase, stretchScale;
        float[] stretchBottom, stretchTop;
        bool captured;

        void Awake() { Capture(); }
        void Capture()
        {
            if (captured) return;
            captured = true;
            movedBase = new Vector3[moved.Length];
            for (int i = 0; i < moved.Length; i++) movedBase[i] = moved[i].position;
            fixedBase = new Vector3[fixedToFloor.Length];
            for (int i = 0; i < fixedToFloor.Length; i++) fixedBase[i] = fixedToFloor[i].position;
            stretchBase = new Vector3[stretched.Length]; stretchScale = new Vector3[stretched.Length];
            stretchBottom = new float[stretched.Length]; stretchTop = new float[stretched.Length];
            for (int i = 0; i < stretched.Length; i++)
            {
                var t = stretched[i]; stretchBase[i] = t.position; stretchScale[i] = t.localScale;
                var r = t.GetComponent<Renderer>();
                Bounds b = r != null ? r.bounds : new Bounds(t.position, Vector3.zero);
                stretchBottom[i] = b.min.y; stretchTop[i] = b.max.y;
            }
            capturedTableTop = TableTopHeight;
        }

        // Ease to a new height at the station's lifting speed.
        public void SetTargetOffset(float offset) { Capture(); TargetOffset = Mathf.Clamp(offset, minOffset, maxOffset); }
        // Jump straight to a height (new run, or replaying a recording).
        public void SetOffsetImmediate(float offset) { SetTargetOffset(offset); Apply(TargetOffset); }

        void Update()
        {
            if (!Moving) return;
            Apply(Mathf.MoveTowards(Offset, TargetOffset, speed * Time.deltaTime));
        }

        void Apply(float offset)
        {
            Capture();
            Offset = offset;
            for (int i = 0; i < moved.Length; i++) moved[i].position = movedBase[i] + Vector3.up * offset;
            for (int i = 0; i < stretched.Length; i++)
            {
                var t = stretched[i];
                float height = stretchTop[i] - stretchBottom[i];
                if (height <= .001f) continue;
                float factor = Mathf.Max(.05f, (height + offset) / height);
                // Scale about the floor: the pivot's height above the bottom grows with the leg.
                Vector3 p = stretchBase[i];
                p.y = stretchBottom[i] + (stretchBase[i].y - stretchBottom[i]) * factor;
                t.position = p;
                var s = stretchScale[i]; s.y *= factor; t.localScale = s;
            }
            for (int i = 0; i < fixedToFloor.Length; i++) fixedToFloor[i].position = fixedBase[i];
            Physics.SyncTransforms();
        }
    }
}
