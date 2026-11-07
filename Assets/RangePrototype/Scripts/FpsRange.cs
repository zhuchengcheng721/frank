using UnityEngine;
using UnityEngine.InputSystem;

namespace RangePrototype
{
    [DisallowMultipleComponent]
    public sealed class FpsRange : MonoBehaviour
    {
        private const int Capacity = 30;
        internal const float MagazineRemoved = 0.34f;
        internal const float MagazineSeated = 0.98f;
        private const float ReloadChargeStarts = 1.05f;
        private const float ReloadDuration = 1.80f;
        private const float ChargeSoundStarts = 0.16f;
        private const float ChargeReleased = 0.52f;
        private const float StandingFov = 66f;
        private const float AimingFov = 45f;
        private const float ShotInterval = 0.135f;
        private const int WorldMask = ~(1 << 2);

        [SerializeField] private GameObject viewModel = null;
        [SerializeField] private AudioClip shot;
        [SerializeField] private AudioClip magOut;
        [SerializeField] private AudioClip magIn;
        [SerializeField] private AudioClip chamber;
        [SerializeField] private AudioClip dryFire;

        private enum Action { Ready, Reload, Charge }

        private CharacterController controller;
        private Camera eyes;
        private RifleView rifle;
        private AudioSource firingAudio;
        private AudioSource handlingAudio;
        private LineRenderer tracer;
        private RangeImpactEffects impactEffects;
        private Material tracerMaterial;
        private int rounds = Capacity - 1;
        private int reserve = Capacity * 4;
        private int shots;
        private int hits;
        private int lastCue;
        private bool chambered = true;
        private bool unlocked = true;
        private Action action;
        private float actionTime;
        private float nextShot;
        private float flashUntil;
        private float hitUntil;
        private float yaw;
        private float pitch;
        private float verticalSpeed;
        private float kick;
        private float recoilPitch;
        private float swayX;
        private float swayY;
        private float aim;
        private float walk;
        private float step;
        private Vector3 spawnPoint;
        private GUIStyle label;
        private GUIStyle largeLabel;
        private GUIStyle centerLabel;
        private GUIStyle heading;
        private readonly Color accent = new Color(1f, 0.66f, 0.31f);
        private readonly Color muted = new Color(0.69f, 0.74f, 0.74f);

        private void Awake()
        {
            gameObject.layer = 2;
            spawnPoint = transform.position;
            yaw = transform.eulerAngles.y;
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.29f;
            controller.center = new Vector3(0f, 0.92f, 0f);
            controller.stepOffset = 0.27f;
            controller.minMoveDistance = 0f;

            var cameraObject = new GameObject("First Person Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = Vector3.up * 1.64f;
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            eyes = cameraObject.AddComponent<Camera>();
            eyes.fieldOfView = StandingFov;
            eyes.nearClipPlane = 0.025f;
            eyes.farClipPlane = 150f;
            eyes.allowHDR = true;

            rifle = new RifleView(cameraObject.transform, viewModel != null ? viewModel : Resources.Load<GameObject>("RangeModels/FirstPersonRig"));
            firingAudio = CreateAudio(cameraObject, "Weapon Report");
            handlingAudio = CreateAudio(cameraObject, "Weapon Handling");
            shot = FindClip(shot, "shot");
            magOut = FindClip(magOut, "mag_out");
            magIn = FindClip(magIn, "mag_in");
            chamber = FindClip(chamber, "chamber");
            dryFire = FindClip(dryFire, "dry_fire");

            impactEffects = GetComponent<RangeImpactEffects>();
            tracerMaterial = RifleView.CreateUnlit(new Color(1f, 0.75f, 0.34f));
            var tracerObject = new GameObject("Shot Tracer");
            tracerObject.transform.SetParent(transform, false);
            tracer = tracerObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.sharedMaterial = tracerMaterial;
            tracer.startWidth = 0.013f;
            tracer.endWidth = 0.002f;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.enabled = false;
        }

        private void OnDisable()
        {
            UnlockMouse();
        }

        private void OnDestroy()
        {
            rifle?.Dispose();
            Destroy(tracerMaterial);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                UnlockMouse();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                UnlockMouse();

            if (unlocked && mouse.leftButton.wasPressedThisFrame && Application.isFocused)
            {
                unlocked = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                nextShot = Time.time + 0.22f;
            }

            if (!unlocked)
            {
                UpdateLook(mouse.delta.ReadValue());
                UpdateMovement(keyboard);
                if (action == Action.Ready)
                {
                    if (keyboard.rKey.wasPressedThisFrame && rounds < Capacity && reserve > 0)
                        Begin(Action.Reload);
                    else if (keyboard.tKey.wasPressedThisFrame)
                        Begin(Action.Charge);
                    else if (mouse.leftButton.isPressed && Time.time >= nextShot)
                        Fire();
                }
            }

            AdvanceAction();
            UpdateView(!unlocked && mouse.rightButton.isPressed && action == Action.Ready);
        }

        private void LateUpdate()
        {
            rifle?.UpdateArmRigs();
        }

        private void UnlockMouse()
        {
            unlocked = true;
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void UpdateLook(Vector2 delta)
        {
            float sensitivity = Mathf.Lerp(0.12f, 0.067f, aim);
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -83f, 83f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            swayX = Mathf.Lerp(swayX, Mathf.Clamp(delta.x * 0.003f, -0.10f, 0.10f), Damp(16f));
            swayY = Mathf.Lerp(swayY, Mathf.Clamp(delta.y * 0.003f, -0.10f, 0.10f), Damp(16f));
        }

        private void UpdateMovement(Keyboard keyboard)
        {
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            if (controller.isGrounded)
            {
                verticalSpeed = -2f;
                if (keyboard.spaceKey.wasPressedThisFrame)
                    verticalSpeed = 4.6f;
            }
            else
            {
                verticalSpeed -= 15f * Time.deltaTime;
            }

            float speed = Mathf.Lerp(4.3f, 2.25f, aim);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            walk = Mathf.Lerp(walk, controller.isGrounded ? input.magnitude : 0f, Damp(10f));
            step += Time.deltaTime * speed * 2.8f * walk;
            if (transform.position.y < spawnPoint.y - 8f)
            {
                controller.enabled = false;
                transform.position = spawnPoint;
                controller.enabled = true;
                verticalSpeed = 0f;
            }
        }

        private void Begin(Action next)
        {
            action = next;
            actionTime = 0f;
            lastCue = 0;
        }

        private void AdvanceAction()
        {
            if (action == Action.Ready)
                return;

            actionTime += Time.deltaTime;
            if (action == Action.Reload)
            {
                if (lastCue == 0 && actionTime >= MagazineRemoved - 0.12f)
                {
                    PlayHandling(magOut, 0.76f);
                    lastCue++;
                }
                if (lastCue == 1 && actionTime >= MagazineSeated - 0.08f)
                {
                    PlayHandling(magIn, 0.84f);
                    lastCue++;
                }
                if (lastCue == 2 && actionTime >= MagazineSeated)
                {
                    int loaded = Mathf.Min(Capacity - rounds, reserve);
                    rounds += loaded;
                    reserve -= loaded;
                    lastCue++;
                }
                if (lastCue == 3 && actionTime >= ReloadChargeStarts + ChargeSoundStarts)
                {
                    PlayHandling(chamber, 0.86f);
                    lastCue++;
                }
                if (lastCue == 4 && actionTime >= ReloadChargeStarts + ChargeReleased)
                {
                    if (!chambered)
                        ChamberRound();
                    lastCue++;
                }
                if (actionTime >= ReloadDuration)
                    action = Action.Ready;
            }
            else
            {
                if (lastCue == 0 && actionTime >= ChargeSoundStarts)
                {
                    PlayHandling(chamber, 0.81f);
                    lastCue++;
                }
                if (lastCue == 1 && actionTime >= ChargeReleased)
                {
                    if (!chambered)
                        ChamberRound();
                    lastCue++;
                }
                if (actionTime >= 0.75f)
                    action = Action.Ready;
            }
        }

        private void ChamberRound()
        {
            if (rounds <= 0)
                return;

            rounds--;
            chambered = true;
        }

        private void Fire()
        {
            nextShot = Time.time + ShotInterval;
            if (!chambered)
            {
                PlayHandling(dryFire != null ? dryFire : chamber, dryFire != null ? 0.55f : 0.18f);
                nextShot = Time.time + 0.33f;
                return;
            }

            shots++;
            chambered = false;
            ChamberRound();
            firingAudio.pitch = Random.Range(0.98f, 1.035f);
            if (shot != null)
                firingAudio.PlayOneShot(shot, 0.84f);

            Vector3 direction = eyes.transform.forward;
            if (aim < 0.94f)
            {
                Vector2 spread = Random.insideUnitCircle * (0.005f * (1f - aim) + 0.0035f * walk);
                direction = (direction + eyes.transform.right * spread.x + eyes.transform.up * spread.y).normalized;
            }

            Vector3 end = eyes.transform.position + direction * 80f;
            if (Physics.Raycast(eyes.transform.position, direction, out RaycastHit impact, 80f, WorldMask, QueryTriggerInteraction.Ignore))
            {
                end = impact.point;
                RangeTarget target = impact.collider.GetComponentInParent<RangeTarget>();
                if (target != null)
                {
                    hits++;
                    hitUntil = Time.time + 0.15f;
                    target.RegisterHit();
                }

                impactEffects?.Play(impact, direction);
            }

            tracer.SetPosition(0, rifle.MuzzlePosition);
            tracer.SetPosition(1, end);
            flashUntil = Time.time + 0.045f;
            tracer.enabled = true;
            kick = Mathf.Min(kick + 1f, 1.6f);
            recoilPitch = Mathf.Min(recoilPitch + Mathf.Lerp(1.65f, 1.05f, aim), 4.5f);
            rifle.Flash();
        }

        private void UpdateView(bool aiming)
        {
            aim = Mathf.Lerp(aim, aiming ? 1f : 0f, Damp(aiming ? 14f : 18f));
            kick = Mathf.Lerp(kick, 0f, Damp(19f));
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, Damp(8f));
            if (unlocked)
                walk = Mathf.Lerp(walk, 0f, Damp(10f));

            eyes.fieldOfView = Mathf.Lerp(StandingFov, AimingFov, aim);
            eyes.transform.localRotation = Quaternion.Euler(pitch - recoilPitch, 0f, 0f);
            eyes.transform.localPosition = new Vector3(0f, 1.64f + Mathf.Sin(step * 2f) * walk * 0.018f, 0f);

            float reload = action == Action.Reload ? actionTime : -1f;
            float charge = action == Action.Charge ? actionTime : -1f;
            if (reload >= ReloadChargeStarts)
                charge = reload - ReloadChargeStarts;

            rifle.Pose(aim, walk, step, swayX, swayY, kick, reload, charge, Time.time <= flashUntil);
            if (Time.time > flashUntil)
                tracer.enabled = false;
        }

        private static float Damp(float rate)
        {
            return 1f - Mathf.Exp(-rate * Time.deltaTime);
        }

        private static AudioClip FindClip(AudioClip overrideClip, string key)
        {
            return overrideClip != null ? overrideClip : Resources.Load<AudioClip>("RangeAudio/" + key);
        }

        private static AudioSource CreateAudio(GameObject parent, string objectName)
        {
            var sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(parent.transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void PlayHandling(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            handlingAudio.pitch = Random.Range(0.96f, 1.04f);
            handlingAudio.PlayOneShot(clip, volume);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            float scale = Mathf.Max(0.75f, Screen.height / 960f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            InitStyles();
            DrawHud(width, height);

            if (unlocked)
                DrawStart(width, height);

            GUI.matrix = Matrix4x4.identity;
        }

        private void InitStyles()
        {
            if (label != null)
                return;

            label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            label.normal.textColor = muted;
            largeLabel = new GUIStyle(label) { fontSize = 36, fontStyle = FontStyle.Bold };
            largeLabel.normal.textColor = Color.white;
            centerLabel = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            heading = new GUIStyle(centerLabel) { fontSize = 33, fontStyle = FontStyle.Bold };
            heading.normal.textColor = Color.white;
        }

        private void DrawHud(float width, float height)
        {
            Panel(new Rect(22f, 22f, 218f, 59f), new Color(0.03f, 0.045f, 0.049f, 0.86f));
            Panel(new Rect(22f, 22f, 3f, 59f), accent);
            GUI.Label(new Rect(36f, 28f, 202f, 22f), "RANGE  /  LIVE FIRE", label);
            GUI.Label(new Rect(36f, 50f, 202f, 22f), $"HITS {hits:00}    /    SHOTS {shots:00}", label);

            float ammoX = width - 212f;
            float ammoY = height - 103f;
            Panel(new Rect(ammoX, ammoY, 190f, 81f), new Color(0.03f, 0.045f, 0.049f, 0.88f));
            Panel(new Rect(ammoX, ammoY + 78f, 190f, 3f), accent);
            GUI.Label(new Rect(ammoX + 12f, ammoY + 2f, 170f, 21f), StatusLabel(), label);
            GUI.Label(new Rect(ammoX + 12f, ammoY + 21f, 82f, 45f), (rounds + (chambered ? 1 : 0)).ToString("00"), largeLabel);
            GUI.Label(new Rect(ammoX + 77f, ammoY + 39f, 96f, 25f), $"/ {reserve:000}", label);

            if (unlocked)
                return;

            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            if (aim < 0.72f && action == Action.Ready)
            {
                float gap = 6f + kick * 4f + walk * 4f;
                Color white = new Color(0.92f, 0.96f, 0.96f, 0.83f);
                Panel(new Rect(center.x - gap - 6f, center.y - 0.65f, 6f, 1.3f), white);
                Panel(new Rect(center.x + gap, center.y - 0.65f, 6f, 1.3f), white);
                Panel(new Rect(center.x - 0.65f, center.y - gap - 6f, 1.3f, 6f), white);
                Panel(new Rect(center.x - 0.65f, center.y + gap, 1.3f, 6f), white);
            }

            if (Time.time <= hitUntil)
            {
                for (int quadrant = 0; quadrant < 4; quadrant++)
                {
                    GUIUtility.RotateAroundPivot(45f + quadrant * 90f, center);
                    Panel(new Rect(center.x + 8f, center.y - 1f, 7f, 2f), accent);
                    GUIUtility.RotateAroundPivot(-45f - quadrant * 90f, center);
                }
            }
        }

        private string StatusLabel()
        {
            if (action == Action.Reload)
                return "RELOADING";
            if (action == Action.Charge)
                return "CHAMBERING";
            return chambered ? "CHAMBER READY" : "CHAMBER EMPTY";
        }

        private void DrawStart(float width, float height)
        {
            Panel(new Rect(0f, 0f, width, height), new Color(0.025f, 0.036f, 0.038f, 0.42f));
            float x = (width - 526f) * 0.5f;
            float y = (height - 190f) * 0.49f;
            Panel(new Rect(x, y, 526f, 190f), new Color(0.035f, 0.049f, 0.053f, 0.94f));
            Panel(new Rect(x, y, 526f, 3f), accent);
            GUI.Label(new Rect(x, y + 18f, 526f, 43f), "INDOOR FIRING RANGE", heading);
            GUI.Label(new Rect(x, y + 67f, 526f, 26f), "CLICK TO CAPTURE THE MOUSE", centerLabel);
            GUI.Label(new Rect(x, y + 106f, 526f, 25f), "WASD  Move     RMB  Aim     LMB  Fire     Space  Jump", centerLabel);
            GUI.Label(new Rect(x, y + 134f, 526f, 25f), "R  Reload     T  Chamber     Esc  Release mouse", centerLabel);
        }

        private static void Panel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
