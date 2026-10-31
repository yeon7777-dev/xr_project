using UnityEngine;

// ThirdPersonController · AIThirdPersonController 처럼 휴머노이드 Animator 를 쓰는 캐릭터에 붙인다.
// 애니메이션의 두 발 높이를 보고, 들려 있던 발이 디딘 발보다 낮아지는 순간(= 발이 땅에 닿는 순간) 발소리를 낸다.
// 발 위치를 애니메이션에서 바로 읽으므로 걷기 · 뛰기 · 제자리 회전 모두 발에 맞춰 소리가 나고, 경사 · 계단에서도 똑같이 동작한다.
// 화면 밖에서도 발이 움직여야 소리가 나므로 Animator 의 Culling Mode 는 Always Animate 로 둔다.
[RequireComponent(typeof(AudioSource))]
public class footstep_sound : MonoBehaviour {

    public AudioClip[] clips;              // 발소리 (같은 소리가 연달아 나지 않게 무작위로 고른다)
    public float walk_volume = 0.4f;       // 걸을 때 (Animator 의 Forward 0.5 이하)
    public float run_volume = 0.8f;        // 뛸 때 (Forward 1)
    public float pitch_range = 0.08f;      // 피치를 1 ± 이만큼 흔들어 단조롭지 않게 한다
    public float foot_gap = 0.03f;         // 두 발 높이 차이가 이보다 커야 디딘 발이 바뀐 것으로 본다 (m)
    public float min_interval = 0.15f;     // 발소리 사이 최소 시간 (초)

    static readonly int forward_id = Animator.StringToHash("Forward");
    static readonly int on_ground_id = Animator.StringToHash("OnGround");

    Animator animator;
    AudioSource source;
    Transform left_foot;
    Transform right_foot;
    bool has_forward;
    bool has_on_ground;
    int planted;              // 디디고 있는 발: -1 왼발, 1 오른발, 0 두 발로 서 있음
    float even_time;          // 두 발 높이가 비슷하게 이어진 시간
    float last_step_time = -1f;
    int last_clip = -1;

    void Awake() {
        animator = GetComponent<Animator>();
        source = GetComponent<AudioSource>();
        if (animator != null && animator.isHuman) {
            left_foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            right_foot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        if (left_foot == null || right_foot == null) {
            Debug.LogWarning("footstep_sound: 휴머노이드 Animator 의 발 뼈를 찾을 수 없어 발소리를 끕니다.", this);
            enabled = false;
            return;
        }
        // ThirdPersonCharacter 가 넣어 주는 파라미터 (없으면 항상 땅 위 · 뛰는 소리로 본다)
        foreach (var p in animator.parameters) {
            if (p.nameHash == forward_id) has_forward = true;
            if (p.nameHash == on_ground_id) has_on_ground = true;
        }
    }

    // Animator 가 이번 프레임 포즈를 만든 뒤에 발 높이를 읽는다
    void LateUpdate() {
        // 캐릭터 기준 높이라 경사 · 계단에 있어도 애니메이션의 발 높이 그대로다
        float left = transform.InverseTransformPoint(left_foot.position).y;
        float right = transform.InverseTransformPoint(right_foot.position).y;

        int lower = 0;
        if (left < right - foot_gap) lower = -1;
        else if (right < left - foot_gap) lower = 1;

        if (lower == 0) {
            // 두 발로 한동안 서 있으면 처음 상태로 돌린다 (다시 걸을 때 먼저 떼는 발에서 소리가 나지 않게)
            even_time += Time.deltaTime;
            if (even_time > 0.3f) planted = 0;
            return;
        }
        even_time = 0f;
        if (lower == planted) return;

        bool first = planted == 0;
        planted = lower;
        // 서 있다가 처음 바뀐 건 발을 뗀 것이고, 점프 중에 바뀐 건 땅을 디딘 게 아니다
        if (first) return;
        if (has_on_ground && !animator.GetBool(on_ground_id)) return;
        if (Time.time - last_step_time < min_interval) return;
        PlayStep();
    }

    void PlayStep() {
        if (clips == null || clips.Length == 0) return;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last_clip) i = (i + 1) % clips.Length;
        last_clip = i;

        // Forward 0.5 = 걷기, 1 = 뛰기 (ThirdPersonUserControl 은 Shift 를 누르면 걷는다)
        float run = has_forward ? Mathf.InverseLerp(0.5f, 1f, animator.GetFloat(forward_id)) : 1f;
        source.pitch = 1f + Random.Range(-pitch_range, pitch_range);
        source.PlayOneShot(clips[i], Mathf.Lerp(walk_volume, run_volume, run));
        last_step_time = Time.time;
    }

    // 컴포넌트를 처음 붙일 때 AudioSource 를 발소리용 3D 소리로 맞춘다
    void Reset() {
        var s = GetComponent<AudioSource>();
        if (s == null) return;
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.minDistance = 2f;
        s.maxDistance = 25f;
    }
}
