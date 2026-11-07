using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ReferenceRange
{
    [DisallowMultipleComponent]
    public sealed class FpsRange : MonoBehaviour
    {
        private const int Capacity = 30;
        private const float HipFov = 62f;
        private const float AimFov = 47f;
        private const float ShotInterval = 0.115f;
        private const float ShotMotionSeconds = 0.155f;
        private const int WorldMask = ~(1 << 2);
        private const float ReloadPauseProgress = 0.342f;
        private const float GlanceLowerSeconds = 0.24f;
        private const float GlanceRaiseStart = 0.41f;
        private const float GlanceSeconds = 0.65f;

        [SerializeField] private GameObject viewModel = null;
        [SerializeField] private AudioClip shot = null;
        [SerializeField] private AudioClip magOut = null;
        [SerializeField] private AudioClip magIn = null;
        [SerializeField] private AudioClip chamber = null;
        [SerializeField] private AudioClip dryFire = null;

        private enum Action { Ready, Reload, Charge }

        private CharacterController controller;
        private Camera eyes;
        private RifleView rifle;
        private RangeImpactEffects impactEffects;
        private AudioSource shotsAudio;
        private AudioSource handlingAudio;
        private Action action;
        private int rounds = Capacity - 1;
        private int reserve = Capacity * 4;
        private int cues;
        private bool chambered = true;
        private bool emptyReload;
        private bool unlocked = true;
        private bool awaitMouseUp;
        private Vector3 reloadCues;
        private bool reloadPauseComplete;
        private float dropWait;
        private float glanceTime = -1f;
        private float glanceWeight;
        private Vector3 glanceTarget;
        private float actionDuration;
        private float actionTime;
        private float nextShot;
        private float lastShot = float.NegativeInfinity;
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
        private GUIStyle textStyle;
        private GUIStyle helpStyle;

        private void Awake()
        {
            gameObject.layer = 2;
            spawnPoint = transform.position;
            yaw = transform.eulerAngles.y;
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.29f;
            controller.center = Vector3.up * 0.92f;
            controller.stepOffset = 0.27f;
            controller.minMoveDistance = 0f;
            impactEffects = GetComponent<RangeImpactEffects>();

            var cameraObject = new GameObject("First Person Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = Vector3.up * 1.64f;
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            eyes = cameraObject.AddComponent<Camera>();
            eyes.fieldOfView = HipFov;
            eyes.nearClipPlane = 0.018f;
            eyes.farClipPlane = 150f;
            eyes.allowHDR = true;
            var cameraData = eyes.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            rifle = new RifleView(cameraObject.transform,
                viewModel != null ? viewModel : Resources.Load<GameObject>("ReferenceModels/FirstPersonRig"));
            shotsAudio = AddAudio(cameraObject, "Gunshot");
            handlingAudio = AddAudio(cameraObject, "Weapon Handling");
            shot = FindClip(shot, "shot");
            magOut = FindClip(magOut, "mag_out");
            magIn = FindClip(magIn, "mag_in");
            chamber = FindClip(chamber, "chamber");
            dryFire = FindClip(dryFire, "dry_fire");
        }

        private void OnDisable()
        {
            UnlockMouse();
        }

        private void OnDestroy()
        {
            rifle?.Dispose();
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
                awaitMouseUp = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (awaitMouseUp && !mouse.leftButton.isPressed)
                awaitMouseUp = false;

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
                    else if (!awaitMouseUp && mouse.leftButton.isPressed && Time.time >= nextShot)
                        Fire();
                }
            }

            AdvanceAction();
            UpdateView(!unlocked && mouse.rightButton.isPressed && action == Action.Ready);
        }

        private void UnlockMouse()
        {
            unlocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void UpdateLook(Vector2 mouseDelta)
        {
            float sensitivity = Mathf.Lerp(0.113f, 0.069f, aim);
            yaw += mouseDelta.x * sensitivity;
            if (glanceTime >= 0f)
                mouseDelta.y = 0f;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * sensitivity, -84f, 84f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            eyes.transform.localRotation = ViewRotation();
            swayX = Mathf.Lerp(swayX, Mathf.Clamp(mouseDelta.x * 0.003f, -0.10f, 0.10f), Damp(17f));
            swayY = Mathf.Lerp(swayY, Mathf.Clamp(mouseDelta.y * 0.003f, -0.10f, 0.10f), Damp(17f));
        }

        private void UpdateMovement(Keyboard keyboard)
        {
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            if (controller.isGrounded)
                verticalSpeed = keyboard.spaceKey.wasPressedThisFrame ? 4.45f : -2f;
            else
                verticalSpeed -= 15.5f * Time.deltaTime;

            float speed = Mathf.Lerp(4.15f, 2.15f, aim);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            walk = Mathf.Lerp(walk, controller.isGrounded ? input.magnitude : 0f, Damp(11f));
            step += Time.deltaTime * speed * 2.7f * walk;

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
            cues = 0;
            reloadPauseComplete = false;
            dropWait = 0f;
            glanceTime = -1f;
            glanceWeight = 0f;
            if (next == Action.Reload)
            {
                emptyReload = !chambered;
                actionDuration = rifle.ReloadSeconds(emptyReload);
                reloadCues = rifle.ReloadCues(emptyReload) * actionDuration;
                if (reloadCues.z >= 0f)
                    reloadCues.z = Mathf.Max(reloadCues.z, reloadCues.y);
            }
            else
            {
                actionDuration = rifle.ChargeSeconds;
            }
        }

        private void AdvanceAction()
        {
            if (action == Action.Ready)
                return;

            if (action == Action.Reload)
            {
                AdvanceReloadTimeline(Time.deltaTime);
                AdvanceReload();
            }
            else
            {
                actionTime += Time.deltaTime;
                AdvanceCharge();
            }

            if (actionTime >= actionDuration)
                action = Action.Ready;
        }

        private void AdvanceReloadTimeline(float deltaTime)
        {
            if (reloadPauseComplete)
            {
                actionTime += deltaTime;
                return;
            }

            float pauseAt = actionDuration * ReloadPauseProgress;
            if (actionTime < pauseAt)
            {
                actionTime = Mathf.Min(actionTime + deltaTime, pauseAt);
                return;
            }

            if (glanceTime < 0f)
            {
                dropWait += deltaTime;
                bool hasMagazine = rifle.TryGetDroppedMagazine(out Vector3 position, out bool landed);
                if (landed)
                {
                    glanceTarget = position;
                    glanceTime = 0f;
                }
                else if (dropWait >= (hasMagazine ? 1.35f : 0.16f))
                {
                    reloadPauseComplete = true;
                }
                return;
            }

            glanceTime += deltaTime;
            float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, GlanceLowerSeconds, glanceTime));
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GlanceRaiseStart, GlanceSeconds, glanceTime));
            glanceWeight = down * (1f - back);
            if (glanceTime >= GlanceSeconds)
            {
                glanceTime = -1f;
                glanceWeight = 0f;
                reloadPauseComplete = true;
            }
        }

        private Quaternion ViewRotation()
        {
            if (glanceWeight <= 0f)
                return Quaternion.Euler(pitch - recoilPitch, 0f, 0f);

            Vector3 direction = transform.InverseTransformDirection(glanceTarget - eyes.transform.position);
            float planarDistance = new Vector2(direction.x, direction.z).magnitude;
            float down = Mathf.Atan2(-direction.y, Mathf.Max(0.1f, planarDistance)) * Mathf.Rad2Deg;
            float across = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            return Quaternion.Euler(
                Mathf.Lerp(pitch, Mathf.Clamp(down, 8f, 83f), glanceWeight) - recoilPitch,
                Mathf.Clamp(across, -27f, 27f) * glanceWeight, 0f);
        }

        private void AdvanceReload()
        {
            if (cues == 0 && actionTime >= reloadCues.x - 0.12f)
            {
                PlayHandling(magOut, 0.78f);
                cues++;
            }
            if (cues == 1 && actionTime >= reloadCues.y - 0.08f)
            {
                PlayHandling(magIn, 0.83f);
                cues++;
            }
            if (cues == 2 && actionTime >= reloadCues.y)
            {
                int loaded = Mathf.Min(Capacity - rounds, reserve);
                rounds += loaded;
                reserve -= loaded;
                cues++;
            }
            if (reloadCues.z < 0f)
                return;
            if (cues == 3 && actionTime >= reloadCues.z - 0.36f)
            {
                PlayHandling(chamber, 0.82f);
                cues++;
            }
            if (cues == 4 && actionTime >= reloadCues.z)
            {
                if (!chambered)
                    ChamberRound();
                cues++;
            }
        }

        private void AdvanceCharge()
        {
            float cue = rifle.ChargeCue * actionDuration;
            if (cues == 0 && actionTime >= cue - 0.36f)
            {
                PlayHandling(chamber, 0.83f);
                cues++;
            }
            if (cues == 1 && actionTime >= cue)
            {
                if (!chambered)
                    ChamberRound();
                cues++;
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
                PlayHandling(dryFire != null ? dryFire : chamber, dryFire != null ? 0.62f : 0.19f);
                nextShot = Time.time + 0.29f;
                return;
            }

            chambered = false;
            ChamberRound();
            lastShot = Time.time;
            shotsAudio.pitch = Random.Range(0.978f, 1.022f);
            if (shot != null)
                shotsAudio.PlayOneShot(shot, 0.88f);

            Transform view = eyes.transform;
            Vector2 spread = Random.insideUnitCircle * (Mathf.Lerp(0.006f, 0.0011f, aim) + 0.0022f * walk);
            Vector3 direction = (view.forward + view.right * spread.x + view.up * spread.y).normalized;
            if (Physics.Raycast(view.position, direction, out RaycastHit impact, 115f, WorldMask, QueryTriggerInteraction.Ignore))
            {
                impact.collider.GetComponentInParent<RangeTarget>()?.RegisterHit();
                impactEffects?.Play(impact, direction);
            }

            kick = Mathf.Min(kick + 1f, 1.75f);
            recoilPitch = Mathf.Min(recoilPitch + Mathf.Lerp(1.5f, 1.03f, aim), 5f);
            rifle.Flash();
        }

        private void UpdateView(bool aiming)
        {
            aim = Mathf.Lerp(aim, aiming ? 1f : 0f, Damp(aiming ? 13f : 19f));
            kick = Mathf.Lerp(kick, 0f, Damp(19f));
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, Damp(8.4f));
            if (unlocked)
            {
                walk = Mathf.Lerp(walk, 0f, Damp(10f));
                swayX = Mathf.Lerp(swayX, 0f, Damp(14f));
                swayY = Mathf.Lerp(swayY, 0f, Damp(14f));
            }

            eyes.fieldOfView = Mathf.Lerp(HipFov, AimFov, aim);
            eyes.transform.localRotation = ViewRotation();
            eyes.transform.localPosition = Vector3.up * (1.64f + Mathf.Sin(step * 2f) * walk * 0.015f);

            float progress = action == Action.Ready ? -1f : Mathf.Clamp01(actionTime / actionDuration);
            rifle.Pose(aim, walk, step, swayX, swayY, kick, Time.time - lastShot, ShotMotionSeconds,
                action == Action.Reload ? progress : -1f, emptyReload, action == Action.Charge ? progress : -1f, glanceWeight);
        }

        private static float Damp(float rate) => 1f - Mathf.Exp(-rate * Time.deltaTime);

        private static AudioClip FindClip(AudioClip clip, string key)
        {
            return clip != null ? clip : Resources.Load<AudioClip>("ReferenceAudio/" + key);
        }

        private static AudioSource AddAudio(GameObject parent, string label)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(parent.transform, false);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void PlayHandling(AudioClip clip, float volume)
        {
            if (clip != null)
                handlingAudio.PlayOneShot(clip, volume);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                textStyle.normal.textColor = new Color(0.94f, 0.94f, 0.94f, 0.78f);
                helpStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter };
            }

            GUI.Label(new Rect(Screen.width - 145f, Screen.height - 43f, 124f, 24f),
                $"{rounds + (chambered ? 1 : 0):00}   /   {reserve:000}", textStyle);

            if (unlocked)
            {
                float width = Mathf.Min(530f, Screen.width - 20f);
                var prompt = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.66f, width, 58f);
                DrawTint(prompt, new Color(0.035f, 0.04f, 0.043f, 0.79f));
                GUI.Label(new Rect(prompt.x, prompt.y + 4f, prompt.width, 23f), "Click to enter the range", helpStyle);
                GUI.Label(new Rect(prompt.x, prompt.y + 27f, prompt.width, 23f),
                    "WASD Move   ·   RMB Aim   ·   LMB Fire   ·   R Reload   ·   T Chamber", helpStyle);
            }
            else if (aim < 0.76f && action == Action.Ready)
            {
                DrawTint(new Rect((Screen.width - 2f) * 0.5f, (Screen.height - 2f) * 0.5f, 2f, 2f),
                    new Color(1f, 1f, 1f, 0.65f));
            }
            else if (aim > 0.95f && action == Action.Ready)
            {
                DrawTint(new Rect((Screen.width - 2f) * 0.5f, (Screen.height - 2f) * 0.5f, 2f, 2f),
                    new Color(1f, 0.30f, 0.17f, 0.90f));
            }
        }

        private static void DrawTint(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
