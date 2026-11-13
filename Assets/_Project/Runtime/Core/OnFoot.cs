// The player on foot (E-01, E-02): a capsule that walks on the chunk colliders the car drives on, and the person who is drawn there. It moves where the camera looks, turns to
// face the way it goes, runs with Sprint, and falls when the ground goes. Nothing here is special to one model: Who says who you are. It jumps, crouches (a lower capsule and
// the crouching clips), faces where you aim, and stands, walks and runs with the stance of the gun in hand (2026-11-10).
using UnityEngine;

namespace Dad.Core
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class OnFoot : MonoBehaviour
    {
        /// <summary>Who you are: the one of the twenty people you chose on the start screen (Cast, owner, 2026-11-13).</summary>
        public static string Who => Cast.You;
        public const float Height = 1.75f, Radius = 0.3f, Gravity = 18f, TurnRate = 720f;
        /// <summary>Metres a second up at the jump (about 0.6 m high), how tall the capsule is crouched, how fast a crouch goes, and how long in the air before falling shows.</summary>
        public const float JumpSpeed = 4.6f, CrouchHeight = 1.15f, CrouchPace = 0.65f, FallsAfter = 0.45f;

        public GameInput Input;
        public Person Person { get; private set; }
        /// <summary>What the controls say, for tests: x right, y forward.</summary>
        public Vector2? Override;
        public bool Run;
        public Transform View;
        /// <summary>Degrees: face this way rather than the way you go (while aiming).</summary>
        public float? Facing;
        /// <summary>Walking pace at most (while aiming).</summary>
        public bool Slow;
        public bool Crouched { get; private set; }
        public bool Grounded => body.isGrounded;
        /// <summary>The gun in hand, whose stance the body stands and walks with; null for none.</summary>
        public Armed.Kind Gun { get => gun; set { gun = value; Stance(); } }

        /// <summary>Not walking: in a clip, or left behind by the free camera. The capsule is off, so nothing moves the body and nothing hits it.</summary>
        public bool Held
        {
            get => !body.enabled;
            set { body.enabled = !value; enabled = !value; fall = 0; Person.Speed = 0; Leaping = false; lift = 0; Person.Body.transform.localPosition = Vector3.zero; }
        }

        CharacterController body;
        float fall, height, inAir;
        bool sprintOn, falling;
        float jumpedAt = -99, landUntil;
        /// <summary>Seconds in the air after which coming down shows the landing's clip.</summary>
        public const float HardLanding = 0.6f;
        /// <summary>Seconds a jump is off flat ground.</summary>
        public static float Air => 2 * JumpSpeed / Gravity;
        // Where the jump clips leave the ground and come down, in seconds (measured, AnimationLayerTests.JumpClipsLeaveTheGround, 2026-11-12): standing, the feet leave at 0.63
        // after a crouch and are down at 1.07; running, they leave at once and are down at 0.67. A standing jump starts late in its crouch, so it goes up when you press.
        const float StandFrom = 0.50f, StandDown = 1.07f, RunDown = 0.67f;
        /// <summary>In a jump's clip: from the jump until it lands, or falling's clip takes over.</summary>
        public bool Leaping { get; private set; }
        float standHips = -1, lift;

        /// <summary>Up off the ground with the clip of a jump, standing or running, its time in the air stretched to the jump's. Falling's clip if there is neither.</summary>
        public void Jump()
        {
            fall = -JumpSpeed;
            jumpedAt = Time.time;
            falling = true;
            bool running = Person.Speed > (Person.WalkSpeed + Person.RunSpeed) / 2;
            float length = running ? Person.Play("run-jump", 0.08f, 0, RunDown / Air) : Person.Play("jump", 0.1f, StandFrom, (StandDown - StandFrom) / Air);
            Leaping = length > 0;
            if (!Leaping) Person.Play("fall", 0.15f);
        }
        Armed.Kind gun;

        /// <summary>The player on foot: who you chose (Cast) unless another person is named, dressed as the street dresses them. The capsule is as tall as the person is.</summary>
        public static OnFoot Spawn(Vector3 at, float heading, GameInput input, Transform view, string who = null)
        {
            who ??= Who;
            var p = Person.Spawn(who, at, heading);
            if (p == null) return null;
            NpcCrowd.Dress(p, new NpcRecord { id = "PLAYER", model = who, sex = Cast.IsWoman(who) ? "female" : "male" }); // cut-out hair and lashes, eye shells, as in the crowd
            Hitbox.Give(p.Body); // shots meet your parts, not your capsule; your own shots leave them out (Hitbox.Cast)
            p.Directional = true; // walking and running sideways and back with their own clips, not turned to face the way you go
            var go = p.gameObject;
            go.layer = WorldLayers.Player;
            float height = Height, top = float.MinValue;
            foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>()) top = Mathf.Max(top, r.bounds.max.y);
            if (top > at.y) height = Mathf.Clamp(top - at.y, 1.4f, 2.0f);
            var c = go.AddComponent<CharacterController>();
            c.height = height;
            c.radius = Radius;
            c.center = new Vector3(0, height / 2, 0);
            c.stepOffset = 0.35f;
            c.slopeLimit = 50;
            c.skinWidth = 0.03f;
            c.minMoveDistance = 0; // At a high frame rate a walking step is under the default, and the capsule would stand still.
            var f = go.AddComponent<OnFoot>();
            f.height = height;
            f.Person = p;
            f.Input = input;
            f.View = view;
            return f;
        }

        void Awake() => body = GetComponent<CharacterController>();

        /// <summary>Down to a crouch or up again, if there is room to stand.</summary>
        public void Crouch(bool down)
        {
            if (down == Crouched) return;
            if (!down && Physics.SphereCast(transform.position + Vector3.up * (CrouchHeight - Radius), Radius * 0.9f, Vector3.up, out _, height - CrouchHeight, WorldLayers.Solid, QueryTriggerInteraction.Ignore)) return;
            Crouched = down;
            float h = down ? CrouchHeight : height;
            body.height = h;
            body.center = new Vector3(0, h / 2, 0);
            Stance();
        }

        // The clips to stand, walk and run with: crouching, the gun's, or the person's own.
        void Stance()
        {
            if (Person == null) return;
            if (Crouched)
            {
                Person.Use(0, gun == null ? "crouch-idle" : gun.Pistol ? "pistol-kneel" : "crouch-aim");
                Person.Use(1, "crouch-walk");
                Person.Use(2, "crouch-walk");
            }
            else
            {
                Person.Use(0, gun?.Idle);
                Person.Use(1, gun?.Walk);
                Person.Use(2, gun != null && !gun.Pistol ? "rifle-run" : null);
            }
        }

        // A jump clip lifts the hips as the jump does, and the capsule is already lifting the body: the clip's rise above standing is taken off, so the body goes as high
        // as the jump and no higher. Standing, the hips' height is kept as the mark.
        void LateUpdate()
        {
            var figure = Person.Body.transform;
            var hips = Person.Body.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            float h = figure.InverseTransformPoint(hips.position).y;
            if (!Person.Acting && body.isGrounded && Person.Speed < 0.1f) standHips = standHips < 0 ? h : Mathf.Lerp(standHips, h, 0.1f);
            float want = Leaping && standHips > 0 ? -Mathf.Max(0, h - standHips) * Person.ActWeight : 0;
            lift = Leaping ? want : Mathf.MoveTowards(lift, 0, Time.deltaTime * 2);
            figure.localPosition = new Vector3(0, lift, 0);
        }

        void Update()
        {
            var move = Override ?? (Input != null ? Input.Move.ReadValue<Vector2>() : Vector2.zero);
            bool sprint = Run;
            if (Input != null)
            {
                if (GameSettings.Current.SprintToggle)
                {
                    if (Input.Sprint.WasPressedThisFrame()) sprintOn = !sprintOn;
                    if (move.sqrMagnitude < 0.01f) sprintOn = false;
                    sprint |= sprintOn;
                }
                else sprint |= Input.Sprint.IsPressed();
                if (Input.Crouch.WasPressedThisFrame()) Crouch(!Crouched);
                if (Input.Jump.WasPressedThisFrame() && body.isGrounded)
                {
                    if (Crouched) Crouch(false);
                    else Jump();
                }
            }
            if (sprint && Crouched && !Slow) Crouch(false); // sprinting stands you up
            if (Crouched || Slow) sprint = false;
            float yaw = View != null ? View.eulerAngles.y : transform.eulerAngles.y;
            var wish = Quaternion.Euler(0, yaw, 0) * new Vector3(move.x, 0, move.y);
            float amount = Mathf.Min(1, wish.magnitude);
            float speed = amount * (sprint ? Person.RunSpeed : Person.WalkSpeed) * (Crouched ? CrouchPace : 1);
            float heading = transform.eulerAngles.y;
            if (Facing.HasValue) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, Facing.Value, 0), TurnRate * Time.deltaTime);
            else if (amount > 0.05f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wish), TurnRate * Time.deltaTime);
            // turning on the spot steps the feet round; facing one way and going the other (aiming) walks backwards
            Person.Turning = Time.deltaTime > 0 ? Mathf.Abs(Mathf.DeltaAngle(heading, transform.eulerAngles.y)) / Time.deltaTime : 0;
            Person.Backward = amount > 0.05f && Vector3.Dot(wish, transform.forward) < -0.35f * wish.magnitude;
            if (amount > 0.05f) Person.Going = Vector3.SignedAngle(transform.forward, wish, Vector3.up); // stopping keeps the last way, so the feet do not swap clips as they stop
            // in the air the speed down changes over the frame, so the frame moves by its average: a jump is as high at any frame rate (it was 38 cm at 30 fps, 2026-11-12)
            float fallFrom = fall;
            if (!(body.isGrounded && fall >= 0)) fall += Gravity * Time.deltaTime;
            else fall = fallFrom = 2f;
            float down = (fallFrom + fall) / 2;
            // in the air: falling's clip from a jump at once, or after a while off a ledge; landing ends it, with the landing's clip after a long drop
            bool grounded = body.isGrounded;
            if (!grounded) inAir += Time.deltaTime;
            if (!falling && !grounded && inAir > FallsAfter) { falling = true; Person.Play("fall", 0.15f); }
            else if (Leaping && !grounded && Time.time - jumpedAt > Air + 0.05f) { Leaping = false; Person.Play("fall", 0.2f); } // still going down: off a ledge
            else if (falling && grounded && (inAir > 0.05f || Time.time - jumpedAt > 0.3f))
            {
                falling = false;
                if (inAir > HardLanding) landUntil = Time.time + Mathf.Min(0.45f, Person.Play("land", 0.08f));
                else if (Leaping) landUntil = Time.time + 0.2f; // the jump clip's own landing, briefly
                else Person.Stop(0.2f);
                Leaping = false;
            }
            if (grounded) inAir = 0;
            if (landUntil > 0 && (Time.time >= landUntil || amount > 0.05f)) { landUntil = 0; Person.Stop(0.25f); }
            var step = wish.normalized * speed + Vector3.down * down;
            var before = transform.position;
            body.Move(step * Time.deltaTime);
            var moved = transform.position - before;
            moved.y = 0;
            Person.Speed = Time.deltaTime > 0 ? moved.magnitude / Time.deltaTime : 0;
        }
    }
}
