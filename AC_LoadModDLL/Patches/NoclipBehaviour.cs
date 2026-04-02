using AC.Scene.Explore;
using UnityEngine;
using UnityEngine.AI;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
// Noclip MonoBehaviour
// - Disables NavMeshAgent and moves player Transform directly
// - Camera-relative WASD movement + Q/E for vertical
// ─────────────────────────────────────────────────────────────
public class NoclipBehaviour : MonoBehaviour
{
    public static bool Enabled = false;
    public static float Speed = 5f;

    private AC.Scene.Explore.Player? _player;
    private UnityEngine.Transform? _playerTransform;
    private UnityEngine.AI.NavMeshAgent? _agent;
    private UnityEngine.Rigidbody? _rigidbody;
    private bool _noclipActive = false;
    private bool _wasAgentEnabled = true;
    private bool _wasKinematic = true;

    private bool FindPlayer()
    {
        var scene = ExplorationSceneHooks.GetExploreScene();
        if (scene == null)
        {
            Plugin.Log.LogWarning("[Noclip] ExploreScene not found!");
            return false;
        }

        var player = scene._player;
        if (player == null)
        {
            Plugin.Log.LogWarning("[Noclip] Player not found!");
            return false;
        }

        _player = player;
        _playerTransform = player._transform;
        _agent = _playerTransform.GetComponent<UnityEngine.AI.NavMeshAgent>();
        _rigidbody = _playerTransform.GetComponent<UnityEngine.Rigidbody>();
        Plugin.Log.LogWarning($"[Noclip] agent={_agent != null}, rigidbody={_rigidbody != null}");
        return _playerTransform != null;
    }

    private void EnableNoclip()
    {
        if (!FindPlayer())
            return;
        _noclipActive = true;

        if (_agent != null)
        {
            _wasAgentEnabled = _agent.enabled;
            _agent.updatePosition = false;
            _agent.updateRotation = false;
        }

        if (_rigidbody != null)
        {
            _wasKinematic = _rigidbody.isKinematic;
            _rigidbody.isKinematic = false;
        }

        Plugin.Log.LogWarning("[Noclip] Enabled");
    }

    private void DisableNoclip()
    {
        _noclipActive = false;
        _playerTransform = null;
        _player = null;

        if (_agent != null)
        {
            _agent.updatePosition = true;
            _agent.updateRotation = true;
            _agent.enabled = _wasAgentEnabled;
        }

        if (_rigidbody != null)
            _rigidbody.isKinematic = _wasKinematic;

        Plugin.Log.LogWarning("[Noclip] Disabled");
    }

    private void Update()
    {
        if (Enabled && !_noclipActive)
            EnableNoclip();
        else if (!Enabled && _noclipActive)
            DisableNoclip();
    }

    private void LateUpdate()
    {
        if (Enabled && !_noclipActive)
            EnableNoclip();
        else if (!Enabled && _noclipActive)
            DisableNoclip();
        if (!_noclipActive || _playerTransform == null)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;

        var forward = cam.transform.forward;
        var right = cam.transform.right;
        forward.y = 0f;
        right.y = 0f;

        var move = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
            move += forward.normalized;
        if (Input.GetKey(KeyCode.S))
            move -= forward.normalized;
        if (Input.GetKey(KeyCode.A))
            move -= right.normalized;
        if (Input.GetKey(KeyCode.D))
            move += right.normalized;
        if (Input.GetKey(KeyCode.E))
            move.y += 1f;
        if (Input.GetKey(KeyCode.Q))
            move.y -= 1f;

        if (_rigidbody != null)
            _rigidbody.MovePosition(_rigidbody.position + move * Speed * Time.deltaTime);
        else
            _playerTransform.position += move * Speed * Time.deltaTime;
    }
}
