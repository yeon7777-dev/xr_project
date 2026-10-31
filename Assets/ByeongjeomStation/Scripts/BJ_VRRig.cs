using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Byeongjeom
{
    /// <summary>
    /// 메타 퀘스트(Quest Link / Air Link, OpenXR 또는 Oculus 플러그인)용 간단한 VR 플레이어.
    /// 외부 패키지(XR Interaction Toolkit 등) 없이 UnityEngine.XR 입력만 사용한다.
    ///
    /// [퀘스트 컨트롤러]
    ///  왼쪽 스틱 : 이동 (머리가 보는 방향 기준)      왼쪽 스틱 누르기 : 달리기 (멈추면 해제)
    ///  오른쪽 스틱 좌/우 : 스냅 회전 (기본 45도)      A 버튼 : 손전등 켜기/끄기
    /// [헤드셋이 없을 때 - 데스크톱 테스트]
    ///  WASD 이동, 마우스 시점(클릭하면 커서 잠금, Esc 해제), Shift 달리기, F 손전등
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class BJ_VRRig : MonoBehaviour
    {
        [Header("구성 (자동 연결됨)")]
        public Transform cameraOffset;
        public Camera head;
        public Transform leftHand;
        public Transform rightHand;
        public Light flashlight;
        public Renderer flashlightLens;
        public Renderer tunnelVignette;
        [Tooltip("몸(머리 아래) 발밑. 룸스케일로 걸어도 따라간다. AI 가 쫓는 지점")]
        public Transform feet;

        [Header("이동")]
        public float walkSpeed = 1.8f;
        public float sprintSpeed = 3.3f;
        public float snapTurnAngle = 45f;
        public float gravity = -9.81f;
        [Tooltip("트래킹 기준이 바닥이 아닐 때(Device 모드) 눈 높이")]
        public float deviceModeEyeHeight = 1.6f;

        [Header("멀미 완화")]
        public bool comfortVignette = true;
        [Range(0f, 1f)] public float vignetteStrength = 0.7f;

        [Header("데스크톱 테스트 (헤드셋 없을 때)")]
        public float desktopEyeHeight = 1.65f;
        public float mouseSensitivity = 0.12f;

        CharacterController cc;
        float verticalVelocity;
        bool snapReady = true;
        bool sprinting;
        bool lastFlashButton;
        float desktopPitch;
        float vignetteAmount;
        float turnFlash;
        bool originConfigured;
        bool wasXR;
        Material vignetteMat;
        static readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        static readonly int ColorId = Shader.PropertyToID("_Color");

        bool XRActive { get { return XRSettings.isDeviceActive; } }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            // 'Player' 태그로 플레이어를 찾는 스크립트(트리거 · 순찰 AI 등)가 VR 에서도 동작하도록
            if (CompareTag("Untagged")) gameObject.tag = "Player";
            if (feet == null)
            {
                feet = new GameObject("Feet").transform;
                feet.SetParent(transform, false);
            }
            SetupVignette();
        }

        void OnEnable() { Application.onBeforeRender += OnBeforeRender; }
        void OnDisable() { Application.onBeforeRender -= OnBeforeRender; }

        void Update()
        {
            float dt = Time.deltaTime;
            bool xr = XRActive;
            if (xr != wasXR) { originConfigured = false; wasXR = xr; }

            Vector2 move;
            bool sprintPressed, flashPressed;
            float turn = 0f;
            if (xr)
            {
                ConfigureTrackingOrigin();
                UpdateTrackedPoses();
                ReadXRInput(out move, out sprintPressed, out turn, out flashPressed);
            }
            else
            {
                ReadDesktopInput(dt, out move, out sprintPressed, out flashPressed);
            }

            // 손전등
            if (flashPressed && !lastFlashButton) ToggleFlashlight();
            lastFlashButton = flashPressed;

            // 스냅 회전 (머리 위치를 축으로)
            if (xr)
            {
                if (snapReady && Mathf.Abs(turn) > 0.7f)
                {
                    transform.RotateAround(head.transform.position, Vector3.up, Mathf.Sign(turn) * snapTurnAngle);
                    Physics.SyncTransforms();
                    snapReady = false;
                    turnFlash = 0.35f;
                }
                else if (Mathf.Abs(turn) < 0.3f) snapReady = true;
            }

            // 캐릭터 컨트롤러를 머리 위치에 맞춤 (룸스케일로 걸어도 몸이 따라감)
            FitBodyToHead();

            // 이동
            if (move.sqrMagnitude < 0.02f) { move = Vector2.zero; sprinting = false; }
            if (sprintPressed) sprinting = true;
            float speed = sprinting ? sprintSpeed : walkSpeed;
            Vector3 fwd = head.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 vel = (fwd * move.y + right * move.x) * speed;
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
            verticalVelocity += gravity * dt;
            vel.y = verticalVelocity;
            cc.Move(vel * dt);

            UpdateVignette(xr, move.magnitude * (sprinting ? 1.3f : 1f), dt);
        }

        // ------------------------------------------------------------------ XR
        void ConfigureTrackingOrigin()
        {
            if (originConfigured) return;
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0) return;
            bool floor = false;
            foreach (var s in subsystems)
            {
                if (s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)) floor = true;
                else if (s.GetTrackingOriginMode() == TrackingOriginModeFlags.Floor) floor = true;
            }
            if (cameraOffset != null) cameraOffset.localPosition = new Vector3(0f, floor ? 0f : deviceModeEyeHeight, 0f);
            originConfigured = true;
        }

        void OnBeforeRender()
        {
            if (XRActive) UpdateTrackedPoses();
        }

        void UpdateTrackedPoses()
        {
            Vector3 p; Quaternion q;
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (hmd.isValid && head != null)
            {
                if (hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out p) || hmd.TryGetFeatureValue(CommonUsages.devicePosition, out p))
                    head.transform.localPosition = p;
                if (hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out q) || hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out q))
                    head.transform.localRotation = q;
            }
            PoseHand(XRNode.LeftHand, leftHand);
            PoseHand(XRNode.RightHand, rightHand);
        }

        static void PoseHand(XRNode node, Transform t)
        {
            if (t == null) return;
            var dev = InputDevices.GetDeviceAtXRNode(node);
            if (!dev.isValid) return;
            Vector3 p; Quaternion q;
            if (dev.TryGetFeatureValue(CommonUsages.devicePosition, out p)) t.localPosition = p;
            if (dev.TryGetFeatureValue(CommonUsages.deviceRotation, out q)) t.localRotation = q;
        }

        static void ReadXRInput(out Vector2 move, out bool sprint, out float turn, out bool flash)
        {
            move = Vector2.zero; sprint = false; turn = 0f; flash = false;
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (left.isValid)
            {
                left.TryGetFeatureValue(CommonUsages.primary2DAxis, out move);
                left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out sprint);
            }
            if (right.isValid)
            {
                Vector2 r;
                if (right.TryGetFeatureValue(CommonUsages.primary2DAxis, out r)) turn = r.x;
                right.TryGetFeatureValue(CommonUsages.primaryButton, out flash);
            }
        }

        // ------------------------------------------------------------------ desktop fallback
        void ReadDesktopInput(float dt, out Vector2 move, out bool sprint, out bool flash)
        {
            move = Vector2.zero; sprint = false; flash = false;
            Vector2 look = Vector2.zero;
            bool click = false, esc = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                sprint = kb.leftShiftKey.isPressed;
                flash = kb.fKey.isPressed;
                esc = kb.escapeKey.wasPressedThisFrame;
            }
            if (mouse != null)
            {
                look = mouse.delta.ReadValue() * mouseSensitivity;
                click = mouse.leftButton.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            sprint = Input.GetKey(KeyCode.LeftShift);
            flash = Input.GetKey(KeyCode.F);
            look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (mouseSensitivity * 20f);
            click = Input.GetMouseButtonDown(0);
            esc = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (move.sqrMagnitude > 1f) move.Normalize();
            if (click) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (esc) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState != CursorLockMode.Locked) look = Vector2.zero;

            transform.Rotate(0f, look.x, 0f, Space.World);
            desktopPitch = Mathf.Clamp(desktopPitch - look.y, -80f, 80f);
            if (cameraOffset != null) cameraOffset.localPosition = Vector3.zero;
            if (head != null)
            {
                head.transform.localPosition = new Vector3(0f, desktopEyeHeight, 0f);
                head.transform.localRotation = Quaternion.Euler(desktopPitch, 0f, 0f);
            }
            // 손전등을 화면 오른쪽 아래에
            if (rightHand != null && head != null)
            {
                rightHand.position = head.transform.TransformPoint(new Vector3(0.22f, -0.25f, 0.4f));
                rightHand.rotation = head.transform.rotation;
            }
            if (leftHand != null) leftHand.position = head != null ? head.transform.TransformPoint(new Vector3(-0.25f, -0.35f, 0.3f)) : leftHand.position;
        }

        // ------------------------------------------------------------------ body / flashlight / comfort
        void FitBodyToHead()
        {
            if (head == null) return;
            Vector3 local = transform.InverseTransformPoint(head.transform.position);
            float h = Mathf.Clamp(local.y + 0.1f, 1.0f, 2.1f);
            cc.height = h;
            cc.center = new Vector3(local.x, h * 0.5f + cc.skinWidth, local.z);
            if (feet != null) feet.localPosition = new Vector3(local.x, 0f, local.z);
        }

        public void ToggleFlashlight()
        {
            if (flashlight == null) return;
            flashlight.enabled = !flashlight.enabled;
            if (flashlightLens != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_EmissionColor", flashlight.enabled ? new Color(2.5f, 2.3f, 1.9f) : Color.black);
                flashlightLens.SetPropertyBlock(mpb);
            }
        }

        void SetupVignette()
        {
            if (tunnelVignette == null) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) { tunnelVignette.enabled = false; return; }
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BJ_TunnelVignette" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.09f, 0.24f, r));
                    px[y * n + x] = new Color(0f, 0f, 0f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            vignetteMat = new Material(shader) { name = "BJ_TunnelVignette", mainTexture = tex, renderQueue = 4000 };
            tunnelVignette.sharedMaterial = vignetteMat;
            tunnelVignette.enabled = false;
        }

        void UpdateVignette(bool xr, float moveAmount, float dt)
        {
            if (tunnelVignette == null || vignetteMat == null) return;
            turnFlash = Mathf.Max(0f, turnFlash - dt);
            float target = (xr && comfortVignette) ? Mathf.Clamp01(Mathf.Max(moveAmount, turnFlash > 0f ? 1f : 0f)) * vignetteStrength : 0f;
            vignetteAmount = Mathf.MoveTowards(vignetteAmount, target, dt * 4f);
            bool on = vignetteAmount > 0.01f;
            tunnelVignette.enabled = on;
            if (on) vignetteMat.SetColor(ColorId, new Color(1f, 1f, 1f, vignetteAmount));
        }

        void OnDestroy()
        {
            if (vignetteMat != null)
            {
                if (vignetteMat.mainTexture != null) Destroy(vignetteMat.mainTexture);
                Destroy(vignetteMat);
            }
        }
    }
}
