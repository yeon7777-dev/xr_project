using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.ThirdPerson;

// 씬에 놓인 AIThirdPersonController 에 붙인다 (AICharacterControl 과 함께).
// AI 가 플레이어까지 걸어갈 수 있으면 AICharacterControl 이 평소처럼 target 을 쫓아가고,
// 플레이어가 AI 가 갈 수 없는 곳(NavMesh 밖이거나, NavMesh 위라도 길이 이어지지 않은 곳)에 lost_delay 초 동안 있으면
// 쫓기를 멈추고 NavMesh 위의 아무 지점이나 골라 걸어 다닌다. 다시 갈 수 있는 곳으로 오면 바로 쫓는다.
// AI 를 선택하면 Scene 뷰에 지금 가고 있는 배회 지점이 노란 선으로 보인다.
[RequireComponent(typeof(AICharacterControl))]
[DisallowMultipleComponent]
public class ai_wander : MonoBehaviour {

    public Transform player;              // 비워 두면 AICharacterControl 의 target → VR 플레이어 발밑 → Player 태그 순서로 찾는다
    public float navmesh_range = 1f;      // 플레이어 발밑에서 이 거리(m) 안에 NavMesh 가 없으면 밖으로 나간 것으로 본다
    public float lost_delay = 1f;         // 갈 수 없는 곳에 이만큼(초) 있어야 배회를 시작한다 (점프 · 가장자리에서 왔다 갔다 하지 않게)
    public float wander_radius = 15f;     // 지금 자리에서 이 거리(m) 안의 지점을 고른다
    public float wander_speed = 0.5f;     // 배회할 때 NavMeshAgent 속도 (0.5 = 걷기, 1 = 뛰기)
    public float min_wait = 1f;           // 지점에 닿은 뒤 서 있는 시간 (초)
    public float max_wait = 3f;
    public float give_up_time = 20f;      // 이 시간 안에 못 닿으면 다른 지점을 고른다

    public bool wandering { get; private set; }

    AICharacterControl control;
    NavMeshAgent agent;
    NavMeshPath path;
    Transform chase_target;   // 다시 쫓을 대상 (시작할 때의 AICharacterControl target, 없으면 player)
    float chase_speed;
    bool reachable = true;
    float check_timer;
    float off_time;
    float wait_timer;
    float walk_timer;
    bool has_point;
    Vector3 point;

    void Start() {
        control = GetComponent<AICharacterControl>();
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        chase_speed = agent.speed;

        if (player == null) player = control.target;
        // VR 모드에서는 3인칭 플레이어가 지워져 target 이 비어 있다. 룸스케일로 걸어도 몸을 따라가는 VR 플레이어 발밑을 쫓는다
        if (player == null) {
            var vr = FindObjectOfType<Byeongjeom.BJ_VRRig>();
            if (vr != null) player = vr.feet != null ? vr.feet : vr.transform;
        }
        if (player == null) {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        // 배회를 시작할 때마다 target 을 저장하면, 이미 비워 둔 null 을 저장해 버릴 수 있어 처음에 한 번만 정한다
        chase_target = control.target != null ? control.target : player;
        if (control.target == null) control.SetTarget(chase_target);

        // 같은 AI 에 두 개가 붙어 있으면 서로 target · 목적지를 덮어써서 AI 가 멈추므로 첫 번째 것만 동작한다
        if (GetComponents<ai_wander>()[0] != this) {
            Debug.LogWarning("ai_wander: 같은 오브젝트에 ai_wander 가 두 개 있어 하나를 끕니다. Inspector 에서 하나를 지워 주세요.", this);
            enabled = false;
        }
    }

    void Update() {
        if (!agent.isOnNavMesh) return;

        // 길찾기는 무거우니 0.25초마다만 확인한다
        check_timer -= Time.deltaTime;
        if (check_timer <= 0f) {
            check_timer = 0.25f;
            reachable = CanReach();
        }
        off_time = reachable ? 0f : off_time + Time.deltaTime;

        if (!wandering && off_time >= lost_delay) {
            StartWander();
            Debug.Log("ai_wander: 플레이어가 AI 가 갈 수 없는 곳에 있어 배회를 시작합니다.", this);
        } else if (wandering && reachable) {
            StopWander();
            Debug.Log("ai_wander: 플레이어에게 갈 수 있게 되어 다시 쫓습니다.", this);
        }

        if (wandering) Wander();
    }

    // spotlight_trigger 등이 이 스크립트를 끄면 쫓던 상태로 돌려 둔다 (다시 켜지면 Update 가 다시 판단한다)
    void OnDisable() {
        if (wandering) StopWander();
    }

    // 플레이어 발밑에 NavMesh 가 있고, AI 자리에서 거기까지 끝까지 이어진 길이 있는지
    bool CanReach() {
        if (player == null) return false;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(player.position, out hit, navmesh_range, agent.areaMask)) return false;
        return agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    void StartWander() {
        wandering = true;
        control.SetTarget(null);    // AICharacterControl 이 목적지를 플레이어 쪽으로 덮어쓰지 않게
        agent.speed = wander_speed;
        has_point = false;
        wait_timer = 0f;
    }

    void StopWander() {
        wandering = false;
        has_point = false;
        if (agent != null) agent.speed = chase_speed;
        if (control != null) control.SetTarget(chase_target);
        // 배회 경로를 지우지 않고 바로 새 목적지를 준다 (지우면 먼 길을 찾는 동안 길이 없어 그 자리에 선다)
        if (chase_target != null && agent != null && agent.isOnNavMesh) agent.SetDestination(chase_target.position);
    }

    void Wander() {
        if (!agent.isOnNavMesh) return;
        if (has_point) {
            walk_timer += Time.deltaTime;
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
            if (!arrived && walk_timer < give_up_time) return;
            // 닿았으면 잠깐 서 있다가, 못 닿았으면 바로 다음 지점으로
            has_point = false;
            wait_timer = arrived ? Random.Range(min_wait, max_wait) : 0f;
        }
        wait_timer -= Time.deltaTime;
        if (wait_timer > 0f) return;

        if (PickPoint()) {
            has_point = true;
            walk_timer = 0f;
        } else {
            wait_timer = 0.5f;      // 못 찾았으면 잠시 뒤에 다시 (매 프레임 길찾기를 돌리지 않게)
        }
    }

    // 지금 자리 둘레에서 NavMesh 위의, 끝까지 걸어갈 수 있는 지점을 고른다
    bool PickPoint() {
        for (int i = 0; i < 10; i++) {
            Vector2 r = Random.insideUnitCircle * wander_radius;
            Vector3 p = transform.position + new Vector3(r.x, 0f, r.y);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(p, out hit, 2f, agent.areaMask)) continue;
            if ((hit.position - transform.position).sqrMagnitude < 4f) continue;     // 2m 보다 가까우면 다시
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            point = hit.position;
            agent.SetDestination(point);
            return true;
        }
        return false;
    }

    void OnDrawGizmosSelected() {
        if (!wandering || !has_point) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, point);
        Gizmos.DrawWireSphere(point, 0.3f);
    }
}
