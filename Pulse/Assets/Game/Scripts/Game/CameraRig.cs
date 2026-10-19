using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse
{
    // Fixed-angle camera that follows the machine from above.
    public sealed class CameraRig : MonoBehaviour
    {
        // Where the camera sits during a run, relative to the machine.
        public static readonly Vector3 RunOffset = new Vector3(0f, 31f, -20f);
        // The iris: closed to IrisTight it shows the machine and nothing else; IrisWide is past the
        // edge of the picture.
        public const float IrisTight = 2.6f, IrisWide = 40f;
        const int IrisPoints = 64;
        const float IrisDepth = 1f, IrisOuter = 60f, IrisHeight = 0.8f;

        public Vector3 offset = RunOffset;
        public float follow = 6f;
        // What to follow. The machine of the current run when left empty.
        public Transform target;
        // 1 is the normal distance. Scripted scenes move the camera in and out with this.
        [System.NonSerialized] public float zoom = 1f;
        // How far the camera is turned round what it follows, in degrees. The rooms of the lobby
        // that lie behind its far wall are looked into from the side.
        [System.NonSerialized] public float yaw;
        // From 0 to 1: how firmly the camera is held exactly on its target instead of trailing behind.
        // Scripted scenes pin it, so that two places can be joined without the picture jumping.
        [System.NonSerialized] public float pin;
        // How far the picture is open around what the camera follows, in metres. Infinity: all of it.
        // Zero: nothing. Everything outside is covered in the colour of the dark around the world.
        // Closing it around the machine and opening it again somewhere else joins two places without
        // a visible cut, because the machine never leaves the picture.
        [System.NonSerialized] public float iris = float.PositiveInfinity;

        readonly Vector3[] irisVertices = new Vector3[IrisPoints * 2];
        Camera view;
        Mesh irisMesh;
        Material irisMaterial;
        float shake;
        bool irisBuilt;

        // A short rattle, for hits and collisions.
        public void Shake(float strength) => shake = Mathf.Max(shake, strength * Options.Shake);

        // Jumps to the machine at once, for teleports.
        public void Snap()
        {
            var followed = Followed;
            if (followed != null) transform.position = followed.position + Turned * (zoom * Options.Distance);
        }

        // Where the camera stands, seen from what it follows.
        Vector3 Turned => yaw == 0f ? offset : Quaternion.Euler(0f, yaw, 0f) * offset;

        Transform Followed => target != null ? target : Combat.Rig != null ? Combat.Rig.transform : null;

        void LateUpdate()
        {
            var followed = Followed;
            if (followed == null) return;
            Vector3 turned = Turned, wanted = followed.position + turned * (zoom * Options.Distance);
            Vector3 trailing = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime));
            transform.position = Vector3.Lerp(trailing, wanted, pin);
            transform.rotation = Quaternion.LookRotation(-turned);
            if (shake >= 0.002f)
            {
                transform.position += Random.insideUnitSphere * shake;
                shake = Mathf.Lerp(shake, 0f, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
            if (!float.IsPositiveInfinity(iris)) DrawIris(followed.position + Vector3.up * IrisHeight);
        }

        // The cover is a sheet with a round hole, held one metre in front of the lens. It is part of
        // the scene, not of the interface, so it gets the same grading as the dark it has to match.
        void DrawIris(Vector3 around)
        {
            if (view == null) view = GetComponent<Camera>();
            if (view == null || Combat.Config == null) return;
            if (irisMesh == null)
            {
                irisMesh = new Mesh { name = "Iris" };
                irisMesh.MarkDynamic();
                irisMaterial = new Material(Combat.Config.particle) { color = Tokens.Soot };
            }

            // Where the point sits on that sheet, and how large a metre around it is there.
            Vector3 local = transform.InverseTransformPoint(around);
            float depth = Mathf.Max(local.z, 0.01f), scale = IrisDepth / depth;
            var center = new Vector3(local.x * scale, local.y * scale, IrisDepth);
            float hole = Mathf.Max(0f, iris) * scale;
            // Past the corners of the picture there is nothing to cover.
            float halfHeight = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * IrisDepth;
            if (hole > halfHeight * (1f + view.aspect) + new Vector2(center.x, center.y).magnitude) return;

            for (int p = 0; p < IrisPoints; p++)
            {
                float angle = p * Mathf.PI * 2f / IrisPoints;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                irisVertices[p * 2] = center + dir * hole;
                irisVertices[p * 2 + 1] = center + dir * IrisOuter;
            }
            irisMesh.vertices = irisVertices;
            if (!irisBuilt)
            {
                irisBuilt = true;
                // Both sides of every face, so it shows whichever way the shader culls.
                var triangles = new int[IrisPoints * 12];
                for (int p = 0; p < IrisPoints; p++)
                {
                    int a = p * 2, b = a + 1, c = (p + 1) % IrisPoints * 2, d = c + 1, t = p * 12;
                    triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = d;
                    triangles[t + 3] = a; triangles[t + 4] = d; triangles[t + 5] = c;
                    triangles[t + 6] = a; triangles[t + 7] = d; triangles[t + 8] = b;
                    triangles[t + 9] = a; triangles[t + 10] = c; triangles[t + 11] = d;
                }
                irisMesh.triangles = triangles;
            }
            irisMesh.bounds = new Bounds(Vector3.forward, Vector3.one * 200f);
            Graphics.RenderMesh(new RenderParams(irisMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
                irisMesh, 0, transform.localToWorldMatrix);
        }
    }
}
