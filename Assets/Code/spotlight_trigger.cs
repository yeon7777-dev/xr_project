using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.ThirdPerson;

// 트리거 큐브(Box Collider 의 Is Trigger 체크)에 붙인다.
// Player 나 AI(AIThirdPersonController)가 닿으면 AI 머리 위의 스포트라이트를 켜고, AI 를 hold_time 초 동안 가만히 세워 둔다.
// Spot_light 를 비워 두면 AI 위에 스포트라이트를 자동으로 만들고, Play 시작 때는 꺼 둔다.
// 씬에 실제 라이트로 두려면 Inspector 의 ⋮ 메뉴 > '스포트라이트를 씬에 만들기'.
public class spotlight_trigger : MonoBehaviour {

    public Transform ai_target;      // 비워 두면 씬의 AIThirdPersonController 를 찾는다
    public Light spot_light;         // 비워 두면 자동으로 만든다
    public float height = 2.5f;      // AI 발밑에서 라이트까지 높이 (천장보다 낮게)
    public float spot_angle = 60f;
    public float intensity = 4f;
    public Color color = new Color(1f, 0.95f, 0.85f);
    public float hold_time = 5f;            // 닿은 뒤 AI 가 가만히 있는 시간 (초)
    public bool hold_from_start = false;    // 체크하면 Play 시작부터 닿을 때까지도 가만히 있는다

    ThirdPersonCharacter character;
    NavMeshAgent agent;
    readonly List<MonoBehaviour> paused = new List<MonoBehaviour>();
    bool holding;
    bool triggered;

    // AI 스크립트들의 Start 보다 먼저 멈춰 둘 수 있도록 Awake 에서 준비한다
    void Awake() {
        if (ai_target == null) {
            var ai = FindObjectOfType<AICharacterControl>();
            if (ai != null) ai_target = ai.transform;
        }
        if (ai_target != null) {
            character = ai_target.GetComponent<ThirdPersonCharacter>();
            agent = ai_target.GetComponent<NavMeshAgent>();
        }

        if (spot_light == null && ai_target != null) spot_light = CreateSpotlight();

        if (spot_light != null) spot_light.enabled = false;
        else Debug.LogWarning("spotlight_trigger: AI 대상(ai_target)이나 스포트라이트(spot_light)를 지정해 주세요.", this);

        if (hold_from_start) Hold();
    }

    // AI 의 자식으로 두어 AI 가 움직여도 계속 위에서 비춘다
    Light CreateSpotlight() {
        var go = new GameObject("AI_Spotlight");
        go.transform.SetParent(ai_target, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = spot_angle;
        light.range = height * 3f;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.Soft;
        return light;
    }

#if UNITY_EDITOR
    // Inspector 에서 이 컴포넌트의 ⋮ 메뉴 > '스포트라이트를 씬에 만들기'.
    // Play 때마다 만드는 대신 씬에 실제 라이트로 두고 spot_light 에 연결한다 (Ctrl+Z 가능)
    [ContextMenu("스포트라이트를 씬에 만들기")]
    void CreateSpotlightInScene() {
        if (spot_light != null) {
            UnityEditor.Selection.activeObject = spot_light.gameObject;
            Debug.Log("spotlight_trigger: 이미 연결된 스포트라이트가 있습니다 (" + spot_light.name + ").", spot_light);
            return;
        }
        UnityEditor.Undo.RecordObject(this, "Create AI Spotlight");
        if (ai_target == null) {
            var ai = FindObjectOfType<AICharacterControl>();
            if (ai != null) ai_target = ai.transform;
        }
        if (ai_target == null) {
            Debug.LogWarning("spotlight_trigger: AI 대상(ai_target)을 먼저 지정해 주세요.", this);
            return;
        }
        spot_light = CreateSpotlight();
        UnityEditor.Undo.RegisterCreatedObjectUndo(spot_light.gameObject, "Create AI Spotlight");
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.Selection.activeObject = spot_light.gameObject;
    }
#endif

    void Update() {
        if (!holding) return;
        // 이동 스크립트를 꺼 둔 동안 걷기 애니메이션이 이어지지 않도록 멈춤 입력을 계속 준다
        if (character != null) character.Move(Vector3.zero, false, false);
        // NavMeshAgent 는 켜 둔 채 세운다. 끄면 AI 를 NavMesh 에 붙잡아 두는 게 없어 바닥 아래로 빠진다
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    void OnTriggerEnter(Collider col) {
        if (triggered) return;
        bool is_ai = ai_target != null && col.transform.IsChildOf(ai_target);
        if (!is_ai && !col.CompareTag("Player")) return;
        triggered = true;

        if (spot_light != null) {
            spot_light.gameObject.SetActive(true);
            spot_light.enabled = true;
        }
        StartCoroutine(HoldFor(hold_time));
    }

    IEnumerator HoldFor(float seconds) {
        Hold();
        yield return new WaitForSeconds(seconds);
        Release();
    }

    // AI 의 이동 스크립트(AICharacterControl, patrol_* 등)를 잠시 끈다
    void Hold() {
        if (holding || ai_target == null) return;
        holding = true;
        paused.Clear();
        foreach (var b in ai_target.GetComponents<MonoBehaviour>()) {
            if (b.enabled && b != character && b != this) {
                b.enabled = false;
                paused.Add(b);
            }
        }
    }

    void Release() {
        if (!holding) return;
        holding = false;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = false;
        foreach (var b in paused) {
            if (b != null) b.enabled = true;
        }
        paused.Clear();
    }
}
