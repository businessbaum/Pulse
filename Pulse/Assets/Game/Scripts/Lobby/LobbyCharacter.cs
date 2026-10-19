using UnityEngine;

namespace Pulse
{
    // The small robot the player walks around the lobby with. It walks where the stick points,
    // seen from the camera, and swings its arms and legs as it goes.
    public sealed class LobbyCharacter : MonoBehaviour
    {
        const float HipHeight = 0.36f, HipSpacing = 0.08f, ShoulderHeight = 0.7f, ShoulderSpacing = 0.24f;
        const float StepsPerMetre = 2.6f, LegSwing = 38f, ArmSwing = 26f, Bob = 0.03f, TurnSpeed = 720f, Size = 0.35f;

        Transform body, legLeft, legRight, armLeft, armRight;
        float walkSpeed, speed, step, heldYaw;

        // Set while a screen is open: the character ignores input and stands still.
        public bool Frozen { get; set; }
        // Where it may walk and what stands in its way.
        public LobbyWalk Walk { get; set; }
        // How far the camera is turned at the moment. "Up" on the stick is away from the camera.
        public float ViewYaw { get; set; }

        public void Build(LobbyDef def, GameConfig config)
        {
            walkSpeed = def.walkSpeed;
            body = Part("Body", def.character, config, Vector3.zero);
            legLeft = Part("Left leg", def.characterLeg, config, new Vector3(-HipSpacing, HipHeight, 0f));
            legRight = Part("Right leg", def.characterLeg, config, new Vector3(HipSpacing, HipHeight, 0f));
            armLeft = Part("Left arm", def.characterArm, config, new Vector3(-ShoulderSpacing, ShoulderHeight, 0f));
            armRight = Part("Right arm", def.characterArm, config, new Vector3(ShoulderSpacing, ShoulderHeight, 0f));
        }

        Transform Part(string label, Mesh mesh, GameConfig config, Vector3 localPos)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            if (mesh == null) return go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = Slots.For(mesh, config.body, config.part, config.dark, config.pulse);
            return go.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = Frozen ? Vector2.zero : Vector2.ClampMagnitude(Rig.ReadMove(), 1f);
            // The camera turns when the character walks into some rooms. A stick that is being
            // held keeps meaning what it meant when it was pushed, so the character does not swerve
            // while the picture turns; the new direction counts from the next push.
            if (input.sqrMagnitude < 0.02f) heldYaw = ViewYaw;
            var wanted = Quaternion.Euler(0f, heldYaw, 0f) * new Vector3(input.x, 0f, input.y);
            speed = Mathf.MoveTowards(speed, wanted.magnitude * walkSpeed, walkSpeed * 8f * dt);

            if (wanted.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wanted), TurnSpeed * dt);

            Vector3 pos = transform.position + transform.forward * (speed * dt);
            transform.position = Walk != null ? Walk.Resolve(pos, Size) : new Vector3(pos.x, 0f, pos.z);

            // Arms swing against the legs; everything settles when the character stands still.
            step += speed * StepsPerMetre * dt;
            float swing = Mathf.Sin(step * Mathf.PI) * Mathf.Clamp01(speed / walkSpeed);
            legLeft.localRotation = Quaternion.Euler(swing * LegSwing, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(-swing * LegSwing, 0f, 0f);
            armLeft.localRotation = Quaternion.Euler(-swing * ArmSwing, 0f, 0f);
            armRight.localRotation = Quaternion.Euler(swing * ArmSwing, 0f, 0f);
            body.localPosition = Vector3.up * (Mathf.Abs(swing) * Bob);
        }
    }
}
