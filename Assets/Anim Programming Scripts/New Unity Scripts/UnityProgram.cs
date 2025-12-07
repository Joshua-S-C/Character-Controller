using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.InputSystem;


using Nyteshade.Modules.Anim;
using Nyteshade.Modules.Anim.Unity_Specific;
using Nyteshade.Modules.Maths;
using AnimationClip = Nyteshade.Modules.Anim.AnimationClip;
using NumVec3 = System.Numerics.Vector3;
using NumQuat = System.Numerics.Quaternion;
using UnityVec3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;

public class UnityProgram : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Core Components
    // ─────────────────────────────────────────────
    private Skeleton _skeleton;
    private AnimationPlayer _player;

    private GameObject _rigInstance;
    private SkinnedMeshRenderer _smr;

    private bool _isPaused = false;
    private bool _spaceHeld = false;

    // Animation Library
    private Dictionary<string, AnimationClip> _animLibrary = new();

    // Graph Nodes
    private LerpNode _idleWalkBlend;
    private LerpNode _walkRunBlend;
    private LerpNode _locomotionRoot;
    private LerpNode _turningBlend;
    private LayeredAddNode _turningLayer;

    private ClipNode _jumpNode;
    private ClipNode _danceNode;

    private bool _triggerJump = false;
    private bool _isDancing = false;

    // Input variables
    public float _currentSpeed = 0.0f;
    private float _turnAmount = 0.0f;

    // IK
    private IKManager _ikManager;
    private LookAtIKSolver _lookSolver;
    private CCDIK_Solver _leftArmSolver;
    private NumVec3 _lookTargetGui = new(0, -1.5f, 0f);
    private float _lookWeight = 1f;

    private NumVec3 _grabTargetGui = new(0.5f, 1.0f, -0.5f);
    private float _grabWeight = 0f;
    private int _chainSelection = 0;

    //Controller
    [SerializeField] UnityEngine.Transform parentController;


    // ─────────────────────────────────────────────
    // Unity Initialization
    // ─────────────────────────────────────────────
    private void Start()
    {
        Debug.Log("=== Nyteshade Unity Animation Runtime Initialized ===");

        // 1. Load GLB Rig from Resources
        var rigPrefab = Resources.Load<GameObject>("Skeleton_Rigged");
        if (!rigPrefab)
        {
            Debug.LogError("Cannot find rig FBX prefab in Resources!");
            return;
        }

        _rigInstance = Instantiate(rigPrefab, parentController);
        _rigInstance.name = "RuntimeRig";

        // 2. Build skeleton from SkinnedMeshRenderer
        _smr = UnityBridge.FindSkinnedMeshRendererRecursive(_rigInstance);
        _skeleton = UnityBridge.BuildSkeletonFromSkinnedMesh(_smr);

        // 3. Load Animation Files
        Debug.Log("[Init] Loading animations...");
        LoadClip("Idle");
        LoadClip("Walk");
        LoadClip("Slow_Run");
        LoadClip("Turn_Left");
        LoadClip("Turn_Right");
        LoadClip("Jump");
        LoadClip("Dance");

        Debug.Log($"Loaded {_animLibrary.Count} animations.");

        // 4. AnimationPlayer & Skinner
        _player = new AnimationPlayer(_skeleton);

        // 5. Build Graph
        BuildGraph();

        // 6. Setup IK
        SetupIK();
    }


    // ─────────────────────────────────────────────
    // Load a GLB animation and bake into Nyteshade clip
    // ─────────────────────────────────────────────
    private void LoadClip(string clipName)
    {
        // Load animation clip from Resources (FBX)
        var unityClip = Resources.Load<UnityEngine.AnimationClip>($"Animations/{clipName}");
        if (!unityClip)
        {
            Debug.LogError($"Animation '{clipName}' not found as FBX AnimationClip!");
            return;
        }

        // Instantiate a preview rig (this MUST be a clone)
        var previewRigPrefab = Resources.Load<GameObject>("Skeleton_Rigged");
        var previewRig = Instantiate(previewRigPrefab);
        previewRig.hideFlags = HideFlags.HideAndDontSave;

        // Bake into Nyteshade
        var baked = UnityBridge.BuildClipFromUnityClip(
            previewRig,
            unityClip,
            _skeleton,
            30f
        );

        Destroy(previewRig);

        if (baked == null)
        {
            Debug.LogError($"Failed to bake animation '{clipName}'");
            return;
        }

        _animLibrary[clipName] = baked;
    }


    // ─────────────────────────────────────────────
    // Build animation graph
    // ─────────────────────────────────────────────
    private void BuildGraph()
    {
        Debug.Log("[Init] Building animation graph...");

        var idle = new ClipNode(_animLibrary["Idle"]);
        var walk = new ClipNode(_animLibrary["Walk"]);
        var run  = new ClipNode(_animLibrary["Slow_Run"]);
        var turnL = new ClipNode(_animLibrary["Turn_Left"]);
        var turnR = new ClipNode(_animLibrary["Turn_Right"]);

        _idleWalkBlend = new LerpNode(idle, walk);
        _walkRunBlend = new LerpNode(walk, run);
        _locomotionRoot = new LerpNode(_idleWalkBlend, _walkRunBlend);

        _turningBlend = new LerpNode(turnL, turnR);
        _turningLayer = new LayeredAddNode(_locomotionRoot, _turningBlend, _skeleton.BoneCount);

        // Weight torso chain
        var torso = BuildChain("mixamorig_Head", "mixamorig_Spine");
        if (torso != null)
            foreach (var idx in torso) _turningLayer.BoneWeights[idx] = 1f;

        // Extra states
        _jumpNode = new ClipNode(_animLibrary["Jump"]) { Loops = false };
        _danceNode = new ClipNode(_animLibrary["Dance"]) { Loops = false };

        var stateLocomotion = new AnimState(_turningLayer);
        var stateJump = new AnimState(_jumpNode);
        var stateDance = new AnimState(_danceNode);

        stateLocomotion.AddTransition("Jump", () => { var f = _triggerJump; _triggerJump = false; return f; }, 0.1f);
        stateLocomotion.AddTransition("Dance", () => { var f = _isDancing; _isDancing = false; return f; }, 0.3f);

        stateJump.AddTransition("Locomotion", () => !_jumpNode.IsPlaying, 0.25f);

        stateDance.AddTransition("Locomotion",
            () => !_danceNode.IsPlaying || Keyboard.current.rKey.isPressed,
            0.5f);

        var stateMachine = new StateMachineNode("Locomotion", stateLocomotion);
        stateMachine.AddState("Jump", stateJump);
        stateMachine.AddState("Dance", stateDance);

        var baseNode = new BasePoseNode(_skeleton);
        var root = new AddNode(baseNode, stateMachine);

        _player.SetRoot(root);
    }


    // ─────────────────────────────────────────────
    // IK Setup
    // ─────────────────────────────────────────────
    private void SetupIK()
    {
        Debug.Log("[Init] Setting up IK...");

        _ikManager = new IKManager(_skeleton);

        int neckIndex = _skeleton.GetBoneIndex("mixamorig_Neck");
        if (neckIndex != -1)
        {
            _lookSolver = new LookAtIKSolver(neckIndex, new NumVec3(0, 0, 1));
            _ikManager.AddSolver(_lookSolver);
            Debug.Log($"LookAt solver for neck added.");
        }

        RebuildLeftArmSolver(_chainSelection);
    }


    private void RebuildLeftArmSolver(int sel)
    {
        if (_leftArmSolver != null)
        {
            _ikManager.RemoveSolver(_leftArmSolver);
            _leftArmSolver = null;
        }

        int[] chain = sel switch
        {
            0 => BuildChain("mixamorig_LeftHand", "mixamorig_LeftArm"),
            1 => BuildChain("mixamorig_LeftForeArm", "mixamorig_LeftArm"),
            2 => BuildChain("mixamorig_LeftArm", "mixamorig_LeftShoulder"),
            3 => BuildChain("mixamorig_LeftHand", "mixamorig_LeftForeArm"),
            _ => null
        };

        if (chain == null) return;

        _leftArmSolver = new CCDIK_Solver(chain);
        _leftArmSolver.Weight = _grabWeight;
        _ikManager.AddSolver(_leftArmSolver);
    }


    private int[] BuildChain(string endBone, string rootBone)
    {
        int endIdx = _skeleton.GetBoneIndex(endBone);
        int rootIdx = _skeleton.GetBoneIndex(rootBone);
        if (endIdx == -1 || rootIdx == -1) return null;

        var list = new List<int>();
        var cur = _skeleton.GetBone(endIdx);

        for (int i = 0; i < _skeleton.BoneCount; i++)
        {
            list.Add(_skeleton.GetBoneIndex(cur.Name));
            if (cur.Name == rootBone) break;
            cur = cur.Parent;
        }

        list.Reverse();
        return list.ToArray();
    }


    // ─────────────────────────────────────────────
    // Per-frame update (equivalent to _Process)
    // ─────────────────────────────────────────────
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return; // no keyboard connected or input system not ready

        // --- Pause Toggle (Space) ---
        if (kb.pKey.wasPressedThisFrame) //Swapped to P so player can jump
        {
            if (!_spaceHeld)
            {
                _spaceHeld = true;
                _isPaused = !_isPaused;
                Debug.Log(_isPaused ? "Paused" : "Resumed");
            }
        }

        if (kb.spaceKey.wasReleasedThisFrame)
            _spaceHeld = false;

        // --- Animation Update ---
        if (!_isPaused)
        {
            float dt = Time.deltaTime;

            HandleInput(dt);
            UpdateAnimationGraph(dt);
            _player.Update(dt);

            // IK
            UpdateIKSolvers();
            _ikManager.ResolveSolvers();
        }

        // --- Skinning → Unity bones ---
        UnityBridge.ApplyBoneOverridesToUnity(_smr, _skeleton);
    }
    private void HandleInput(float dt)
    {
        var kb = Keyboard.current;
        if (kb == null) return; // no keyboard? no input.

        // Walking / Running
        bool walking = kb.rKey.isPressed;
        bool running = kb.leftShiftKey.isPressed;

        /*float targetSpeed = walking ? (running ? 5f : 1f) : 0f;
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, dt * 5f);*/

        // Turning
        float t = 0f;
        if (kb.leftArrowKey.isPressed) t = -1f;
        else if (kb.rightArrowKey.isPressed) t = 1f;

        _turnAmount = Mathf.Lerp(_turnAmount, t, dt * 7f);

        // Jump (V key)
        if (kb.vKey.wasPressedThisFrame)
            jumpAnimation();

        // Dance (B key)
        if (kb.bKey.wasPressedThisFrame)
            if (!_jumpNode.IsPlaying && !_danceNode.IsPlaying)
                _isDancing = true;
    }

    public bool jumpAnimation() //making public to be called from Controller, pulled from line 330
    {
        if (!_jumpNode.IsPlaying && !_danceNode.IsPlaying)
        {
            _triggerJump = true;
            return true;
        }
        else
            return false;
    }


    private void UpdateAnimationGraph(float dt)
    {
        _idleWalkBlend.BlendWeight = Mathf.Clamp(_currentSpeed * 2f, 0, 1);
        _walkRunBlend.BlendWeight = Mathf.Clamp((_currentSpeed - 0.5f) * 2f, 0, 1); //Need help adjusting to make Walkblend trigger at 1 rather than 0.75
        _locomotionRoot.BlendWeight = _walkRunBlend.BlendWeight;

        float turn = (_turnAmount + 1f) * 0.5f;
        _turningBlend.BlendWeight = turn;
        _turningLayer.Weight = Mathf.Abs(_turnAmount);
    }


    private void UpdateIKSolvers()
    {
        if (_lookSolver != null)
        {
            var gui = _lookTargetGui;
            var cm = new NumVec3(gui.X * 100, gui.Z * -100, gui.Y * 100);
            _lookSolver.SetTarget(cm);
            _lookSolver.Weight = _lookWeight;
        }

        if (_leftArmSolver != null)
        {
            var gui = _grabTargetGui;
            var cm = new NumVec3(gui.X * 100, gui.Z * -100, gui.Y * 100);
            _leftArmSolver.SetTarget(cm);
            _leftArmSolver.Weight = _grabWeight;
        }
    }

    
    private Vector2 _debugScroll;
   void OnGUI()
{
    if (_skeleton == null)
        return;

    // A fixed-width window area
    Rect windowRect = new Rect(20, 20, 360, Screen.height - 40);
    GUILayout.BeginArea(windowRect, GUI.skin.window);

    // Start scroll section
    _debugScroll = GUILayout.BeginScrollView(_debugScroll, false, true);

    GUILayout.Label("Nyteshade Debug Panel", GUI.skin.box);

    GUILayout.Space(10);
    GUILayout.Label($"Bone Count: {_skeleton.BoneCount}");
    GUILayout.Label(_isPaused ? "Paused (Press Space)" : "Playing (Press Space)");

    GUILayout.Space(10);
    GUILayout.Box("", GUILayout.Height(2));

    // ----------------------------------------------------
    // Locomotion Controls
    // ----------------------------------------------------
    GUILayout.Label("Locomotion (R = Walk, Shift = Run)");
    GUILayout.Label($"Speed: {_currentSpeed:F2}");
    _currentSpeed = GUILayout.HorizontalSlider(_currentSpeed, 0f, 5f);

    GUILayout.Space(6);
    GUILayout.Label("Turning (Left / Right Arrows)");
    GUILayout.Label($"Turn Amount: {_turnAmount:F2}");
    _turnAmount = GUILayout.HorizontalSlider(_turnAmount, -1f, 1f);

    GUILayout.Space(10);
    GUILayout.Box("", GUILayout.Height(2));

    // ----------------------------------------------------
    // State Info
    // ----------------------------------------------------
    GUILayout.Label("— STATES —");
    GUILayout.Label("Press [V] to Jump");
    GUILayout.Label("Press [B] to Dance");
    GUILayout.Label("Press [R] to interrupt Dance");

    string currentState = "Locomotion";
    if (_jumpNode != null && _jumpNode.IsPlaying) currentState = "JUMPING!";
    else if (_danceNode != null && _danceNode.IsPlaying) currentState = "DANCING!";

    GUILayout.Label($"Current State: {currentState}");

    GUILayout.Space(10);
    GUILayout.Box("", GUILayout.Height(2));

    // ----------------------------------------------------
    // Look-At IK
    // ----------------------------------------------------
    GUILayout.Label("Look-At IK Controls (Meters)");

    GUILayout.Label($"Look X: {_lookTargetGui.X:F2}");
    _lookTargetGui.X = GUILayout.HorizontalSlider(_lookTargetGui.X, -2f, 2f);

    GUILayout.Label($"Look Y: {_lookTargetGui.Y:F2}");
    _lookTargetGui.Y = GUILayout.HorizontalSlider(_lookTargetGui.Y, -2f, 0f);

    GUILayout.Label($"Look Weight: {_lookWeight:F2}");
    _lookWeight = GUILayout.HorizontalSlider(_lookWeight, 0f, 1f);

    GUILayout.Space(10);
    GUILayout.Box("", GUILayout.Height(2));

    // ----------------------------------------------------
    // Grab IK
    // ----------------------------------------------------
    GUILayout.Label("Grab IK Controls");

    GUILayout.Label("Grab Chain:");

    if (GUILayout.Toggle(_chainSelection == 0, "Hand (Full Arm)"))
        RebuildLeftArmSolver(_chainSelection = 0);
    if (GUILayout.Toggle(_chainSelection == 1, "Forearm Only"))
        RebuildLeftArmSolver(_chainSelection = 1);
    if (GUILayout.Toggle(_chainSelection == 2, "Upper Arm Only"))
        RebuildLeftArmSolver(_chainSelection = 2);
    if (GUILayout.Toggle(_chainSelection == 3, "Wrist"))
        RebuildLeftArmSolver(_chainSelection = 3);

    GUILayout.Label($"Grab X: {_grabTargetGui.X:F2}");
    _grabTargetGui.X = GUILayout.HorizontalSlider(_grabTargetGui.X, -2, 2);

    GUILayout.Label($"Grab Y: {_grabTargetGui.Y:F2}");
    _grabTargetGui.Y = GUILayout.HorizontalSlider(_grabTargetGui.Y, -2, 2);

    GUILayout.Label($"Grab Z: {_grabTargetGui.Z:F2}");
    _grabTargetGui.Z = GUILayout.HorizontalSlider(_grabTargetGui.Z, -2, 2);

    GUILayout.Label($"Grab Weight: {_grabWeight:F2}");
    _grabWeight = GUILayout.HorizontalSlider(_grabWeight, 0, 1);

    GUILayout.Space(10);
    GUILayout.Box("", GUILayout.Height(2));

    // ----------------------------------------------------
    // Bone Info
    // ----------------------------------------------------
    GUILayout.Label("Bone Debug (first 30 bones)");

    int showCount = Mathf.Min(30, _skeleton.BoneCount);

    for (int i = 0; i < showCount; i++)
    {
        var bone = _skeleton.GetBone(i);
        var p = bone.Position;
        GUILayout.Label($"{i}: {bone.Name}");
        GUILayout.Label($"Pos: ({p.X:F2}, {p.Y:F2}, {p.Z:F2})");
        GUILayout.Space(3);
    }

    GUILayout.EndScrollView();
    GUILayout.EndArea();
}


}
