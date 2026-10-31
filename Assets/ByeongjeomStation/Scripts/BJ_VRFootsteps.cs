using UnityEngine;

namespace Byeongjeom
{
    /// <summary>
    /// VR 플레이어(BJ_VRRig) 발소리.
    /// VR 플레이어는 몸 모델이 없어서 발 뼈 대신 몸(CharacterController)이 걸은 거리로 걸음을 센다.
    /// 스틱 이동과 룸스케일로 실제로 걷는 것 모두 소리가 나고, 스냅 회전 · 몸 기울이기로는 나지 않는다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class BJ_VRFootsteps : MonoBehaviour
    {
        public AudioClip[] clips;
        [Tooltip("걸을 때 한 걸음 거리 (m)")]
        public float walkStride = 0.75f;
        [Tooltip("달릴 때 한 걸음 거리 (m)")]
        public float runStride = 1.1f;
        [Tooltip("이보다 느리면 걷는 게 아니라 몸을 기울이는 것으로 보고 소리를 내지 않는다 (m/s)")]
        public float minSpeed = 0.5f;
        [Tooltip("내 발소리라 2D 로 바로 들리므로 3인칭(카메라 3m 뒤에서 3D)보다 작게")]
        [Range(0f, 1f)] public float walkVolume = 0.25f;
        [Range(0f, 1f)] public float runVolume = 0.5f;
        public float pitchRange = 0.08f;

        CharacterController cc;
        AudioSource source;
        float walkSpeed = 1.8f;
        float runSpeed = 3.3f;
        Vector3 lastPos;
        float speed;
        float steps;        // 다음 발소리까지 걸은 양 (1 = 한 걸음)
        int lastClip = -1;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            source = GetComponent<AudioSource>();
            var rig = GetComponent<BJ_VRRig>();
            if (rig != null) { walkSpeed = rig.walkSpeed; runSpeed = rig.sprintSpeed; }
            lastPos = BodyPosition();
        }

        // 몸(머리 아래) 바닥 위치. BJ_VRRig 가 CharacterController 중심을 머리 아래로 옮겨 준다
        Vector3 BodyPosition()
        {
            Vector3 p = transform.TransformPoint(cc.center);
            p.y = 0f;
            return p;
        }

        // BJ_VRRig.Update 가 몸을 옮긴 뒤에 잰다
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 pos = BodyPosition();
            float moved = Vector3.Distance(pos, lastPos);
            lastPos = pos;
            if (dt <= 0f) return;
            // 순간이동(포털 · 텔레포트)은 걸은 게 아니다
            if (moved > 2f) { steps = 0.5f; return; }

            speed = Mathf.Lerp(speed, moved / dt, 1f - Mathf.Exp(-10f * dt));
            if (!cc.isGrounded || speed < minSpeed)
            {
                // 멈췄다가 다시 걸으면 반 걸음 뒤에 첫 소리가 난다
                steps = 0.5f;
                return;
            }

            float run = Mathf.InverseLerp(walkSpeed, runSpeed, speed);
            steps += moved / Mathf.Lerp(walkStride, runStride, run);
            if (steps < 1f) return;
            steps = Mathf.Min(steps - 1f, 0.5f);
            PlayStep(run);
        }

        void PlayStep(float run)
        {
            if (clips == null || clips.Length == 0) return;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == lastClip) i = (i + 1) % clips.Length;
            lastClip = i;
            source.pitch = 1f + Random.Range(-pitchRange, pitchRange);
            source.PlayOneShot(clips[i], Mathf.Lerp(walkVolume, runVolume, run));
        }

        void Reset()
        {
            var s = GetComponent<AudioSource>();
            if (s == null) return;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
        }
    }
}
